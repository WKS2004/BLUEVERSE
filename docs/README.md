# BLUEVERSE Documentation

## Sections

- `architecture/` — target system, network and deployment architecture
- `adr/` — Architecture Decision Records
- `agentic-ai/` — Agentic AI architecture, evaluation and safety
- `database/` — database design and migration guidance
- `deployment/` — local/cloud deployment
- `development/` — developer setup, Git workflow and CI workflows
- `development/agent-resources.md` — agent routing, skills, provenance and validation
- `project/` — roadmap, contribution and AI-use documentation
- `ai-contribution/` — one AI usage record per participating GitHub account
- `security/` — security practices
- `testing/` — test strategy and acceptance evidence
- `api/` — public API conventions

## Reading the documentation

The repository is a v0 foundation and the documentation deliberately separates
the checked-in state from the target contract. The current clients are starter
projects; `services/api` and `services/auth` are expected service locations but
are not present in this checkout yet. Pages that describe routes, persistence,
authorization or Agentic AI workflows therefore describe the implementation
target unless they explicitly say that the behavior is currently available.

For a current-state summary, see
[`project/foundation-gap-analysis.md`](project/foundation-gap-analysis.md).

For the finalized coding-agent workflow, read
[`development/agent-resources.md`](development/agent-resources.md), then
[`.agents/README.md`](../.agents/README.md) and the routing matrix at
[`.agents/routing.md`](../.agents/routing.md). The root
[`AGENTS.md`](../AGENTS.md) remains the repository-wide authority.
