# Test Matrix

The implementation plan and test-directory convention are documented in
[`implementation-plan.md`](implementation-plan.md). The entries below describe
the minimum evidence expected as each component is introduced.

| Area | Minimum evidence |
|---|---|
| API | `services/api/tests` covers current foundation behavior with stable cases `API-HEALTH-*`, `API-CONTRACT-*`, `API-CORS-*`, `API-EDGE-*`, `API-ERROR-*`, `API-PROXY-*` and `API-AUTH-*`: unit/HTTP health, non-versioned routing, public/Auth OpenAPI routing, CORS allow/deny/preflight, forwarded headers, safe gateway errors, JWT issuer/audience/lifetime/algorithm/cookie allow/deny and YARP forwarding/failure isolation. The default suite currently passes 21 cases. |
| Authentication | `services/auth/tests` covers 67 default cases for registration/login, validation and malformed-input problem details, server-issued installations and device proof, native/cookie transport and cookie cleanup, rotating refresh tokens and replay/expiry failures, one/30-day expiry, per-session revocation and cross-account isolation, active-session archival and refresh-token relinking, five-session account eviction, five-account device capacity, protected endpoints and safe errors, account/device/everywhere logout, profile-only updates, complete user/role/permission administration, administrative mutation authorization, strict assignment validation, system-role escalation, bootstrap seeding, token revocation, password hashing, JWT/configuration boundaries and persistence-model constraints. |
| Authorization | Permission allow/deny, policy-provider behavior, catalog isolation, administrative mutation protection and system-role escalation tests in `services/auth/tests` |
| PostgreSQL | Checked-in Auth EF Core model/migrations, including device installations and refresh-token rotation; `AUTH-POSTGRES-SESSION-001` exercises real-provider refresh replay and concurrent capacity decisions when explicitly enabled, while local live migration and gateway smoke checks provide deployment evidence. Provider-independent tests may use the isolated test provider; they do not replace PostgreSQL evidence. |
| React | `npm.cmd run build`, `npm.cmd run lint` and the Node 24 native test runner validate the public cookie-based Auth surface; three `WEB-AUTH-*` request-boundary cases cover request construction, RFC 7807 error mapping and 204 logout handling. Future workflow tests cover clients, staff and administrators wherever the workflow applies; component/browser workflow tests remain the next increment |
| Flutter | `flutter analyze` and machine-mode `flutter test` validate five visible package-level gateway/configuration, build-define and widget cases plus the secure-storage/API boundary; future workflow tests cover the same participating roles with mobile-appropriate interaction; no `apps/mobile/integration_test` directory is checked in yet |
| Backend services | Matching `services/<service-name>/tests` project for every service under `services/` |
| UI integration contract | Dependency-free registry/validator checks that every product workflow has both React and Flutter routes and that client API literals resolve to declared public `/api/...` endpoints; role coverage and platform parity are verified in workflow tests |
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
