---
name: blueverse-client-contract
description: Implement or review BLUEVERSE React and Flutter work that consumes the public API, especially shared contracts, authorization behavior, cross-platform workflows, and client tests. Do not use for isolated styling with no contract impact.
---

# BLUEVERSE client contract

Read the root `AGENTS.md`, `.agents/routing.md`, and the architecture, security,
testing and validation rules. Inspect the public contract and existing client
code before changing either client.

- React 19 + Vite and Flutter call only the public ASP.NET Core API/gateway.
  Never add direct Auth, Agentic AI, PostgreSQL or internal-service access.
- Keep server rules authoritative. Permission-aware UI improves the experience
  but never replaces backend authorization or the role-to-permission model.
- Preserve one API contract and authorization model across clients. Assess both
  clients for shared workflows and document intentional platform differences.
- Handle loading, success, empty, validation, denied, dependency-failure,
  timeout/retry, cancellation and malformed-response states as applicable.
- `vercel-react-best-practices` is limited to browser/Vite-compatible guidance;
  ignore Next.js/server-only advice unless explicitly adopted.
- Official `flutter-*` skills are supplementary and must not bypass this API
  boundary or create a replacement sample application.
- Add deterministic React component/request/accessibility tests and Flutter
  unit/widget/API-boundary tests under the authoritative `test/` paths with
  stable IDs and CI discovery.

Validate the affected client locally, then verify contract, permission and
cross-platform behavior. State unavailable platform tooling explicitly.
