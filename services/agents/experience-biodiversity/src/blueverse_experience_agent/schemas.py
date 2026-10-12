"""Zero-dependency schema definitions and validator for Coastal Experience & Biodiversity Agent.

Provides standard library dataclasses with strict JSON serialization, deserialization,
and boundary validation without external binary C/Rust extension requirements.
Compatible across minimal Python 3.12 and 3.13 runtimes (including DHI Alpine minimal image).
"""

from __future__ import annotations

import math
from dataclasses import asdict, dataclass, field
from datetime import datetime, timezone
from enum import Enum
from typing import Any, Dict, List, Optional
from uuid import UUID


ALLOWLISTED_TOOLS: tuple[str, ...] = (
    "destination_lookup",
    "activity_lookup",
    "offering_lookup",
    "schedule_lookup",
    "biodiversity_prediction_lookup",
)

FORBIDDEN_SECRET_KEYS: frozenset[str] = frozenset(
    {
        "password",
        "access_token",
        "refresh_token",
        "jwt_secret",
        "signing_key",
        "api_key",
        "private_key",
        "secret",
        "client_secret",
    }
)


def parse_non_empty_uuid(value: Any, field_name: str) -> str:
    """Validate that value is a canonical non-empty UUID string."""
    if not isinstance(value, str) or not value.strip():
        raise ValueError(f"{field_name} must be a non-empty UUID string")
    try:
        parsed = UUID(value.strip())
    except (ValueError, AttributeError, TypeError) as exc:
        raise ValueError(f"{field_name} must be a valid UUID string") from exc
    if parsed.int == 0:
        raise ValueError(f"{field_name} must not be the empty UUID (00000000-0000-0000-0000-000000000000)")
    return str(parsed)


def parse_iso8601_timestamp(value: Any, field_name: str) -> datetime:
    """Validate and parse an ISO-8601 timestamp string."""
    if not isinstance(value, str) or not value.strip():
        raise ValueError(f"{field_name} must be a non-empty ISO-8601 timestamp string")
    normalized = value.strip()
    if normalized.endswith("Z"):
        normalized = normalized[:-1] + "+00:00"
    try:
        dt = datetime.fromisoformat(normalized)
    except ValueError as exc:
        raise ValueError(f"{field_name} must be a valid ISO-8601 timestamp") from exc
    if dt.tzinfo is None:
        dt = dt.replace(tzinfo=timezone.utc)
    return dt


class AgentCompletionOutcome(str, Enum):
    SUCCESS_COMPLETE = "success_complete"
    SUCCESS_OPTIONAL_DATA_UNAVAILABLE = "success_optional_data_unavailable"
    REQUIRES_UPSTREAM_CORRECTION = "requires_upstream_correction"
    FAILED = "failed"


class BiodiversityStatus(str, Enum):
    AVAILABLE = "available"
    NOT_REQUESTED = "not_requested"
    UNAVAILABLE = "unavailable"
    MISSING = "missing"
    STALE = "stale"
    INVALID = "invalid"


@dataclass(frozen=True)
class FocalSpeciesPrediction:
    species_id: str
    species_name: str
    scientific_name: str
    conservation_status: Optional[str] = None
    occurrence_probability: Optional[float] = None
    habitat_suitability: float = 0.0
    primary_threats: Optional[str] = None
    confidence_level: Optional[str] = None
    uncertainty_text: Optional[str] = None

    def __post_init__(self) -> None:
        parse_non_empty_uuid(self.species_id, "species_id")
        if not isinstance(self.species_name, str) or not self.species_name.strip():
            raise ValueError("species_name must be a non-empty string")
        if not isinstance(self.scientific_name, str) or not self.scientific_name.strip():
            raise ValueError("scientific_name must be a non-empty string")
        if (
            isinstance(self.habitat_suitability, bool)
            or not isinstance(self.habitat_suitability, (int, float))
            or not math.isfinite(float(self.habitat_suitability))
            or not (0.0 <= float(self.habitat_suitability) <= 1.0)
        ):
            raise ValueError(f"habitat_suitability must be a finite number in [0.0, 1.0], got {self.habitat_suitability}")
        if self.occurrence_probability is not None:
            if (
                isinstance(self.occurrence_probability, bool)
                or not isinstance(self.occurrence_probability, (int, float))
                or not math.isfinite(float(self.occurrence_probability))
                or not (0.0 <= float(self.occurrence_probability) <= 1.0)
            ):
                raise ValueError(
                    f"occurrence_probability must be a finite number in [0.0, 1.0], got {self.occurrence_probability}"
                )


@dataclass(frozen=True)
class DestinationContext:
    destination_id: str
    name: str
    region: str
    latitude: float
    longitude: float
    status: str
    slug: Optional[str] = None
    description: Optional[str] = None
    created_at: Optional[str] = None
    updated_at: Optional[str] = None
    source_freshness_timestamp: Optional[str] = None

    def __post_init__(self) -> None:
        parse_non_empty_uuid(self.destination_id, "destination_id")
        if not isinstance(self.name, str) or not self.name.strip():
            raise ValueError("Destination name must be non-empty")
        if (
            isinstance(self.latitude, bool)
            or not isinstance(self.latitude, (int, float))
            or not math.isfinite(float(self.latitude))
            or not (-90.0 <= float(self.latitude) <= 90.0)
        ):
            raise ValueError(f"Destination latitude must be in [-90.0, 90.0], got {self.latitude}")
        if (
            isinstance(self.longitude, bool)
            or not isinstance(self.longitude, (int, float))
            or not math.isfinite(float(self.longitude))
            or not (-180.0 <= float(self.longitude) <= 180.0)
        ):
            raise ValueError(f"Destination longitude must be in [-180.0, 180.0], got {self.longitude}")


@dataclass(frozen=True)
class ActivityContext:
    activity_id: str
    title: str
    category: str
    code: str = ""
    name: str = ""
    description: Optional[str] = None
    experience_level: Optional[str] = None
    constraints: List[str] = field(default_factory=list)
    equipment_required: List[str] = field(default_factory=list)
    status: str = "PUBLISHED"
    created_at: Optional[str] = None
    updated_at: Optional[str] = None
    source_freshness_timestamp: Optional[str] = None

    def __post_init__(self) -> None:
        parse_non_empty_uuid(self.activity_id, "activity_id")
        if not isinstance(self.title, str) or not self.title.strip():
            raise ValueError("Activity title/name must be non-empty")


@dataclass(frozen=True)
class OfferingContext:
    """Separates publication status, schedule coverage status, and time-window availability status.

    Never collapses publication eligibility, schedule coverage, and operational restrictions
    into a single unsupported boolean.
    """

    offering_id: str
    destination_id: str
    activity_id: str
    status: str
    publication_status: str = "PUBLISHED"
    schedule_status: str = "NOT_EVALUATED"
    availability_status: str = "NOT_EVALUATED"
    is_available: bool = False
    title: Optional[str] = None
    destination_name: Optional[str] = None
    activity_name: Optional[str] = None
    activity_code: Optional[str] = None
    description: Optional[str] = None
    price: Optional[float] = None
    price_cents: Optional[int] = None
    currency: Optional[str] = None
    duration_minutes: Optional[int] = None
    max_capacity: Optional[int] = None
    max_participants: Optional[int] = None
    experience_levels: List[str] = field(default_factory=list)
    operational_restriction: Optional[Dict[str, Any]] = None
    created_at: Optional[str] = None
    updated_at: Optional[str] = None
    source_freshness_timestamp: Optional[str] = None

    def __post_init__(self) -> None:
        parse_non_empty_uuid(self.offering_id, "offering_id")
        parse_non_empty_uuid(self.destination_id, "destination_id")
        parse_non_empty_uuid(self.activity_id, "activity_id")


@dataclass(frozen=True)
class ScheduleSlot:
    slot_id: str
    start_time: str
    end_time: str
    is_open: bool
    is_active: bool = True
    time_zone_id: str = "UTC"
    offering_id: Optional[str] = None
    capacity_remaining: Optional[int] = None
    created_at: Optional[str] = None
    updated_at: Optional[str] = None


@dataclass(frozen=True)
class ScheduleContext:
    offering_id: str
    is_scheduled: bool
    slots: List[ScheduleSlot] = field(default_factory=list)
    availability_status: str = "NOT_EVALUATED"
    reason_codes: List[str] = field(default_factory=list)
    operational_restriction: Optional[Dict[str, Any]] = None
    evaluated_at: Optional[str] = None
    notes: Optional[str] = None


@dataclass(frozen=True)
class BiodiversityContext:
    status: BiodiversityStatus
    predictions: List[FocalSpeciesPrediction] = field(default_factory=list)
    model_version: Optional[str] = None
    model_source: Optional[str] = None
    evaluated_at: Optional[str] = None
    uncertainty_notes: Optional[str] = None
    disclaimer: str = (
        "Biodiversity predictions are informative model outputs, not confirmed sightings, "
        "conservation assessments, safety decisions or availability evidence."
    )


@dataclass(frozen=True)
class AuditableToolCall:
    tool_name: str
    workflow_id: str
    step_id: str
    started_at: str
    elapsed_ms: float
    outcome: str
    agent_name: str = "Coastal Experience & Biodiversity Agent"
    error_summary: Optional[str] = None
    retry_count: int = 0

    def __post_init__(self) -> None:
        if self.tool_name not in ALLOWLISTED_TOOLS:
            raise ValueError(f"Tool '{self.tool_name}' is not in the allowlisted tool registry")


@dataclass(frozen=True)
class ExperienceAgentInput:
    workflow_id: str
    step_id: str
    objective: str
    destination_id: Optional[str] = None
    activity_id: Optional[str] = None
    offering_id: Optional[str] = None
    target_date: Optional[str] = None
    period_starts_at: Optional[str] = None
    period_ends_at: Optional[str] = None
    include_biodiversity: bool = False
    context_parameters: Dict[str, Any] = field(default_factory=dict)

    def __post_init__(self) -> None:
        parse_non_empty_uuid(self.workflow_id, "workflow_id")
        if not isinstance(self.step_id, str) or not (1 <= len(self.step_id.strip()) <= 128):
            raise ValueError("step_id must be a non-empty string between 1 and 128 characters")
        if not isinstance(self.objective, str) or not (3 <= len(self.objective.strip()) <= 1000):
            raise ValueError("objective must be between 3 and 1000 characters")

        if self.destination_id is not None:
            parse_non_empty_uuid(self.destination_id, "destination_id")
        if self.activity_id is not None:
            parse_non_empty_uuid(self.activity_id, "activity_id")
        if self.offering_id is not None:
            parse_non_empty_uuid(self.offering_id, "offering_id")

        if not (self.destination_id or self.activity_id or self.offering_id):
            raise ValueError(
                "At least one selector (destination_id, activity_id, or offering_id) must be provided"
            )

        if (self.period_starts_at is None) ^ (self.period_ends_at is None):
            raise ValueError("period_starts_at and period_ends_at must both be provided when evaluating a time window")
        if self.period_starts_at and self.period_ends_at:
            starts_dt = parse_iso8601_timestamp(self.period_starts_at, "period_starts_at")
            ends_dt = parse_iso8601_timestamp(self.period_ends_at, "period_ends_at")
            if ends_dt <= starts_dt:
                raise ValueError("period_ends_at must be strictly greater than period_starts_at")

        if not isinstance(self.context_parameters, dict):
            raise ValueError("context_parameters must be a dictionary")
        for key in self.context_parameters:
            norm_key = str(key).strip().lower()
            if norm_key in FORBIDDEN_SECRET_KEYS:
                raise ValueError(f"Forbidden secret key '{key}' is not permitted in agent input")

        requested_tools = self.context_parameters.get("requested_tools")
        if requested_tools is not None:
            if not isinstance(requested_tools, list) or any(t not in ALLOWLISTED_TOOLS for t in requested_tools):
                raise ValueError("requested_tools contains a tool outside the agent allowlist")

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> ExperienceAgentInput:
        if not isinstance(data, dict):
            raise ValueError("Input payload must be a JSON object")

        allowed_fields = {
            "workflow_id",
            "step_id",
            "objective",
            "destination_id",
            "activity_id",
            "offering_id",
            "target_date",
            "period_starts_at",
            "period_ends_at",
            "include_biodiversity",
            "context_parameters",
        }
        unknown_fields = set(data.keys()) - allowed_fields
        if unknown_fields:
            for field_key in unknown_fields:
                if str(field_key).strip().lower() in FORBIDDEN_SECRET_KEYS:
                    raise ValueError(f"Forbidden secret field '{field_key}' is not permitted")
            raise ValueError(f"Unknown input fields are not allowed: {sorted(unknown_fields)}")

        if not data.get("workflow_id"):
            raise ValueError("workflow_id is required")
        if not data.get("step_id"):
            raise ValueError("step_id is required")

        include_biodiv_raw = data.get("include_biodiversity", False)
        if not isinstance(include_biodiv_raw, bool):
            raise ValueError("include_biodiversity must be a boolean")

        return cls(
            workflow_id=str(data["workflow_id"]).strip(),
            step_id=str(data["step_id"]).strip(),
            objective=str(data.get("objective", "")),
            destination_id=str(data["destination_id"]).strip() if data.get("destination_id") is not None else None,
            activity_id=str(data["activity_id"]).strip() if data.get("activity_id") is not None else None,
            offering_id=str(data["offering_id"]).strip() if data.get("offering_id") is not None else None,
            target_date=str(data["target_date"]).strip() if data.get("target_date") is not None else None,
            period_starts_at=(
                str(data["period_starts_at"]).strip() if data.get("period_starts_at") is not None else None
            ),
            period_ends_at=(
                str(data["period_ends_at"]).strip() if data.get("period_ends_at") is not None else None
            ),
            include_biodiversity=include_biodiv_raw,
            context_parameters=data.get("context_parameters") if data.get("context_parameters") is not None else {},
        )


@dataclass(frozen=True)
class ExperienceBiodiversityContextReport:
    workflow_id: str
    step_id: str
    objective: str
    completion_outcome: AgentCompletionOutcome
    biodiversity: BiodiversityContext
    agent_identity: str = "Coastal Experience & Biodiversity Agent"
    destination: Optional[DestinationContext] = None
    activity: Optional[ActivityContext] = None
    offering: Optional[OfferingContext] = None
    schedule: Optional[ScheduleContext] = None
    experience_constraints: List[str] = field(default_factory=list)
    audited_tool_calls: List[AuditableToolCall] = field(default_factory=list)
    evidence_references: List[str] = field(default_factory=list)
    uncertainty_and_gaps: List[str] = field(default_factory=list)
    generated_at: str = field(default_factory=lambda: datetime.now(timezone.utc).isoformat())

    def to_dict(self) -> Dict[str, Any]:
        raw = asdict(self)
        raw["completion_outcome"] = self.completion_outcome.value
        raw["biodiversity"]["status"] = self.biodiversity.status.value
        return raw
