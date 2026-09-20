---
name: blueverse-foundation-audit
description: Audit BLUEVERSE repository readiness, architecture alignment, agent resources, documentation drift, CI coverage, or missing foundation components. Use for repository-wide reviews and gap analyses, not ordinary feature implementation.
---

# BLUEVERSE foundation audit

Read the root `AGENTS.md`, `.agents/repository-map.md`,
`.agents/rules/change-safety.md` and `.agents/rules/validation.md`.

1. Inspect `git status --short` and inventory tracked/source files with
   targeted `rg --files` exclusions; do not use generated output as evidence.
2. Compare implementation against `PROJECT_REQUIREMENTS.md`, architecture/ADR
   documents, the UI integration registry, CI workflows, tests, Docker
   configuration and `.agents` registry, overlays and skills.
3. Distinguish implemented behavior, reserved paths, ignored build output,
   target architecture and environment-blocked checks. Documentation or Docker
   stubs never count as a working service.
4. Report each finding with severity, evidence path, impact and the smallest
   corrective action. Separate confirmed defects from risks and planned gaps.
5. If implementation is requested, route each change through `.agents/routing.md`,
   preserve existing tests, update the AI usage log after validation and review
   the final diff.

For any React/Flutter finding, verify the shared workflow ID, frontend routes,
public API references, gateway boundary and the
`scripts/validation/validate_ui_integrations.py` result. Distinguish the
current starter/zero-endpoint state from a missing or broken integration.

Run `python .agents/scripts/validate_agent_resources.py` for agent-resource
audits and report unavailable foundation, application, Docker or platform
checks exactly.
