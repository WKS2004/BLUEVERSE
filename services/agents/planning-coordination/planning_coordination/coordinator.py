from __future__ import annotations

import json
import logging
from datetime import datetime, timezone
from typing import Any
from uuid import NAMESPACE_URL, uuid4, uuid5

from .clients import DependencyUnavailable, ExecutionStore, ModelClient, SpecialistClient
from .contracts import ContractError, DispatchRequest, ValidatedPlan

logger = logging.getLogger("blueverse.planning_coordination")


class PlanningCoordinator:
    def __init__(self, model: ModelClient, specialists: SpecialistClient, store: ExecutionStore):
        self.model = model
        self.specialists = specialists
        self.store = store

    def execute(
        self,
        request: DispatchRequest,
        correlation_id: str | None = None,
        delegated_access_token: str | None = None,
    ) -> dict[str, Any]:
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
            self.store.write(request.workflow_id, state, request.workflow_type)
            plan = ValidatedPlan.parse(self.model.create_plan(request), request.workflow_type)
            state["plan"] = plan.to_json()
            state["status"] = "DELEGATING"
            self._save(request, state)
            reports: dict[str, dict[str, Any]] = {}
            step_ids = {step.agent_id: step.step_id for step in plan.steps}
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
                    "reportType": step.expected_report_type,
                    "summary": None,
                    "sourceReferences": [],
                    "uncertaintyNotes": [],
                    "producedAtUtc": None,
                    "errorCode": None,
                }
                state["steps"].append(step_state)
                self._save(request, state)

                specialist_body = self._specialist_request(
                    request=request,
                    correlation_id=correlation_id,
                    step_id=step.step_id,
                    expected_report_type=step.expected_report_type,
                    safety_reports=self._safety_reports(request, reports) if step.agent_id == "safety_operations" else None,
                )
                try:
                    raw_report = self.specialists.dispatch(step.agent_id, specialist_body, delegated_access_token)
                    report = self._validate_report(
                        raw_report, request, step.step_id, step.agent_id, step.expected_report_type
                    )
                except (DependencyUnavailable, ContractError):
                    step_state["status"] = "UNAVAILABLE"
                    step_state["errorCode"] = "SPECIALIST_UNAVAILABLE_OR_INVALID"
                    state["status"] = "BLOCKED"
                    state["errorCode"] = step_state["errorCode"]
                    self._save(request, state)
                    return self._response(request, state)

                step_state["status"] = report["status"]
                step_state["summary"] = report["summary"]
                step_state["sourceReferences"] = report["sourceReferences"]
                step_state["uncertaintyNotes"] = report["uncertaintyNotes"]
                step_state["producedAtUtc"] = report["producedAtUtc"]
                if report.get("errorCode"):
                    step_state["errorCode"] = report["errorCode"]
                reports[step.step_id] = report
                self._save(request, state)
                if report["status"] != "SUCCEEDED":
                    state["status"] = "BLOCKED"
                    state["errorCode"] = report.get("errorCode") or "SPECIALIST_RESULT_INCOMPLETE"
                    self._save(request, state)
                    return self._response(request, state)

            safety_report = None
            if request.workflow_type == "OPERATIONAL_ASSESSMENT":
                safety_step_id = step_ids["safety_operations"]
                safety_report = reports[safety_step_id]["report"]
            state["status"] = "COMPLETED"
            state["result"] = {
                "workflowId": request.workflow_id,
                "objectiveSummary": plan.objective_summary,
                "agentSummaries": [
                    {
                        "agentId": item["agentId"],
                        "status": item["status"],
                        "summary": item["summary"],
                        "sourceReferences": item["sourceReferences"],
                        "uncertaintyNotes": item["uncertaintyNotes"],
                    }
                    for item in state["steps"]
                ],
                "safetyProposal": safety_report,
            }
            self._save(request, state)
            self._log(correlation_id, "COMPLETED", state.get("errorCode"), state.get("steps"))
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
        self._log(correlation_id, str(state["status"]), state.get("errorCode"), state.get("steps"))
        return self._response(request, state)

    @staticmethod
    def _specialist_request(
        request: DispatchRequest,
        correlation_id: str,
        step_id: str,
        expected_report_type: str,
        safety_reports: list[dict[str, object]] | None,
    ) -> dict[str, object]:
        body: dict[str, object] = {
            "workflowId": request.workflow_id,
            "workflowType": request.workflow_type,
            "stepId": step_id,
            "expectedReportType": expected_report_type,
            "objective": request.objective,
            "agentContext": request.agent_context.to_json(),
            "timeRange": {"startsAt": request.starts_at, "endsAt": request.ends_at},
            "correlationId": correlation_id,
            "constraints": request.constraints or {},
        }
        if request.recommendation_id is not None:
            body["recommendationId"] = request.recommendation_id
            body["destinationId"] = request.destination_id
            body["preferredActivityIds"] = list(request.preferred_activity_ids)
        if request.assessment is not None:
            body["assessment"] = request.assessment.to_json()
        if safety_reports is not None:
            body["safetyReports"] = safety_reports
        return body

    def _safety_reports(
        self, request: DispatchRequest, reports: dict[str, dict[str, Any]]
    ) -> list[dict[str, object]]:
        assessment = request.assessment
        if assessment is None:
            raise ContractError("operational assessment context is missing")
        result = [item.to_json() for item in assessment.reports]
        for agent_id in ("experience_biodiversity", "marine_conditions"):
            step_id = next((key for key, value in reports.items() if value.get("agentId") == agent_id), None)
            if step_id is None:
                raise ContractError("required agent evidence is missing")
            agent_report = reports[step_id]
            result.append(self._agent_evidence(request, agent_id, agent_report))
        categories = {item["category"] for item in result}
        if categories != {"planner_report", "operations_context", "safety_profile", "experience_report", "marine_report"}:
            raise ContractError("safety evidence categories are incomplete")
        return result

    @staticmethod
    def _agent_evidence(
        request: DispatchRequest, agent_id: str, report: dict[str, Any]
    ) -> dict[str, object]:
        produced_at = report.get("producedAtUtc")
        if not isinstance(produced_at, str):
            raise ContractError("agent evidence timestamp is invalid")
        valid_until = request.ends_at
        reference = str(uuid5(NAMESPACE_URL, f"blueverse:{request.workflow_id}:{report['stepId']}:{agent_id}"))
        if agent_id == "experience_biodiversity":
            content = report.get("report")
            offering = content.get("offering") if isinstance(content, dict) else None
            facts = offering if isinstance(offering, dict) else {}
            outcome = content.get("completionOutcome") if isinstance(content, dict) else None
            status = "VALID" if outcome in {"success_complete", "success_optional_data_unavailable"} else "UNAVAILABLE"
            return {
                "reference": reference,
                "category": "experience_report",
                "status": status,
                "source": "experience-biodiversity-agent",
                "observedAt": produced_at,
                "validUntil": valid_until,
                "facts": facts,
            }

        content = report.get("report")
        if not isinstance(content, dict):
            raise ContractError("marine report evidence is invalid")
        factors = content.get("environmentalFactors")
        if not isinstance(factors, dict):
            raise ContractError("marine factor evidence is invalid")
        field_map = {
            "wind_speed": "windSpeed", "wave_height": "waveHeight", "swell_wave_height": "swellHeight",
            "precipitation": "rain", "weather_code": "weatherCode",
        }
        facts: dict[str, object] = {"status": "UNKNOWN", "freshness": content.get("freshness")}
        for source_name, output_name in field_map.items():
            factor = factors.get(source_name)
            if isinstance(factor, dict):
                facts[output_name] = factor.get("value")
        required_fields = {"windSpeed", "waveHeight", "swellHeight", "rain", "weatherCode"}
        valid = (
            content.get("status") == "COMPLETE" and content.get("freshness") == "FRESH" and
            required_fields.issubset(facts) and all(facts[name] is not None for name in required_fields)
        )
        return {
            "reference": reference,
            "category": "marine_report",
            "status": "VALID" if valid else "UNAVAILABLE",
            "source": "marine-conditions-agent",
            "observedAt": produced_at,
            "validUntil": valid_until,
            "facts": facts,
        }

    def _save(self, request: DispatchRequest, state: dict[str, Any]) -> None:
        state["updatedAtUtc"] = self._now()
        # State contains only execution status and safe summaries; the short-lived
        # delegated caller credential and raw provider/model payload never enter it.
        self.store.write(request.workflow_id, state, request.workflow_type)

    @staticmethod
    def _validate_report(
        raw: object, request: DispatchRequest, step_id: str, agent_id: str, expected: str
    ) -> dict[str, Any]:
        if not isinstance(raw, dict) or raw.get("contractVersion") != 1:
            raise ContractError("specialist report contractVersion is invalid")
        if raw.get("workflowId") != request.workflow_id or raw.get("stepId") != step_id or raw.get("agentId") != agent_id:
            raise ContractError("specialist report identity does not match request")
        if raw.get("reportType") != expected:
            raise ContractError("specialist report type is invalid")
        status = raw.get("status")
        if status not in {"SUCCEEDED", "BLOCKED", "INSUFFICIENT_DATA", "UNAVAILABLE", "INVALID_RESULT"}:
            raise ContractError("specialist status is invalid")
        report = raw.get("report")
        summary = raw.get("summary")
        source_refs = raw.get("sourceReferences")
        uncertainty = raw.get("uncertaintyNotes")
        if status == "SUCCEEDED":
            if not isinstance(report, dict) or PlanningCoordinator._contains_forbidden_data(report):
                raise ContractError("specialist report body is invalid")
            if len(json.dumps(report, separators=(",", ":"))) > 24_000:
                raise ContractError("specialist report exceeds the maximum size")
        if not isinstance(summary, str) or not 1 <= len(summary.strip()) <= 1000 or "http://" in summary.lower() or "https://" in summary.lower():
            raise ContractError("specialist summary is invalid")
        if (not isinstance(source_refs, list) or len(source_refs) > 32 or
                not all(isinstance(item, str) and 0 < len(item) <= 256 for item in source_refs)):
            raise ContractError("specialist provenance is invalid")
        if status == "SUCCEEDED" and not source_refs:
            raise ContractError("successful specialist report must include provenance")
        if not isinstance(uncertainty, list) or len(uncertainty) > 32 or not all(isinstance(item, str) and len(item) <= 500 for item in uncertainty):
            raise ContractError("specialist uncertainty notes are invalid")
        produced = raw.get("producedAtUtc")
        produced_dt = datetime.fromisoformat(produced.replace("Z", "+00:00")) if isinstance(produced, str) else None
        if produced_dt is None or produced_dt.tzinfo is None:
            raise ContractError("specialist timestamp must include timezone")

        sanitized: dict[str, Any] = {
            "contractVersion": 1,
            "workflowId": request.workflow_id,
            "stepId": step_id,
            "agentId": agent_id,
            "reportType": expected,
            "status": status,
            "summary": " ".join(summary.split()),
            "sourceReferences": source_refs,
            "uncertaintyNotes": [" ".join(item.split()) for item in uncertainty],
            "producedAtUtc": produced,
        }
        if status == "SUCCEEDED":
            sanitized["report"] = report
        error_code = raw.get("errorCode")
        if error_code is not None:
            if not isinstance(error_code, str) or not error_code.isascii() or not 1 <= len(error_code) <= 64:
                raise ContractError("specialist error code is invalid")
            sanitized["errorCode"] = error_code
        return sanitized

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
    def _log(
        correlation_id: str, outcome: str, error_code: object = None, steps: object = None
    ) -> None:
        safe_steps = []
        if isinstance(steps, list):
            safe_steps = [
                f"{item.get('agentId')}:{item.get('status')}"
                for item in steps if isinstance(item, dict)
            ]
        logger.info(
            "agent_execution correlation_id=%s outcome=%s error_code=%s steps=%s",
            correlation_id, outcome, error_code or "none", ",".join(safe_steps) or "none",
        )

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
