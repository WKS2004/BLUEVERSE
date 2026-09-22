# Agent Resources

## Purpose and current status

BLUEVERSE keeps repository-specific instructions, routing, validation and
workflow skills under `.agents/`. These resources help coding agents load the
smallest useful context while preserving the repository's architecture,
security, testing and contribution requirements.

The resource set is finalized for the current v0 foundation checkout. It
contains eight BLUEVERSE-owned skills and fifteen source-pinned supplementary
skills. Technology guidance covers the stack that is actually present or
explicitly planned: React 19 + Vite, Flutter, ASP.NET Core, PostgreSQL, EF Core
and test-quality workflows. The set also includes the optional `caveman`
response-style skill. The registry records six verified candidates that remain
deferred because they are broad, operationally privileged or not yet required.

This documentation describes how to use the resources. The operational source
of truth remains the files under `.agents/` and the root [`AGENTS.md`](../../AGENTS.md).

## Instruction authority

Use the following order when guidance overlaps:

1. [`AGENTS.md`](../../AGENTS.md) — repository-wide architecture, security,
   testing, Git and contribution requirements.
2. [`.agents/routing.md`](../../.agents/routing.md) — task-to-rule and
   task-to-skill selection.
3. The applicable files under [`.agents/rules/`](../../.agents/rules/) —
   focused safety, architecture, security, Docker, documentation, testing,
   Git, AI-usage and validation constraints.
4. The applicable BLUEVERSE-owned skill — the workflow for the requested
   change.
5. An imported skill — supplementary framework guidance only. Project rules
   and owned skills always win when wording conflicts.
6. The current source tree, requirements, ADRs and detailed documentation —
   the evidence for what is implemented and what remains planned.

Documentation, Docker configuration and reserved directories do not prove that
a service or workflow exists. Agents must verify the current checkout before
claiming implementation or test completion.

## Resource layout

| Path | Responsibility |
|---|---|
| `.agents/README.md` | Agent-resource operating guide and completion standard |
| `.agents/repository-map.md` | Current paths, boundaries and foundation facts |
| `.agents/routing.md` | Minimal rule/skill routing matrix |
| `.agents/rules/` | Focused repository constraints |
| `.agents/rules/endpoint-catalog.md` | Fast endpoint lookup and source-escalation rule |
| `.agents/rules/data-access.md` | PostgreSQL, EF Core, migration and test-provider policy |
| `docs/api/endpoint-catalog.md` | Fast, readable endpoint and route lookup |
| `docs/api/endpoint-catalog.json` | Machine-checked source for the readable catalog |
| `.agents/skills/` | On-demand project and supplementary workflows |
| `.agents/registry/skills.json` | Skill status, scope, provenance, revisions and compatibility notes |
| `.agents/registry/THIRD-PARTY-NOTICES.md` | Imported-skill license notices |
| `.agents/skill-overlays/` | BLUEVERSE bindings for portable skills |
| `.agents/evals/` | Small deterministic routing/resource evaluation fixtures |
| `.agents/scripts/validate_agent_resources.py` | Dependency-free structural validator |

## Efficient loading protocol

For every implementation or review task:

1. Read the root `AGENTS.md`.
2. Read `.agents/routing.md` and identify the changed paths.
3. Always read `rules/change-safety.md` and `rules/validation.md`.
4. Read only the additional rules selected by the routing row.
5. Read one matching BLUEVERSE-owned skill; load an additional cross-layer
   owned skill when the routing row selects it.
6. Read an imported skill only when the task needs its framework-specific
   guidance.
7. For any route, endpoint, client API call, gateway mapping or AI API task,
   read `.agents/rules/endpoint-catalog.md` and
   `docs/api/endpoint-catalog.md` before scanning the full source tree. Use the
   catalog as the default answer source; inspect implementation only when the
   lookup rule's escalation conditions apply.
8. For persistence, migration, EF Core or PostgreSQL work, read
   `.agents/rules/data-access.md`, `docs/database/README.md` and
   `docs/database/schema.md` before scanning the complete service.
9. Inspect the targeted source, tests, workflow and detailed documentation
   named by the routing row.
10. Run the narrowest useful validation first and report missing tools,
   services, credentials or foundation projects exactly.

Skill discovery should remain lightweight: descriptions are used for routing,
and the full `SKILL.md` is loaded only after a workflow matches. Do not copy
large requirements matrices into skills or load every skill for every task.

## BLUEVERSE skill coverage

| Work area | Required owned workflow | Optional supplementary guidance |
|---|---|---|
| Repository readiness and gap analysis | `blueverse-foundation-audit` | — |
| ASP.NET API, Auth, persistence and backend services | `blueverse-backend-service` | `dotnet-webapi`, `optimizing-ef-core-queries` |
| PostgreSQL/EF Core persistence, migrations and provider-specific tests | `blueverse-postgresql-efcore` | deferred PostgreSQL/Testcontainers skills only when approved and needed |
| React/Flutter API contracts and permission-aware clients | `blueverse-client-contract` | matching Flutter skills; `vercel-react-best-practices` for React/Vite performance |
| Test design, IDs, fixtures and discovery | `blueverse-test-design` | `run-tests`, `assertion-quality`, `test-anti-patterns`, `test-gap-analysis`, `grade-tests` |
| Agentic AI orchestration, tools, approvals and evaluation | `blueverse-agentic-ai-workflow` | deferred governance/OWASP skills until executable AI workflows exist |
| Docker, Compose, edge-nginx, networks and health | `blueverse-docker-gateway` | — |
| GitHub Actions, path filters, metrics and artifacts | `blueverse-ci-validation` | — |
| Response-style compression | — | `caveman` for explicit, user-requested conversational compression |

The current React application is React 19 + Vite. Next.js, React Server
Components, server actions, route handlers and server-only caching guidance
must be ignored unless the repository explicitly adopts those technologies.
React and Flutter must use only the public `/api/...` boundary.
For every new, generated or updated UI, also update
[`docs/contracts/ui-integration.json`](../contracts/ui-integration.json) and
run the route/API contract validator described in
[`ui-integration.md`](ui-integration.md). Shared workflow IDs connect the
relevant React and Flutter surfaces; the clients never call each other or
internal service hostnames.
React Web and Flutter Mobile are co-equal surfaces for clients, staff and
administrators, so agent guidance must plan both clients for a shared workflow
by default. Platform adaptation is about interaction context, not stakeholder
ownership. User-facing work also follows
[`../project/ui-experience-principles.md`](../project/ui-experience-principles.md).
For every frontend route, client API target, gateway/YARP/Nginx mapping,
backend or internal service endpoint, health/OpenAPI route, test-only fixture
endpoint or Agentic AI endpoint addition, update, rename, move or removal,
update `docs/api/endpoint-catalog.json` in the same change with its exact
method/path, source, owner, boundary, authorization, purpose and usage;
regenerate its Markdown view and run the catalog validator before completion.
Ordinary endpoint work does not require editing `.agents` instructions.

Test cases belong in the framework-default directories defined by
`blueverse-test-design` and `docs/testing/implementation-plan.md`: React tests
are colocated under `apps/web/src` (with optional `apps/web/e2e`), Flutter tests
under `apps/mobile/test` and `apps/mobile/integration_test`, backend tests under
`services/<service>/tests`, and AI-agent tests under each agent package’s local
`tests/` directory. Do not create a repository-root centralized `test/` tree.

## Registry and maintenance

The [skill registry](../../.agents/registry/skills.json) is the inventory for
all twenty-three discovered skills. Imported skills are supplementary and must
have:

- a source repository and exact forty-character revision;
- declared licensing information;
- a local path containing `SKILL.md`;
- a BLUEVERSE compatibility note;
- an explicit scope where routing benefits from one.

Update the registry and review the compatibility note whenever an imported
skill changes. Keep deferred candidates in the registry with a reason rather
than importing guidance that has no current implementation surface. Review
the [third-party notices](../../.agents/registry/THIRD-PARTY-NOTICES.md) when
adding or replacing an upstream skill.

Do not add generated output, caches, secrets, private data or machine-specific
configuration under `.agents/`. Do not weaken protected tests or replace
missing backend/AI projects with sample applications.

## Validation and CI

Run from the repository root:

```bash
python .agents/scripts/validate_endpoint_catalog.py --write-markdown
python .agents/scripts/validate_endpoint_catalog.py
python .agents/scripts/validate_agent_resources.py
git diff --check
```

The dependency-free validators check required resource files, skill
frontmatter and names, registry/provenance metadata, overlays, routing
fixtures, relative Markdown links, secret-like assignments, generated-path
exclusions, route declarations, gateway mappings, client API literals and
UI-catalog parity. The resource validator runs in
`.github/workflows/repository-ci.yml` whenever repository-foundation paths,
including `.agents/`, are affected. The endpoint validator runs in the UI
integration workflow whenever route, service, gateway, client or
endpoint-catalog paths are affected.

The endpoint source check supports literal C# controller routes and literal
`app.MapGet/Post/Put/Patch/Delete/Head/Options` endpoints, Swagger mappings,
and the current Nginx/YARP configuration. It checks route source identity,
production/test/AI separation, service ownership and declared authorization.
Grouped or dynamic endpoint forms and complex authorization policies require
an approved validator extension before adoption. These static checks do not
prove runtime authorization or the accuracy of prose usage descriptions;
review those against implementation and the application contract tests.

The Windows foundation verifier and application/Docker checks are separate
from agent-resource validation. They may remain blocked while the expected
ASP.NET service projects, credentials, Docker runtime or elevated shell are
unavailable. Report those conditions; never call a blocked check successful.

## Current repository boundary

The current checkout contains React and Flutter starter projects with the
implemented shared Auth workflow, plus the tracked `services/api` and
`services/auth` ASP.NET projects. Executable
Agentic AI services are not present. Ignored `bin/` and `obj/` output is not
implementation evidence. See the [foundation gap analysis](../project/foundation-gap-analysis.md)
for the current repository status and the next implementation gates.

The current database implementation is the Auth EF Core/Npgsql model with
checked-in PostgreSQL migrations. The default Auth tests intentionally use an
isolated provider for deterministic provider-independent cases, while
PostgreSQL-specific behavior is covered by explicitly enabled real-provider
tests. Future domain and Agentic AI schemas remain unimplemented.
