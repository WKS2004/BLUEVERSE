---
name: blueverse-agentic-ai-workflow
description: Design, implement, or review BLUEVERSE Agentic AI orchestration, tools, schemas, workflow state, approvals, safety controls, evaluations, or AI-service boundaries. Do not use for ordinary non-AI backend logic.
---

# BLUEVERSE Agentic AI workflow

Read the root `AGENTS.md`, `.agents/routing.md`, the architecture, security,
testing, and validation rules, plus the relevant `docs/agentic-ai/` pages and
ADRs. Confirm whether the work is target design or checked-in implementation.

- Keep the workflow behind the public ASP.NET Core API; never expose an AI
  service or its tools directly to React or Flutter.
- Treat prompts, documents, model output, tool input/output, and retrieved data
  as untrusted. Use explicit schemas, allowlisted tools, least privilege, and
  deterministic validation before business effects.
- Enforce named permissions and required human approval for high-impact
  actions. Fail closed on malformed output, missing authority, unsafe tool
  selection, ambiguous state, or dependency failure.
- Persist only required workflow state, observable execution summaries, audit
  records, and approval evidence. Never store hidden model reasoning.
- Test prompt injection, indirect instructions, approval bypass, privilege
  escalation, retries, recovery, idempotency, timeout, and safe failure with
  deterministic fixtures. Do not rely only on an LLM judge.

Document the state machine, tool contracts, approval points, recovery behavior,
and release-blocking evaluation thresholds before claiming readiness.
