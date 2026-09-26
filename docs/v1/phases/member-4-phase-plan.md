# Wanshaja Sooriyabandara (Member 4) phase plan — Coastal Operations, Advisories & Alerts

**Assigned owner:** Wanshaja Sooriyabandara (`@WKS2004`). Feature branch:
`features/coastal-operations`; Agentic AI branch after G07:
`agentic-ai/safety-operations`.

This plan divides the [Wanshaja Sooriyabandara (Member 4) component contract](../components/member-4-coastal-operations-advisories-alerts.md)
into work areas for one complete component branch. The
[component relationship map](../component-relationships.md) describes its
producer/consumer contracts, and the
[branch and integration workflow](../member-branch-workflow.md) defines
parallel work, pull requests and the G07 gate. Work areas are not separate
branches or PRs and do not schedule other members.

The [shared foundation and file-ownership rules](../member-branch-workflow.md#shared-foundation-and-file-ownership)
apply to every phase: keep this component additive, preserve existing API/Auth
flows, implement Wanshaja Sooriyabandara's business logic in its own `services/` microservice,
and limit `services/api` to integration code. Minimize shared React, Flutter,
route-registry and infrastructure edits.

Wanshaja Sooriyabandara (Member 4) owns BLUEVERSE-managed operational targets/state, assessment
records, proposal and decision lifecycle, reviewer authorization, alerts,
protected execution and audit/history. The Safety & Operations Agent may
later propose an action; it never approves or executes. The read-only status
contract is consumed by Ushan Srinuka and Adithya Gunawardana, while the full assessment workflow
consumes evidence and workflow context from Ushan Srinuka, Sanuda Abeysinghe and Adithya Gunawardana. All members
develop against the contracts agreed at G00.

## Component work areas

| Area | Work |
|---:|---|
| 1 | Managed target, operational state and status API |
| 2 | Assessment, evidence, proposal and status records |
| 3 | Reviewer decisions, revalidation and protected execution |
| 4 | Agentic backend boundary, alerts, client workflow and closeout |

Use the single branch `features/coastal-operations` for all four work areas
and submit one complete feature PR to `dev`. Implement against the G00-agreed
Ushan Srinuka (Member 1) identities and evidence, Sanuda Abeysinghe (Member 2) condition/suitability, and Adithya Gunawardana (Member 3)
workflow contracts while those branches are in progress. Verify live
provider/consumer behavior on `dev` after the component PRs merge. Keep
operational state distinct from assessment/business-workflow status. Do not
add the Safety & Operations Agent or production AI behavior before G07.

The numbered phases below are contract/dependency work areas, not a required
implementation timeline or separate branch sequence. Implement all four as
one component on the single member branch; work areas may overlap where their
local technical dependencies permit. Use the component relationship map to
see cross-member producer/consumer dependencies.

## Phase 1 — Managed target, operational state and status API

**Starts after:** the team agrees canonical destination/activity/offering IDs
and ownership at G00. Build against that shared contract while the Ushan Srinuka (Member 1)
branch is in progress.

**Implement:**

- references to the canonical Ushan Srinuka (Member 1) managed targets, without a duplicate
  catalogue or target identity;
- a BLUEVERSE-owned operational state model and permitted transition policy,
  separate from publication, schedule, workflow and alert lifecycle;
- the public application status contract and read-only lookup needed by
  Ushan Srinuka and Adithya Gunawardana, with resource scope and server-owned permission checks;
- the persistence, relationships, constraints, audit needs and current-state
  result semantics;
- initial managed-state records through approved seed/setup data so the
  read-only consumer contract has a real authoritative result; and
- explicit scope that actions affect only BLUEVERSE-managed operations, not
  external government closures, emergency dispatch or regulatory authority.

This status API is read-only for consumers. After work area 3 is implemented,
operational-state changes occur only through the authorized approval and
execution path; seed/setup data is not a runtime mutation shortcut. Keep the
reviewed status schema available to Ushan Srinuka and Adithya Gunawardana while all branches are in
progress; live integration is verified on `dev` after the component PRs merge.

**Handoff:** a stable authoritative current-state/restriction result with
time/version semantics that Ushan Srinuka (Member 1) can use in availability and Adithya Gunawardana (Member 3)
can apply during candidate eligibility.

## Phase 2 — Assessment, evidence, proposal and status records

**Shared contracts:** use the Ushan Srinuka (Member 1) availability, Sanuda Abeysinghe (Member 2)
condition/suitability, and Adithya Gunawardana (Member 3) workflow request/status contracts agreed
at G00, together with this component's work-area 1 status contract. Implement
against reviewed schemas and test doubles while the other branches are in
progress; verify live provider integration on `dev` after the component PRs
merge.

**Implement:**

- permission-checked assessment initiation, list/queue, detail, progress and
  status retrieval;
- the public proposal/status API contract and a typed, private backend
  integration seam for the future Safety & Operations Agent, with an explicit
  not-connected/unavailable outcome before that agent exists;
- durable objective/time context and references to affected managed target,
  Ushan Srinuka (Member 1) experience evidence, Sanuda Abeysinghe (Member 2) condition/suitability evidence and
  Adithya Gunawardana (Member 3) workflow identity;
- optional operator image evidence on an assessment version: Flutter camera/
  image selection and React image-file upload, public-API streaming, server
  validation/inspection, private storage, reviewer-only authorized retrieval,
  version immutability and audit; finalize format/count/size, storage,
  sanitization and retention choices against [ADR-0018](../../adr/ADR-0018-assessment-evidence-storage-boundary.md);
- proposal versions separated from actual operational state and from
  execution history;
- reviewer-visible source, time/freshness, missing-evidence, validation,
  proposed outcome and audit summaries; and
- authorized history/filter behavior and safe unavailable/blocked states.

Before Agentic AI exists, use controlled proposal fixtures only in tests or
development validation of the proposal boundary. Do not ship a fixture-backed
production proposal generator. The member branch implements the business API
and prepared private adapter, not the Safety & Operations Agent or its runtime.
Wanshaja Sooriyabandara's component service must accept and validate a versioned proposal
through the architecture-approved private boundary once the agent is built;
the public API remains the authenticated route to that service.

**Handoff:** a reviewer can inspect a persisted assessment/evidence record;
reading it cannot trigger any protected change. Optional images are available
only to authorized reviewers through the public API, are not publicly hosted,
and are not passed to the future Agentic AI agent as raw media. See the shared
[device-capability contract](../device-capabilities.md).

## Phase 3 — Reviewer decisions, revalidation and protected execution

**Local dependency and shared contracts:** build on work area 2 and the
Ushan Srinuka (Member 1) availability, Sanuda Abeysinghe (Member 2) deterministic suitability, Adithya Gunawardana (Member 3) workflow
identity, and Wanshaja Sooriyabandara (Member 4) state/version contracts agreed at G00. Use fixtures for
contract-level development; verify integrated evidence against the real
provider components on `dev` after the PRs merge.

**Implement:**

- approve, reject and request-revision decisions with exact reviewer
  permissions, proposal version and recorded rationale/context as required;
- explicit pending, stale, expired, rejected, revised, approved and executed
  state transitions as defined by the owner contract;
- server-side authorization, idempotent duplicate handling and concurrency
  protection for competing reviewers;
- immediate pre-execution revalidation of actor permission, current target
  state, proposal applicability, evidence freshness, deterministic safety
  outcomes and legal state transitions;
- transactional protected state changes with consistent audit/history; and
- proof that rejection, revision, lost permission, stale evidence, blocked
  validation, duplicate decisions or failure produce no protected effect.

Test fixtures are inputs for domain tests only. The live AI agent will submit
the same typed proposal after G07; it will not receive an approval or
execution tool.

**Handoff:** a proposal can change protected state only after an eligible,
current, authorized reviewer decision and server-side revalidation.

## Phase 4 — Agentic backend boundary, alerts, history and closeout

**Local dependency:** complete work area 3's decision, execution, alert and
history APIs on this branch.

**Verify the future Agentic dependency behavior:** its server-side readiness
and dispatch handling distinguishes not connected/unavailable from business
or database failure, gives authorized users a safe workflow status, and
cannot cause or authorize protected state changes. Follow
[Member feature integration with Agentic AI](../agentic-ai-integration-boundary.md).

**Implement in both clients:**

- operator assessment initiation and status tracking;
- reviewer queue, evidence review, approve/reject/revise and decision result;
- visible operational target state, audit/history and alert/advisory scope,
  severity and lifecycle for permitted roles; and
- usable denied, stale, duplicate, conflict, missing-evidence, dependency
  failure and safe-failure feedback.

The assessed Flutter-initiation/React-review demonstration is one path, not a
client restriction. The same authorized initiation, review, decision and
status capabilities must exist in both clients. Update route/API metadata and
verify database, transaction, concurrency, permission and client evidence.

**Wanshaja Sooriyabandara (Member 4) exit:** proposal, human decision and execution are distinct and
auditable; every high-impact mutation has server-side approval and
revalidation; both clients show equivalent permissions and outcomes. The
Safety & Operations Agent remains deferred until after G07.

## Progress record

Update the Wanshaja Sooriyabandara (Member 4) row in the
[component branch status tracker](../member-branch-workflow.md#component-branch-status)
with branch/PR/merge status and integration evidence. Record work-area
milestones in the PR or the team's agreed contribution record. The canonical
[owner map](../../project/ai-team-members.md) records the exact identity and
branches.
