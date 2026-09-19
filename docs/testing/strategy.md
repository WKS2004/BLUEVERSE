# Testing Strategy

Testing will cover the current foundation and the later business workflows:

- ASP.NET Core unit tests
- API/integration tests
- authorization tests
- validation/business-rule tests
- React component/integration tests
- Flutter tests and static analysis
- database/migration verification
- cross-platform API integration
- Agentic AI workflow evaluation
- deployment smoke tests

## Current evidence

- React: lint and production build scripts are configured; no component test
  runner is checked in yet.
- Flutter: the generated counter widget test and static-analysis workflow are
  present.
- ASP.NET/API: `services/api` is checked in and builds, but no API test project
  is checked in yet. The Auth service and its tests remain pending.
- Docker: web/backend image and Compose health workflows are configured. The
  API image can be built, while the complete backend/Compose workflow remains
  blocked by the missing Auth service.

Critical business rules should have deterministic tests.

Agentic AI evaluation must include the complete assessed workflow and should not depend solely on an LLM judge.
