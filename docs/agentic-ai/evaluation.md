# Agentic AI Evaluation

The final evaluation must demonstrate a complete workflow rather than a generic chatbot.

The acceptance workflow should provide evidence for:

- planning/delegation
- agent/tool selection
- structured outputs
- persisted state
- deterministic validation
- business-rule compliance
- approval enforcement
- prompt-injection resistance
- failure recovery
- safe failure

Each evaluation case passes only when all of its assertions pass. Matching one
field, status, tool result or structured-output property is not sufficient if
the case also requires authorization, deterministic validation, persisted
state, approval, audit information, recovery behavior or side-effect limits.
Evaluation metrics must come from the complete test/evaluation result rather
than a superficial status check.

LLM-as-judge may be supplementary but must not be the sole evaluator.
