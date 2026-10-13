from __future__ import annotations

import logging
import json
from datetime import datetime, timezone
from typing import Any
from uuid import uuid4

from .clients import DependencyUnavailable, ExecutionStore, ModelClient, SpecialistClient
from .contracts import ContractError, DispatchRequest, ValidatedPlan

logger = logging.getLogger("blueverse.planning_coordination")


class PlanningCoordinator:
    def __init__(self, model: ModelClient, specialists: SpecialistClient, store: ExecutionStore):
        self.model = model
        self.specialists = specialists
        self.store = store

    def execute(self, request: DispatchRequest, correlation_id: str | None = None) -> dict[str, Any]:
        correlation_id = correlation_id or str(uuid4())
        state: dict[str, Any] = {
            "contractVersion": 1,
            "workflowId": request.workflow_id,
            "correlationId": correlation_id,
            "status": "PLANNING",
            "plan": None,
            "steps": [],
            "result": None,
            "errorCode": None,
            "updatedAtUtc": self._now(),
        }
        try:
            self.store.write(request.workflow_id, state)
            plan = ValidatedPlan.parse(self.model.create_plan(request), request.workflow_type)
            state["plan"] = plan.to_json()
            state["status"] = "DELEGATING"
            self._save(request, state)
            reports: dict[str, dict[str, Any]] = {}
            for step in plan.steps:
                if any(dependency not in reports for dependency in step.depends_on):
                    state["status"] = "BLOCKED"
                    state["errorCode"] = "REQUIRED_DEPENDENCY_MISSING"
                    self._save(request, state)
                    return self._response(request, state)
                step_state: dict[str, Any] = {
                    "stepId": step.step_id,
                    "agentId": step.agent_id,
                    "status": "RUNNING",
                    "report": None,
                    "errorCode": None,
                }
                state["steps"].append(step_state)
                self._save(request, state)
                try:
                    raw_report = self.specialists.dispatch(step.agent_id, {
                        "contractVersion": 1,
                        "workflowId": request.workflow_id,
                        "workflowType": request.workflow_type,
                        "recommendationId": request.recommendation_id,
                        "stepId": step.step_id,
                        "expectedReportType": step.expected_report_type,
                        "objective": request.objective,
                        "destinationId": request.destination_id,
                        "timeRange": {"startsAt": request.starts_at, "endsAt": request.ends_at},
                        "preferredActivityIds": list(request.preferred_activity_ids),
                        "constraints": request.constraints,
                        "dependencyReports": [reports[item] for item in step.depends_on],
                    })
                    report = self._validate_report(raw_report, request, step.step_id, step.agent_id, step.expected_report_type)
                except (DependencyUnavailable, ContractError):
                    step_state["status"] = "UNAVAILABLE"
                    step_state["errorCode"] = "SPECIALIST_UNAVAILABLE_OR_INVALID"
                    state["status"] = "BLOCKED"
                    state["errorCode"] = step_state["errorCode"]
                    self._save(request, state)
                    return self._response(request, state)
                step_state["status"] = "COMPLETED"
                step_state["report"] = report
                reports[step.step_id] = report
                self._save(request, state)
            state["status"] = "COMPLETED"
            state["result"] = {
                "workflowId": request.workflow_id,
                "objectiveSummary": plan.objective_summary,
                "validatedReports": list(reports.values()),
                "assemblyRule": "Source fields and deterministic business decisions remain authoritative.",
            }
            self._save(request, state)
            self._log(correlation_id, "COMPLETED")
            return self._response(request, state)
        except DependencyUnavailable:
            state["status"] = "UNAVAILABLE"
            state["errorCode"] = "DEPENDENCY_UNAVAILABLE"
        except ContractError:
            state["status"] = "SAFE_FAILURE"
            state["errorCode"] = "PLAN_INVALID"
        except Exception:
            state["status"] = "SAFE_FAILURE"
            state["errorCode"] = "EXECUTION_FAILED"
        try:
            self._save(request, state)
        except DependencyUnavailable:
            state["status"] = "SAFE_FAILURE"
            state["errorCode"] = "EXECUTION_STATE_UNAVAILABLE"
        self._log(correlation_id, str(state["status"]))
        return self._response(request, state)

    def _save(self, request: DispatchRequest, state: dict[str, Any]) -> None:
        state["updatedAtUtc"] = self._now()
        self.store.write(request.workflow_id, state)

    @staticmethod
    def _validate_report(raw: object, request: DispatchRequest, step_id: str, agent_id: str, expected: str) -> dict[str, Any]:
        if not isinstance(raw, dict) or raw.get("contractVersion") != 1:
            raise ContractError("specialist report contractVersion is invalid")
        if raw.get("workflowId") != request.workflow_id or raw.get("stepId") != step_id or raw.get("agentId") != agent_id:
            raise ContractError("specialist report identity does not match request")
        if raw.get("reportType") != expected or raw.get("status") != "SUCCEEDED":
            raise ContractError("specialist report type or status is invalid")
        report = raw.get("report")
        source_refs = raw.get("sourceReferences")
        uncertainty = raw.get("uncertaintyNotes")
        if (not isinstance(report, dict) or not isinstance(source_refs, list) or not source_refs or len(source_refs) > 32 or
                not all(isinstance(item, str) and 0 < len(item) <= 256 for item in source_refs)):
            raise ContractError("specialist report body or provenance is invalid")
        if PlanningCoordinator._contains_forbidden_data(report):
            raise ContractError("specialist report contains a forbidden field")
        if not isinstance(uncertainty, list) or len(uncertainty) > 32 or not all(isinstance(item, str) and len(item) <= 500 for item in uncertainty):
            raise ContractError("specialist uncertainty notes are invalid")
        if len(json.dumps(report, separators=(",", ":"))) > 24_000:
            raise ContractError("specialist report exceeds the maximum size")
        produced = raw.get("producedAtUtc")
        if not isinstance(produced, str):
            raise ContractError("specialist report timestamp is required")
        try:
            timestamp = datetime.fromisoformat(produced.replace("Z", "+00:00"))
        except ValueError as exc:
            raise ContractError("specialist report timestamp is invalid") from exc
        if timestamp.tzinfo is None:
            raise ContractError("specialist report timestamp must include timezone")
        return {
            "contractVersion": 1,
            "workflowId": request.workflow_id,
            "stepId": step_id,
            "agentId": agent_id,
            "reportType": expected,
            "status": "SUCCEEDED",
            "report": report,
            "sourceReferences": source_refs,
            "uncertaintyNotes": uncertainty,
            "producedAtUtc": produced,
        }

    @staticmethod
    def _response(request: DispatchRequest, state: dict[str, Any]) -> dict[str, Any]:
        return {
            "contractVersion": 1,
            "workflowId": request.workflow_id,
            "status": state["status"],
            "plan": state["plan"],
            "steps": state["steps"],
            "result": state["result"],
            "errorCode": state["errorCode"],
        }

    @staticmethod
    def _now() -> str:
        return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")

    @staticmethod
    def _log(correlation_id: str, outcome: str) -> None:
        logger.info("agent_execution correlation_id=%s outcome=%s", correlation_id, outcome)

    @staticmethod
    def _contains_forbidden_data(value: object) -> bool:
        forbidden = {"reasoning", "chainofthought", "prompt", "password", "token", "apikey", "credential", "secret"}
        if isinstance(value, dict):
            for key, child in value.items():
                normalized = "".join(char for char in key.lower() if char.isalnum()) if isinstance(key, str) else ""
                if normalized in forbidden or PlanningCoordinator._contains_forbidden_data(child):
                    return True
        elif isinstance(value, list):
            return any(PlanningCoordinator._contains_forbidden_data(child) for child in value)
        return False

