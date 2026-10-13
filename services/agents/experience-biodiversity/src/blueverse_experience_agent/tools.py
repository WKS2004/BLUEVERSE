"""Allowlisted Tool Implementations for Coastal Experience & Biodiversity Agent.

Tools:
1. destination_lookup: Read destination details from experience-biodiversity service.
2. activity_lookup: Read activity metadata from experience-biodiversity service.
3. offering_lookup: Read offering context linking destination and activity.
4. schedule_lookup: Read schedule slots and time-window availability for an offering.
5. biodiversity_prediction_lookup: Read biodiversity predictions via Coastal Planner adapter (ADR-0019).

Strictly preserves:
- Read-only operations (zero state modifications).
- Resilient 0.5s timeouts and one bounded retry by default.
- Authoritative error statuses: NOT_FOUND, UNAUTHORIZED, UNAVAILABLE, INVALID_PAYLOAD, STALE.
- Prediction provenance and uncertainty; never invents or extrapolates predictions.
"""

from __future__ import annotations

import json
import math
import time
import urllib.error
import urllib.parse
import urllib.request
from dataclasses import replace
from datetime import date, datetime, timedelta, timezone
from typing import Any, Dict, List, Optional, Tuple
from zoneinfo import ZoneInfo, ZoneInfoNotFoundError

from .schemas import (
    ActivityContext,
    AuditableToolCall,
    BiodiversityContext,
    BiodiversityStatus,
    DestinationContext,
    FocalSpeciesPrediction,
    OfferingContext,
    ScheduleContext,
    ScheduleSlot,
    parse_iso8601_timestamp,
    parse_non_empty_uuid,
)


class ToolExecutionError(Exception):
    """Raised when a tool encounters an unrecoverable failure."""


class ExperienceAgentTools:
    def __init__(
        self,
        experience_service_url: str = "http://experience-biodiversity:8080",
        planner_service_url: str = "http://coastal-planner:8080",
        timeout_seconds: float = 0.5,
        max_retries: int = 1,
    ):
        self.experience_service_url = experience_service_url.rstrip("/")
        self.planner_service_url = planner_service_url.rstrip("/")
        self.timeout_seconds = timeout_seconds
        self.max_retries = max_retries

    def _execute_http_get(
        self, url: str, auth_token: Optional[str] = None
    ) -> Tuple[int, Any, float, int]:
        """Execute a resilient HTTP GET with bounded retries and timeout."""
        headers = {
            "Accept": "application/json",
            "User-Agent": "Blueverse-Experience-Biodiversity-Agent/1.0",
        }
        if auth_token:
            headers["Authorization"] = f"Bearer {auth_token}"

        req = urllib.request.Request(url, headers=headers, method="GET")
        start_time = time.perf_counter()
        attempts = 0

        for attempt in range(self.max_retries + 1):
            attempts += 1
            try:
                with urllib.request.urlopen(req, timeout=self.timeout_seconds) as resp:
                    elapsed_ms = (time.perf_counter() - start_time) * 1000.0
                    content = resp.read().decode("utf-8")
                    data = json.loads(content) if content else {}
                    return resp.status, data, elapsed_ms, attempts - 1
            except urllib.error.HTTPError as e:
                elapsed_ms = (time.perf_counter() - start_time) * 1000.0
                if e.code in (400, 401, 403, 404, 422):
                    # Deterministic client/domain responses must not be retried
                    return e.code, None, elapsed_ms, attempts - 1
            except json.JSONDecodeError:
                elapsed_ms = (time.perf_counter() - start_time) * 1000.0
                return 502, None, elapsed_ms, attempts - 1
            except (urllib.error.URLError, TimeoutError, OSError):
                pass

            if attempt < self.max_retries:
                time.sleep(0.05 * (attempt + 1))

        elapsed_ms = (time.perf_counter() - start_time) * 1000.0
        return 503, None, elapsed_ms, attempts - 1

    # 1. destination_lookup
    def destination_lookup(
        self,
        destination_id: str,
        workflow_id: str,
        step_id: str,
        auth_token: Optional[str] = None,
    ) -> Tuple[Optional[DestinationContext], AuditableToolCall]:
        started_at = datetime.now(timezone.utc).isoformat()
        canonical_id = parse_non_empty_uuid(destination_id, "destination_id")
        url = f"{self.experience_service_url}/api/experiences/destinations/{urllib.parse.quote(canonical_id)}"
        status, data, elapsed_ms, retries = self._execute_http_get(url, auth_token)

        if status == 200 and isinstance(data, dict):
            try:
                returned_id = parse_non_empty_uuid(str(data.get("id", canonical_id)), "destination.id")
                if returned_id != canonical_id:
                    raise ValueError(
                        f"Destination ID mismatch: requested {canonical_id}, received {returned_id}"
                    )
                freshness = data.get("updatedAt") or data.get("createdAt") or data.get("sourceFreshnessTimestamp")
                dest = DestinationContext(
                    destination_id=returned_id,
                    name=str(data.get("name", "")).strip(),
                    region=str(data.get("region") or "Unspecified Coastal Region").strip(),
                    latitude=float(data["latitude"]),
                    longitude=float(data["longitude"]),
                    status=str(data.get("status") or "UNKNOWN").strip().upper(),
                    slug=data.get("slug"),
                    description=data.get("description"),
                    created_at=data.get("createdAt"),
                    updated_at=data.get("updatedAt"),
                    source_freshness_timestamp=freshness,
                )
                audit = AuditableToolCall(
                    tool_name="destination_lookup",
                    workflow_id=workflow_id,
                    step_id=step_id,
                    started_at=started_at,
                    elapsed_ms=elapsed_ms,
                    outcome="SUCCESS",
                    retry_count=retries,
                )
                return dest, audit
            except (KeyError, TypeError, ValueError) as exc:
                audit = AuditableToolCall(
                    tool_name="destination_lookup",
                    workflow_id=workflow_id,
                    step_id=step_id,
                    started_at=started_at,
                    elapsed_ms=elapsed_ms,
                    outcome="INVALID_PAYLOAD",
                    error_summary=str(exc),
                    retry_count=retries,
                )
                return None, audit

        outcome = "NOT_FOUND" if status == 404 else ("UNAUTHORIZED" if status in (401, 403) else "UNAVAILABLE")
        audit = AuditableToolCall(
            tool_name="destination_lookup",
            workflow_id=workflow_id,
            step_id=step_id,
            started_at=started_at,
            elapsed_ms=elapsed_ms,
            outcome=outcome,
            error_summary=f"HTTP status {status}",
            retry_count=retries,
        )
        return None, audit

    # 2. activity_lookup
    def activity_lookup(
        self,
        activity_id: str,
        workflow_id: str,
        step_id: str,
        auth_token: Optional[str] = None,
    ) -> Tuple[Optional[ActivityContext], AuditableToolCall]:
        started_at = datetime.now(timezone.utc).isoformat()
        canonical_id = parse_non_empty_uuid(activity_id, "activity_id")
        url = f"{self.experience_service_url}/api/experiences/activities/{urllib.parse.quote(canonical_id)}"
        status, data, elapsed_ms, retries = self._execute_http_get(url, auth_token)

        if status == 200 and isinstance(data, dict):
            try:
                returned_id = parse_non_empty_uuid(str(data.get("id", canonical_id)), "activity.id")
                if returned_id != canonical_id:
                    raise ValueError(
                        f"Activity ID mismatch: requested {canonical_id}, received {returned_id}"
                    )
                raw_name = str(data.get("name") or data.get("title") or "").strip()
                raw_title = str(data.get("title") or data.get("name") or "").strip()
                freshness = data.get("updatedAt") or data.get("createdAt") or data.get("sourceFreshnessTimestamp")
                act = ActivityContext(
                    activity_id=returned_id,
                    code=str(data.get("code") or "").strip(),
                    name=raw_name,
                    title=raw_title,
                    category=str(data.get("category") or "General").strip(),
                    description=data.get("description"),
                    experience_level=data.get("experienceLevel"),
                    constraints=list(data.get("constraints") or []),
                    equipment_required=list(data.get("equipmentRequired") or []),
                    status=str(data.get("status") or "UNKNOWN").strip().upper(),
                    created_at=data.get("createdAt"),
                    updated_at=data.get("updatedAt"),
                    source_freshness_timestamp=freshness,
                )
                audit = AuditableToolCall(
                    tool_name="activity_lookup",
                    workflow_id=workflow_id,
                    step_id=step_id,
                    started_at=started_at,
                    elapsed_ms=elapsed_ms,
                    outcome="SUCCESS",
                    retry_count=retries,
                )
                return act, audit
            except (KeyError, TypeError, ValueError) as exc:
                audit = AuditableToolCall(
                    tool_name="activity_lookup",
                    workflow_id=workflow_id,
                    step_id=step_id,
                    started_at=started_at,
                    elapsed_ms=elapsed_ms,
                    outcome="INVALID_PAYLOAD",
                    error_summary=str(exc),
                    retry_count=retries,
                )
                return None, audit

        outcome = "NOT_FOUND" if status == 404 else ("UNAUTHORIZED" if status in (401, 403) else "UNAVAILABLE")
        audit = AuditableToolCall(
            tool_name="activity_lookup",
            workflow_id=workflow_id,
            step_id=step_id,
            started_at=started_at,
            elapsed_ms=elapsed_ms,
            outcome=outcome,
            error_summary=f"HTTP status {status}",
            retry_count=retries,
        )
        return None, audit

    # 3. offering_lookup
    def offering_lookup(
        self,
        offering_id: str,
        workflow_id: str,
        step_id: str,
        auth_token: Optional[str] = None,
    ) -> Tuple[Optional[OfferingContext], AuditableToolCall]:
        started_at = datetime.now(timezone.utc).isoformat()
        canonical_id = parse_non_empty_uuid(offering_id, "offering_id")
        url = f"{self.experience_service_url}/api/experiences/offerings/{urllib.parse.quote(canonical_id)}"
        status, data, elapsed_ms, retries = self._execute_http_get(url, auth_token)

        if status == 200 and isinstance(data, dict):
            try:
                returned_id = parse_non_empty_uuid(str(data.get("id", canonical_id)), "offering.id")
                if returned_id != canonical_id:
                    raise ValueError(
                        f"Offering ID mismatch: requested {canonical_id}, received {returned_id}"
                    )
                dest_id = parse_non_empty_uuid(str(data["destinationId"]), "offering.destinationId")
                act_id = parse_non_empty_uuid(str(data["activityId"]), "offering.activityId")

                pub_status = str(data.get("status") or "UNKNOWN").strip().upper()
                explicit_avail = data.get("isAvailable")
                avail_status_raw = data.get("availabilityStatus")
                if avail_status_raw:
                    avail_status = str(avail_status_raw).strip().upper()
                elif explicit_avail is False or pub_status != "PUBLISHED":
                    avail_status = "UNAVAILABLE"
                elif explicit_avail is True:
                    avail_status = "AVAILABLE"
                else:
                    avail_status = "NOT_EVALUATED"

                is_avail_bool = (
                    bool(explicit_avail) and pub_status == "PUBLISHED"
                    if explicit_avail is not None
                    else (avail_status == "AVAILABLE" and pub_status == "PUBLISHED")
                )

                raw_price = data.get("price")
                price_float = float(raw_price) if raw_price is not None else None
                raw_price_cents = data.get("priceCents")
                if raw_price_cents is not None:
                    price_cents = int(raw_price_cents)
                elif price_float is not None:
                    price_cents = int(round(price_float * 100))
                else:
                    price_cents = None

                max_cap = data.get("maxCapacity") if data.get("maxCapacity") is not None else data.get("maxParticipants")
                max_cap_int = int(max_cap) if max_cap is not None else None
                freshness = data.get("updatedAt") or data.get("createdAt") or data.get("sourceFreshnessTimestamp")

                offering = OfferingContext(
                    offering_id=returned_id,
                    destination_id=dest_id,
                    activity_id=act_id,
                    status=pub_status,
                    publication_status=pub_status,
                    schedule_status=str(data.get("scheduleStatus") or "NOT_EVALUATED"),
                    availability_status=avail_status,
                    is_available=is_avail_bool,
                    title=data.get("title"),
                    destination_name=data.get("destinationName"),
                    activity_name=data.get("activityName"),
                    activity_code=data.get("activityCode"),
                    description=data.get("description"),
                    price=price_float,
                    price_cents=price_cents,
                    currency=data.get("currency", "USD"),
                    duration_minutes=int(data["durationMinutes"]) if data.get("durationMinutes") is not None else None,
                    max_capacity=max_cap_int,
                    max_participants=max_cap_int,
                    experience_levels=list(data.get("experienceLevels") or []),
                    operational_restriction=data.get("operationalRestriction"),
                    created_at=data.get("createdAt"),
                    updated_at=data.get("updatedAt"),
                    source_freshness_timestamp=freshness,
                )
                audit = AuditableToolCall(
                    tool_name="offering_lookup",
                    workflow_id=workflow_id,
                    step_id=step_id,
                    started_at=started_at,
                    elapsed_ms=elapsed_ms,
                    outcome="SUCCESS",
                    retry_count=retries,
                )
                return offering, audit
            except (KeyError, TypeError, ValueError) as exc:
                audit = AuditableToolCall(
                    tool_name="offering_lookup",
                    workflow_id=workflow_id,
                    step_id=step_id,
                    started_at=started_at,
                    elapsed_ms=elapsed_ms,
                    outcome="INVALID_PAYLOAD",
                    error_summary=str(exc),
                    retry_count=retries,
                )
                return None, audit

        outcome = "NOT_FOUND" if status == 404 else ("UNAUTHORIZED" if status in (401, 403) else "UNAVAILABLE")
        audit = AuditableToolCall(
            tool_name="offering_lookup",
            workflow_id=workflow_id,
            step_id=step_id,
            started_at=started_at,
            elapsed_ms=elapsed_ms,
            outcome=outcome,
            error_summary=f"HTTP status {status}",
            retry_count=retries,
        )
        return None, audit

    # 4. schedule_lookup
    def schedule_lookup(
        self,
        offering_id: str,
        target_date: Optional[str],
        workflow_id: str,
        step_id: str,
        auth_token: Optional[str] = None,
        period_starts_at: Optional[str] = None,
        period_ends_at: Optional[str] = None,
    ) -> Tuple[Optional[ScheduleContext], AuditableToolCall]:
        started_at = datetime.now(timezone.utc).isoformat()
        canonical_id = parse_non_empty_uuid(offering_id, "offering_id")
        url = f"{self.experience_service_url}/api/experiences/offerings/{urllib.parse.quote(canonical_id)}/schedules"

        if (period_starts_at is None) != (period_ends_at is None):
            raise ValueError("period_starts_at and period_ends_at must be provided together")

        requested_date: Optional[date] = None
        request_start: Optional[datetime] = None
        request_end: Optional[datetime] = None
        try:
            if target_date is not None:
                if not isinstance(target_date, str) or len(target_date) != 10:
                    raise ValueError("target_date must use YYYY-MM-DD format")
                requested_date = date.fromisoformat(target_date)
                if requested_date.isoformat() != target_date:
                    raise ValueError("target_date must use YYYY-MM-DD format")
            if period_starts_at is not None and period_ends_at is not None:
                request_start = parse_iso8601_timestamp(period_starts_at, "period_starts_at")
                request_end = parse_iso8601_timestamp(period_ends_at, "period_ends_at")
                if request_end <= request_start:
                    raise ValueError("period_ends_at must be later than period_starts_at")
        except ValueError as exc:
            return None, AuditableToolCall(
                tool_name="schedule_lookup",
                workflow_id=workflow_id,
                step_id=step_id,
                started_at=started_at,
                elapsed_ms=0.0,
                outcome="INVALID_INPUT",
                error_summary=str(exc),
            )

        status, data, elapsed_ms, retries = self._execute_http_get(url, auth_token)

        if status != 200:
            outcome = "NOT_FOUND" if status == 404 else ("UNAUTHORIZED" if status in (401, 403) else "UNAVAILABLE")
            return None, AuditableToolCall(
                tool_name="schedule_lookup",
                workflow_id=workflow_id,
                step_id=step_id,
                started_at=started_at,
                elapsed_ms=elapsed_ms,
                outcome=outcome,
                error_summary=f"Schedule endpoint returned HTTP {status}",
                retry_count=retries,
            )

        if isinstance(data, list):
            raw_slots = data
        elif isinstance(data, dict) and isinstance(data.get("slots", data.get("schedules")), list):
            raw_slots = data.get("slots", data.get("schedules"))
        else:
            return None, AuditableToolCall(
                tool_name="schedule_lookup",
                workflow_id=workflow_id,
                step_id=step_id,
                started_at=started_at,
                elapsed_ms=elapsed_ms,
                outcome="INVALID_PAYLOAD",
                error_summary="Schedule response must be a list of schedule DTOs",
                retry_count=retries,
            )

        if any(not isinstance(item, dict) for item in raw_slots):
            return None, AuditableToolCall(
                tool_name="schedule_lookup",
                workflow_id=workflow_id,
                step_id=step_id,
                started_at=started_at,
                elapsed_ms=elapsed_ms,
                outcome="INVALID_PAYLOAD",
                error_summary="Schedule response contains a non-object item",
                retry_count=retries,
            )

        parsed_slots: List[Tuple[ScheduleSlot, datetime, datetime]] = []
        try:
            for item in raw_slots:
                slot_id = parse_non_empty_uuid(item.get("id"), "schedule.id")
                returned_offering_id = parse_non_empty_uuid(item.get("offeringId"), "schedule.offeringId")
                if returned_offering_id != canonical_id:
                    raise ValueError("Schedule response contains a slot for a different offering")

                starts_at_raw = item.get("startsAt")
                ends_at_raw = item.get("endsAt")
                slot_start = parse_iso8601_timestamp(starts_at_raw, "schedule.startsAt")
                slot_end = parse_iso8601_timestamp(ends_at_raw, "schedule.endsAt")
                if slot_end <= slot_start:
                    raise ValueError("Schedule endsAt must be later than startsAt")

                timezone_id = item.get("timeZoneId")
                is_active = item.get("isActive")
                if not isinstance(timezone_id, str) or not timezone_id.strip():
                    raise ValueError("Schedule timeZoneId must be a non-empty string")
                if not isinstance(is_active, bool):
                    raise ValueError("Schedule isActive must be a boolean")

                slot = ScheduleSlot(
                    slot_id=slot_id,
                    offering_id=returned_offering_id,
                    start_time=str(starts_at_raw),
                    end_time=str(ends_at_raw),
                    time_zone_id=timezone_id.strip(),
                    is_active=is_active,
                    # The schedules endpoint has no seat/capacity field. Do not infer openness from IsActive.
                    is_open=False,
                    capacity_remaining=None,
                    created_at=item.get("createdAt"),
                    updated_at=item.get("updatedAt"),
                )
                parsed_slots.append((slot, slot_start, slot_end))
        except (TypeError, ValueError) as exc:
            return None, AuditableToolCall(
                tool_name="schedule_lookup",
                workflow_id=workflow_id,
                step_id=step_id,
                started_at=started_at,
                elapsed_ms=elapsed_ms,
                outcome="INVALID_PAYLOAD",
                error_summary=str(exc),
                retry_count=retries,
            )

        date_timezone_unverified = False

        def matches_requested_date(entry: Tuple[ScheduleSlot, datetime, datetime]) -> bool:
            nonlocal date_timezone_unverified
            if requested_date is None:
                return True
            try:
                local_zone = ZoneInfo(entry[0].time_zone_id)
                return (
                    entry[1].astimezone(local_zone).date()
                    <= requested_date
                    <= entry[2].astimezone(local_zone).date()
                )
            except (ZoneInfoNotFoundError, ValueError):
                # Minimal runtimes may not ship the IANA database. Keep a
                # conservative candidate window and disclose that filtering
                # could not be exact; interval availability is still checked
                # by the authoritative experience service below.
                date_timezone_unverified = True
                try:
                    requested_start = datetime.combine(
                        requested_date, datetime.min.time(), tzinfo=timezone.utc
                    )
                    possible_local_start = requested_start - timedelta(hours=14)
                    possible_local_end = requested_start + timedelta(days=1, hours=14)
                except OverflowError:
                    return True
                return (
                    entry[1].astimezone(timezone.utc) < possible_local_end
                    and entry[2].astimezone(timezone.utc) > possible_local_start
                )

        date_filtered_slots = [entry for entry in parsed_slots if matches_requested_date(entry)]
        period_filtered_slots = [
            entry
            for entry in date_filtered_slots
            if request_start is None or (entry[1] < request_end and entry[2] > request_start)
        ]
        covering_active_slots = [
            entry
            for entry in period_filtered_slots
            if entry[0].is_active
            and request_start is not None
            and request_end is not None
            and entry[1] <= request_start
            and entry[2] >= request_end
        ]

        reason_codes: List[str] = []
        if date_timezone_unverified:
            reason_codes.append(
                "DATE_TIMEZONE_UNVERIFIED: The runtime lacks timezone data; date filtering includes conservative candidates."
            )
        availability_status = "NOT_EVALUATED"
        evaluated_at: Optional[str] = None
        total_elapsed_ms = elapsed_ms
        total_retries = retries
        availability_audit_outcome = "SUCCESS"
        availability_error: Optional[str] = None

        if request_start is not None and request_end is not None:
            availability_query = urllib.parse.urlencode(
                {
                    "periodStartsAt": period_starts_at,
                    "periodEndsAt": period_ends_at,
                }
            )
            availability_url = (
                f"{self.experience_service_url}/api/experiences/OFFERING/"
                f"{urllib.parse.quote(canonical_id)}/availability?{availability_query}"
            )
            availability_http_status, availability_data, availability_elapsed_ms, availability_retries = (
                self._execute_http_get(availability_url, auth_token)
            )
            total_elapsed_ms += availability_elapsed_ms
            total_retries += availability_retries

            if availability_http_status == 200 and isinstance(availability_data, dict):
                try:
                    response_type = str(availability_data.get("targetType") or "").strip().upper()
                    response_id = parse_non_empty_uuid(availability_data.get("targetId"), "availability.targetId")
                    response_status = str(availability_data.get("status") or "").strip().upper()
                    if response_type != "OFFERING" or response_id != canonical_id:
                        raise ValueError("Availability response did not match the requested offering")
                    if response_status not in {"AVAILABLE", "UNAVAILABLE"}:
                        raise ValueError("Availability response status must be AVAILABLE or UNAVAILABLE")

                    evaluated_at_raw = availability_data.get("evaluatedAt")
                    if evaluated_at_raw is not None:
                        parse_iso8601_timestamp(evaluated_at_raw, "availability.evaluatedAt")
                        evaluated_at = str(evaluated_at_raw)

                    if response_status == "AVAILABLE" and not covering_active_slots:
                        raise ValueError("Availability response says AVAILABLE but no active schedule covers the interval")

                    availability_status = response_status
                    if response_status == "UNAVAILABLE":
                        availability_audit_outcome = "SUCCESS"
                        reason_codes.append("SOURCE_UNAVAILABLE: The experience service evaluated this interval as unavailable.")
                except (TypeError, ValueError) as exc:
                    availability_status = "UNKNOWN"
                    availability_audit_outcome = "INVALID_PAYLOAD"
                    availability_error = str(exc)
            else:
                availability_status = "UNKNOWN"
                availability_audit_outcome = (
                    "NOT_FOUND" if availability_http_status == 404
                    else "UNAUTHORIZED" if availability_http_status in (401, 403)
                    else "UNAVAILABLE"
                )
                availability_error = f"Availability endpoint returned HTTP {availability_http_status}"

            if not covering_active_slots and "NO_SCHEDULE_COVERAGE" not in reason_codes:
                reason_codes.append("NO_SCHEDULE_COVERAGE: No active schedule covers the requested interval.")
            if availability_status == "UNKNOWN":
                reason_codes.append("AVAILABILITY_UNVERIFIED: The experience service did not provide a valid evaluation.")

        elif not any(slot.is_active for slot, _, _ in period_filtered_slots):
            reason_codes.append(
                "NO_ACTIVE_SCHEDULE: No active schedule matched the requested date."
                if requested_date is not None
                else "NO_ACTIVE_SCHEDULE: No active schedules were returned."
            )

        is_scheduled = any(slot.is_active for slot, _, _ in period_filtered_slots)
        slots = [slot for slot, _, _ in period_filtered_slots]

        if availability_status == "AVAILABLE":
            covering_ids = {slot.slot_id for slot, _, _ in covering_active_slots}
            slots = [replace(slot, is_open=slot.slot_id in covering_ids) for slot in slots]

        audit_outcome = availability_audit_outcome
        if audit_outcome == "SUCCESS" and availability_status == "UNKNOWN":
            audit_outcome = "UNAVAILABLE"
        return ScheduleContext(
            offering_id=canonical_id,
            is_scheduled=is_scheduled,
            slots=slots,
            availability_status=availability_status,
            reason_codes=reason_codes,
            evaluated_at=evaluated_at,
        ), AuditableToolCall(
            tool_name="schedule_lookup",
            workflow_id=workflow_id,
            step_id=step_id,
            started_at=started_at,
            elapsed_ms=total_elapsed_ms,
            outcome=audit_outcome,
            error_summary=availability_error,
            retry_count=total_retries,
        )

    # 5. biodiversity_prediction_lookup (ADR-0019)
    def biodiversity_prediction_lookup(
        self,
        destination_id: str,
        workflow_id: str,
        step_id: str,
        auth_token: Optional[str] = None,
    ) -> Tuple[BiodiversityContext, AuditableToolCall]:
        started_at = datetime.now(timezone.utc).isoformat()
        canonical_dest_id = parse_non_empty_uuid(destination_id, "destination_id")
        url = (
            f"{self.planner_service_url}/api/planner/biodiversity/predictions"
            f"?destinationId={urllib.parse.quote(canonical_dest_id)}"
        )
        status, data, elapsed_ms, retries = self._execute_http_get(url, auth_token)

        if status == 200 and isinstance(data, dict):
            # 1. Verify destinationId matches requested destination (ADR-0019 / IBiodiversityConsumerService)
            raw_resp_dest_id = data.get("destinationId")
            try:
                resp_dest_id = parse_non_empty_uuid(raw_resp_dest_id, "response.destinationId")
            except ValueError:
                resp_dest_id = None
            if resp_dest_id != canonical_dest_id:
                msg = (
                    "The biodiversity service omitted or returned a different destination; "
                    "its predictions were discarded."
                )
                return (
                    BiodiversityContext(
                        status=BiodiversityStatus.INVALID,
                        predictions=[],
                        uncertainty_notes=msg,
                    ),
                    AuditableToolCall(
                        tool_name="biodiversity_prediction_lookup",
                        workflow_id=workflow_id,
                        step_id=step_id,
                        started_at=started_at,
                        elapsed_ms=elapsed_ms,
                        outcome="INVALID_PAYLOAD",
                        error_summary=msg,
                        retry_count=retries,
                    ),
                )

            remote_status = str(data.get("status", "unavailable")).strip().lower()
            if remote_status == "available":
                metadata = data.get("modelMetadata") if isinstance(data.get("modelMetadata"), dict) else {}
                provenance = data.get("provenance") if isinstance(data.get("provenance"), dict) else {}

                model_version = (
                    metadata.get("modelVersion")
                    or provenance.get("modelVersion")
                    or data.get("modelVersion")
                )
                model_source = (
                    provenance.get("provider")
                    or metadata.get("provider")
                    or data.get("modelSource")
                )
                raw_timestamp = (
                    metadata.get("inferenceTimestamp")
                    or provenance.get("inferenceTimestamp")
                    or data.get("evaluatedAt")
                )

                if (
                    not isinstance(model_version, str)
                    or not model_version.strip()
                    or not isinstance(model_source, str)
                    or not model_source.strip()
                ):
                    msg = "The biodiversity service omitted required model provenance; its predictions were discarded."
                    return (
                        BiodiversityContext(
                            status=BiodiversityStatus.INVALID,
                            predictions=[],
                            uncertainty_notes=msg,
                        ),
                        AuditableToolCall(
                            tool_name="biodiversity_prediction_lookup",
                            workflow_id=workflow_id,
                            step_id=step_id,
                            started_at=started_at,
                            elapsed_ms=elapsed_ms,
                            outcome="INVALID_PAYLOAD",
                            error_summary=msg,
                            retry_count=retries,
                        ),
                    )

                evaluated_at_str: Optional[str] = None
                if raw_timestamp is not None:
                    try:
                        parsed_ts = parse_iso8601_timestamp(str(raw_timestamp), "inferenceTimestamp")
                        evaluated_at_str = parsed_ts.isoformat()
                    except ValueError:
                        msg = "The biodiversity service returned an invalid inferenceTimestamp; its predictions were discarded."
                        return (
                            BiodiversityContext(
                                status=BiodiversityStatus.INVALID,
                                predictions=[],
                                uncertainty_notes=msg,
                            ),
                            AuditableToolCall(
                                tool_name="biodiversity_prediction_lookup",
                                workflow_id=workflow_id,
                                step_id=step_id,
                                started_at=started_at,
                                elapsed_ms=elapsed_ms,
                                outcome="INVALID_PAYLOAD",
                                error_summary=msg,
                                retry_count=retries,
                            ),
                        )

                raw_species = data.get("predictedSpecies")
                if not isinstance(raw_species, list) or len(raw_species) > 100:
                    msg = "The biodiversity service returned a malformed prediction list; its predictions were discarded."
                    return (
                        BiodiversityContext(
                            status=BiodiversityStatus.INVALID,
                            predictions=[],
                            uncertainty_notes=msg,
                        ),
                        AuditableToolCall(
                            tool_name="biodiversity_prediction_lookup",
                            workflow_id=workflow_id,
                            step_id=step_id,
                            started_at=started_at,
                            elapsed_ms=elapsed_ms,
                            outcome="INVALID_PAYLOAD",
                            error_summary=msg,
                            retry_count=retries,
                        ),
                    )

                species_list: List[FocalSpeciesPrediction] = []
                for item in raw_species:
                    if not isinstance(item, dict):
                        msg = "The biodiversity service returned a non-object species item; its predictions were discarded."
                        return (
                            BiodiversityContext(
                                status=BiodiversityStatus.INVALID,
                                predictions=[],
                                uncertainty_notes=msg,
                            ),
                            AuditableToolCall(
                                tool_name="biodiversity_prediction_lookup",
                                workflow_id=workflow_id,
                                step_id=step_id,
                                started_at=started_at,
                                elapsed_ms=elapsed_ms,
                                outcome="INVALID_PAYLOAD",
                                error_summary=msg,
                                retry_count=retries,
                            ),
                        )
                    try:
                        raw_suitability = item.get("habitatSuitability")
                        if (
                            isinstance(raw_suitability, bool)
                            or not isinstance(raw_suitability, (int, float))
                            or not math.isfinite(float(raw_suitability))
                        ):
                            raise ValueError("habitatSuitability must be a finite number")

                        raw_occ = item.get("occurrenceProbability")
                        occ_val: Optional[float] = None
                        if raw_occ is not None:
                            if (
                                isinstance(raw_occ, bool)
                                or not isinstance(raw_occ, (int, float))
                                or not math.isfinite(float(raw_occ))
                            ):
                                raise ValueError("occurrenceProbability must be a finite number")
                            occ_val = float(raw_occ)

                        species_list.append(
                            FocalSpeciesPrediction(
                                species_id=parse_non_empty_uuid(str(item.get("speciesId", "")), "speciesId"),
                                species_name=str(item.get("commonName") or item.get("speciesName") or "").strip(),
                                scientific_name=str(item.get("scientificName") or "").strip(),
                                conservation_status=item.get("conservationStatus"),
                                occurrence_probability=occ_val,
                                habitat_suitability=float(raw_suitability),
                                primary_threats=item.get("primaryThreats"),
                                confidence_level=item.get("confidenceLevel"),
                                uncertainty_text=item.get("uncertaintyText"),
                            )
                        )
                    except (ValueError, TypeError) as exc:
                        msg = f"The biodiversity service returned a malformed prediction ({exc}); its predictions were discarded."
                        return (
                            BiodiversityContext(
                                status=BiodiversityStatus.INVALID,
                                predictions=[],
                                uncertainty_notes=msg,
                            ),
                            AuditableToolCall(
                                tool_name="biodiversity_prediction_lookup",
                                workflow_id=workflow_id,
                                step_id=step_id,
                                started_at=started_at,
                                elapsed_ms=elapsed_ms,
                                outcome="INVALID_PAYLOAD",
                                error_summary=msg,
                                retry_count=retries,
                            ),
                        )

                limitations = data.get("limitations")
                uncertainty_text = data.get("uncertaintyText") or data.get("uncertaintyNotes")
                joined_notes = " ".join(
                    part.strip()
                    for part in (limitations, uncertainty_text)
                    if isinstance(part, str) and part.strip()
                ) or None

                ctx = BiodiversityContext(
                    status=BiodiversityStatus.AVAILABLE,
                    predictions=species_list,
                    model_version=model_version.strip(),
                    model_source=model_source.strip(),
                    evaluated_at=evaluated_at_str,
                    uncertainty_notes=joined_notes,
                )
                audit = AuditableToolCall(
                    tool_name="biodiversity_prediction_lookup",
                    workflow_id=workflow_id,
                    step_id=step_id,
                    started_at=started_at,
                    elapsed_ms=elapsed_ms,
                    outcome="SUCCESS",
                    retry_count=retries,
                )
                return ctx, audit

            allowed_statuses = {e.value: e for e in BiodiversityStatus}
            enum_status = allowed_statuses.get(remote_status, BiodiversityStatus.UNAVAILABLE)
            outcome_label = "STALE" if enum_status == BiodiversityStatus.STALE else "UNAVAILABLE"
            msg = (
                data.get("message")
                or data.get("limitations")
                or f"The biodiversity service returned status '{enum_status.value}'; no current predictions are available."
            )
            ctx = BiodiversityContext(
                status=enum_status,
                predictions=[],
                uncertainty_notes=msg,
            )
            audit = AuditableToolCall(
                tool_name="biodiversity_prediction_lookup",
                workflow_id=workflow_id,
                step_id=step_id,
                started_at=started_at,
                elapsed_ms=elapsed_ms,
                outcome=outcome_label,
                error_summary=msg,
                retry_count=retries,
            )
            return ctx, audit

        ctx = BiodiversityContext(
            status=BiodiversityStatus.UNAVAILABLE,
            predictions=[],
            uncertainty_notes=f"Remote biodiversity inference service did not return an available result (HTTP {status}).",
        )
        audit = AuditableToolCall(
            tool_name="biodiversity_prediction_lookup",
            workflow_id=workflow_id,
            step_id=step_id,
            started_at=started_at,
            elapsed_ms=elapsed_ms,
            outcome="UNAVAILABLE",
            error_summary=f"HTTP status {status}",
            retry_count=retries,
        )
        return ctx, audit
