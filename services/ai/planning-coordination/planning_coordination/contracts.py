from __future__ import annotations

from dataclasses import dataclass
from datetime import datetime
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


@dataclass(frozen=True)
class DispatchRequest:
    workflow_id: str
    recommendation_id: str
    workflow_type: str
    objective: str
    destination_id: str
    starts_at: str
    ends_at: str
    preferred_activity_ids: tuple[str, ...]
    constraints: dict[str, object]

    @classmethod
    def parse(cls, payload: object) -> DispatchRequest:
        if not isinstance(payload, dict):
            raise ContractError("request body must be an object")
        if payload.get("contractVersion") != 1:
            raise ContractError("contractVersion must equal 1")
        workflow_type = payload.get("workflowType")
        if workflow_type not in {"TOURIST_RECOMMENDATION", "OPERATIONAL_ASSESSMENT"}:
            raise ContractError("workflowType is unsupported")
        objective = payload.get("objective")
        if not isinstance(objective, str) or not objective.strip() or len(objective) > 1000:
            raise ContractError("objective must contain 1 to 1000 characters")
        starts_at = payload.get("timeRange", {}).get("startsAt") if isinstance(payload.get("timeRange"), dict) else None
        ends_at = payload.get("timeRange", {}).get("endsAt") if isinstance(payload.get("timeRange"), dict) else None
        for value, name in ((starts_at, "timeRange.startsAt"), (ends_at, "timeRange.endsAt")):
            if not isinstance(value, str):
                raise ContractError(f"{name} is required")
            try:
                parsed = datetime.fromisoformat(value.replace("Z", "+00:00"))
            except ValueError as exc:
                raise ContractError(f"{name} must be an ISO-8601 timestamp") from exc
            if parsed.tzinfo is None:
                raise ContractError(f"{name} must include a timezone")
        if datetime.fromisoformat(ends_at.replace("Z", "+00:00")) <= datetime.fromisoformat(starts_at.replace("Z", "+00:00")):
            raise ContractError("timeRange.endsAt must be after startsAt")
        preferred = payload.get("preferredActivityIds", [])
        if not isinstance(preferred, list) or len(preferred) > 50:
            raise ContractError("preferredActivityIds must be an array with at most 50 UUIDs")
        preferred_ids = tuple(_uuid(value, "preferredActivityIds[]") for value in preferred)
        if len(set(preferred_ids)) != len(preferred_ids):
            raise ContractError("preferredActivityIds must be unique")
        constraints = payload.get("constraints", {})
        if not isinstance(constraints, dict) or len(constraints) > 32:
            raise ContractError("constraints must be an object with at most 32 properties")
        for key, value in constraints.items():
            if not isinstance(key, str) or not key.strip() or len(key) > 64:
                raise ContractError("constraint names must contain 1 to 64 characters")
            if isinstance(value, str):
                if len(value) > 200:
                    raise ContractError("constraint text must not exceed 200 characters")
            elif value is not None and (not isinstance(value, (int, float, bool)) or isinstance(value, float) and not __import__("math").isfinite(value)):
                raise ContractError("constraint values must be short scalar values")
        return cls(
            workflow_id=_uuid(payload.get("workflowId"), "workflowId"),
            recommendation_id=_uuid(payload.get("recommendationId"), "recommendationId"),
            workflow_type=workflow_type,
            objective=objective.strip(),
            destination_id=_uuid(payload.get("destinationId"), "destinationId"),
            starts_at=starts_at,
            ends_at=ends_at,
            preferred_activity_ids=preferred_ids,
            constraints=constraints,
        )


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
        if not isinstance(raw_steps, list) or not 1 <= len(raw_steps) <= 3:
            raise ContractError("steps must contain 1 to 3 entries")
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
        if not required.issubset(seen_agents):
            raise ContractError("plan must include experience and marine specialists")
        if workflow_type == "OPERATIONAL_ASSESSMENT" and "safety_operations" not in seen_agents:
            raise ContractError("operational assessment plan must include safety operations specialist")
        return cls(summary.strip(), tuple(steps))

    def to_json(self) -> dict[str, object]:
        return {
            "schemaVersion": 1,
            "objectiveSummary": self.objective_summary,
            "steps": [
                {
                    "stepId": step.step_id,
                    "agentId": step.agent_id,
                    "dependsOn": list(step.depends_on),
                    "expectedReportType": step.expected_report_type,
                }
                for step in self.steps
            ],
        }

