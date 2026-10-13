"""Data transfer schemas and validation models for Marine Conditions Intelligence Agent.

Contract ID: v1.agent.marine-conditions
Owner: Sanuda Abeysinghe (@sanudaabey), Member 2
"""

from __future__ import annotations

from dataclasses import dataclass, field, asdict
from datetime import datetime, timezone
from enum import Enum
import re
from typing import Any, Dict, List, Optional


class ReportStatus(str, Enum):
    COMPLETE = "COMPLETE"
    INCOMPLETE = "INCOMPLETE"
    SOURCE_UNAVAILABLE = "SOURCE_UNAVAILABLE"
    INVALID_RESULT = "INVALID_RESULT"
    SAFE_FAILURE = "SAFE_FAILURE"


class FreshnessStatus(str, Enum):
    FRESH = "FRESH"
    STALE = "STALE"
    UNKNOWN = "UNKNOWN"


DEFAULT_ALLOWED_TOOLS = [
    "weather_forecast_lookup",
    "marine_forecast_lookup",
    "condition_snapshot_lookup",
]

DEFAULT_REQUIRED_FACTORS = [
    "wind_speed",
    "wave_height",
    "swell_wave_height",
    "precipitation",
    "weather_code",
]

STANDARD_DISCLAIMER = (
    "BLUEVERSE marine conditions reports provide decision support only "
    "and are not professional marine navigation guidance."
)


def parse_iso_datetime(dt_str: str) -> datetime:
    """Parse an ISO 8601 string to a timezone-aware UTC datetime."""
    if not isinstance(dt_str, str) or not dt_str.strip():
        raise ValueError("Timestamp string cannot be empty.")
    # Standardize 'Z' to '+00:00'
    normalized = dt_str.strip().replace("Z", "+00:00")
    try:
        dt = datetime.fromisoformat(normalized)
    except Exception as exc:
        raise ValueError(f"Invalid ISO 8601 timestamp: '{dt_str}'") from exc
    if dt.tzinfo is None:
        dt = dt.replace(tzinfo=timezone.utc)
    else:
        dt = dt.astimezone(timezone.utc)
    return dt


def format_iso_datetime(dt: datetime) -> str:
    """Format a datetime into UTC ISO 8601 string."""
    if dt.tzinfo is None:
        dt = dt.replace(tzinfo=timezone.utc)
    else:
        dt = dt.astimezone(timezone.utc)
    return dt.strftime("%Y-%m-%dT%H:%M:%SZ")


@dataclass(frozen=True)
class LocationDto:
    latitude: float
    longitude: float

    def __post_init__(self):
        if not (-90.0 <= self.latitude <= 90.0):
            raise ValueError(f"Latitude {self.latitude} out of bounds [-90.0, 90.0].")
        if not (-180.0 <= self.longitude <= 180.0):
            raise ValueError(f"Longitude {self.longitude} out of bounds [-180.0, 180.0].")

    def to_dict(self) -> Dict[str, Any]:
        return {"latitude": round(self.latitude, 6), "longitude": round(self.longitude, 6)}

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> LocationDto:
        if not isinstance(data, dict):
            raise ValueError("Location data must be a dictionary.")
        if "latitude" not in data or "longitude" not in data:
            raise ValueError("Location must contain 'latitude' and 'longitude'.")
        try:
            lat = float(data["latitude"])
            lon = float(data["longitude"])
        except (ValueError, TypeError) as exc:
            raise ValueError(f"Coordinates must be numeric: {exc}") from exc
        return cls(latitude=lat, longitude=lon)


@dataclass(frozen=True)
class TimePeriodDto:
    start_time: str
    end_time: str

    def __post_init__(self):
        dt_start = parse_iso_datetime(self.start_time)
        dt_end = parse_iso_datetime(self.end_time)
        if dt_end < dt_start:
            raise ValueError(f"end_time '{self.end_time}' cannot be earlier than start_time '{self.start_time}'.")

    def to_dict(self) -> Dict[str, Any]:
        return {"start_time": self.start_time, "end_time": self.end_time}

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> TimePeriodDto:
        if not isinstance(data, dict):
            raise ValueError("Period data must be a dictionary.")
        if "start_time" not in data or "end_time" not in data:
            raise ValueError("Period must contain 'start_time' and 'end_time'.")
        return cls(
            start_time=format_iso_datetime(parse_iso_datetime(data["start_time"])),
            end_time=format_iso_datetime(parse_iso_datetime(data["end_time"])),
        )


@dataclass(frozen=True)
class ToolPolicyDto:
    allowed_tools: List[str] = field(default_factory=lambda: list(DEFAULT_ALLOWED_TOOLS))
    max_tool_calls: int = 5
    timeout_seconds: float = 10.0

    def to_dict(self) -> Dict[str, Any]:
        return {
            "allowed_tools": list(self.allowed_tools),
            "max_tool_calls": self.max_tool_calls,
            "timeout_seconds": self.timeout_seconds,
        }

    @classmethod
    def from_dict(cls, data: Optional[Dict[str, Any]]) -> ToolPolicyDto:
        if not data:
            return cls()
        tools = data.get("allowed_tools", DEFAULT_ALLOWED_TOOLS)
        max_calls = int(data.get("max_tool_calls", 5))
        timeout = float(data.get("timeout_seconds", 10.0))
        return cls(allowed_tools=list(tools), max_tool_calls=max_calls, timeout_seconds=timeout)


@dataclass
class MarineConditionsAgentInput:
    workflow_id: str
    step_id: str
    objective: str
    location: LocationDto
    activity: str
    period: TimePeriodDto
    required_factors: List[str] = field(default_factory=lambda: list(DEFAULT_REQUIRED_FACTORS))
    existing_context: Optional[Dict[str, Any]] = None
    tool_policy: ToolPolicyDto = field(default_factory=ToolPolicyDto)

    def __post_init__(self):
        if not self.workflow_id or not isinstance(self.workflow_id, str):
            raise ValueError("workflow_id must be a non-empty string.")
        if not self.step_id or not isinstance(self.step_id, str):
            raise ValueError("step_id must be a non-empty string.")
        if not self.objective or not isinstance(self.objective, str):
            raise ValueError("objective must be a non-empty string.")
        if not self.activity or not isinstance(self.activity, str):
            raise ValueError("activity must be a non-empty string.")

    def to_dict(self) -> Dict[str, Any]:
        return {
            "workflow_id": self.workflow_id,
            "step_id": self.step_id,
            "objective": self.objective,
            "location": self.location.to_dict(),
            "activity": self.activity,
            "period": self.period.to_dict(),
            "required_factors": list(self.required_factors),
            "existing_context": self.existing_context,
            "tool_policy": self.tool_policy.to_dict(),
        }

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> MarineConditionsAgentInput:
        if not isinstance(data, dict):
            raise ValueError("Input data must be a dictionary.")
        req_fields = ["workflow_id", "step_id", "objective", "location", "activity", "period"]
        for f in req_fields:
            if f not in data:
                raise ValueError(f"Missing required input field: '{f}'")

        location = LocationDto.from_dict(data["location"])
        period = TimePeriodDto.from_dict(data["period"])
        factors = list(data.get("required_factors") or DEFAULT_REQUIRED_FACTORS)
        policy = ToolPolicyDto.from_dict(data.get("tool_policy"))
        return cls(
            workflow_id=str(data["workflow_id"]).strip(),
            step_id=str(data["step_id"]).strip(),
            objective=str(data["objective"]).strip(),
            location=location,
            activity=str(data["activity"]).strip(),
            period=period,
            required_factors=factors,
            existing_context=data.get("existing_context"),
            tool_policy=policy,
        )


@dataclass
class EnvironmentalFactor:
    value: Optional[float]
    unit: str
    description: Optional[str] = None
    forecast_or_observed_time: Optional[str] = None

    def to_dict(self) -> Dict[str, Any]:
        return {
            "value": self.value,
            "unit": self.unit,
            "description": self.description,
            "forecast_or_observed_time": self.forecast_or_observed_time,
        }

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> EnvironmentalFactor:
        val = data.get("value")
        val_float = float(val) if val is not None else None
        return cls(
            value=val_float,
            unit=str(data.get("unit", "")),
            description=data.get("description"),
            forecast_or_observed_time=data.get("forecast_or_observed_time"),
        )


@dataclass
class SourceProvenance:
    provider: str = "Open-Meteo"
    retrieval_time: str = field(default_factory=lambda: format_iso_datetime(datetime.now(timezone.utc)))
    source_updated_time: Optional[str] = None
    is_forecast: bool = True

    def to_dict(self) -> Dict[str, Any]:
        return {
            "provider": self.provider,
            "retrieval_time": self.retrieval_time,
            "source_updated_time": self.source_updated_time,
            "is_forecast": self.is_forecast,
        }

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> SourceProvenance:
        return cls(
            provider=str(data.get("provider", "Open-Meteo")),
            retrieval_time=str(data.get("retrieval_time", format_iso_datetime(datetime.now(timezone.utc)))),
            source_updated_time=data.get("source_updated_time"),
            is_forecast=bool(data.get("is_forecast", True)),
        )


@dataclass
class FreshnessAssessment:
    status: FreshnessStatus = FreshnessStatus.FRESH
    rule: str = "FreshnessMaxAgeMinutes: 60"
    age_minutes: Optional[float] = None

    def to_dict(self) -> Dict[str, Any]:
        return {
            "status": self.status.value,
            "rule": self.rule,
            "age_minutes": round(self.age_minutes, 2) if self.age_minutes is not None else None,
        }

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> FreshnessAssessment:
        status_str = data.get("status", FreshnessStatus.UNKNOWN.value)
        try:
            status = FreshnessStatus(status_str)
        except ValueError:
            status = FreshnessStatus.UNKNOWN
        age = data.get("age_minutes")
        return cls(
            status=status,
            rule=str(data.get("rule", "FreshnessMaxAgeMinutes: 60")),
            age_minutes=float(age) if age is not None else None,
        )


@dataclass
class MarineConditionsReport:
    workflow_id: str
    step_id: str
    status: ReportStatus
    location: LocationDto
    activity: str
    requested_period: TimePeriodDto
    source_provenance: SourceProvenance
    freshness: FreshnessAssessment
    environmental_factors: Dict[str, EnvironmentalFactor] = field(default_factory=dict)
    missing_or_unavailable_factors: List[str] = field(default_factory=list)
    objective_relevance_summary: str = ""
    uncertainty_and_limitations: str = ""
    disclaimer: str = STANDARD_DISCLAIMER
    error_detail: Optional[str] = None

    def to_dict(self) -> Dict[str, Any]:
        return {
            "workflow_id": self.workflow_id,
            "step_id": self.step_id,
            "status": self.status.value,
            "location": self.location.to_dict(),
            "activity": self.activity,
            "requested_period": self.requested_period.to_dict(),
            "source_provenance": self.source_provenance.to_dict(),
            "freshness": self.freshness.to_dict(),
            "environmental_factors": {
                k: v.to_dict() for k, v in self.environmental_factors.items()
            },
            "missing_or_unavailable_factors": list(self.missing_or_unavailable_factors),
            "objective_relevance_summary": self.objective_relevance_summary,
            "uncertainty_and_limitations": self.uncertainty_and_limitations,
            "disclaimer": self.disclaimer,
            "error_detail": self.error_detail,
        }

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> MarineConditionsReport:
        status_val = data.get("status", ReportStatus.SAFE_FAILURE.value)
        try:
            status = ReportStatus(status_val)
        except ValueError:
            status = ReportStatus.SAFE_FAILURE

        factors = {}
        for k, v in data.get("environmental_factors", {}).items():
            if isinstance(v, dict):
                factors[k] = EnvironmentalFactor.from_dict(v)

        return cls(
            workflow_id=str(data.get("workflow_id", "")),
            step_id=str(data.get("step_id", "")),
            status=status,
            location=LocationDto.from_dict(data.get("location", {"latitude": 0.0, "longitude": 0.0})),
            activity=str(data.get("activity", "")),
            requested_period=TimePeriodDto.from_dict(data.get("requested_period", {"start_time": "1970-01-01T00:00:00Z", "end_time": "1970-01-01T00:00:00Z"})),
            source_provenance=SourceProvenance.from_dict(data.get("source_provenance", {})),
            freshness=FreshnessAssessment.from_dict(data.get("freshness", {})),
            environmental_factors=factors,
            missing_or_unavailable_factors=list(data.get("missing_or_unavailable_factors", [])),
            objective_relevance_summary=str(data.get("objective_relevance_summary", "")),
            uncertainty_and_limitations=str(data.get("uncertainty_and_limitations", "")),
            disclaimer=str(data.get("disclaimer", STANDARD_DISCLAIMER)),
            error_detail=data.get("error_detail"),
        )
