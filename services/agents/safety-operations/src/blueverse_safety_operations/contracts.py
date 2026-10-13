"""Versioned, dependency-free contracts for the Safety & Operations role."""

from __future__ import annotations

from dataclasses import dataclass
from datetime import datetime, timedelta, timezone
import json
import math
import re
from typing import Any, Mapping
from uuid import UUID


INPUT_SCHEMA_VERSION = "safety-operations.input.v1"
OUTPUT_SCHEMA_VERSION = "safety-operations.recommendation.v1"
MAX_INPUT_BYTES = 128 * 1024
MAX_OUTPUT_BYTES = 32 * 1024
MAX_OBJECTIVE_LENGTH = 4_000
MAX_REPORTS = 12
MAX_FACTS = 64

REQUIRED_REPORT_CATEGORIES = frozenset(
    {
        "planner_report",
        "experience_report",
        "marine_report",
        "operations_context",
        "safety_profile",
    }
)

MODEL_SAFE_REPORT_ENUMS: Mapping[str, Mapping[str, frozenset[str]]] = {
    "planner_report": {
        "workflowStatus": frozenset({"PENDING", "PROCESSING", "COMPLETED", "FAILED"}),
    },
    "experience_report": {
        "availabilityStatus": frozenset({"AVAILABLE", "UNAVAILABLE", "UNKNOWN"}),
        "publicationState": frozenset({"DRAFT", "PUBLISHED", "ARCHIVED"}),
    },
    "marine_report": {
        "status": frozenset({"SUITABLE", "CAUTION", "UNSUITABLE", "UNKNOWN"}),
        "freshness": frozenset({"FRESH", "STALE", "UNAVAILABLE"}),
    },
    "operations_context": {
        "operationalStatus": frozenset(
            {"OPEN", "CAUTION", "TEMPORARILY_SUSPENDED", "CANCELLED", "COMPLETED"}
        ),
        "severity": frozenset({"LOW", "MODERATE", "HIGH", "CRITICAL"}),
    },
    "safety_profile": {},
}
MODEL_SAFE_REPORT_NUMBER_FIELDS: Mapping[str, frozenset[str]] = {
    "planner_report": frozenset(),
    "experience_report": frozenset(),
    "marine_report": frozenset({"windSpeed", "waveHeight", "swellHeight", "rain"}),
    "operations_context": frozenset(),
    "safety_profile": frozenset(
        {
            "maxWindSpeed",
            "maxWaveHeight",
            "maxSwellHeight",
            "cautionWindSpeed",
            "cautionWaveHeight",
            "cautionSwellHeight",
        }
    ),
}
MODEL_SAFE_REPORT_INTEGER_FIELDS: Mapping[str, frozenset[str]] = {
    "planner_report": frozenset(),
    "experience_report": frozenset(),
    "marine_report": frozenset({"weatherCode"}),
    "operations_context": frozenset(),
    "safety_profile": frozenset(),
}
MODEL_SAFE_REPORT_BOOLEANS: Mapping[str, frozenset[str]] = {
    "planner_report": frozenset(),
    "experience_report": frozenset(),
    "marine_report": frozenset(),
    "operations_context": frozenset(),
    "safety_profile": frozenset({"isActive"}),
}
MODEL_CONTEXT_FIELDS = frozenset(
    {
        "schemaVersion",
        "workflowId",
        "stepId",
        "dispatchId",
        "assessmentId",
        "correlationId",
        "target",
        "period",
        "objective",
        "policyVersion",
        "allowedActions",
        "reports",
    }
)
MODEL_TARGET_FIELDS = frozenset({"type", "id", "currentState", "stateVersion"})
MODEL_PERIOD_FIELDS = frozenset({"startsAt", "endsAt"})

RECOMMENDATIONS = frozenset(
    {"CONTINUE", "CONTINUE_WITH_CAUTION", "PROPOSE_ACTION", "INSUFFICIENT_DATA"}
)
REVIEW_SUGGESTIONS = frozenset({"NONE", "REVIEW_RECOMMENDED"})
EVIDENCE_STATUSES = frozenset(
    {"VALID", "UNSUITABLE", "BLOCKED", "UNKNOWN", "STALE", "CONFLICTING", "INVALID", "UNAVAILABLE"}
)
SENSITIVE_MEDIA_FACT_NAMES = frozenset(
    {"image", "imagebytes", "photo", "photobytes", "rawimage", "storageurl", "imageurl"}
)
CODE = re.compile(r"^[A-Z][A-Z0-9_]{0,63}$")


class ContractError(ValueError):
    """An input or model result does not satisfy the versioned role contract."""


def _model_safe_facts(category: Any, facts: Mapping[str, Any]) -> dict[str, Any]:
    """Project only typed operational facts with source-backed field names."""
    if not isinstance(category, str) or category not in REQUIRED_REPORT_CATEGORIES:
        raise ContractError("model context report category is unsupported")
    enum_fields = MODEL_SAFE_REPORT_ENUMS.get(category, {})
    number_fields = MODEL_SAFE_REPORT_NUMBER_FIELDS.get(category, frozenset())
    integer_fields = MODEL_SAFE_REPORT_INTEGER_FIELDS.get(category, frozenset())
    boolean_fields = MODEL_SAFE_REPORT_BOOLEANS.get(category, frozenset())
    safe: dict[str, Any] = {}
    for key, value in facts.items():
        if key not in enum_fields and key not in number_fields and key not in integer_fields and key not in boolean_fields:
            continue
        if value is None:
            safe[key] = None
        elif key in enum_fields:
            if not isinstance(value, str) or value not in enum_fields[key]:
                raise ContractError("model context report contains an invalid operational status")
            safe[key] = value
        elif key in number_fields:
            if isinstance(value, bool) or not isinstance(value, (int, float)):
                raise ContractError("model context report contains an invalid numeric fact")
            try:
                finite = math.isfinite(value)
            except OverflowError:
                finite = False
            if not finite:
                raise ContractError("model context report contains a non-finite numeric fact")
            safe[key] = value
        elif key in integer_fields:
            if type(value) is not int:
                raise ContractError("model context report contains an invalid integer fact")
            safe[key] = value
        elif key in boolean_fields:
            if not isinstance(value, bool):
                raise ContractError("model context report contains an invalid boolean fact")
            safe[key] = value
    return safe


def _model_safe_report(report: Mapping[str, Any]) -> dict[str, Any]:
    """Keep only validated report identifiers, statuses, and timestamps."""
    reference = _uuid(report.get("reference"), "model context report reference")
    category = _text(report.get("category"), "model context report category", max_length=64)
    if category not in REQUIRED_REPORT_CATEGORIES:
        raise ContractError("model context report category is unsupported")
    status = _code(report.get("status"), "model context report status")
    if status not in EVIDENCE_STATUSES:
        raise ContractError("model context report status is unsupported")
    observed_text = _text(report.get("observedAt"), "model context report observedAt", max_length=40)
    observed_at = _timestamp(observed_text, "model context report observedAt")
    valid_text = _text(report.get("validUntil"), "model context report validUntil", max_length=40)
    valid_until = _timestamp(valid_text, "model context report validUntil")
    if valid_until <= observed_at:
        raise ContractError("model context report validity period is empty")
    facts = report.get("facts", {})
    if not isinstance(facts, Mapping):
        raise ContractError("model context report facts must be an object")
    return {
        "reference": reference,
        "category": category,
        "status": status,
        "observedAt": observed_text,
        "validUntil": valid_text,
        "facts": _model_safe_facts(category, facts),
    }


def minimize_model_context(context: Mapping[str, Any]) -> dict[str, Any]:
    """Project the contract context and filter report facts at the model boundary."""
    minimized = {key: value for key, value in context.items() if key in MODEL_CONTEXT_FIELDS}
    for name, allowed_fields in (("target", MODEL_TARGET_FIELDS), ("period", MODEL_PERIOD_FIELDS)):
        nested = minimized.get(name)
        if nested is not None:
            if not isinstance(nested, Mapping):
                raise ContractError(f"model context {name} must be an object")
            minimized[name] = {key: value for key, value in nested.items() if key in allowed_fields}
    if "reports" not in minimized:
        return minimized
    reports = minimized["reports"]
    if not isinstance(reports, list):
        raise ContractError("model context reports must be a list")

    safe_reports: list[dict[str, Any]] = []
    for report in reports:
        if not isinstance(report, Mapping):
            raise ContractError("model context report must be an object")
        safe_reports.append(_model_safe_report(report))
    minimized["reports"] = safe_reports
    return minimized


def _object(value: Any, name: str) -> Mapping[str, Any]:
    if not isinstance(value, Mapping):
        raise ContractError(f"{name} must be an object")
    return value


def _exact_keys(
    value: Mapping[str, Any], *, required: set[str], optional: set[str] | None = None, name: str
) -> None:
    optional = optional or set()
    missing = required - set(value)
    extra = set(value) - required - optional
    if missing:
        raise ContractError(f"{name} is missing required fields")
    if extra:
        raise ContractError(f"{name} contains unsupported fields")


def _text(value: Any, name: str, *, max_length: int, allow_empty: bool = False) -> str:
    if not isinstance(value, str):
        raise ContractError(f"{name} must be text")
    if len(value) > max_length or (not allow_empty and not value.strip()):
        raise ContractError(f"{name} has an invalid length")
    return value


def _uuid(value: Any, name: str) -> str:
    try:
        return str(UUID(str(value)))
    except (ValueError, TypeError, AttributeError) as exc:
        raise ContractError(f"{name} must be a UUID") from exc


def _timestamp(value: Any, name: str) -> datetime:
    if not isinstance(value, str) or len(value) > 40:
        raise ContractError(f"{name} must be an RFC 3339 timestamp")
    try:
        parsed = datetime.fromisoformat(value.replace("Z", "+00:00"))
    except ValueError as exc:
        raise ContractError(f"{name} must be an RFC 3339 timestamp") from exc
    if parsed.tzinfo is None or parsed.utcoffset() is None:
        raise ContractError(f"{name} must include a timezone")
    return parsed.astimezone(timezone.utc)


def _code(value: Any, name: str) -> str:
    result = _text(value, name, max_length=64)
    if CODE.fullmatch(result) is None:
        raise ContractError(f"{name} must be an uppercase code")
    return result


def _safe_scalar(value: Any, name: str) -> str | int | float | bool | None:
    if value is None or isinstance(value, (str, bool, int)):
        if isinstance(value, str) and len(value) > 2_000:
            raise ContractError(f"{name} is too long")
        return value
    if isinstance(value, float) and math.isfinite(value):
        return value
    raise ContractError(f"{name} must be a scalar value")


@dataclass(frozen=True)
class EvidenceReport:
    reference: str
    category: str
    status: str
    source: str
    observed_at: datetime
    valid_until: datetime
    facts: Mapping[str, str | int | float | bool | None]

    @classmethod
    def from_mapping(cls, raw: Any) -> EvidenceReport:
        value = _object(raw, "report")
        _exact_keys(
            value,
            required={"reference", "category", "status", "source", "observedAt", "validUntil", "facts"},
            name="report",
        )
        reference = _uuid(value["reference"], "report.reference")
        category = _text(value["category"], "report.category", max_length=64)
        if category not in REQUIRED_REPORT_CATEGORIES:
            raise ContractError("report.category is unsupported")
        status = _code(value["status"], "report.status")
        if status not in EVIDENCE_STATUSES:
            raise ContractError("report.status is unsupported")
        source = _text(value["source"], "report.source", max_length=128)
        facts_raw = _object(value["facts"], "report.facts")
        if len(facts_raw) > MAX_FACTS:
            raise ContractError("report.facts contains too many values")
        facts: dict[str, str | int | float | bool | None] = {}
        for key, fact in facts_raw.items():
            name = _text(key, "report.facts key", max_length=64)
            normalized_name = re.sub(r"[^a-z]", "", name.casefold())
            if (
                normalized_name in SENSITIVE_MEDIA_FACT_NAMES
                or any(token in normalized_name for token in ("image", "photo", "storageurl", "signedurl", "attachment"))
            ):
                raise ContractError("raw media fields are not permitted")
            facts[name] = _safe_scalar(fact, f"report.facts.{name}")
        # Recognized operational fields have a closed type/value contract;
        # unrecognized fields remain local and are omitted from model context.
        _model_safe_facts(category, facts)
        observed_at = _timestamp(value["observedAt"], "report.observedAt")
        valid_until = _timestamp(value["validUntil"], "report.validUntil")
        if valid_until <= observed_at:
            raise ContractError("report validity period is empty")
        return cls(reference, category, status, source, observed_at, valid_until, facts)


@dataclass(frozen=True)
class SafetyAssessmentInput:
    workflow_id: str
    step_id: str
    dispatch_id: str
    assessment_id: str
    correlation_id: str
    target_type: str
    target_id: str
    current_state: str
    target_state_version: int
    period_starts_at: datetime
    period_ends_at: datetime
    objective: str
    policy_version: str
    allowed_actions: tuple[str, ...]
    reports: tuple[EvidenceReport, ...]

    @classmethod
    def from_mapping(cls, raw: Any) -> SafetyAssessmentInput:
        value = _object(raw, "input")
        _exact_keys(
            value,
            required={
                "schemaVersion",
                "workflowId",
                "stepId",
                "dispatchId",
                "assessmentId",
                "correlationId",
                "target",
                "period",
                "objective",
                "policyVersion",
                "allowedActions",
                "reports",
            },
            name="input",
        )
        try:
            encoded = json.dumps(value, ensure_ascii=False, allow_nan=False, separators=(",", ":")).encode("utf-8")
        except (TypeError, ValueError) as exc:
            raise ContractError("input must contain JSON values") from exc
        if len(encoded) > MAX_INPUT_BYTES:
            raise ContractError("input exceeds the byte limit")
        if value["schemaVersion"] != INPUT_SCHEMA_VERSION:
            raise ContractError("input schema version is unsupported")

        target = _object(value["target"], "target")
        _exact_keys(
            target,
            required={"type", "id", "currentState", "stateVersion"},
            name="target",
        )
        target_type = _code(target["type"], "target.type")
        target_id = _uuid(target["id"], "target.id")
        current_state = _code(target["currentState"], "target.currentState")
        state_version = target["stateVersion"]
        if isinstance(state_version, bool) or not isinstance(state_version, int) or state_version < 1:
            raise ContractError("target.stateVersion must be a positive integer")

        period = _object(value["period"], "period")
        _exact_keys(period, required={"startsAt", "endsAt"}, name="period")
        starts_at = _timestamp(period["startsAt"], "period.startsAt")
        ends_at = _timestamp(period["endsAt"], "period.endsAt")
        if ends_at <= starts_at:
            raise ContractError("period must end after it starts")

        actions_raw = value["allowedActions"]
        if not isinstance(actions_raw, list) or len(actions_raw) > 32:
            raise ContractError("allowedActions must be a bounded list")
        actions = tuple(_code(item, "allowedActions item") for item in actions_raw)
        if len(set(actions)) != len(actions):
            raise ContractError("allowedActions must be unique")

        reports_raw = value["reports"]
        if not isinstance(reports_raw, list) or not 1 <= len(reports_raw) <= MAX_REPORTS:
            raise ContractError("reports must be a bounded non-empty list")
        reports = tuple(EvidenceReport.from_mapping(item) for item in reports_raw)
        if len({item.reference for item in reports}) != len(reports):
            raise ContractError("report references must be unique")
        categories = [item.category for item in reports]
        if len(set(categories)) != len(categories):
            raise ContractError("report categories must be unique")
        if not REQUIRED_REPORT_CATEGORIES.issubset(categories):
            raise ContractError("required reports are missing")

        objective = _text(value["objective"], "objective", max_length=MAX_OBJECTIVE_LENGTH)
        policy_version = _text(value["policyVersion"], "policyVersion", max_length=64)
        correlation_id = _text(value["correlationId"], "correlationId", max_length=128)

        return cls(
            workflow_id=_uuid(value["workflowId"], "workflowId"),
            step_id=_text(value["stepId"], "stepId", max_length=128),
            dispatch_id=_uuid(value["dispatchId"], "dispatchId"),
            assessment_id=_uuid(value["assessmentId"], "assessmentId"),
            correlation_id=correlation_id,
            target_type=target_type,
            target_id=target_id,
            current_state=current_state,
            target_state_version=state_version,
            period_starts_at=starts_at,
            period_ends_at=ends_at,
            objective=objective,
            policy_version=policy_version,
            allowed_actions=actions,
            reports=reports,
        )

    def evidence_status(self, now: datetime) -> str:
        """Return a fail-closed evidence outcome before any model call."""
        if now.tzinfo is None or now.utcoffset() is None:
            raise ValueError("now must be timezone-aware")
        current = now.astimezone(timezone.utc)
        statuses = {report.status for report in self.reports}
        if statuses.intersection({"UNSUITABLE", "BLOCKED"}):
            return "BLOCKED"
        if any(
            report.status != "VALID"
            or report.valid_until <= current
            or (
                report.category == "marine_report"
                and report.valid_until < self.period_ends_at
            )
            or report.observed_at > current + timedelta(minutes=5)
            for report in self.reports
        ):
            return "INSUFFICIENT_DATA"
        return "READY"

    def references_by_category(self) -> dict[str, str]:
        return {report.category: report.reference for report in self.reports}

    def to_model_context(self) -> dict[str, Any]:
        """Build minimized data separately from the fixed role instructions.

        Report facts are source-defined free-form data. Only the typed,
        category-specific operational fields in the model-safe allowlist cross
        the provider boundary; arbitrary and free-form values remain local.
        """
        return minimize_model_context({
            "schemaVersion": INPUT_SCHEMA_VERSION,
            "workflowId": self.workflow_id,
            "stepId": self.step_id,
            "dispatchId": self.dispatch_id,
            "assessmentId": self.assessment_id,
            "correlationId": self.correlation_id,
            "target": {
                "type": self.target_type,
                "id": self.target_id,
                "currentState": self.current_state,
                "stateVersion": self.target_state_version,
            },
            "period": {
                "startsAt": self.period_starts_at.isoformat().replace("+00:00", "Z"),
                "endsAt": self.period_ends_at.isoformat().replace("+00:00", "Z"),
            },
            "objective": self.objective,
            "policyVersion": self.policy_version,
            "allowedActions": list(self.allowed_actions),
            "reports": [
                {
                    "reference": report.reference,
                    "category": report.category,
                    "status": report.status,
                    "source": report.source,
                    "observedAt": report.observed_at.isoformat().replace("+00:00", "Z"),
                    "validUntil": report.valid_until.isoformat().replace("+00:00", "Z"),
                    "facts": _model_safe_facts(report.category, report.facts),
                }
                for report in self.reports
            ],
        })


@dataclass(frozen=True)
class SafetyOperationsRecommendation:
    workflow_id: str
    step_id: str
    assessment_id: str
    target_id: str
    recommendation: str
    summary: str
    proposed_action: str | None
    evidence_references: tuple[str, ...]
    uncertainties: tuple[str, ...]
    review_suggestion: str

    @classmethod
    def from_mapping(
        cls, raw: Any, request: SafetyAssessmentInput
    ) -> SafetyOperationsRecommendation:
        value = _object(raw, "recommendation")
        try:
            encoded = json.dumps(value, ensure_ascii=False, allow_nan=False, separators=(",", ":")).encode("utf-8")
        except (TypeError, ValueError) as exc:
            raise ContractError("recommendation must contain JSON values") from exc
        if len(encoded) > MAX_OUTPUT_BYTES:
            raise ContractError("recommendation exceeds the byte limit")
        _exact_keys(
            value,
            required={
                "schemaVersion",
                "workflowId",
                "stepId",
                "assessmentId",
                "targetId",
                "recommendation",
                "summary",
                "proposedAction",
                "evidenceReferences",
                "uncertainties",
                "reviewSuggestion",
            },
            name="recommendation",
        )
        if value["schemaVersion"] != OUTPUT_SCHEMA_VERSION:
            raise ContractError("recommendation schema version is unsupported")
        workflow_id = _uuid(value["workflowId"], "recommendation.workflowId")
        step_id = _text(value["stepId"], "recommendation.stepId", max_length=128)
        assessment_id = _uuid(value["assessmentId"], "recommendation.assessmentId")
        target_id = _uuid(value["targetId"], "recommendation.targetId")
        if (workflow_id, step_id, assessment_id, target_id) != (
            request.workflow_id,
            request.step_id,
            request.assessment_id,
            request.target_id,
        ):
            raise ContractError("recommendation correlation does not match the request")

        recommendation = _code(value["recommendation"], "recommendation.recommendation")
        if recommendation not in RECOMMENDATIONS:
            raise ContractError("recommendation value is unsupported")
        summary = _text(value["summary"], "recommendation.summary", max_length=1_000)
        proposed_action_raw = value["proposedAction"]
        proposed_action = None if proposed_action_raw is None else _code(
            proposed_action_raw, "recommendation.proposedAction"
        )
        if proposed_action is not None and proposed_action not in request.allowed_actions:
            raise ContractError("proposed action is outside the policy allowlist")
        if (recommendation == "PROPOSE_ACTION") != (proposed_action is not None):
            raise ContractError("proposedAction does not match the recommendation")
        if recommendation == "INSUFFICIENT_DATA" and proposed_action is not None:
            raise ContractError("insufficient data cannot carry an action")

        references_raw = value["evidenceReferences"]
        if not isinstance(references_raw, list) or not 1 <= len(references_raw) <= MAX_REPORTS:
            raise ContractError("evidenceReferences must be a bounded non-empty list")
        references = tuple(
            _text(item, "evidenceReferences item", max_length=128) for item in references_raw
        )
        known_references = {report.reference for report in request.reports}
        if len(set(references)) != len(references) or not set(references).issubset(known_references):
            raise ContractError("evidenceReferences must identify unique input reports")
        cited_categories = {
            report.category for report in request.reports if report.reference in references
        }
        if not REQUIRED_REPORT_CATEGORIES.issubset(cited_categories):
            raise ContractError("recommendation must cite every required evidence category")

        uncertainties_raw = value["uncertainties"]
        if not isinstance(uncertainties_raw, list) or len(uncertainties_raw) > 10:
            raise ContractError("uncertainties must be a bounded list")
        uncertainties = tuple(
            _text(item, "uncertainties item", max_length=300) for item in uncertainties_raw
        )
        review_suggestion = _code(value["reviewSuggestion"], "reviewSuggestion")
        if review_suggestion not in REVIEW_SUGGESTIONS:
            raise ContractError("reviewSuggestion is unsupported")

        return cls(
            workflow_id=workflow_id,
            step_id=step_id,
            assessment_id=assessment_id,
            target_id=target_id,
            recommendation=recommendation,
            summary=summary,
            proposed_action=proposed_action,
            evidence_references=references,
            uncertainties=uncertainties,
            review_suggestion=review_suggestion,
        )

    def to_mapping(self) -> dict[str, Any]:
        return {
            "schemaVersion": OUTPUT_SCHEMA_VERSION,
            "workflowId": self.workflow_id,
            "stepId": self.step_id,
            "assessmentId": self.assessment_id,
            "targetId": self.target_id,
            "recommendation": self.recommendation,
            "summary": self.summary,
            "proposedAction": self.proposed_action,
            "evidenceReferences": list(self.evidence_references),
            "uncertainties": list(self.uncertainties),
            "reviewSuggestion": self.review_suggestion,
        }
