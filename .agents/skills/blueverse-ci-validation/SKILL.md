---
name: blueverse-ci-validation
description: Create, change, or diagnose BLUEVERSE GitHub Actions workflows, path filters, test discovery, metrics, artifacts, Docker CI, branch policy, or repository validation. Do not use for application changes with no CI impact.
---

# BLUEVERSE CI validation

Read the root `AGENTS.md`, `.agents/routing.md`, and the testing, Docker,
documentation, Git and validation rules. Inspect an affected workflow and its
called scripts as one unit.

- Keep triggers and path filters aligned with source, tests, fixtures, scripts,
  Docker configuration and behavior-changing documentation.
- Treat `docs/contracts/ui-integration.json` and
  `scripts/validation/validate_ui_integrations.py` as a cross-client CI
  boundary. UI workflows must run the validator for React and Flutter changes;
  service, API/architecture documentation, Compose/Docker gateway or workflow
  changes must also trigger the shared UI contract workflow.
- Treat `docs/api/endpoint-catalog.json`, its generated Markdown view and
  `.agents/scripts/validate_endpoint_catalog.py` as the complete route/API CI
  boundary. Route, service, gateway, client and endpoint-documentation changes
  **must** update the JSON and regenerate Markdown in the same change; the
  endpoint validator must run and pass before completion. Keep its path filters
  synchronized for every route source and documentation path.
- Prefer repository helpers and reproducible dependencies over duplicated YAML.
  Never expose secrets to forks, logs, artifacts or summaries.
- Preserve complete runner outcomes, result artifacts and a final
  `FAILED TEST CASES` list. Backend and Agentic AI workflows retain aggregate
  and per-service evidence and attempt every discovered service.
- Preserve selected DHI authentication/build behavior and exact-commit Docker
  health coordination.
- Distinguish intentional zero-test foundation state from broken discovery;
  missing suites must fail once the corresponding implementation exists.
- When PostgreSQL integration tests are enabled, make Docker/database
  prerequisites and failure behavior explicit; never silently skip provider
  coverage or expose connection strings in logs and artifacts.
- For `.agents` changes, keep `repository-ci.yml` as the single CI gate and
  extend `validate_agent_resources.py` rather than adding a duplicate workflow.

Validate YAML/script syntax, trigger scope, called paths, failure propagation,
summary behavior and local helpers where the environment permits.
