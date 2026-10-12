# ADR-0022: Member service data availability and fault isolation

- Status: Accepted for the Marine Safety branch; other member implementations apply this contract in their own branches.
- Date: 2026-10-08
- Acting owner: Sanuda Abeysinghe (`sanudaabey`)

## Context

Member components are implemented and merged independently. A missing, stopped,
misconfigured or unreachable producer service can make its data unavailable
without making every consumer capability unavailable. Deployment order must not
be represented as a product state or concealed with invented data.

## Decision

Each member service owns its domain behavior, persistence and readiness. Other
member services are contacted through agreed backend API contracts only when
their data is required by the requested operation. Do not require another member
service for process startup or include it in the consumer's own readiness probe.
Required local schema, database and shared authentication remain real platform
prerequisites; this decision does not bypass permission or approval checks.

Missing dependency data uses structured operation-level unavailability. Marine
returns 503 ProblemDetails with `availability`, `dependency`, `code`, `reason`,
`missingFields`, `retryable` and `data: null`. A valid empty dataset remains a
successful empty dataset. Partial provider evidence retains nullable values,
missing-field labels and the existing deterministic `UNKNOWN` rules.

Marine retains independent condition queries, saved profile/snapshot/assessment
reads and profile deactivation during an activity catalogue outage. New profile
drafts, edits, reviews and evaluations require verified published activity data
and fail safely while it is unavailable. No cached projection is promoted to an
authoritative published catalogue and no placeholder activity IDs are invented.

Both clients present availability in plain language, retain saved records and
disable only actions requiring the unavailable activity data. Retry refreshes
that data; recovery requires no application restart. Product wording uses the
current availability state and makes no promise about an implementation date.

The public API's Marine integration translates an unreachable/timed-out Marine
destination into the same unavailable contract. It preserves received upstream
responses, started responses and unrelated routes. A forwarded write may have
completed, so an ambiguous forwarding failure is not marked retryable and does
not trigger an automatic second attempt. Auth verification failures remain
closed and identify missing verified actor data.

Local Compose keeps Marine dependent only on its own PostgreSQL readiness.
Stack CI checks member OpenAPI documents only when their source is present;
an implemented member's failed health/OpenAPI check remains a failure.

## Consequences and evidence

The branch can run without the other member services. Data-dependent actions
remain unavailable until their producer supplies valid data. Required platform
outages and local persistence failures are not hidden by this isolation.

No other member service or executable agent is added by this decision. G07,
live cross-member deployment and PostgreSQL runtime evidence remain separate
requirements. See [the response contract](../api/marine-dependency-availability.md)
and the owning `M2-ISOLATION-*`, `API-MARINE-AVAIL-*`, `WEB-MARINE-AVAIL-*`,
`MOB-MARINE-ISOLATION-*` and CI regression cases.
