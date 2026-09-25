# BLUEVERSE Documentation

## Sections

- `architecture/` — target system, network and deployment architecture
- `adr/` — Architecture Decision Records
- `agentic-ai/` — Agentic AI architecture, evaluation and safety
- `database/` — database design and migration guidance
- `deployment/` — local/cloud deployment
- `development/` — developer setup, Git workflow and CI workflows
- `development/ui-integration.md` — cross-client route/API contract and CI gate
- `development/agent-resources.md` — agent routing, skills, provenance and validation
- `project/` — roadmap, contribution and AI-use documentation
- `project/ui-experience-principles.md` — cross-platform product and UI direction
- `../DESIGN.md` — shared React Web and Flutter Mobile visual system and design tokens
- `v0/` — implemented foundation components, shared Auth flow and extension contracts
- `v1/` — finalized v1 scope, separate member components and agents, shared workflows and acceptance
- `ai-contribution/` — one AI usage record per participating GitHub account
- `security/` — security practices
- `testing/` — test strategy and acceptance evidence
- `api/` — public API conventions
- `contracts/` — machine-readable cross-client workflow and endpoint registries

## Reading the documentation

The checked-in application provides the v0 foundation; v0 plus v1 is the
submission target. Documentation separates the checked-in state from the
target contract. React and Flutter implement shared account registration,
session, profile and permission-aware Auth administration workflows. The
public API gateway, internal Auth service, Auth persistence, authorization and
session-management contract are checked in at `services/api` and
`services/auth`. V1 domain workflows and executable Agentic AI workflows
remain implementation targets unless a page explicitly says the behavior is
currently available.

React Web and Flutter Mobile provide equal capability coverage for every
permitted role and workflow. Device input or layout does not allocate business
responsibility to a platform. Read the
[v0 foundation guide](v0/README.md) for technical components and the
[v1 index](v1/README.md) for the four member components, four agents and
integrated workflows. See
[`project/ui-experience-principles.md`](project/ui-experience-principles.md)
for the product-facing UI standard, and the
[BLUEVERSE Design System](../DESIGN.md) for the shared palette,
typography hierarchy, image treatment, component character and platform
implementation guidance.

For a current-state summary, see
[`project/foundation-gap-analysis.md`](project/foundation-gap-analysis.md).

For the public Auth route inventory, transport/session contract and error
behavior, see [`api/README.md`](api/README.md). For current automated-test
evidence, see [`testing/test-matrix.md`](testing/test-matrix.md) and
[`testing/strategy.md`](testing/strategy.md). These pages describe the
checked-in v0 foundation; deferred domain and Agentic AI work remains clearly
marked as planned.

For the finalized coding-agent workflow, read
[`development/agent-resources.md`](development/agent-resources.md), then
[`.agents/README.md`](../.agents/README.md) and the routing matrix at
[`.agents/routing.md`](../.agents/routing.md). The root
[`AGENTS.md`](../AGENTS.md) remains the repository-wide authority.
