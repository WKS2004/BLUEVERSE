---
name: blueverse-ci-validation
description: Create, change, or diagnose BLUEVERSE GitHub Actions workflows, path filters, test discovery, metrics, artifacts, Docker CI, branch policy, or repository validation. Do not use for application changes with no CI impact.
---

# BLUEVERSE CI validation

Read the root `AGENTS.md`, `.agents/routing.md`, and the testing, Docker,
documentation, Git, and validation rules. Inspect the affected workflow and
its called scripts as one unit.

- Keep triggers and path filters aligned with source, tests, shared fixtures,
  scripts, Docker configuration, and documentation that changes behavior.
- Use reproducible dependencies and repository helpers instead of duplicating
  commands in YAML. Never expose secrets to forks, logs, artifacts, or summaries.
- Dedicated test workflows must use complete runner outcomes, preserve result
  artifacts, and end with a `FAILED TEST CASES` list. Backend and Agentic AI
  runs retain aggregate and per-service evidence and attempt every service.
- Preserve the selected DHI authentication/build behavior and exact-commit
  coordination for Docker stack health checks.
- Distinguish an intentional zero-test foundation state from broken discovery;
  once implementation exists, missing suites or undiscovered tests must fail.

Validate YAML/script syntax, trigger scope, called paths, failure propagation,
summary behavior, and local helper execution where the environment permits.
