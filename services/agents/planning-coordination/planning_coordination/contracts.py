from __future__ import annotations

from dataclasses import dataclass
from datetime import datetime
import math
import re
from typing import Any
from uuid import UUID


class ContractError(ValueError):
    pass


def _uuid(value: object, name: str) -> str:
    if not isinstance(value, str):
        raise ContractError(f"{name} must be a UUID")
    try:
        parsed = UUID(value)
    except (ValueError, AttributeError) as exc:
        raise ContractError(f"{name} must be a UUID") from exc
    if parsed.int == 0:
        raise ContractError(f"{name} must not be empty")
    return str(parsed)


def _timestamp(value: object, name: str) -> datetime:
    if not isinstance(value, str) or len(value) > 40:
        raise ContractError(f"{name} must be an RFC 3339 timestamp")
    try:
        parsed = datetime.fromisoformat(value.replace("Z", "+00:00"))
    except ValueError as exc:
        raise ContractError(f"{name} must be an RFC 3339 timestamp") from exc
    if parsed.tzinfo is None or parsed.utcoffset() is None:
        raise ContractError(f"{name} must include a timezone")
    return parsed


def _exact_keys(value: object, expected: set[str], name: str) -> dict[str, Any]:
    if not isinstance(value, dict) or set(value) != expected:
        raise ContractError(f"{name} has missing or unsupported fields")
    return value


def _code(value: object, name: str, limit: int = 64) -> str:
    if not isinstance(value, str) or not 1 <= len(value) <= limit or not re.fullmatch(r"[A-Z][A-Z0-9_]*", value):
        raise ContractError(f"{name} must be a bounded uppercase code")
    return value


@dataclass(frozen=True)
class AgentContext:
    destination_id: str
    activity_id: str
    activity_name: str
    latitude: float
    longitude: float
    offering_id: str | None = None

    @classmethod
    def parse(cls, raw: object) -> AgentContext:
        allowed = {"destinationId", "activityId", "activityName", "latitude", "longitude", "offeringId"}
        required = allowed - {"offeringId"}
        if not isinstance(raw, dict) or not required.issubset(raw) or set(raw) - allowed:
            raise ContractError("agentContext has missing or unsupported fields")
        latitude, longitude = raw.get("latitude"), raw.get("longitude")
        if (isinstance(latitude, bool) or not isinstance(latitude, (int, float)) or not math.isfinite(latitude)
                or not -90 <= latitude <= 90):
            raise ContractError("agentContext.latitude is invalid")
        if (isinstance(longitude, bool) or not isinstance(longitude, (int, float)) or not math.isfinite(longitude)
                or not -180 <= longitude <= 180):
            raise ContractError("agentContext.longitude is invalid")
        activity_name = raw.get("activityName")
        if not isinstance(activity_name, str) or not 1 <= len(activity_name.strip()) <= 100:
            raise ContractError("agentContext.activityName is invalid")
        return cls(
            _uuid(raw.get("destinationId"), "agentContext.destinationId"),
            _uuid(raw.get("activityId"), "agentContext.activityId"),
            activity_name.strip(),
            float(latitude),
            float(longitude),
            _uuid(raw["offeringId"], "agentContext.offeringId") if raw.get("offeringId") is not None else None,
        )

    def to_json(self) -> dict[str, object]:
        result: dict[str, object] = {
            "destinationId": self.destination_id,
            "activityId": self.activity_id,
            "activityName": self.activity_name,
            "latitude": self.latitude,
            "longitude": self.longitude,
        }
        if self.offering_id is not None:
            result["offeringId"] = self.offering_id
        return result


@dataclass(frozen=True)
class EvidenceInput:
    reference: str
    category: str
    status: str
    source: str
    observed_at: str
    valid_until: str
    facts: dict[str, str | int | float | bool | None]

    @classmethod
    def parse(cls, raw: object) -> EvidenceInput:
        value = _exact_keys(raw, {"reference", "category", "status", "source", "observedAt", "validUntil", "facts"}, "assessment report")
        reference = _uuid(value["reference"], "assessment report.reference")
        category = value["category"]
        if category not in {"planner_report", "operations_context", "safety_profile"}:
            raise ContractError("assessment reports must be planner, operations, or safety-profile evidence")
        status = _code(value["status"], "assessment report.status")
        if status not in {"VALID", "UNSUITABLE", "BLOCKED", "UNKNOWN", "STALE", "CONFLICTING", "INVALID", "UNAVAILABLE"}:
            raise ContractError("assessment report.status is unsupported")
        source = value["source"]
        if not isinstance(source, str) or not 1 <= len(source.strip()) <= 128:
            raise ContractError("assessment report.source is invalid")
        observed = value["observedAt"]
        valid = value["validUntil"]
        observed_dt, valid_dt = _timestamp(observed, "assessment report.observedAt"), _timestamp(valid, "assessment report.validUntil")
        if valid_dt <= observed_dt:
            raise ContractError("assessment report validity period is empty")
        facts = value["facts"]
        if not isinstance(facts, dict) or len(facts) > 64:
            raise ContractError("assessment report.facts is invalid")
        for key, fact in facts.items():
            if not isinstance(key, str) or not 1 <= len(key) <= 64:
                raise ContractError("assessment report fact name is invalid")
            if isinstance(fact, str):
                if len(fact) > 2000:
                    raise ContractError("assessment report fact is too long")
            elif fact is not None and (not isinstance(fact, (int, float, bool)) or
                                      isinstance(fact, float) and not math.isfinite(fact)):
                raise ContractError("assessment report facts must be scalar")
        return cls(reference, category, status, source.strip(), observed, valid, facts)

    def to_json(self) -> dict[str, object]:
        return {"reference": self.reference, "category": self.category, "status": self.status,
                "source": self.source, "observedAt": self.observed_at, "validUntil": self.valid_until,
                "facts": self.facts}


@dataclass(frozen=True)
class OperationalAssessment:
    assessment_id: str
    dispatch_id: str
    target: dict[str, object]
    policy_version: str
    allowed_actions: tuple[str, ...]
    reports: tuple[EvidenceInput, ...]

    @classmethod
    def parse(cls, raw: object) -> OperationalAssessment:
        value = _exact_keys(raw, {"assessmentId", "dispatchId", "target", "policyVersion", "allowedActions", "reports"}, "assessment")
        target = _exact_keys(value["target"], {"type", "id", "currentState", "stateVersion"}, "assessment.target")
        target_id = _uuid(target["id"], "assessment.target.id")
        state_version = target["stateVersion"]
        if isinstance(state_version, bool) or not isinstance(state_version, int) or state_version < 1:
            raise ContractError("assessment.target.stateVersion must be positive")
        normalized_target = {
            "type": _code(target["type"], "assessment.target.type"),
            "id": target_id,
            "currentState": _code(target["currentState"], "assessment.target.currentState"),
            "stateVersion": state_version,
        }
        policy = value["policyVersion"]
        if not isinstance(policy, str) or not 1 <= len(policy.strip()) <= 64:
            raise ContractError("assessment.policyVersion is invalid")
        actions = value["allowedActions"]
        if not isinstance(actions, list) or len(actions) > 32:
            raise ContractError("assessment.allowedActions must be a bounded list")
        parsed_actions = tuple(_code(action, "assessment.allowedActions[]") for action in actions)
        if len(set(parsed_actions)) != len(parsed_actions):
            raise ContractError("assessment.allowedActions must be unique")
        reports_raw = value["reports"]
        if not isinstance(reports_raw, list) or len(reports_raw) != 3:
            raise ContractError("assessment.reports must contain the three source reports")
        reports = tuple(EvidenceInput.parse(item) for item in reports_raw)
        required = {"planner_report", "operations_context", "safety_profile"}
        if {item.category for item in reports} != required or len({item.reference for item in reports}) != 3:
            raise ContractError("assessment.reports must uniquely cover planner, operations, and safety-profile evidence")
        return cls(_uuid(value["assessmentId"], "assessment.assessmentId"),
                   _uuid(value["dispatchId"], "assessment.dispatchId"), normalized_target,
                   policy.strip(), parsed_actions, reports)

    def to_json(self) -> dict[str, object]:
        return {"assessmentId": self.assessment_id, "dispatchId": self.dispatch_id,
                "target": self.target, "policyVersion": self.policy_version,
                "allowedActions": list(self.allowed_actions), "reports": [item.to_json() for item in self.reports]}


@dataclass(frozen=True)
class DispatchRequest:
    workflow_id: str
    workflow_type: str
    objective: str
    starts_at: str
    ends_at: str
    agent_context: AgentContext
    recommendation_id: str | None = None
    destination_id: str | None = None
    preferred_activity_ids: tuple[str, ...] = ()
    constraints: dict[str, object] | None = None
    assessment: OperationalAssessment | None = None

    @classmethod
    def parse(cls, payload: object) -> DispatchRequest:
        if not isinstance(payload, dict):
            raise ContractError("request body must be an object")
        workflow_type = payload.get("workflowType")
        common = {"contractVersion", "workflowId", "workflowType", "objective", "timeRange", "agentContext"}
        if workflow_type == "TOURIST_RECOMMENDATION":
            expected = common | {"recommendationId", "destinationId", "preferredActivityIds", "constraints"}
        elif workflow_type == "OPERATIONAL_ASSESSMENT":
            expected = common | {"assessment"}
        else:
            raise ContractError("workflowType is unsupported")
        if set(payload) != expected or payload.get("contractVersion") != 1:
            raise ContractError("request has missing or unsupported fields")
        objective = payload.get("objective")
        if not isinstance(objective, str) or not objective.strip() or len(objective) > 1000:
            raise ContractError("objective must contain 1 to 1000 characters")
        time_range = _exact_keys(payload.get("timeRange"), {"startsAt", "endsAt"}, "timeRange")
        start, end = _timestamp(time_range["startsAt"], "timeRange.startsAt"), _timestamp(time_range["endsAt"], "timeRange.endsAt")
        if end <= start:
            raise ContractError("timeRange.endsAt must be after startsAt")
        context = AgentContext.parse(payload["agentContext"])
        workflow_id = _uuid(payload.get("workflowId"), "workflowId")
        if workflow_type == "TOURIST_RECOMMENDATION":
            preferred = payload["preferredActivityIds"]
            if not isinstance(preferred, list) or len(preferred) > 50:
                raise ContractError("preferredActivityIds must be a bounded UUID array")
            preferred_ids = tuple(_uuid(value, "preferredActivityIds[]") for value in preferred)
            if len(set(preferred_ids)) != len(preferred_ids):
                raise ContractError("preferredActivityIds must be unique")
            if preferred_ids and context.activity_id not in preferred_ids:
                raise ContractError("agentContext.activityId must be one of the preferred activities")
            constraints = payload["constraints"]
            if not isinstance(constraints, dict) or len(constraints) > 32:
                raise ContractError("constraints must be a bounded object")
            for key, value in constraints.items():
                if not isinstance(key, str) or not 1 <= len(key) <= 64:
                    raise ContractError("constraint name is invalid")
                if isinstance(value, str):
                    if len(value) > 200:
                        raise ContractError("constraint text is too long")
                elif value is not None and (not isinstance(value, (int, float, bool)) or
                                            isinstance(value, float) and not math.isfinite(value)):
                    raise ContractError("constraint values must be finite scalars")
            destination_id = _uuid(payload["destinationId"], "destinationId")
            if destination_id != context.destination_id:
                raise ContractError("agentContext.destinationId does not match destinationId")
            return cls(workflow_id, workflow_type, objective.strip(), time_range["startsAt"], time_range["endsAt"], context,
                       _uuid(payload["recommendationId"], "recommendationId"), destination_id, preferred_ids,
                       constraints, None)

        assessment = OperationalAssessment.parse(payload["assessment"])
        return cls(workflow_id, workflow_type, objective.strip(), time_range["startsAt"], time_range["endsAt"], context,
                   assessment=assessment)


@dataclass(frozen=True)
class PlanStep:
    step_id: str
    agent_id: str
    depends_on: tuple[str, ...]
    expected_report_type: str


@dataclass(frozen=True)
class ValidatedPlan:
    objective_summary: str
    steps: tuple[PlanStep, ...]

    @classmethod
    def parse(cls, value: object, workflow_type: str) -> ValidatedPlan:
        if not isinstance(value, dict) or value.get("schemaVersion") != 1:
            raise ContractError("plan schemaVersion must equal 1")
        summary = value.get("objectiveSummary")
        if not isinstance(summary, str) or not summary.strip() or len(summary) > 500:
            raise ContractError("objectiveSummary must contain 1 to 500 characters")
        raw_steps = value.get("steps")
        expected_count = 3 if workflow_type == "OPERATIONAL_ASSESSMENT" else 2
        if not isinstance(raw_steps, list) or len(raw_steps) != expected_count:
            raise ContractError("steps must contain exactly the allowlisted specialist roles")
        role_reports = {
            "experience_biodiversity": "ExperienceBiodiversityContextReport.v1",
            "marine_conditions": "MarineConditionsReport.v1",
            "safety_operations": "SafetyOperationsRecommendation.v1",
        }
        allowed_agents = {"experience_biodiversity", "marine_conditions"}
        if workflow_type == "OPERATIONAL_ASSESSMENT":
            allowed_agents.add("safety_operations")
        steps: list[PlanStep] = []
        seen_ids: set[str] = set()
        seen_agents: set[str] = set()
        for raw in raw_steps:
            if not isinstance(raw, dict) or set(raw) != {"stepId", "agentId", "dependsOn", "expectedReportType"}:
                raise ContractError("each step must contain only stepId, agentId, dependsOn, expectedReportType")
            step_id, agent_id = raw["stepId"], raw["agentId"]
            if not isinstance(step_id, str) or not 1 <= len(step_id) <= 64 or not step_id.replace("-", "").isalnum():
                raise ContractError("stepId has invalid format")
            if step_id in seen_ids or agent_id not in allowed_agents or agent_id in seen_agents:
                raise ContractError("step ID or assigned agent is duplicate or not allowlisted")
            expected = role_reports[agent_id]
            if raw["expectedReportType"] != expected:
                raise ContractError("expectedReportType does not match assigned agent")
            dependencies = raw["dependsOn"]
            if not isinstance(dependencies, list) or any(dep not in seen_ids for dep in dependencies):
                raise ContractError("dependencies must refer only to earlier steps")
            if len(set(dependencies)) != len(dependencies):
                raise ContractError("dependencies must be unique")
            steps.append(PlanStep(step_id, agent_id, tuple(dependencies), expected))
            seen_ids.add(step_id)
            seen_agents.add(agent_id)
        required = {"experience_biodiversity", "marine_conditions"}
        if workflow_type == "OPERATIONAL_ASSESSMENT":
            required.add("safety_operations")
        if seen_agents != required:
            raise ContractError("plan must include every required specialist exactly once")
        if workflow_type == "OPERATIONAL_ASSESSMENT" and steps[-1].agent_id != "safety_operations":
            raise ContractError("safety_operations must run after its evidence specialists")
        return cls(summary.strip(), tuple(steps))

    def to_json(self) -> dict[str, object]:
        return {
            "schemaVersion": 1,
            "objectiveSummary": self.objective_summary,
            "steps": [
                {"stepId": step.step_id, "agentId": step.agent_id, "dependsOn": list(step.depends_on),
                 "expectedReportType": step.expected_report_type}
                for step in self.steps
            ],
        }
