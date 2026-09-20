# Agent Resources

## Purpose and current status

BLUEVERSE keeps repository-specific instructions, routing, validation and
workflow skills under `.agents/`. These resources help coding agents load the
smallest useful context while preserving the repository's architecture,
security, testing and contribution requirements.

The resource set is finalized for the current v0 foundation checkout. It
contains seven BLUEVERSE-owned skills and fourteen source-pinned supplementary
skills for the technologies that are actually present or explicitly planned:
React 19 + Vite, Flutter, ASP.NET Core, EF Core and test-quality workflows.
The registry also records three candidates that remain deferred until the
corresponding implementation exists.

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
5. Read one matching BLUEVERSE-owned skill.
6. Read an imported skill only when the task needs its framework-specific
   guidance.
7. Inspect the actual source, tests, workflow and detailed documentation
   named by the routing row.
8. Run the narrowest useful validation first and report missing tools,
   services, credentials or foundation projects exactly.

Skill discovery should remain lightweight: descriptions are used for routing,
and the full `SKILL.md` is loaded only after a workflow matches. Do not copy
large requirements matrices into skills or load every skill for every task.

## BLUEVERSE skill coverage

| Work area | Required owned workflow | Optional supplementary guidance |
|---|---|---|
| Repository readiness and gap analysis | `blueverse-foundation-audit` | — |
| ASP.NET API, Auth, persistence and backend services | `blueverse-backend-service` | `dotnet-webapi`, `optimizing-ef-core-queries` |
| React/Flutter API contracts and permission-aware clients | `blueverse-client-contract` | matching Flutter skills; `vercel-react-best-practices` for React/Vite performance |
| Test design, IDs, fixtures and discovery | `blueverse-test-design` | `run-tests`, `assertion-quality`, `test-anti-patterns`, `test-gap-analysis`, `grade-tests` |
| Agentic AI orchestration, tools, approvals and evaluation | `blueverse-agentic-ai-workflow` | deferred governance/OWASP skills until executable AI workflows exist |
| Docker, Compose, edge-nginx, networks and health | `blueverse-docker-gateway` | — |
| GitHub Actions, path filters, metrics and artifacts | `blueverse-ci-validation` | — |

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

Test cases belong in the framework-default directories defined by
`blueverse-test-design` and `docs/testing/implementation-plan.md`: React tests
are colocated under `apps/web/src` (with optional `apps/web/e2e`), Flutter tests
under `apps/mobile/test` and `apps/mobile/integration_test`, backend tests under
`services/<service>/tests`, and AI-agent tests under each agent package’s local
`tests/` directory. Do not create a repository-root centralized `test/` tree.

## Registry and maintenance

The [skill registry](../../.agents/registry/skills.json) is the inventory for
all twenty-one discovered skills. Imported skills are supplementary and must
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
python .agents/scripts/validate_agent_resources.py
git diff --check
```

The dependency-free validator checks required resource files, skill
frontmatter and names, registry/provenance metadata, overlays, routing
fixtures, relative Markdown links, secret-like assignments and generated-path
exclusions. The same validator runs in
`.github/workflows/repository-ci.yml` whenever repository-foundation paths,
including `.agents/`, are affected.

The Windows foundation verifier and application/Docker checks are separate
from agent-resource validation. They may remain blocked while the expected
ASP.NET service projects, credentials, Docker runtime or elevated shell are
unavailable. Report those conditions; never call a blocked check successful.

## Current repository boundary

The current checkout contains React and Flutter starter projects. The
`services/api` and `services/auth` locations are reserved by the architecture
but do not contain tracked source projects, and executable Agentic AI services
are not present. Ignored `bin/` and `obj/` output is not implementation
evidence. See the [foundation gap analysis](../project/foundation-gap-analysis.md)
for the current repository status and the next implementation gates.
