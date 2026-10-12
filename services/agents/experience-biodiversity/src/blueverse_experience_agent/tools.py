"""Allowlisted Tool Implementations for Coastal Experience & Biodiversity Agent.

Tools:
1. destination_lookup: Read destination details from experience-biodiversity service.
2. activity_lookup: Read activity metadata from experience-biodiversity service.
3. offering_lookup: Read offering context linking destination and activity.
4. schedule_lookup: Read schedule slots and time-window availability for an offering.
5. biodiversity_prediction_lookup: Read biodiversity predictions via Coastal Planner adapter (ADR-0019).

Strictly preserves:
- Read-only operations (zero state modifications).
- Resilient timeouts (2.0s default) and bounded retries (max 2 retries).
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
from datetime import datetime, timezone
from typing import Any, Dict, List, Optional, Tuple

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
        timeout_seconds: float = 2.0,
        max_retries: int = 2,
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
        query_params: Dict[str, str] = {}
        if target_date:
            query_params["date"] = target_date
        if period_starts_at:
            query_params["startsAt"] = period_starts_at
        if period_ends_at:
            query_params["endsAt"] = period_ends_at
        if query_params:
            url += "?" + urllib.parse.urlencode(query_params)

        status, data, elapsed_ms, retries = self._execute_http_get(url, auth_token)

        if status == 200 and data is not None:
            raw_slots: List[Dict[str, Any]] = []
            reason_codes: List[str] = []
            operational_restriction: Optional[Dict[str, Any]] = None
            evaluated_at: Optional[str] = None
            notes: Optional[str] = None
            explicit_avail_status: Optional[str] = None

            if isinstance(data, list):
                raw_slots = [item for item in data if isinstance(item, dict)]
            elif isinstance(data, dict):
                raw_slots = [item for item in (data.get("slots") or data.get("schedules") or []) if isinstance(item, dict)]
                reason_codes = [str(r) for r in (data.get("reasonCodes") or [])]
                operational_restriction = data.get("operationalRestriction")
                evaluated_at = data.get("evaluatedAt")
                notes = data.get("notes")
                if data.get("status"):
                    explicit_avail_status = str(data["status"]).strip().upper()
            else:
                audit = AuditableToolCall(
                    tool_name="schedule_lookup",
                    workflow_id=workflow_id,
                    step_id=step_id,
                    started_at=started_at,
                    elapsed_ms=elapsed_ms,
                    outcome="INVALID_PAYLOAD",
                    error_summary="Schedule response was neither a list nor an object",
                    retry_count=retries,
                )
                return None, audit

            slots: List[ScheduleSlot] = []
            for idx, s in enumerate(raw_slots):
                slot_id = str(s.get("id") or s.get("slotId") or f"slot-{idx}")
                start_str = str(s.get("startsAt") or s.get("startTime") or "")
                end_str = str(s.get("endsAt") or s.get("endTime") or "")
                is_active = bool(s.get("isActive", True))
                is_open = bool(s.get("isOpen", is_active)) and is_active
                slots.append(
                    ScheduleSlot(
                        slot_id=slot_id,
                        offering_id=str(s.get("offeringId") or canonical_id),
                        start_time=start_str,
                        end_time=end_str,
                        time_zone_id=str(s.get("timeZoneId") or "UTC"),
                        is_active=is_active,
                        is_open=is_open,
                        capacity_remaining=s.get("capacityRemaining"),
                        created_at=s.get("createdAt"),
                        updated_at=s.get("updatedAt"),
                    )
                )

            open_slots = [s for s in slots if s.is_open]
            is_scheduled = (
                bool(data.get("isScheduled", len(open_slots) > 0))
                if isinstance(data, dict)
                else len(open_slots) > 0
            )

            # Evaluate interval coverage if period_starts_at and period_ends_at were requested
            if period_starts_at and period_ends_at:
                req_start = parse_iso8601_timestamp(period_starts_at, "period_starts_at")
                req_end = parse_iso8601_timestamp(period_ends_at, "period_ends_at")
                matching_window = False
                for slot in open_slots:
                    try:
                        slot_start = parse_iso8601_timestamp(slot.start_time, "slot.start_time")
                        slot_end = parse_iso8601_timestamp(slot.end_time, "slot.end_time")
                        if slot_start <= req_start and slot_end >= req_end:
                            matching_window = True
                            break
                    except ValueError:
                        continue
                if not matching_window and "NO_SCHEDULE_COVERAGE" not in reason_codes:
                    reason_codes.append("NO_SCHEDULE_COVERAGE: No active schedule encompasses the requested interval.")

            # Check operational restrictions if present
            if isinstance(operational_restriction, dict):
                source_status = str(operational_restriction.get("sourceStatus") or "").upper()
                if operational_restriction.get("hasRestriction") is True:
                    reason_text = operational_restriction.get("reason") or "Active restriction imposed by coastal operations."
                    reason_codes.append(f"OPERATIONAL_RESTRICTION_ACTIVE: {reason_text}")
                if source_status == "UNAVAILABLE":
                    reason_codes.append(
                        "OPERATIONAL_SERVICE_UNAVAILABLE: Operational review status could not be verified."
                    )
                    explicit_avail_status = "UNKNOWN"

            if explicit_avail_status:
                availability_status = explicit_avail_status
            elif any("OPERATIONAL_SERVICE_UNAVAILABLE" in r for r in reason_codes):
                availability_status = "UNKNOWN"
            elif reason_codes or not is_scheduled or len(open_slots) == 0:
                availability_status = "UNAVAILABLE"
            else:
                availability_status = "AVAILABLE"

            sched = ScheduleContext(
                offering_id=canonical_id,
                is_scheduled=is_scheduled,
                slots=slots,
                availability_status=availability_status,
                reason_codes=reason_codes,
                operational_restriction=operational_restriction,
                evaluated_at=evaluated_at,
                notes=notes,
            )
            audit = AuditableToolCall(
                tool_name="schedule_lookup",
                workflow_id=workflow_id,
                step_id=step_id,
                started_at=started_at,
                elapsed_ms=elapsed_ms,
                outcome="SUCCESS",
                retry_count=retries,
            )
            return sched, audit

        outcome = "NOT_FOUND" if status == 404 else ("UNAUTHORIZED" if status in (401, 403) else "UNAVAILABLE")
        audit = AuditableToolCall(
            tool_name="schedule_lookup",
            workflow_id=workflow_id,
            step_id=step_id,
            started_at=started_at,
            elapsed_ms=elapsed_ms,
            outcome=outcome,
            error_summary=f"HTTP status {status}",
            retry_count=retries,
        )
        return None, audit

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
            if raw_resp_dest_id is not None:
                try:
                    resp_dest_id = parse_non_empty_uuid(str(raw_resp_dest_id), "response.destinationId")
                except ValueError:
                    resp_dest_id = None
                if resp_dest_id != canonical_dest_id:
                    msg = "The biodiversity service returned a different destination; its predictions were discarded."
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
