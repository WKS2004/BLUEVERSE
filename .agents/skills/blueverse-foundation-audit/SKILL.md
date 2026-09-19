---
name: blueverse-foundation-audit
description: Audit BLUEVERSE repository readiness, architecture alignment, agent resources, documentation drift, CI coverage, or missing foundation components. Use for repository-wide reviews and gap analyses, not ordinary feature implementation.
---

# BLUEVERSE foundation audit

Read the root `AGENTS.md`, `.agents/repository-map.md`,
`.agents/rules/change-safety.md`, and `.agents/rules/validation.md`.

1. Inspect `git status --short` and inventory actual files with `rg --files`.
2. Compare implementation against `PROJECT_REQUIREMENTS.md`, architecture/ADR
   documents, CI workflows, tests, Docker configuration, and agent resources.
3. Distinguish implemented behavior, reserved paths, target architecture, and
   environment-blocked checks. Never count documentation or Docker stubs as a
   working service.
4. Report each finding with severity, evidence path, impact, and the smallest
   corrective action. Separate confirmed defects from risks and planned gaps.
5. If implementation is requested, route each change through
   `.agents/routing.md`, preserve existing tests, and validate the final diff.

Run `python .agents/scripts/validate_agent_resources.py` for agent-resource
audits and report any unavailable foundation, application, or Docker checks.
