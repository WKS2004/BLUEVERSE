# Validation rules and command matrix

Choose checks from the changed paths; do not run unrelated expensive suites.
Commands are examples for the current repository and must be verified against
the current files before use.

| Changed area | Minimum useful checks | Evidence to report |
|---|---|---|
| `.agents`, docs, root config | `git diff --check`; link/path review; foundation verifier if applicable | files, structural result, known gaps |
| `apps/web` | `npm install && npm run lint && npm run build` in `apps/web`; `npm run test:ci` when configured | lint/build/test result and test IDs |
| `apps/mobile` | `flutter pub get && flutter analyze`; `flutter test test` and `flutter test integration_test` when present | analysis/test result and platform limits |
| `services/**` | matching service-local `dotnet restore`, build and test; API contract/authorization checks | service, migration, auth and test evidence |
| `services/ai/**`, `services/ai-agents/**`, `services/agents/**` | deterministic local `tests/` suite, safety/evaluation checks and metrics reporter | safety cases, failed-case list, fixture mode |
| `compose.yaml`, `infrastructure/docker/**` | foundation verifier; `docker compose --env-file .env config --quiet`; affected image/health checks when available | image, network, route, health and cleanup evidence |
| `.github/**` | YAML/script syntax review; affected workflow logic and local helper checks | trigger/path/discovery/metric behavior |
| `docs/contracts/**`, `scripts/validation/**`, UI route/API integration | `python scripts/validation/validate_ui_integrations.py`; `python -m unittest discover -s scripts/validation/tests -p "test_*.py"` | shared workflow routes, public API references, direct-target rejection and validator results |
| Any frontend route, client API target, backend/internal/test/AI endpoint, health/OpenAPI route or gateway mapping addition, update, rename, move or removal; `docs/api/endpoint-catalog.*` changes | **Required:** update `docs/api/endpoint-catalog.json`, run `python .agents/scripts/validate_endpoint_catalog.py --write-markdown`, then run `python .agents/scripts/validate_endpoint_catalog.py`; run the UI validator and its tests when a client workflow is affected | source/catalog parity, readable catalog freshness, public-boundary ownership, authorization, test-only separation and AI endpoint status |
| `.agents/**`, `AGENTS.md` | `python .agents/scripts/validate_agent_resources.py`; `git diff --check` | skill metadata, links, routing, provenance, overlays, evaluations and file integrity |

Repository helpers:

- Windows foundation: `pwsh -File scripts/Windows/powershell/verify-foundation.ps1`
- Unix foundation: `bash scripts/Unix/bash/verify-foundation.sh`
- Web lockfile: `scripts/Windows/powershell/sync-web-lockfile.ps1` or
  `scripts/Unix/bash/sync-web-lockfile.sh`
- Agent resources: `python .agents/scripts/validate_agent_resources.py`
- Endpoint catalog: `python .agents/scripts/validate_endpoint_catalog.py`

The agent-resource and endpoint-catalog validators are dependency-free and are
the repository's required documentation/configuration gates. They validate
skills, the external-skill registry, repository overlays, evaluation fixtures,
relative links, generated-path exclusions, route declarations, UI references,
client API literals and gateway/documentation mappings. Source checks cover
literal C# controller routes, literal `app.MapGet/Post/Put/Patch/Delete/Head/Options`
routes, Swagger mappings and C# test/AI routes. They compare source identity,
exposure, service ownership and declared authorization metadata. Grouped,
dynamic or unsupported route/authorization forms require validator support
before adoption; do not bypass a rejected form or edit guidance without user
authorization. Prose descriptions and runtime authorization still require
source review and application tests. The optional upstream skill validator may be run
in an environment with its YAML dependency, but it is not a CI prerequisite.

If a verifier reports a missing project or service, confirm the current
source tree and report the exact condition. Do not create substitute projects
or call a failed check successful. Do not claim Docker/Compose health without
Docker, credentials and actual services being available.
