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
| `.agents/**`, `AGENTS.md` | `python .agents/scripts/validate_agent_resources.py`; `git diff --check` | skill metadata, links, routing, provenance, overlays, evaluations and file integrity |

Repository helpers:

- Windows foundation: `pwsh -File scripts/Windows/powershell/verify-foundation.ps1`
- Unix foundation: `bash scripts/Unix/bash/verify-foundation.sh`
- Web lockfile: `scripts/Windows/powershell/sync-web-lockfile.ps1` or
  `scripts/Unix/bash/sync-web-lockfile.sh`
- Agent resources: `python .agents/scripts/validate_agent_resources.py`

The agent-resource validator is dependency-free and is the repository's
required gate. It validates direct skills, the external-skill registry,
repository overlays, evaluation fixtures, relative links and generated-path
exclusions. The optional upstream skill validator may be run in an environment
with its YAML dependency, but it is not a CI prerequisite.

The current foundation verifier intentionally expects backend project files
that may not yet exist. If it fails for that known gap, report the exact
missing paths; do not create substitute projects or call the check successful.
Do not claim Docker/Compose health without Docker, credentials and actual
services being available.
