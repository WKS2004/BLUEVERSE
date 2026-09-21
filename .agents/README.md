# BLUEVERSE Agent Resources

This directory contains focused, repository-specific guidance for coding
agents working in BLUEVERSE. Use [`routing.md`](routing.md) to load only the
rules relevant to the touched paths. It supplements the root
[`AGENTS.md`](../AGENTS.md), which remains the primary instruction file for
architecture, security, Docker, API, database, client, testing and Git
decisions.

For the repository-facing explanation of the finalized resource set, see
[`docs/development/agent-resources.md`](../docs/development/agent-resources.md).

For endpoint lookup questions that do not change code, use
[`rules/endpoint-catalog.md`](rules/endpoint-catalog.md) and the readable
catalog first. Do not scan the whole repository when the catalog answers the
question; escalate to the catalog-listed source only under that rule's doubt,
missing-data, validation or explicit-verification conditions.

## How agents must use these resources

Before changing a part of the repository, an agent must:

1. read the root `AGENTS.md`;
2. identify the relevant rule files in `.agents/rules/`;
3. follow the more specific rule when it adds constraints for the requested
   area;
4. preserve existing user changes and repository conventions;
5. update documentation and tests when the change affects behavior or
   architecture;
6. report applicable checks, test results and any environment or foundation
   limitation before claiming completion.

## Mandatory endpoint-documentation workflow

For every frontend route, client API target, gateway/YARP/Nginx mapping,
backend or internal service endpoint, health route, OpenAPI/Swagger route,
test-only fixture endpoint or Agentic AI endpoint addition, update, rename,
move or removal, agents **MUST**:

1. Read the [readable endpoint catalog](../docs/api/endpoint-catalog.md)
   before scanning the complete source tree.
2. Update its [JSON source](../docs/api/endpoint-catalog.json) in the same
   change with the exact method/path, source, owner, boundary,
   authorization, purpose and usage. Remove entries for removed routes.
3. Update `docs/contracts/ui-integration.json` when a React or Flutter
   workflow or public API reference is affected.
4. Regenerate the Markdown view with
   `python .agents/scripts/validate_endpoint_catalog.py --write-markdown`.
5. Run `python .agents/scripts/validate_endpoint_catalog.py`; run
   `python scripts/validation/validate_ui_integrations.py` and its tests for
   affected client workflows.

This is a blocking definition-of-done requirement. Do not claim an endpoint
change is complete while the catalog validator reports drift, stale Markdown,
missing metadata, an undocumented route or an unsupported route form. Routine
route changes update `docs/api/`, not `.agents` guidance.

The route inventory is application documentation under `docs/api/`.
Routine route changes update that documentation; they do not rewrite
`.agents` instructions. Update rules or skills here only when the user
explicitly requests or approves an agent-guidance change.

For every task also read [`rules/change-safety.md`](rules/change-safety.md),
then [`rules/validation.md`](rules/validation.md) before handoff. Use
[`repository-map.md`](repository-map.md) for current paths and known
foundation gaps. This routing keeps context small without weakening any
architecture, security or testing requirement.

These resources are instructions, not a replacement for inspecting the
current source. Agents must verify that a referenced service, workflow,
directory or tool actually exists before modifying or reporting on it.

## Resource map

```text
.agents/
├── README.md                 # This guide and completion standard
├── routing.md                # Task-to-rule and validation routing
├── repository-map.md         # Current paths and foundation facts
├── rules/
    ├── architecture.md       # Boundaries and architectural discipline
    ├── change-safety.md       # Scope, permissions and context discipline
    ├── docker.md              # Images, Dockerfiles and network behavior
    ├── documentation.md       # Setup, architecture and ADR documentation
    ├── endpoint-catalog.md    # Fast endpoint lookup and source escalation
    ├── git.md                 # Commit, pull-request and contribution hygiene
    ├── ai-usage.md            # AI contribution logging and identity checks
    ├── security.md            # Input, secrets, authorization and AI safety
    ├── testing.md             # Mandatory test-case implementation rules
    └── validation.md          # Changed-path validation and evidence
├── scripts/
│   ├── validate_agent_resources.py
│   │                           # Deterministic rules/skills/link validation
│   └── validate_endpoint_catalog.py
│                               # Source/catalog/UI contract drift validation
├── registry/
│   └── skills.json            # Pinned source and compatibility metadata
├── skill-overlays/            # Repository bindings for portable skills
├── evals/                     # Deterministic resource/routing fixtures
└── skills/                    # On-demand repository workflows
    ├── blueverse-agentic-ai-workflow/
    ├── blueverse-backend-service/
    ├── blueverse-ci-validation/
    ├── blueverse-client-contract/
    ├── blueverse-docker-gateway/
    ├── blueverse-foundation-audit/
    ├── blueverse-test-design/
    ├── vercel-react-best-practices/
    ├── flutter-*/
    └── selected dotnet-* and test-quality skills/
```

## Repository skills

Codex discovers skills under `.agents/skills` automatically. Only skill names
and descriptions are loaded for discovery; the full `SKILL.md` is read when a
matching workflow is selected. Keep each skill narrow and reuse rules/docs
instead of copying them.

| Skill | Use for |
|---|---|
| `blueverse-foundation-audit` | Repository-wide readiness and gap analysis |
| `blueverse-backend-service` | ASP.NET API/Auth/internal service work |
| `blueverse-client-contract` | React/Flutter public-contract work |
| `blueverse-test-design` | Test cases, IDs, matrices, fixtures and runners |
| `blueverse-agentic-ai-workflow` | AI orchestration, tools, approvals and evaluation |
| `blueverse-docker-gateway` | DHI, Compose, edge-nginx, networks and health |
| `blueverse-ci-validation` | GitHub Actions, discovery, metrics and artifacts |

### Portable framework skills

The repository vendors only selected, source-pinned skills that materially
match the current stack. They are optional supplements; BLUEVERSE rules and
owned skills remain authoritative.

| Skill family | Current use |
|---|---|
| `vercel-react-best-practices` | React 19 + Vite performance and rendering guidance; Next.js-only rules are excluded by the BLUEVERSE compatibility section |
| `flutter-*` | Official Flutter architecture, networking, JSON, routing and test guidance |
| `dotnet-webapi`, `optimizing-ef-core-queries` | ASP.NET Core and EF Core guidance for the checked-in backend services |
| `run-tests`, `assertion-quality`, `test-anti-patterns`, `test-gap-analysis`, `grade-tests` | Narrow .NET/polyglot test execution and quality analysis; BLUEVERSE testing rules remain binding |

Provenance, upstream revisions, licenses, local paths and deferred candidates
are recorded in [`registry/skills.json`](registry/skills.json). Do not update
an imported skill in place without updating its registry revision and reviewing
its compatibility notes.

Validate this directory with:

```text
python .agents/scripts/validate_agent_resources.py
```

### `rules/architecture.md`

Use this when changing application boundaries, services, clients, data flow or
architectural structure. It reinforces that:

- React and Flutter communicate with the public ASP.NET Core API;
- internal services remain private;
- material architectural changes receive an ADR;
- premature domain services are avoided while the repository is still v0.

Also consult the architecture documents under `docs/architecture/`, the ADRs
under `docs/adr/`, and the root instructions before introducing a new service
or integration boundary.

### `rules/docker.md`

Use this when changing Compose, Dockerfiles, container networks, health checks
or image configuration. It requires:

- the selected Docker Hardened Images to remain in use;
- custom Dockerfiles to remain under `infrastructure/docker`;
- no assumption that minimal runtime images contain a shell;
- services to remain on their intended networks.

Check `compose.yaml`, `infrastructure/docker/`, Docker documentation and the
relevant CI workflows before changing container behavior.

### `rules/documentation.md`

Use this when changing setup, operations, architecture, deployment, service
boundaries or other documented behavior. Keep documentation executable and
current, update architecture pages when boundaries change, and add or update
an ADR for a significant architectural decision.

Documentation changes should remain synchronized with the actual repository;
do not document a future service or workflow as if it were already
implemented.

### `rules/ai-usage.md`

Use this whenever an agent contributes to the repository. It defines the
required per-member log path, fields, account verification process and rules
for preserving historical records. The account mapping is maintained in
[`docs/project/ai-team-members.md`](../docs/project/ai-team-members.md), and
individual records belong under `docs/ai-contribution/`.

### `rules/git.md`

Use this for source-control decisions. Keep commits focused and explainable,
use pull requests for shared changes, preserve meaningful individual
contribution, and do not commit `.env` files or generated build artifacts.

Follow the repository’s branch and contribution guidance in
`docs/development/git-workflow.md` and `docs/project/contribution.md`.

### `rules/security.md`

Use this for authentication, authorization, configuration, API boundaries,
user input, model output, tools and data handling. Treat client and model input
as untrusted, never commit secrets, use permission-based authorization,
validate AI/tool outputs deterministically before execution, and never persist
hidden model reasoning.

Read the detailed guidance in `docs/security/` and `docs/agentic-ai/` for
security-sensitive changes.

### `rules/testing.md`

This is the mandatory agent checklist for test-case work. It defines the
framework-default test locations, stable test IDs, minimum coverage for React,
Flutter, the public API, Auth, every backend service and Agentic AI, plus
deterministic fixtures, safety evaluation and CI discovery requirements.

The complete implementation sequence and test matrix are in
[`docs/testing/implementation-plan.md`](../docs/testing/implementation-plan.md).
Agents must consult both documents when adding or changing tests.

## Repository scope and boundaries

The expected application and service locations are:

```text
apps/web       React client
apps/mobile    Flutter client
services/api   Public ASP.NET Core API
services/auth  Internal authentication service
services/*     Additional internal backend services and their local tests
services/ai/*,
services/ai-agents/*,
services/agents/*
               Agentic AI services and their local `tests/` suites
docs/          Architecture, security, setup and evidence
scripts/validation/
               Dependency-free UI route/API contract checks and tests
infrastructure/docker/
               Docker and gateway configuration
docs/api/endpoint-catalog.md
               Readable inventory of frontend, gateway, public API, test-only
               and Agentic AI endpoint status
```

The system boundary is:

```text
React / Flutter → public ASP.NET Core API → PostgreSQL
                                      └── internal Agentic AI services
```

Clients must not call internal Auth or Agentic AI services directly. Agents
must not introduce API version path segments such as `/api/v1`, replace the
role-to-permission model with hard-coded role checks, expose database access to
clients, or move the edge gateway out of its intended role.

For UI work, `docs/contracts/ui-integration.json` is the canonical mapping
from a shared workflow ID to the React route, Flutter route and public API
endpoint references. Run
`scripts/validation/validate_ui_integrations.py` for every new, generated or
updated client surface. The two clients connect through shared workflow IDs
and the public API; they never call each other or internal service hostnames.

For the complete route inventory, use `docs/api/endpoint-catalog.md`.
The UI registry is intentionally only the workflow-facing subset; the
endpoint catalog also records backend, gateway, documentation and test-host
routes. Update `docs/api/endpoint-catalog.json`, regenerate the Markdown
view and run `.agents/scripts/validate_endpoint_catalog.py` whenever a route,
endpoint, client API literal or gateway mapping changes.

## Current foundation status

This repository is a v0 foundation. At the time these resources were written:

- the React and Flutter starter clients are present;
- the implemented shared Auth session workflow is present in both clients;
- `services/api` and `services/auth` contain tracked ASP.NET Core projects,
  controller routes, tests and OpenAPI configuration;
- Agentic AI service implementations are described by target architecture and
  safety documentation but are not assumed to exist;
- the endpoint catalog intentionally lists no implemented Agentic AI API;
- ignored `bin/` and `obj/` output is not implementation evidence.

Agents must distinguish current implementation from target architecture. Do
not create replacement sample applications or a repository-root centralized
`test/` tree to fill a foundation gap, and do
not claim a workflow, service, test suite or deployment is complete merely
because its documentation or Docker configuration exists.

## Change workflow for agents

For a normal implementation task:

1. inspect the relevant source, tests, documentation and workflow files;
2. read all applicable `.agents/rules/*.md` files;
3. preserve the public API and service boundaries;
4. implement the smallest focused change in the correct repository location;
5. add or update the required tests and case IDs;
6. update setup, architecture, testing or ADR documentation when needed;
7. update the acting member's AI usage log when the task is AI-assisted;
8. run proportionate validation, including lint/build/test or structural
   checks;
9. report changed files, checks run, results and any known blockers.

For changes that affect multiple boundaries, consult all relevant rules. For
example, a new backend endpoint may require `architecture.md`, `security.md`,
`testing.md`, `documentation.md` and possibly `docker.md` if it changes
deployment or health behavior.

## Keeping this directory current

When repository conventions materially change, propose any required guidance
update and apply it only with explicit user authorization. Keep rules concise and enforceable; place
large matrices, implementation phases and evidence requirements in the linked
`docs/` documents. Never add secrets, private data, generated build output or
machine-specific configuration to `.agents`.
