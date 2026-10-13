"""Service-key restricted access to the minimized published catalogue.

This tool set intentionally reads only the Experience service's published
catalogue projection. It never forwards a caller token and never calls the
planner/biodiversity API.
"""

from __future__ import annotations

from datetime import date, datetime, timezone
from typing import Any, Optional
from urllib.parse import urlencode
from zoneinfo import ZoneInfo, ZoneInfoNotFoundError

from .schemas import (
    ActivityContext,
    AuditableToolCall,
    BiodiversityContext,
    BiodiversityStatus,
    DestinationContext,
    OfferingContext,
    ScheduleContext,
    parse_iso8601_timestamp,
    parse_non_empty_uuid,
)
from .tools import ExperienceAgentTools


class PublishedCatalogueOnlyTools(ExperienceAgentTools):
    """Read published, minimized catalogue projections for service workflows."""

    CATALOGUE_PATH = "/api/experiences/catalogue"

    def _catalogue_rows(
        self, selector: str, identifier: str, workflow_id: str, step_id: str, tool_name: str
    ) -> tuple[list[dict[str, Any]], int, float, int]:
        query = urlencode({selector: identifier})
        url = f"{self.experience_service_url}{self.CATALOGUE_PATH}?{query}"
        status, data, elapsed_ms, retries = self._execute_http_get(url)
        if status != 200:
            return [], status, elapsed_ms, retries
        if not isinstance(data, list) or len(data) > 5000 or any(not isinstance(item, dict) for item in data):
            return [], 502, elapsed_ms, retries

        # The endpoint is already publication-filtered. Apply the same rule at
        # this boundary so unexpected upstream rows can never enter a report.
        rows = [item for item in data if item.get("publicationState") == "PUBLISHED"]
        return rows, status, elapsed_ms, retries

    @staticmethod
    def _audit(
        tool_name: str,
        workflow_id: str,
        step_id: str,
        elapsed_ms: float,
        retries: int,
        outcome: str,
        error_summary: str | None = None,
    ) -> AuditableToolCall:
        return AuditableToolCall(
            tool_name=tool_name,
            workflow_id=workflow_id,
            step_id=step_id,
            started_at=datetime.now(timezone.utc).isoformat(),
            elapsed_ms=elapsed_ms,
            outcome=outcome,
            error_summary=error_summary,
            retry_count=retries,
        )

    @staticmethod
    def _lookup_outcome(status: int) -> str:
        if status == 404:
            return "NOT_FOUND"
        if status in (401, 403):
            return "UNAUTHORIZED"
        if status == 502:
            return "INVALID_PAYLOAD"
        return "UNAVAILABLE"

    @staticmethod
    def _matching_rows(rows: list[dict[str, Any]], field: str, identifier: str) -> list[dict[str, Any]]:
        result: list[dict[str, Any]] = []
        for row in rows:
            try:
                returned_id = parse_non_empty_uuid(row.get(field), f"catalogue.{field}")
            except ValueError:
                continue
            if returned_id == identifier:
                result.append(row)
        return result

    def destination_lookup(
        self, destination_id: str, workflow_id: str, step_id: str, auth_token: Optional[str] = None
    ) -> tuple[Optional[DestinationContext], AuditableToolCall]:
        tool_name = "destination_lookup"
        identifier = parse_non_empty_uuid(destination_id, "destination_id")
        rows, status, elapsed, retries = self._catalogue_rows("destinationId", identifier, workflow_id, step_id, tool_name)
        matches = self._matching_rows(rows, "destinationId", identifier) if status == 200 else []
        if matches:
            row = matches[0]
            try:
                destination = DestinationContext(
                    destination_id=identifier,
                    name=str(row.get("destinationName") or "").strip(),
                    region=str(row.get("region") or "Unspecified Coastal Region").strip(),
                    latitude=float(row["latitude"]),
                    longitude=float(row["longitude"]),
                    status="PUBLISHED",
                    source_freshness_timestamp=row.get("checkedAt"),
                )
                return destination, self._audit(tool_name, workflow_id, step_id, elapsed, retries, "SUCCESS")
            except (KeyError, TypeError, ValueError) as exc:
                return None, self._audit(tool_name, workflow_id, step_id, elapsed, retries, "INVALID_PAYLOAD", str(exc))
        outcome = self._lookup_outcome(404 if status == 200 else status)
        return None, self._audit(tool_name, workflow_id, step_id, elapsed, retries, outcome, f"Catalogue returned HTTP {status} or no published match")

    def activity_lookup(
        self, activity_id: str, workflow_id: str, step_id: str, auth_token: Optional[str] = None
    ) -> tuple[Optional[ActivityContext], AuditableToolCall]:
        tool_name = "activity_lookup"
        identifier = parse_non_empty_uuid(activity_id, "activity_id")
        rows, status, elapsed, retries = self._catalogue_rows("activityId", identifier, workflow_id, step_id, tool_name)
        matches = self._matching_rows(rows, "activityId", identifier) if status == 200 else []
        if matches:
            row = matches[0]
            try:
                name = str(row.get("activityName") or "").strip()
                activity = ActivityContext(
                    activity_id=identifier,
                    title=name,
                    name=name,
                    category="General",
                    status="PUBLISHED",
                    source_freshness_timestamp=row.get("checkedAt"),
                )
                return activity, self._audit(tool_name, workflow_id, step_id, elapsed, retries, "SUCCESS")
            except (KeyError, TypeError, ValueError) as exc:
                return None, self._audit(tool_name, workflow_id, step_id, elapsed, retries, "INVALID_PAYLOAD", str(exc))
        outcome = self._lookup_outcome(404 if status == 200 else status)
        return None, self._audit(tool_name, workflow_id, step_id, elapsed, retries, outcome, f"Catalogue returned HTTP {status} or no published match")

    def offering_lookup(
        self, offering_id: str, workflow_id: str, step_id: str, auth_token: Optional[str] = None
    ) -> tuple[Optional[OfferingContext], AuditableToolCall]:
        tool_name = "offering_lookup"
        identifier = parse_non_empty_uuid(offering_id, "offering_id")
        rows, status, elapsed, retries = self._catalogue_rows("offeringId", identifier, workflow_id, step_id, tool_name)
        matches = self._matching_rows(rows, "offeringId", identifier) if status == 200 else []
        if matches:
            row = matches[0]
            try:
                destination_id = parse_non_empty_uuid(row.get("destinationId"), "catalogue.destinationId")
                activity_id = parse_non_empty_uuid(row.get("activityId"), "catalogue.activityId")
                availability = "AVAILABLE" if any(item.get("availabilityStatus") == "AVAILABLE" for item in matches) else "UNAVAILABLE"
                levels = row.get("experienceLevels") or []
                if not isinstance(levels, list) or any(not isinstance(item, str) for item in levels):
                    raise ValueError("catalogue.experienceLevels is invalid")
                offering = OfferingContext(
                    offering_id=identifier,
                    destination_id=destination_id,
                    activity_id=activity_id,
                    status="PUBLISHED",
                    publication_status="PUBLISHED",
                    schedule_status="SCHEDULED" if any(item.get("availableFrom") and item.get("availableUntil") for item in matches) else "UNSCHEDULED",
                    availability_status=availability,
                    is_available=availability == "AVAILABLE",
                    title=row.get("title"),
                    destination_name=row.get("destinationName"),
                    activity_name=row.get("activityName"),
                    experience_levels=levels,
                    source_freshness_timestamp=row.get("checkedAt"),
                )
                return offering, self._audit(tool_name, workflow_id, step_id, elapsed, retries, "SUCCESS")
            except (KeyError, TypeError, ValueError) as exc:
                return None, self._audit(tool_name, workflow_id, step_id, elapsed, retries, "INVALID_PAYLOAD", str(exc))
        outcome = self._lookup_outcome(404 if status == 200 else status)
        return None, self._audit(tool_name, workflow_id, step_id, elapsed, retries, outcome, f"Catalogue returned HTTP {status} or no published match")

    def schedule_lookup(
        self,
        offering_id: str,
        target_date: Optional[str],
        workflow_id: str,
        step_id: str,
        auth_token: Optional[str] = None,
        period_starts_at: Optional[str] = None,
        period_ends_at: Optional[str] = None,
    ) -> tuple[Optional[ScheduleContext], AuditableToolCall]:
        tool_name = "schedule_lookup"
        identifier = parse_non_empty_uuid(offering_id, "offering_id")
        if (period_starts_at is None) != (period_ends_at is None):
            raise ValueError("period_starts_at and period_ends_at must be provided together")
        try:
            request_start = parse_iso8601_timestamp(period_starts_at, "period_starts_at") if period_starts_at else None
            request_end = parse_iso8601_timestamp(period_ends_at, "period_ends_at") if period_ends_at else None
            if request_start and request_end and request_end <= request_start:
                raise ValueError("period_ends_at must be later than period_starts_at")
            parsed_date = date.fromisoformat(target_date) if target_date else None
            if target_date and parsed_date.isoformat() != target_date:
                raise ValueError("target_date must use YYYY-MM-DD format")
        except (TypeError, ValueError) as exc:
            return None, self._audit(tool_name, workflow_id, step_id, 0.0, 0, "INVALID_INPUT", str(exc))

        rows, status, elapsed, retries = self._catalogue_rows("offeringId", identifier, workflow_id, step_id, tool_name)
        matches = self._matching_rows(rows, "offeringId", identifier) if status == 200 else []
        if not matches:
            outcome = self._lookup_outcome(404 if status == 200 else status)
            return None, self._audit(tool_name, workflow_id, step_id, elapsed, retries, outcome, f"Catalogue returned HTTP {status} or no published match")

        schedule_rows: list[tuple[datetime, datetime, bool, str | None]] = []
        try:
            for row in matches:
                start_raw, end_raw = row.get("availableFrom"), row.get("availableUntil")
                if start_raw is None and end_raw is None:
                    continue
                start = parse_iso8601_timestamp(start_raw, "catalogue.availableFrom")
                end = parse_iso8601_timestamp(end_raw, "catalogue.availableUntil")
                if end <= start:
                    raise ValueError("catalogue schedule end must be later than start")
                schedule_rows.append((start, end, row.get("availabilityStatus") == "AVAILABLE", row.get("timeZone")))
        except (TypeError, ValueError) as exc:
            return None, self._audit(tool_name, workflow_id, step_id, elapsed, retries, "INVALID_PAYLOAD", str(exc))

        applicable = schedule_rows
        timezone_unverified = False
        if parsed_date is not None:
            filtered: list[tuple[datetime, datetime, bool, str | None]] = []
            for item in schedule_rows:
                try:
                    zone = ZoneInfo(item[3] or "UTC")
                    slot_start_date = item[0].astimezone(zone).date()
                    slot_end_date = item[1].astimezone(zone).date()
                except (ZoneInfoNotFoundError, ValueError):
                    timezone_unverified = True
                    slot_start_date = item[0].astimezone(timezone.utc).date()
                    slot_end_date = item[1].astimezone(timezone.utc).date()
                if slot_start_date <= parsed_date <= slot_end_date:
                    filtered.append(item)
            applicable = filtered

        covering = [
            item for item in applicable
            if item[2] and (request_start is None or item[0] <= request_start)
            and (request_end is None or item[1] >= request_end)
        ]
        if covering:
            availability = "AVAILABLE"
        elif applicable:
            availability = "UNAVAILABLE"
        elif schedule_rows:
            availability = "UNAVAILABLE"
        else:
            availability = "UNKNOWN"

        reasons = []
        if not schedule_rows:
            reasons.append("NO_ACTIVE_SCHEDULE: The minimized published catalogue returned no schedule interval.")
        elif availability != "AVAILABLE":
            reasons.append("NO_SCHEDULE_COVERAGE: No published catalogue interval verified the requested period.")
        if timezone_unverified:
            reasons.append("PERIOD_TIMEZONE_UNVERIFIED: The catalogue timezone could not be resolved; UTC dates were used.")
        evaluated_at = next((row.get("checkedAt") for row in matches if isinstance(row.get("checkedAt"), str)), None)
        context = ScheduleContext(
            offering_id=identifier,
            is_scheduled=bool(schedule_rows),
            slots=[],  # The minimized catalogue intentionally omits internal schedule IDs.
            availability_status=availability,
            reason_codes=reasons,
            evaluated_at=evaluated_at,
            notes="The published catalogue provides interval coverage without exposing schedule record IDs.",
        )
        return context, self._audit(tool_name, workflow_id, step_id, elapsed, retries, "SUCCESS")

    def biodiversity_prediction_lookup(
        self, destination_id: str, workflow_id: str, step_id: str, auth_token: Optional[str] = None
    ) -> tuple[BiodiversityContext, AuditableToolCall]:
        # Service-key authority stops at the minimized published catalogue.
        # Keep the standard report contract while making this optional source
        # explicitly unavailable and issuing no planner request.
        context = BiodiversityContext(
            status=BiodiversityStatus.UNAVAILABLE,
            predictions=[],
            uncertainty_notes="Biodiversity prediction access is outside the service-key published catalogue scope.",
        )
        return context, self._audit(
            "biodiversity_prediction_lookup", workflow_id, step_id, 0.0, 0, "UNAVAILABLE",
            "Service-key requests may read only the minimized published catalogue.",
        )
