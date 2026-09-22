## Summary

Describe what changed, why it is needed and the user or operational outcome.

## Related issue

Closes #<number> or `Not applicable`

## Branch flow

- Source branch: `features/<feature-name>`, `agentic-ai/<name>`,
  `maintenance/<name>`, `bug-fixes/<name>`, or explain the approved exception.
- Target branch: `dev` for integration, or `main` only for an approved release
  from `dev`.
- [ ] The source and target branches follow the repository branch policy.
- [ ] This PR targets the intended integration branch.

## Scope

- [ ] Repository or GitHub Actions automation
- [ ] Public API or backend service
- [ ] Authentication and authorization
- [ ] PostgreSQL or EF Core
- [ ] React web app
- [ ] Flutter mobile app
- [ ] Agentic AI
- [ ] Docker, gateway or infrastructure
- [ ] Documentation

## Validation evidence

List the exact commands, test IDs, workflow runs or manual checks performed.

- Commands/checks:
- Relevant workflow run(s):
- [ ] Passing and failing test cases were reviewed, including the final
      `FAILED TEST CASES` output when a suite ran.
- [ ] Tests were added or updated for the behavior where applicable.
- [ ] `scripts/validation/validate_ui_integrations.py` passed for UI, API,
      service, gateway or contract changes.
- [ ] `.agents/scripts/validate_endpoint_catalog.py` passed when routes,
      endpoints or endpoint documentation changed.
- [ ] `git diff --check` passed.

## Contract and architecture

- Shared workflow ID: `Not applicable` / `<workflow-id>`
- React route: `Not applicable` / `<route>`
- Flutter route: `Not applicable` / `<route>`
- Public API endpoint references: `Not applicable` / `<method> <path>`
- Owning service and gateway route: `Not applicable` / `<service>`
- [ ] `docs/contracts/ui-integration.json` is updated for every affected UI
      workflow.
- [ ] `docs/api/endpoint-catalog.json` and its generated Markdown are updated
      for every affected route or endpoint.
- [ ] No client-to-client, internal-service, Auth, Agentic AI, PostgreSQL or
      Docker-hostname connection was introduced.
- [ ] An ADR was added or updated for a material architectural decision.

## CI and Docker impact

- [ ] Workflow branch and `paths` filters include every necessary source,
      test, helper, Docker and documentation path.
- [ ] Required-check rules account for workflows that are intentionally not
      triggered when their path filter does not apply.
- [ ] Dockerfile/Compose/gateway changes were validated, including the relevant
      image build and stack-health checks.
- [ ] No workflow change exposes secrets to logs, forks or artifacts.

## Data and security impact

- Database schema or migration impact: `None` / describe
- API contract impact: `None` / describe
- Authorization or permission impact: `None` / describe
- Secrets, credentials or sensitive data introduced: `No` / describe

## Screenshots or API examples

Add sanitized screenshots or request/response examples, or write `Not applicable`.

## Cross-platform impact

Explain whether the change affects React, Flutter, the public API, Auth,
database, gateway or Agentic AI integration. Write `Not applicable` only when
there is no client or integration boundary change.
