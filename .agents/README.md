# BLUEVERSE Agent Resources

This directory contains focused, repository-specific guidance for coding
agents working in BLUEVERSE. It supplements the root
[`AGENTS.md`](../AGENTS.md), which remains the primary instruction file for
architecture, security, Docker, API, database, client, testing and Git
decisions.

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

These resources are instructions, not a replacement for inspecting the
current source. Agents must verify that a referenced service, workflow,
directory or tool actually exists before modifying or reporting on it.

## Resource map

```text
.agents/
├── README.md                 # This guide
└── rules/
    ├── architecture.md       # Boundaries and architectural discipline
    ├── docker.md              # Images, Dockerfiles and network behavior
    ├── documentation.md       # Setup, architecture and ADR documentation
    ├── git.md                 # Commit, pull-request and contribution hygiene
    ├── security.md            # Input, secrets, authorization and AI safety
    └── testing.md             # Mandatory test-case implementation rules
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
centralized test layout, stable test IDs, minimum coverage for React, Flutter,
the public API, Auth, every backend service and Agentic AI, plus deterministic
fixtures, safety evaluation and CI discovery requirements.

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
services/*     Additional internal backend services
test/          Centralized automated test cases
docs/          Architecture, security, setup and evidence
infrastructure/docker/
               Docker and gateway configuration
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

## Current foundation status

This repository is a v0 foundation. At the time these resources were written:

- the React and Flutter starter clients are present;
- the Flutter project contains generated starter tests;
- `services/api` and `services/auth` are reserved locations but may not yet
  contain source projects;
- Agentic AI service implementations are described by target architecture and
  safety documentation but are not assumed to exist;
- Docker and CI workflows may report missing service projects until those
  projects are intentionally added.

Agents must distinguish current implementation from target architecture. Do
not create replacement sample applications to fill a foundation gap, and do
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
7. run proportionate validation, including lint/build/test or structural
   checks;
8. report changed files, checks run, results and any known blockers.

For changes that affect multiple boundaries, consult all relevant rules. For
example, a new backend endpoint may require `architecture.md`, `security.md`,
`testing.md`, `documentation.md` and possibly `docker.md` if it changes
deployment or health behavior.

## Keeping this directory current

When repository conventions materially change, update the applicable rule file
and this README in the same change. Keep rules concise and enforceable; place
large matrices, implementation phases and evidence requirements in the linked
`docs/` documents. Never add secrets, private data, generated build output or
machine-specific configuration to `.agents`.
