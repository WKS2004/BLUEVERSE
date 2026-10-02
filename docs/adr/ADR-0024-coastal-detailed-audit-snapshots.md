# ADR-0024: Coastal activity snapshots and signed display identity

Date: 2026-10-02. Status: Accepted for the Member 4 branch implementation;
shared G00 acceptance remains Pending.

## Context

Action codes and actor UUIDs do not explain a record's history to stakeholders.
The private service needs the verified person's display name and recorded roles,
plus the original before/after values, rather than reconstructing history from
the latest record. Clients cannot supply trusted identity metadata.

## Decision

Capture an allowlisted change array, record title, readable summary and actor
name/roles on new audit entries in the same SaveChanges as the domain mutation.
Persist nullable identity/summary/title columns and required JSONB arrays in
OperationsAudit. Exclude credentials, provider payloads, hidden reasoning and
image bytes. System operations are labeled System; older missing snapshots stay
explicitly unavailable.

Extend the existing API actor envelope with an optional bounded Base64 JSON
identity header sourced only from verified JWT claims. Strip incoming spoofed
headers, bind the snapshot into the HMAC, and validate its structure and bounds
before creating private-service claims. Preserve permission-based authorization,
legacy envelopes without identity and existing Auth behavior. This is an
essential display-identity integration, not a new public endpoint.

## Consequences

Migration `20261002081218_CoastalDetailedAudit` adds columns and an array check;
existing rows receive empty arrays without invented history. Rollback retains
original events but discards the added detail columns. Stored names/roles are
historical snapshots and need the same access and retention controls as audit.
Rebuild/restart the API and Coastal Operations service together using existing
configuration; an older verifier cannot accept the extended signed envelope.
No new secret, role grant or executable Agentic AI workflow is introduced.
See [the change contract](../v1/coastal-operations-record-navigation-and-audit.md).
