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
- Preserve the cross-platform product rule in CI evidence: a shared workflow
  must be traceable to both React Web and Flutter Mobile for its participating
  clients, staff or administrators. CI must not encode role-based frontend
  ownership; platform-specific interaction is not a missing-client waiver.
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
- Distinguish agent-initiated Git actions from repository-approved GitHub
  Actions. The prohibition on agents creating commits/pushes does not prohibit
  automated workflow commits. Preserve `dev-backup.yml` recovery of mistaken
  backup commits to `dev-backup-mistaken-commits/<actor>/<timestamp>` and its
  synchronization of `dev-backup` to `dev`, as well as the `.github`
  configuration-sync workflow. Do not remove or weaken these unless the user
  explicitly asks to change that automation.
- Distinguish an absent implementation from broken test discovery; an
  implemented service or workflow needs its owning suite and CI evidence.
- When PostgreSQL integration tests are enabled, make Docker/database
  prerequisites and failure behavior explicit; never silently skip provider
  coverage or expose connection strings in logs and artifacts.
- For `.agents` changes, keep `repository-ci.yml` as the single CI gate and
  extend `validate_agent_resources.py` rather than adding a duplicate workflow.

Validate YAML/script syntax, trigger scope, called paths, failure propagation,
summary behavior and local helpers where the environment permits.
