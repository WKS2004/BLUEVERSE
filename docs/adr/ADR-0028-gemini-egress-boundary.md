# ADR-0028: Gemini model egress boundary

Date: 2026-10-13. Status: Accepted for the local Docker network boundary;
G07 workflow acceptance remains separate.

## Context

The Safety Operations proposal package uses Google's Gemini API, while
BLUEVERSE agent containers must remain private and must not have unrestricted
outbound access. Attaching the agent to a regular outbound bridge would give
the model-facing code a general network route. Publishing a proxy port would
also create an unnecessary host entry point. The agent must not expose a
direct client endpoint, and provider credentials must not be available to a
network proxy.

## Decision

The optional `agentic-ai` Compose profile uses two dedicated Docker networks:

- `blueverse_gemini_internal` is marked `internal: true` and contains only the
  Safety Operations agent container and `gemini-egress-proxy`.
- `blueverse_agent_egress` is a regular bridge and contains only the proxy.

The Safety Operations agent has no direct outbound route. Its Python transport
uses the fixed `http://gemini-egress-proxy:3128` address, pins the provider URL
to `https://generativelanguage.googleapis.com/v1/interactions`, rejects
redirects and refuses alternate-proxy configuration. It ignores environment
`NO_PROXY` bypass settings. The proxy accepts only a syntactically valid
CONNECT request for `generativelanguage.googleapis.com:443`, resolves that
fixed host, rejects non-global addresses and connects to the selected numeric
address without a second DNS lookup. It bounds headers, concurrency, tunnel
bytes and tunnel lifetime, and does not log request or tunnel contents.

CONNECT carries a TLS tunnel; TLS validation and the Gemini API key remain at
the agent-to-Google connection. Only the agent container receives the API key.
Neither the agent nor proxy publishes a host port. The three other agent
containers do not join either Gemini network. This infrastructure path does
not connect the pre-G07 Coastal Operations dispatch seam, add an agent route or
start provider requests automatically. The existing data-handling approval
gate remains required before provider calls.

## Consequences

The optional Safety Operations container can reach the Gemini API through one
auditable, host-specific egress path without receiving general direct egress.
The proxy is a separate standard-library Python image using the selected DHI
base image, runs without root/capabilities, and has read-only storage, bounded
resources and a private listener. Updating the provider hostname or endpoint
requires synchronized code, tests and architecture review. Docker host and
network administrators remain trusted to control network membership and
container configuration. The route does not mean the G07 workflow or its
Coastal Operations dispatch is complete.

See [network topology](../architecture/networking.md) and the
[local deployment guide](../deployment/local.md).
