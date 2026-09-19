# Test Matrix

The implementation plan and test-directory convention are documented in
[`implementation-plan.md`](implementation-plan.md). The entries below describe
the minimum evidence expected as each component is introduced.

| Area | Minimum evidence |
|---|---|
| API | Unit, HTTP integration, validation, authorization, persistence, migration, security and health tests once `services/api` exists |
| Authentication | Login/token/protected endpoint, password-hashing, claims, expiry and permission tests once `services/auth` exists |
| Authorization | Permission allow/deny tests |
| PostgreSQL | Migration/schema/integration verification |
| React | Unit, component, request-boundary, accessibility and workflow tests under `test/app/web` |
| Flutter | Analyze, unit, widget and workflow tests under `test/app/mobile` |
| Backend services | Matching `test/services/<service-name>` suite for every service under `services/` |
| Integration | React/Flutter against the same public API, gateway routing and cross-platform workflow |
| Agentic AI | Service-specific contract, safety, evaluation and complete acceptance workflow under `test/ai` |
| Security | Unauthorized/invalid-input/prompt-injection cases with complete assertions: expected HTTP outcomes across relevant `1xx`–`5xx` statuses plus body/schema, authorization, state and side-effect validation |
| CI | Passing source, client, backend, AI and integration workflows; current backend/Compose runs remain blocked by missing services |
| Deployment | Health + smoke-test evidence |

Every row requires scenario-based coverage, not a single happy-path case:
nominal passing behavior, invalid/rejected behavior, relevant boundaries,
extreme values, malformed or missing input, and applicable concurrency,
dependency, authorization, retry and platform variations. New behavior and its
tests are delivered in the same change.
