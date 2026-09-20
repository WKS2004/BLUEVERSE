## Summary

Describe the change and why it is needed.

## Related issue

Closes #

## Branch flow

- Source branch: `features/<feature-name>` (or explain the exception)
- Target branch: `dev` for integration, or `main` for an approved release
- [ ] This PR targets the intended integration branch.

## Scope

- [ ] v0 foundation
- [ ] Authentication/authorization
- [ ] Backend/API
- [ ] Database
- [ ] React
- [ ] Flutter
- [ ] Agentic AI
- [ ] Infrastructure/deployment
- [ ] Documentation

## Validation

- Test/build commands and results:

- [ ] Tests added/updated where applicable
- [ ] `scripts/validation/validate_ui_integrations.py` passed for any UI,
  API, service, gateway or contract change
- [ ] React and Flutter routes are registered under the correct shared
  workflow ID
- [ ] Every client API request uses a registered public `/api/...` endpoint
- [ ] No client-to-client, internal-service, Auth, Agentic AI or PostgreSQL
  connection was introduced
- [ ] Manual verification completed
- [ ] No secrets committed
- [ ] Documentation updated
- [ ] ADR added/updated if architecture changed

## Data and security impact

- Database schema or migration impact: None / Describe
- API contract impact: None / Describe
- Authorization or permission impact: None / Describe
- Secrets, credentials, or sensitive data introduced: No

## Screenshots or API examples

Add screenshots, request/response examples, or write `Not applicable`.

## Cross-platform impact

Explain whether the change affects React, Flutter, API, Auth, database or Agentic AI integration.

For any UI impact, list the workflow ID, React route, Flutter route, public API
endpoint references and the relevant API/service test evidence. Write
`Not applicable` only when no UI or integration boundary changed.
