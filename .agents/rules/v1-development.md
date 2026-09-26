# v1 component and Agentic AI development rules

Use these rules for any v1 member feature, its shared integration, or actual
Agentic AI implementation. The v1 documents are detailed target contracts;
they do not prove that a service, route, integration or workflow exists.
Verify the checkout, tests and CI before describing implementation status.

## Read the owning contract

Start with [`docs/v1/README.md`](../../docs/v1/README.md),
[`requirements-coverage-and-readiness.md`](../../docs/v1/requirements-coverage-and-readiness.md),
[`member-branch-workflow.md`](../../docs/v1/member-branch-workflow.md) and
[`component-relationships.md`](../../docs/v1/component-relationships.md).
Then read the assigned owner's component contract and phase plan. Read
[`device-capabilities.md`](../../docs/v1/device-capabilities.md) when device
behavior applies, and the
[`Agentic AI integration boundary`](../../docs/v1/agentic-ai-integration-boundary.md)
for member-owned AI access seams. For actual post-G07 AI work, also read the
owning agent contract and the applicable `docs/agentic-ai/` architecture,
safety, tools, implementation and evaluation documents. Use the canonical
owner map in `docs/project/ai-team-members.md` for names and branch assignments.

## G00, ownership and branch shape

- Do not begin member component implementation until the G00 shared contracts
  are agreed and recorded. This includes workflow and canonical IDs, ownership,
  API and private-service schemas, permissions, error/status meanings,
  service identity and cross-component handoffs.
- Each assigned member implements the complete component on that member's one
  `features/**` branch and submits one component PR to `dev`. All four
  components may proceed concurrently from the agreed `dev` baseline. The
  relationship map shows producer/consumer compatibility; it does not
  prescribe an implementation or merge order. Do not split a component into
  phase or sub-component branches.
- Use the exact owner names, GitHub accounts and feature/Agentic AI branches in
  the source documents. The maintainer merges component PRs sequentially and
  handles shared-file conflicts on `dev`.

## Preserve v0 and keep service ownership

- Each member owns a distinct private ASP.NET Core service under its own
  `services/<component-service>/` subfolder. That service owns the component's
  domain rules, validation, application/data-access code, provider adapters,
  persistence and service-local tests.
- `services/api` remains the only client-facing API. Limit v1 changes there to
  the necessary public route, existing authentication/permission integration,
  authorized actor/operation-context propagation through the G00-approved
  internal contract, typed private-service client/proxy,
  dependency-injection/options wiring and narrowly scoped
  dependency-availability reporting. Do not move member business handlers,
  domain rules, provider logic or persistence into it.
- Reuse `services/auth`. Do not change registration, sign-in, refresh, logout,
  password, session/device, token or role-to-permission behavior. If a
  requirement truly needs an API/Auth flow change, identify the requirement
  and narrowest additive integration at G00; isolate and document the smallest
  essential change with compatibility evidence.
- Keep each service private. React and Flutter call only the public `/api/...`
  contract; component services do not call Auth directly. Persist through the
  owning service and PostgreSQL/EF Core. Keep Docker, health and service
  discovery consistent with the approved boundary and selected DHI images.
- Keep React Web and Flutter Mobile equal in authorized roles, capabilities,
  workflow actions, state outcomes and public contract. Use member-specific
  modules and minimal additive changes to shared route, API, navigation,
  theme and registry files. Register every UI route and API reference in the
  UI registry and endpoint catalog as required by their rules.

## Keep provider ownership distinct

Use each component contract and the relationship map as the detailed source
of provider ownership. The current assignment is: Ushan Srinuka (Member 1)
owns the backend map-provider adapter; Sanuda Abeysinghe (Member 2) owns
Open-Meteo; Adithya Gunawardana (Member 3) owns the IT3091 biodiversity
inference-service adapter and its validated result contract; and Ushan
consumes that result only through the agreed backend contract. The IT3091
inference integration is an external ML capability, not Agentic AI. Do not
move it to the Agentic AI runtime or call IT3091 directly from a client or
agent. Any future agent request must use the approved Member 3 backend
contract. The device-capability contract defines device ownership; do not add
unrelated device features to equalize workloads.

## Enforce the G07 Agentic AI gate

- Before G07, implement only the member-owned business workflow and its typed,
  private Agentic AI adapter/access seam, availability behavior and safe
  `not connected`/unavailable result. Keep exact route/schema/status decisions
  within the G00-reviewed contracts.
- Before G07, do not implement or ship actual agents, model calls, agent tools,
  orchestration, agent prompts/runtime, AI-owned plans/steps or execution
  state. Controlled fixtures and transport doubles may validate the member
  seam but are not a production agent or evidence that the runtime exists.
- Report Agentic AI availability separately from public API liveness and
  database health. Bound checks and dispatch timeouts. An absent/unavailable
  optional AI dependency may disable only the AI-dependent operation; ordinary
  authorized deterministic component behavior must continue when its own
  dependencies are healthy. Never fabricate an AI result or let availability
  bypass authorization, approval, revalidation or audit.
- Start actual `agentic-ai/**` implementation only after all four complete
  member components are merged to `dev`, compatibility defects are resolved,
  required evidence is recorded and G07 is accepted. Verify acceptance in the
  branch tracker and against merged `dev` source/evidence; do not infer it from
  a draft contract. Follow the post-G07 branch/dependency sequence in
  `member-branch-workflow.md`. The ML adapter
  delivered by Member 3 on a feature branch is not subject to this Agentic AI
  runtime gate.
- Before G07, Agentic AI documents may be analyzed or refined as authorized;
  documentation work does not authorize early runtime implementation.

## Component completion evidence

For the assigned component, use requirement-based tests and the owner's
acceptance evidence. Test doubles alone do not establish integration with
another component or a live provider. Check both clients, permissions,
failure/unavailable behavior, relevant persistence, deployment wiring,
endpoint/UI registries and documentation. Update the shared status tracker
and report source-backed implementation gaps accurately. Keep changes to
shared files narrow and list them in the PR.

For every agent-assisted change, follow `.agents/rules/ai-usage.md`. Do not
commit or push without the user's explicit request; that agent rule is
separate from repository-approved GitHub Actions behavior. Preserve the
existing `dev-backup` rescue/synchronization workflow unless the user asks
for that automation to change.
