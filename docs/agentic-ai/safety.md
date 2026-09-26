# Agentic AI safety — v1 target

**Core rule:** model output, objectives, tool results and third-party content
are untrusted. No executable v1 Agentic AI workflow is checked in yet;
these controls are required of its implementation.

Use the [implementation blueprint](implementation-blueprint.md) for the
end-to-end data boundary, model/provider privacy review, retrieval/RAG policy,
recovery design and release checklist.

## Deterministic decision boundary

Application code validates structured output and required fields, configured
activity safety profiles, source freshness, availability, operational state,
allowed transitions, authorization and approval requirements. The LLM
cannot invent thresholds or override an UNSUITABLE, UNKNOWN or BLOCKED
result. Biodiversity prediction is contextual information unless a
separate deterministic rule explicitly gives it another role.

## High-impact action

Temporary suspension of a BLUEVERSE-managed offering, cancellation of a
managed session, another restrictive operational state or a high-severity
BLUEVERSE alert requires deterministic validation, a named permission and
authorized human approval. The workflow pauses while approval is pending.
Reject and request-revision record a decision without executing the
proposed protected action. ASP.NET Core rechecks the action's eligibility
before transactional execution and writes an auditable history. Agents
cannot mutate the record directly.

BLUEVERSE does not issue governmental beach closures, emergency orders
or professional marine-navigation decisions.

## Tool, data and failure controls

Use explicit allowlists, typed inputs and outputs, least-privilege access,
timeouts, bounded retries and auditable execution summaries. Untrusted
content cannot change system instructions, grant tools, reveal secrets,
bypass permission checks or approval, or cause an unauthorized mutation.
Persist only necessary structured state; never hidden reasoning, passwords,
tokens or API keys.

Malformed output, tool/provider timeout, invalid response, stale or missing
marine data, unavailable biodiversity inference, rejected approval and
retry exhaustion require recorded outcomes. If recovery is impossible,
record SAFE_FAILURE and perform no unsafe side effect.

See [v1 workflows](../v1/workflows.md),
[tool controls](tools.md) and the
[security rules](../../.agents/rules/security.md).
