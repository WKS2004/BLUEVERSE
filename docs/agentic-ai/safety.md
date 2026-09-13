# Agentic AI Safety

## Core rule

AI output is untrusted.

## High-impact pattern

```text
Recommendation
      ↓
Deterministic validation
      ↓
Authorization
      ↓
Human approval
      ↓
Execution
```

The system must not allow a model to bypass authorization or deterministic business rules.

Tool calls must have explicit boundaries and validation.

Prompt injection must not be allowed to redefine system permissions, tool access or approval requirements.
