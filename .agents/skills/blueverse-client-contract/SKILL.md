---
name: blueverse-client-contract
description: Implement or review BLUEVERSE React and Flutter work that consumes the public API, especially shared contracts, authorization behavior, cross-platform workflows, and client tests. Do not use for isolated styling with no contract impact.
---

# BLUEVERSE client contract

Read the root `AGENTS.md`, `.agents/routing.md`, and the architecture,
security, testing, and validation rules. Inspect the public API contract and
existing client code before changing either client.

- React and Flutter call only the public ASP.NET Core API/gateway. Never add
  direct Auth, Agentic AI, or PostgreSQL access.
- Keep server rules authoritative. Client permission-aware UI improves the
  experience but never replaces backend authorization.
- Preserve one contract and authorization model across clients. When a workflow
  is shared, assess both clients and document any intentional platform gap.
- Handle loading, success, empty, validation, denied, dependency-failure,
  timeout/retry, cancellation, and malformed-response states as applicable.
- Add deterministic React component/request/accessibility tests and Flutter
  unit/widget/API-boundary tests under the authoritative `test/` paths with
  stable IDs and CI discovery.

Validate the affected client locally, then verify contract, permission, and
cross-platform behavior. State unavailable platform tooling explicitly.
