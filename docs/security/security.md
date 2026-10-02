# Security Checklist

New Coastal Operations audit identity comes only from verified JWT name/role
claims through a bounded, HMAC-bound internal snapshot. The API strips spoofed
headers; the service validates the snapshot before creating claims. Display
roles do not replace permission checks. Legacy absent identity stays unavailable.
Only allowlisted field values are audited; no credentials, provider payloads,
image bytes or hidden reasoning. Query view restoration rechecks grants and
draft state and cannot mutate automatically. See [ADR-0024](../adr/ADR-0024-coastal-detailed-audit-snapshots.md).

Coastal Operations grants and resource scope are specified in
[the publication contract](../v1/coastal-operations-publication-and-ui.md).
Audit reading needs audit.read plus owner/manager scope. Specific publish grants
cannot resolve; draft mutation grants cannot publish. Queue reading cannot
inspect another person's unpublished assessment.
Form-options access derives from existing Coastal Operations grants and
returns scoped named assessment choices with `Cache-Control: no-store`.
Title/ID filters cannot broaden resource scope. IDs are server-generated;
an unlinked draft sentinel cannot publish or dispatch. Catalogue/planner
references remain explicitly unavailable until producer contracts exist.
Time-zone IDs must be active DB entries; the server rejects invalid and
ambiguous local times and resolves UTC before persistence.

Use this checklist as implementation evidence. An unchecked item is not
implemented or has not yet been verified in the current repository.

## Repository

- [x] `.env` ignored
- [x] secrets absent from commits
- [x] no credentials in Dockerfiles
- [x] no private data in test fixtures

## API

- [x] JWT authentication with required 32-byte minimum signing key
- [x] permission-based authorization with system-role escalation protection
- [x] persisted device sessions with five-account-per-device and five-session-per-account limits, scoped logout and logout-all-devices support
- [x] active sessions separated from ended-session lifecycle logs; archived rows cannot authenticate
- [x] malformed JWT lifetime configuration fails closed instead of silently selecting a default
- [x] unexpected API/Auth failures use sanitized RFC 7807 responses without exception or credential disclosure
- [x] server-issued device credentials with hashed keys and secure web/native transport
- [x] rotating hashed refresh tokens with replay revocation and one/30-day absolute expiry
- [x] password changes isolated to `POST /api/auth/change-password`; profile updates do not accept password fields
- [x] validation
- [x] secure CORS
- [x] structured error handling
- [ ] rate/abuse controls considered where appropriate

## Database

- [x] restricted network exposure for internal Auth/database networks
- [x] migration-based Auth schema changes
- [ ] least-privilege credentials
- [x] no plaintext passwords/tokens

## Agentic AI

- [ ] tool authorization
- [ ] deterministic validation
- [ ] prompt-injection defenses
- [ ] human approval for high-impact actions
- [ ] no hidden reasoning persistence
- [ ] safe failure

The v1 target has four distinct agents with typed input/output contracts,
allowlisted tools and durable workflow state. The
[canonical assessment](../v1/workflows.md) requires server-side
permission checks, deterministic validation and authorized human approval
before a BLUEVERSE-managed high-impact state change. Rejection and revision
must not execute that change. Stale conditions, malformed output, tool
failure, prompt injection and retry exhaustion need recorded safe outcomes.
These unchecked controls are not implementation evidence.

## Coastal Operations Logs and draft attachments (2026-10-02)

Logs require `operations.audit.read` and corresponding record-read grants.
Assessment owners see their own retained states; queue readers also see others'
non-draft records. Alert managers see retained alerts; ordinary audit readers
see only their own. IDs and filters cannot widen scope. Audit responses are
`no-store`. Draft image addition/removal requires the existing evidence upload
grant and ownership, DRAFT state and optimistic version. Submitted/revision/
reviewed assessments reject attachment edits. Removed content returns 410
before storage reads, while metadata and audit association are retained. Byte
cleanup follows the committed tombstone and retries storage failures. Authored
published alert/assessment fields remain immutable; controlled lifecycle
operations retain their existing permissions.
