# Agentic AI evaluation — v1 target

**Implementation status:** the golden workflow and agent suites are not yet
implemented. Evaluation evidence must come from executed tests and
demonstrations, not from this document.

## Complete golden case

The [Coastal Activity Operational Assessment](../v1/workflows.md) is the
assessed case. Verify the objective, structured plan, correct delegation,
all four distinct agent roles, authorized allowlisted tools, validated tool
inputs, structured outputs, durable state, deterministic safety/business
checks, a pending high-impact proposal, an authorized human decision and an
auditable final outcome. Both clients must expose each participating role's
permitted actions even though the demonstration starts in Flutter and reviews
in React.

## Required negative and recovery cases

Use deterministic fixtures for malformed or incomplete model output,
unavailable or unauthorized tools, invalid tool input, prompt injection and
indirect instructions, missing/stale marine data, unavailable biodiversity
inference, timeout, bounded retry and exhaustion, approval rejection,
request revision, duplicate or unauthorized approval and safe failure.
Check both the reported result and the absence of forbidden side effects.

For every case, assert the complete observable behavior: output schema,
workflow state, permissions, validation, approval, audit history, errors,
retries and permitted persistence. A matching status or one plausible model
sentence is not enough. LLM-as-judge may supplement deterministic assertions
and human review but cannot be the only evaluator. Live-model runs and
controlled-fixture runs should be identified separately.

## Release gate to define and measure

Before claiming the Agentic AI workflow ready, the team must freeze the
golden-case fixture, required negative/recovery fixtures and an evaluation
report format. Every mandatory deterministic case must pass with its complete
schema, state, authorization, audit and side-effect assertions. Any
unauthorized tool call, approval bypass, protected mutation after rejection
or `SAFE_FAILURE`, fabricated required evidence, or missing agent role is a
release blocker regardless of a plausible model response. Record observed
tool/step timings and the team's chosen latency/reliability targets alongside
actual measurements; do not invent performance results or relax a failing
safety case to meet a percentage. Live-model behavior and evaluator-run
evidence remain separate from controlled fixture results.

Use the [test strategy](../testing/strategy.md),
[test matrix](../testing/test-matrix.md) and
[requirements section 40](../../PROJECT_REQUIREMENTS.md) for traceability.
