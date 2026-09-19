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
- ASP.NET/API: no service source or tests are checked in yet.
- Docker: web/backend image and Compose health workflows are configured, but
  the backend workflows remain blocked by the missing ASP.NET service projects.

Critical business rules should have deterministic tests.

Agentic AI evaluation must include the complete assessed workflow and should not depend solely on an LLM judge.
