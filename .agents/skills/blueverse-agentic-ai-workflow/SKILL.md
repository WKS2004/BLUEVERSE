---
name: blueverse-agentic-ai-workflow
description: Design, implement, or review BLUEVERSE Agentic AI orchestration, tools, schemas, workflow state, approvals, safety controls, evaluations, or AI-service boundaries. Do not use for ordinary non-AI backend logic.
---

# BLUEVERSE Agentic AI workflow

Read `docs/api/endpoint-catalog.md` for AI route questions. Do not invent
an endpoint from target architecture. For every AI endpoint addition, update, rename or
removal, **MUST** update the catalog JSON in the same change with its exact
method/path, usage, owner, public boundary and safety boundary, regenerate
Markdown, then run the endpoint and applicable UI validators. An executable AI
route is not complete while the catalog is stale or validation fails.

Read the root `AGENTS.md`, `.agents/routing.md`, the architecture, security,
testing and validation rules, and only the relevant `docs/agentic-ai/` pages or
ADRs. First classify the task as target design, resource review or checked-in
implementation. Read the applicable owning agent, component and workflow
contracts from the project documentation. Preserve distinct responsibilities,
typed handoffs, authorization, evidence and approval points. Documentation
and Docker stubs are not implementation evidence.

- Keep AI services and tools behind the public ASP.NET Core API; React and
  Flutter must never call them directly.
- Treat prompts, documents, retrieved data, model output and tool I/O as
  untrusted. Use typed schemas, explicit tool allowlists, least privilege and
  deterministic validation before business effects.
- Enforce named permissions and human approval for high-impact actions. Fail
  closed on malformed output, missing authority, unsafe tool selection,
  ambiguous state or dependency failure.
- Persist only required workflow state, execution summaries, audit records and
  approval evidence. Never persist hidden model reasoning.
- Test prompt injection, indirect instructions, approval bypass, privilege
  escalation, retries, recovery, idempotency, timeout, concurrency and safe
  failure with deterministic fixtures; an LLM judge alone is insufficient.

For each implementation, document the state machine, tool contracts,
approval points, recovery behavior, audit fields and release-blocking
evaluation thresholds before claiming readiness. Apply a deferred governance
skill in `.agents/registry/skills.json` only when its implementation surface
and scope justify it.
