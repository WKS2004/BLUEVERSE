# ADR-0022: Durable Coastal Operations publication dispatch

- Date: 2026-10-01
- Status: Accepted for user-directed branch work; shared G00 remains Pending
- Owner: Wanshaja Sooriyabandara (`WKS2004`)

## Context

Publication must durably hand the complete assessment to the future Member 4
agent entry point. A dispatch before commit could start work for a rolled-back
draft. Concurrent submissions could duplicate it. G07 prohibits executable
agents on this member branch.

## Decision

Keep the submit route/permission for Publish assessment. Atomically store one
immutable typed delivery snapshot with the business SUBMITTED record,
idempotency outcome and audit. A member-owned delivery service uses optimistic
concurrency, finite leases/attempts and stable dispatch ID for deduplication.
Persist transport state only; AI plans/steps remain in the future runtime.
Include safe evidence metadata, never raw images or storage URLs.
The record-experience follow-up adds the assessment title and selected time
zone to new snapshots alongside UTC instants. Titled drafts may remain unlinked
until producer catalogues exist, but cannot submit or create a dispatch without
a canonical target. Existing snapshots remain immutable and are not rewritten.

Keep production DI disconnected before G07. Expand the typed proposal port
with a payload overload, preserving its identifier overload. Ratify future
private route/authentication through shared contracts; add no live AI endpoint.
Transport acceptance cannot authorize an action. Alert publication remains
independent. Add specific alert grants with broad grant compatibility and
scoped read-only resource audit timelines.

## Consequences

Publication survives interruption. At-least-once delivery needs receiver
deduplication; exactly-once external execution is not promised. Explicit
failure/attempt audit supports recovery. An additive service-owned table
preserves existing records. Both clients use separate searchable pages.
