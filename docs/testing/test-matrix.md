# Test Matrix

The implementation plan and test-directory convention are documented in
[`implementation-plan.md`](implementation-plan.md). The entries below describe
the minimum evidence expected as each component is introduced.

| Area | Minimum evidence |
|---|---|
| API | `services/api/tests` covers current foundation behavior with stable cases `API-HEALTH-*`, `API-CONTRACT-*`, `API-CORS-*`, `API-EDGE-*`, `API-ERROR-*`, `API-PROXY-*` and `API-AUTH-001`: unit/HTTP health, non-versioned routing, OpenAPI/Swagger, CORS allow/deny/preflight, forwarded headers, safe gateway errors, JWT allow/deny and YARP forwarding/failure isolation. |
| Authentication | `services/auth/tests` covers registration/login, server-issued installations, cookie transport, rotating refresh tokens and replay revocation (`AUTH-SESSION-REFRESH-001`, `AUTH-SESSION-COOKIE-001`), one/30-day expiry (`AUTH-SESSION-EXPIRY-001`), per-session revocation (`AUTH-SESSION-REVOKE-001`), active-session archival and refresh-token relinking (`AUTH-SESSION-ARCHIVE-001`, `AUTH-SESSION-ARCHIVE-002`), five-session account eviction (`AUTH-SESSION-CAP-001`), five-account device capacity, protected endpoints, account/device/everywhere logout, profile-only updates, role/permission catalog reads, strict assignment validation, system-role escalation, token revocation, password hashing and signing-key configuration. |
| Authorization | Permission allow/deny and system-role escalation tests in `services/auth/tests` |
| PostgreSQL | Checked-in Auth EF Core model/migrations, including device installations and refresh-token rotation; `AUTH-POSTGRES-SESSION-001` exercises real-provider refresh replay and concurrent capacity decisions when explicitly enabled, while local live migration and gateway smoke checks provide deployment evidence |
| React | `npm.cmd run build` and `npm.cmd run lint` validate the public cookie-based Auth surface; component/request-boundary tests remain the next product-test increment |
| Flutter | `flutter analyze` and `flutter test` validate the secure-storage/API boundary and existing widget surface; device workflows remain under `apps/mobile/integration_test` |
| Backend services | Matching `services/<service-name>/tests` project for every service under `services/` |
| UI integration contract | Dependency-free registry/validator checks that every shared workflow has the relevant React and Flutter routes and that client API literals resolve to declared public `/api/...` endpoints |
| Integration | React/Flutter against the same public API, gateway routing and cross-platform workflow; no client-to-client or internal-service calls |
| Agentic AI | Service-specific contract, safety, evaluation and complete acceptance workflow under each AI service’s `tests/` directory, normally `services/ai/<agent-service>/tests` |
| Security | Unauthorized/invalid-input/prompt-injection cases with complete assertions: expected HTTP outcomes across relevant `1xx`–`5xx` statuses plus body/schema, authorization, state and side-effect validation |
| CI | Passing source, client, backend, AI and integration workflows; backend test discovery fails when a service source project has no matching local test project |
| Deployment | Health + smoke-test evidence |

Every row requires scenario-based coverage, not a single happy-path case:
nominal passing behavior, invalid/rejected behavior, relevant boundaries,
extreme values, malformed or missing input, and applicable concurrency,
dependency, authorization, retry and platform variations. New behavior and its
tests are delivered in the same change. Tests remain in their owning
framework/service default directories; the repository must not grow a
centralized root `test/` tree for these cases.
