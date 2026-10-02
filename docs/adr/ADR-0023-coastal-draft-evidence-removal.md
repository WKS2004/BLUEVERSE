# ADR-0023: Draft evidence removal with retained metadata

Date: 2026-10-02. Status: Accepted for the Member 4 branch implementation;
shared G00 acceptance remains Pending.

## Context

The user requires correcting a wrong draft attachment and freezing authored
content/evidence when published. Upload was previously also accepted for
SUBMITTED/REVISION_REQUESTED assessments, and removed rows would sever the
existing audit association through evidence IDs. Database/storage deletion
cannot be a single transaction.

## Decision

Restrict additions/removals to the owner's DRAFT under the existing evidence
upload grant. Soft-remove metadata with REMOVED state and RemovedAt, retain
its original assessment association and audit event, and atomically increment
the assessment version. Filter removed evidence from detail/limits/publication.
Commit before attempting byte deletion, then track ContentDeletedAt. Retry
pending deletion through a bounded pass of the existing retention worker;
return no removed content even if the storage deletion fails.

## Consequences

An additive PostgreSQL migration is required. Removal/publish races use the
assessment concurrency token and preserve the publication snapshot. Metadata
is retained for audit, while the private image is deleted. Published user
content remains immutable; authorized lifecycle/retention transitions remain.
See [the change contract](../v1/coastal-operations-logs-and-evidence.md).
