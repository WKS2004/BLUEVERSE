# Local test runners

These small scripts run the existing test suite for one BLUEVERSE component.
They do not start Docker Compose or change the application database. Run them
from any directory; each script finds the repository root automatically.

## Run one component

Use PowerShell on Windows:

```powershell
.\scripts\Windows\Test-Web.ps1
.\scripts\Windows\Test-Mobile.ps1
.\scripts\Windows\Test-ExperienceBiodiversityBackend.ps1
.\scripts\Windows\Test-MarineSafetyBackend.ps1
.\scripts\Windows\Test-CoastalPlannerBackend.ps1
.\scripts\Windows\Test-CoastalOperationsBackend.ps1
.\scripts\Windows\Test-ExperienceBiodiversityAgent.ps1
.\scripts\Windows\Test-MarineConditionsAgent.ps1
.\scripts\Windows\Test-PlanningCoordinationAgent.ps1
.\scripts\Windows\Test-SafetyOperationsAgent.ps1
```

Use Bash on Linux or macOS:

```bash
bash scripts/Unix/bash/test-web.sh
bash scripts/Unix/bash/test-mobile.sh
bash scripts/Unix/bash/test-experience-biodiversity-backend.sh
bash scripts/Unix/bash/test-marine-safety-backend.sh
bash scripts/Unix/bash/test-coastal-planner-backend.sh
bash scripts/Unix/bash/test-coastal-operations-backend.sh
bash scripts/Unix/bash/test-experience-biodiversity-agent.sh
bash scripts/Unix/bash/test-marine-conditions-agent.sh
bash scripts/Unix/bash/test-planning-coordination-agent.sh
bash scripts/Unix/bash/test-safety-operations-agent.sh
```

Each command runs only the listed component's full package test suite. A
non-zero test or tool exit code is returned by the script so it can also be
used in CI or a terminal session.

## Prerequisites and limits

- React Web: install Node.js 24 and run `npm ci` in `apps/web` once. The runner
  uses the package's complete `test:ci` command.
- Flutter Mobile: install Flutter and resolve packages with `flutter pub get`.
  Both runners use Flutter directly and run unit/widget tests. These are not
  connected Android or iOS device tests.
- Backend services: install the .NET SDK selected by `global.json`. The four
  runners target only their owning service's test project. PostgreSQL-backed
  cases remain opt-in and need their documented dedicated test connection;
  skipped opt-in cases are not reported as passing provider tests.
- Agentic AI: install Python 3.12 or newer and the test dependencies listed by
  the component. These deterministic suites do not require Gemini keys and do
  not make live model calls.

The scripts do not run `docker compose down`, remove volumes, seed data or
modify secrets. Use the dedicated Compose instructions for container health
and cross-service integration checks.
