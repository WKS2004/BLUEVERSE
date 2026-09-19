# Validation rules and command matrix

Choose checks from the changed paths; do not run unrelated expensive suites.
Commands are examples for the current repository and must be verified against
the current files before use.

| Changed area | Minimum useful checks | Evidence to report |
|---|---|---|
| `.agents`, docs, root config | `git diff --check`; link/path review; foundation verifier if applicable | files, structural result, known gaps |
| `apps/web` or `test/app/web` | `npm install && npm run lint && npm run build` in `apps/web`; `npm run test:ci` when configured | lint/build/test result and test IDs |
| `apps/mobile` or `test/app/mobile` | `flutter pub get && flutter analyze`; `flutter test` or centralized runner | analysis/test result and platform limits |
| `services/**`, `test/services/**` | matching `dotnet restore`, build and test; API contract/authorization checks | service, migration, auth and test evidence |
| `test/ai/**` or Agentic AI code | deterministic suite, safety/evaluation checks and metrics reporter | safety cases, failed-case list, fixture mode |
| `compose.yaml`, `infrastructure/docker/**` | foundation verifier; `docker compose --env-file .env config --quiet`; affected image/health checks when available | image, network, route, health and cleanup evidence |
| `.github/**` | YAML/script syntax review; affected workflow logic and local helper checks | trigger/path/discovery/metric behavior |
| `.agents/**`, `AGENTS.md` | `python .agents/scripts/validate_agent_resources.py`; `git diff --check` | skill metadata, links, routing and file integrity |

Repository helpers:

- Windows foundation: `pwsh -File scripts/Windows/powershell/verify-foundation.ps1`
- Unix foundation: `bash scripts/Unix/bash/verify-foundation.sh`
- Web lockfile: `scripts/Windows/powershell/sync-web-lockfile.ps1` or
  `scripts/Unix/bash/sync-web-lockfile.sh`
- Agent resources: `python .agents/scripts/validate_agent_resources.py`

The current foundation verifier intentionally expects backend project files
that may not yet exist. If it fails for that known gap, report the exact
missing paths; do not create substitute projects or call the check successful.
Do not claim Docker/Compose health without Docker, credentials and actual
services being available.
