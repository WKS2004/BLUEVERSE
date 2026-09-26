# v1 member branch and integration workflow

The team develops each complete business component on one member-owned
feature branch. All four component branches can be implemented at the same
time. The [component relationship map](component-relationships.md) shows the
contracts those branches must agree on; it is not an implementation or merge
order.

## Member component branches

| Owner | One branch for the complete component | Pull request target |
|---|---|---|
| Member 1 | `features/coastal-experience-biodiversity` | `dev` |
| Member 2 | `features/marine-conditions-safety` | `dev` |
| Member 3 | `features/coastal-planner` | `dev` |
| Member 4 | `features/coastal-operations` | `dev` |

At G00, agree the shared IDs, source-of-truth ownership, contract schemas,
permissions, error/status semantics, timestamps and workflow identity against
the [relationship map](component-relationships.md). Create all four branches
from the agreed `dev` baseline. Each owner implements one new .NET service in
its own component-specific subfolder under `services/`, its data model, React
and Flutter capabilities, paired backend Agentic AI access boundary, tests
and documentation on that single branch. `services/api` remains the sole
public API and receives only the integration code needed to authenticate and
authorize, route or forward requests to those services. Auth is reused. Each
component still provides its required public API operations without changing
the existing API or Auth flows. The work-area plans help each owner cover the
scope; they do not create extra branches or require one member to wait for
another member's full component.

The GPS/location, planner date/time and operations evidence-media capabilities
in the [device-capability contract](device-capabilities.md) are included in
Members 1, 3 and 4's complete component branches respectively. They do not
create separate device-feature branches.

## Shared foundation and file ownership

The v0 API, Auth, React and Flutter foundations are shared by all four
parallel branches. Each member owns a new .NET service in a distinct
`services/<component-service>/` subfolder, including its domain logic and
persistence. Preserve existing shared behavior and make every API/Auth
integration additive and narrow. This section is the implementation rule for
every component and phase plan.

### Public API and Auth

- Implement each member's domain operations, DTOs, business validation,
  provider adapters, persistence and owning tests inside that member's new
  internal ASP.NET Core service under its own `services/<component-service>/`
  subfolder. The subfolder contains that service's project and service-local
  Dockerfile/configuration as needed. The service is private and is not a new
  client-facing boundary.
- `services/api` may receive only integration code: the public `/api/...`
  route/forwarding entry, existing authentication and permission integration,
  typed service client or proxy configuration, dependency-injection and
  options registration, and the minimum service-availability reporting
  required by the component. Do not put member endpoints/business handlers,
  rules, persistence or provider logic in `services/api`. Do not refactor the
  host or change existing endpoint behavior, middleware order, JWT/cookie
  handling, CORS, forwarded-header behavior, existing Auth forwarding, common
  error/health semantics or permission resolution.
- Reuse `services/auth` and its existing public API integration; no Auth
  service change is expected. If an essential integration change is found,
  limit it to integration code and do not alter registration, sign-in,
  refresh, logout, password, device/session, token issuance/revocation,
  role-to-permission resolution or system-role protections.
- All client calls continue through `services/api`. Component-service
  containers, internal routes and credentials stay private. Component
  services do not call Auth directly; the authenticated actor and authorized
  operation context are passed across the agreed internal boundary. At G00,
  agree each subfolder/project/service ID, internal route contracts,
  identity/permission propagation, data/schema ownership, network and health
  semantics. Do not invent those details independently in parallel branches.
- If a requirement appears to need a change to an existing API/Auth flow,
  identify the requirement, affected behavior and narrowest alternative at
  G00. Proceed only with the smallest essential change, keep it separate from
  unrelated component work, and include focused compatibility evidence in the
  PR. Record a material boundary change through the applicable ADR process.

### Docker and other shared files

Each member adds only its own required service Dockerfile and Compose entry,
plus narrowly scoped `.dockerignore`, `.gitignore`, edge configuration or
other root/shared-file changes the service actually requires. Keep changes
local to the affected service, preserve the selected DHI images, private
network boundaries and secret handling, and do not combine unrelated image,
network or repository cleanup. Record each touched shared infrastructure
file and why it is needed in the PR.

Shared cross-layer files include the endpoint catalog and UI integration
registry, both client route entry points, shared application shells/navigation,
React's Tailwind entry stylesheet, Flutter's shared theme, API/auth/network
adapters, and package/dependency manifests. They are not the home for a
component's implementation. Update only the exact route, workflow, endpoint,
registration, token or dependency entry needed by that component. Never
reformat or reorganize a shared file to make a feature fit.

### React and Flutter collision prevention

- Put screens, state, models, view models, repositories and component-specific
  API adapters in a member-specific module. The current React structure can
  use `apps/web/src/features/<component>/` with route-level screens in
  `apps/web/src/pages/<component>/`; Flutter can use
  `apps/mobile/lib/features/<component>/`. These are extension patterns, not
  a claim that all v1 modules already exist.
- Keep every member's route path, workflow ID, permission and endpoint
  references distinct and agree them at G00. Add only the component's route
  declaration to React `apps/web/src/app/routes.tsx` and Flutter
  `apps/mobile/lib/main.dart` when central registration is required. Do not
  reformat, reorder or refactor the shared route/auth/shell code.
- Use the existing shared design tokens, navigation, API clients and
  permission behavior. Touch those shared facilities only when the component
  cannot work through their existing extension points; coordinate such edits
  at G00 and keep each edit additive. React and Flutter must retain equal
  authorized business actions and outcomes.
- Add only the component's entries to `docs/contracts/ui-integration.json`
  and `docs/api/endpoint-catalog.json`; preserve all other entries and
  regenerate the catalog when required. Keep `package.json`, lockfiles,
  `pubspec.yaml` and its lockfile unchanged unless a dependency is necessary
  for the component and the choice is agreed at G00.

These practices reduce overlapping edits; they cannot guarantee conflict-free
merges when several branches add entries to the same registry or route file.
The maintainer handles any remaining conflicts sequentially on `dev` and then
checks both clients against the merged contracts.

## Pull requests and merged-branch compatibility

1. Each member submits one complete component PR to `dev`. The branch may use
   the G00-reviewed contracts and contract-level test doubles during parallel
   development; a test double is not evidence of real integration.
   The PR describes its component-owned folders and lists every shared file
   changed, the specific integration need, and evidence that existing API/Auth
   behavior is preserved. It must include the applicable route/catalog and
   client validation evidence.
2. Merge the four PRs one at a time. There is **no prescribed member or PR
   order**. The repository maintainer personally resolves merge conflicts,
   preserving each component's accepted contract, behavior, permissions and
   evidence.
3. Once the feature PRs are merged, check the real provider/consumer paths
   together on `dev`: Member 4 status with Member 1 availability; Member 1
   availability, Member 2 suitability and Member 4 restrictions with Member
   3 recommendations; and Member 1/2 evidence plus Member 3 workflow identity
   with Member 4 assessment and review.
4. Record incompatibilities found on the merged `dev` baseline and correct
   them there in focused changes, using a PR when required by repository
   branch protection. Update shared API/UI catalogs and validators if the
   corrections change routes or client workflows. Do not mark a relationship
   integrated based only on fixtures.
5. G07 is accepted only when all four complete components work together on
   `dev`, the compatibility corrections are complete, and each owner has
   supplied the evidence in the [component contracts](README.md#component-and-agent-contract-map)
   and [quality guide](quality-and-delivery.md).

## Component branch status

Update this table as each owner starts work, opens a PR, merges, and completes
the `dev` compatibility check. It tracks status only; it does not assign a
turn-taking order.

| Component | Owner label | Initial status | Branch | PR, merge and integration evidence |
|---|---|---|---|---|
| Member 1 — Experience and Biodiversity | Member 1 | Planned | `features/coastal-experience-biodiversity` | — |
| Member 2 — Marine Conditions and Safety | Member 2 | Planned | `features/marine-conditions-safety` | — |
| Member 3 — Planner and Itineraries | Member 3 | Planned | `features/coastal-planner` | — |
| Member 4 — Coastal Operations | Member 4 | Planned | `features/coastal-operations` | — |
| G07 — Integrated component acceptance | All members | Planned | `dev` | — |

This process deliberately distinguishes relationship dependencies from work
scheduling: the graph identifies the producer contracts each consumer needs;
it does not make the member work serial.

## Agentic AI work after G07

The member feature branches implement public workflow routes/status,
authorization, business request persistence, typed private adapters and
bounded not-connected/unavailable behavior. They do not implement executable
agents. Preserve `GET /api/health` as API liveness and keep database readiness
separate from AI dependency availability. See the
[Agentic AI integration boundary](agentic-ai-integration-boundary.md).

Only after all four component PRs are merged, compatibility is resolved and
G07 passes may members begin the actual Agentic AI components. Keep that code
on branches under `agentic-ai/**`, separate from `features/**`. A suggested
post-G07 work sequence is:

| Work | Branch | Relationship/dependency |
|---|---|---|
| AI-00 — Runtime, shared schemas, persistence/safety decisions and evaluation gates | `agentic-ai/runtime-foundation` | Starts after G07 and accepted runtime/tool/state contracts. |
| AI-01a — Member 1 Experience & Biodiversity Agent | `agentic-ai/member-1-experience-biodiversity` | Starts after AI-00 and the Member 1 component is accepted. |
| AI-01b — Member 2 Marine Conditions Agent | `agentic-ai/member-2-marine-conditions` | Starts after AI-00 and the Member 2 component is accepted; may run with AI-01a. |
| AI-02 — Member 3 Planning & Coordination Agent | `agentic-ai/member-3-planning-coordination` | Consumes the validated Member 1/2 agent reports. |
| AI-03 — Member 4 Safety & Operations Agent | `agentic-ai/member-4-safety-operations` | Consumes planner/evidence output and submits a proposal through Member 4; it cannot approve or execute. |
| AI-04 — Golden workflow and evaluations | `agentic-ai/golden-flow-evaluation` | Exercises all four roles, deterministic validation, human approval, protected execution, audit and safe failure. |

This sequence applies only to Agentic AI implementation after the domain gate.
It does not impose an order on the four member feature branches.
