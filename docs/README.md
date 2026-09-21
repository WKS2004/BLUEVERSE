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
- `ai-contribution/` — one AI usage record per participating GitHub account
- `security/` — security practices
- `testing/` — test strategy and acceptance evidence
- `api/` — public API conventions
- `contracts/` — machine-readable cross-client workflow and endpoint registries

## Reading the documentation

The repository is a v0 foundation and the documentation deliberately separates
the checked-in state from the target contract. The clients retain starter
surfaces outside the implemented Auth workflow. The public API gateway,
internal Auth service, Auth persistence, authorization and session-management
contract are checked in at `services/api` and `services/auth`. Domain
workflows and executable Agentic AI workflows remain implementation targets
unless a page explicitly says the behavior is currently available.

For a current-state summary, see
[`project/foundation-gap-analysis.md`](project/foundation-gap-analysis.md).

For the finalized coding-agent workflow, read
[`development/agent-resources.md`](development/agent-resources.md), then
[`.agents/README.md`](../.agents/README.md) and the routing matrix at
[`.agents/routing.md`](../.agents/routing.md). The root
[`AGENTS.md`](../AGENTS.md) remains the repository-wide authority.
