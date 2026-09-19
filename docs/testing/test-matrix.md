# Test Matrix

| Area | Minimum evidence |
|---|---|
| API | Build is available; add automated unit/integration tests for `services/api` |
| Authentication | Login/token/protected endpoint tests once `services/auth` exists |
| Authorization | Permission allow/deny tests |
| PostgreSQL | Migration/schema/integration verification |
| React | Component/workflow tests |
| Flutter | Analyze + widget/workflow tests |
| Integration | React/Flutter against same API |
| Agentic AI | Complete acceptance workflow |
| Security | Unauthorized/invalid-input/prompt-injection cases |
| CI | Passing source workflows, web Docker build, backend Docker build and Compose health workflow evidence; full backend/Compose runs remain blocked by the missing Auth service |
| Deployment | Health + smoke-test evidence |
