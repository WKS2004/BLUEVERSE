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
from the agreed `dev` baseline. Each owner implements their complete API,
data model, React and Flutter capabilities, paired backend Agentic AI access
boundary, tests and documentation on that single branch. The work-area plans
help each owner cover the scope; they do not create extra branches or require
one member to wait for another member's full component.

The GPS/location, planner date/time and operations evidence-media capabilities
in the [device-capability contract](device-capabilities.md) are included in
Members 1, 3 and 4's complete component branches respectively. They do not
create separate device-feature branches.

## Pull requests and merged-branch compatibility

1. Each member submits one complete component PR to `dev`. The branch may use
   the G00-reviewed contracts and contract-level test doubles during parallel
   development; a test double is not evidence of real integration.
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
