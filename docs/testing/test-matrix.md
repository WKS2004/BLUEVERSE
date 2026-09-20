# Test Matrix

The implementation plan and test-directory convention are documented in
[`implementation-plan.md`](implementation-plan.md). The entries below describe
the minimum evidence expected as each component is introduced.

| Area | Minimum evidence |
|---|---|
| API | `services/api/tests` covers current foundation behavior with stable cases `API-HEALTH-*`, `API-CONTRACT-*`, `API-CORS-*`, `API-EDGE-*`, `API-ERROR-*` and `API-PROXY-*`: unit/HTTP health, non-versioned routing, OpenAPI/Swagger, CORS allow/deny/preflight, forwarded headers, safe gateway errors and YARP forwarding/failure isolation. JWT/permission, DTO validation, persistence, migration, audit and domain-workflow tests remain blocked until those API contracts are implemented. |
| Authentication | Login/token/protected endpoint, password-hashing, claims, expiry and permission tests once `services/auth` exists |
| Authorization | Permission allow/deny tests |
| PostgreSQL | Migration/schema/integration verification |
| React | Unit, component, request-boundary, accessibility and workflow tests colocated under `apps/web/src` (and optional `apps/web/e2e`) |
| Flutter | Analyze, unit and widget tests under `apps/mobile/test`; application/device workflows under `apps/mobile/integration_test` |
| Backend services | Matching `services/<service-name>/tests` project for every service under `services/` |
| UI integration contract | Dependency-free registry/validator checks that every shared workflow has the relevant React and Flutter routes and that client API literals resolve to declared public `/api/...` endpoints |
| Integration | React/Flutter against the same public API, gateway routing and cross-platform workflow; no client-to-client or internal-service calls |
| Agentic AI | Service-specific contract, safety, evaluation and complete acceptance workflow under each AI service’s `tests/` directory, normally `services/ai/<agent-service>/tests` |
| Security | Unauthorized/invalid-input/prompt-injection cases with complete assertions: expected HTTP outcomes across relevant `1xx`–`5xx` statuses plus body/schema, authorization, state and side-effect validation |
| CI | Passing source, client, backend, AI and integration workflows; current backend/Compose runs remain blocked by missing services |
| Deployment | Health + smoke-test evidence |

Every row requires scenario-based coverage, not a single happy-path case:
nominal passing behavior, invalid/rejected behavior, relevant boundaries,
extreme values, malformed or missing input, and applicable concurrency,
dependency, authorization, retry and platform variations. New behavior and its
tests are delivered in the same change. Tests remain in their owning
framework/service default directories; the repository must not grow a
centralized root `test/` tree for these cases.
