# Planning & Coordination Agent

This private service builds and validates specialist dispatch plans for Coastal
Planner workflows. Service dispatch and environment-configured Gemini requests
are disabled by default.

## Configuration

Gemini is the default provider. Runtime requests require all of the following:

| Variable | Purpose | Default |
| --- | --- | --- |
| `PLANNING_COORDINATION_GEMINI_ENABLED` | Opt in to environment-configured Gemini requests | `false` |
| `PLANNING_COORDINATION_GEMINI_API_KEY` | This agent's Gemini API key | unset |
| `PLANNING_COORDINATION_GEMINI_MODEL` | Gemini model name for this agent | unset |
| `PLANNING_COORDINATION_MODEL_PROVIDER` | Provider selector: `gemini` or `openai-compatible` | `gemini` |

Gemini requests use Google's HTTPS v1beta `generateContent` endpoint. The base
URL is pinned in the client; environment values cannot redirect requests to a
different host. The key is sent in the `x-goog-api-key` header.

The optional OpenAI-compatible adapter also uses agent-scoped settings:
`PLANNING_COORDINATION_OPENAI_COMPATIBLE_BASE_URL`,
`PLANNING_COORDINATION_OPENAI_COMPATIBLE_MODEL`, and
`PLANNING_COORDINATION_OPENAI_COMPATIBLE_API_KEY`. It does not read another
agent's provider key or model settings.

The HTTP dispatch route remains disabled unless `AGENTIC_ENABLED` is set to
`true`. Use approved secret management for API keys; never commit key values.

## Run locally

From the repository root in PowerShell:

```powershell
$env:PYTHONPATH = "services/agents/planning-coordination"
python -m planning_coordination
```

## Run tests

From the repository root:

```powershell
python -m unittest discover -s services/agents/planning-coordination/tests -v
```
