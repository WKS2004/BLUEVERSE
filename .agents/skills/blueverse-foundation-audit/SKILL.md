---
name: blueverse-foundation-audit
description: Audit BLUEVERSE repository readiness, architecture alignment, agent resources, documentation drift, CI coverage, or missing foundation components. Use for repository-wide reviews and gap analyses, not ordinary feature implementation.
---

# BLUEVERSE foundation audit

Read the root `AGENTS.md`, `.agents/repository-map.md`,
`.agents/rules/change-safety.md` and `.agents/rules/validation.md`.

1. Inspect `git status --short` and inventory tracked/source files with
   targeted `rg --files` exclusions; do not use generated output as evidence.
2. Compare implementation against `PROJECT_REQUIREMENTS.md`, applicable
   release/component/workflow documents, architecture/ADRs, the UI integration
   registry, endpoint catalog, database docs, CI workflows, tests, Docker
   configuration and `.agents` registry, overlays and skills.
   For v1 readiness, explicitly check the G00 and G07 gates, single-branch
   member ownership, one private service per member, API/Auth integration-only
   constraints, provider ownership and React/Flutter parity. Confirm that
   member Agentic AI access seams are distinct from executable runtime work and
   that no v1 Agentic AI runtime starts before G07. When reviewing Git
   automation, distinguish agent-initiated commits from GitHub Actions and
   preserve the documented `dev-backup` rescue/synchronization behavior.
3. Distinguish implemented behavior, reserved paths, ignored build output,
   target architecture and environment-blocked checks. Documentation or Docker
   stubs never count as a working service.
4. Report each finding with severity, evidence path, impact and the smallest
   corrective action. Separate confirmed defects from risks and planned gaps.
5. If implementation is requested, route each change through `.agents/routing.md`,
   preserve existing tests, update the AI usage log after validation and review
   the final diff.

For any React/Flutter finding, verify equal authorized roles, business actions
and outcomes across both clients, the shared workflow ID, both client routes,
public API references, gateway boundary and the
`scripts/validation/validate_ui_integrations.py` result. Distinguish
implemented workflows from documented targets. Also
compare the client scope and user-facing direction with
`docs/project/ui-experience-principles.md`; report any stakeholder priority,
design emphasis, role-based frontend assignment or technical-dashboard
default as documentation drift.

For any route or endpoint finding, compare backend controller and mapped
documentation routes, gateway configuration, client API literals and
`docs/api/endpoint-catalog.md` and its JSON source. Run
`.agents/scripts/validate_endpoint_catalog.py` and report every source/catalog
drift as a concrete finding. Confirm that every route addition, update, rename,
move or removal also has a same-change catalog update and regenerated Markdown;
missing synchronization is a release-blocking documentation defect.

For any database finding, compare the actual EF Core/Npgsql configuration,
checked-in migrations, Compose PostgreSQL service, provider-specific tests and
`docs/database/` against `.agents/rules/data-access.md`. Distinguish the
isolated-provider test scope from real PostgreSQL evidence, and do not treat
a reserved schema as implemented.

Run `python .agents/scripts/validate_agent_resources.py` for agent-resource
audits and report unavailable foundation, application, Docker or platform
checks exactly.
