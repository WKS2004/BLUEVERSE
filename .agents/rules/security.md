# Security rules

- Treat client input, model output, tool results, prompts, external documents,
  logs and configuration as untrusted data.
- Never commit passwords, tokens, JWT keys, API keys, production/private data
  or hidden Agentic AI reasoning. Use environment variables or approved secret
  management and sanitize evidence.
- Enforce authorization on the server through named permissions and the
  role-to-permission model. Test authenticated, unauthenticated, permitted and
  denied paths; do not rely on client visibility checks.
- Validate and constrain model/tool output deterministically before it can
  change state, call a tool, access data or trigger a high-impact action.
- Require the defined human approval for high-impact actions; fail closed on
  malformed input, missing authorization, unsafe tool selection and dependency
  uncertainty.
- Defend against prompt injection, indirect instructions, privilege escalation,
  replay, over-broad data access and unsafe retries. Persist only observable
  workflow state, audit data and execution summaries required by the design.

Read `docs/security/` and `docs/agentic-ai/` for detailed controls and tests.
