# BLUEVERSE Documentation

## Sections

- `architecture/` — target system, network and deployment architecture
- `adr/` — Architecture Decision Records
- `agentic-ai/` — Agentic AI architecture, evaluation and safety
- `database/` — database design and migration guidance
- `deployment/` — local/cloud deployment
- `development/` — developer setup, Git workflow and CI workflows
- `project/` — roadmap, contribution and AI-use documentation
- `security/` — security practices
- `testing/` — test strategy and acceptance evidence
- `api/` — public API conventions

## Reading the documentation

The repository is a v0 foundation and the documentation deliberately separates
the checked-in state from the target contract. The current clients are starter
projects, and the public API foundation is checked in at `services/api`.
`services/auth`, persistence, authorization, domain workflows and Agentic AI
workflows remain implementation targets unless a page explicitly says the
behavior is currently available.

For a current-state summary, see
[`project/foundation-gap-analysis.md`](project/foundation-gap-analysis.md).
