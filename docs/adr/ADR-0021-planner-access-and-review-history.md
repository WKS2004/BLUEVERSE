# ADR-0021: Planner access and durable itinerary reviews

**Status:** Implemented on the Member 3 feature branch; shared G00 and release
acceptance remain pending.

**Date:** 2026-10-05

## Context

Planner endpoints already require named permission claims, but Auth previously
provided no planner permission catalogue or normal traveller access. Users
also need to understand changed conditions without losing their chosen trip,
and must select canonical destinations by name through the public API.

## Decision

Keep business behavior and persistence inside Coastal Planner. Add two narrow
public reads: the canonical catalogue projection and owner-scoped review
history. Persist each review and itinerary evidence/version update atomically.
Preserve UTC schedules while recording the display IANA zone; backfill legacy
Sri Lankan itineraries with `Asia/Colombo`.

The essential additive Auth integration seeds the five planner permissions and
a non-system Coastal traveller role idempotently. Existing Admin receives
planner grants. Configurable self-service assignment grants only that role at
registration; it defaults off in Auth options and on in local Compose. Existing
accounts, sessions and administrative capabilities retain their established
flows. Authorization remains role → permission, with owner filtering.

## Consequences

New local registrations can plan without requesting administrator access.
Existing accounts require role assignment and refreshed permission claims.
Operators can disable self-service assignment. Reviews preserve evidence and
user intent; deleting a trip cascades its review history. Catalogue/conditions
remain owner-sourced; absent services produce unavailable outcomes. No peer
contract or G00/G07 acceptance is implied. No executable Agentic AI is added.

The implementation and verification commands are documented in
[the planner guide](../development/coastal-planner.md).
