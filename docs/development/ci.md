# CI Workflows

BLUEVERSE keeps repository, client, backend, Agentic AI, Docker and cross-client
contract checks as separate GitHub Actions workflows. Separate workflows keep
the check list readable on a commit or pull request while their scope logic
avoids running unrelated work on active development branches.

## Permissions and secrets

Most validation workflows use `contents: read`. The workflows that mutate
repository state are intentionally limited to the permissions they need:

- `branch-policy.yml` can delete a newly created invalid branch;
- `dev-backup.yml` can create the backup/rescue refs and update `dev-backup`;
- `github-config-sync.yml` can create sync branches and pull requests.

Configure the following repository secrets before expecting Docker checks to
pass:

| Secret | Value |
|---|---|
| `DHI_USERNAME` | Docker ID for a PAT, or the Docker organization name for an OAT. |
| `DHI_TOKEN` | A Docker personal or organization access token with read/pull access to the selected DHI images. |

Docker credentials are supplied only to the Docker jobs. Pull requests from
forks do not receive repository secrets, so those runs require a trusted
branch or another approved credential strategy before DHI images can be
pulled.

## Workflow inventory

| Workflow | Trigger/scope | Purpose |
|---|---|---|
| `repository-ci.yml` | Supported work branches; `.github/**`, `.agents/**`, `docs/**` or root-file changes | Checks foundation files, `.agents` resources and CI helper tests. |
| `ui-integration.yml` | Supported work branches; client, service, contract, gateway, Compose or workflow paths | Validates the UI registry, endpoint catalog and validator tests. |
| `backend-ci.yml` | Supported work branches; `services/**`, `global.json` or its workflow | Restores and builds discovered ASP.NET projects. |
| `web-ci.yml` | Supported work branches; web/contract paths or its workflows | Validates the UI contract, lints and builds React. |
| `mobile-ci.yml` | Supported work branches; mobile/contract paths or its workflows | Validates the UI contract and analyzes Flutter. |
| `web-tests.yml` | Supported work branches; web, contract, helper or test-workflow paths | Runs React tests and reports JUnit metrics. |
| `mobile-tests.yml` | Supported work branches; mobile, contract, helper or test-workflow paths | Runs Flutter unit/widget and integration machine tests. |
| `backend-tests.yml` | Supported work branches; service, catalog, helper or test-workflow paths | Runs every discovered backend test project with aggregate and per-service evidence. |
| `agentic-ai-tests.yml` | Supported work branches; Agentic AI, helper or AI-test workflow paths | Runs every discovered AI test suite with aggregate and per-service evidence. |
| `branch-policy.yml` | Branch creation | Enforces lowercase approved branch names and deletes invalid branches. |
| `dev-backup.yml` | Pushes to `dev` or `dev-backup` | Creates and synchronizes the exact `dev-backup` ref while preserving mistaken history. |
| `github-config-sync.yml` | `.github/**` pushes | Creates focused `.github`-only pull requests for other branches and queues auto-merge. |

The supported active work families are `features/**`, `agentic-ai/**`,
`claude/**`, `codex/**`, `antigravity/**`, `gemini/**`, `maintenance/**` and
`bug-fixes/**`. Automation branches such as `github-sync/**`,
`docker-workflow-changes/**` and `dev-backup-mistaken-commits/**` are excluded
from normal source/test workflow triggers.

## Branch and path-aware behavior

Every source, test and Docker image workflow combines a supported branch
filter with a workflow-specific `paths` filter. GitHub Actions therefore does
not start an unrelated workflow for a commit that cannot affect it. Each
workflow also retains a small post-checkout scope guard for manual dispatches,
initial branch pushes and a clear `Not affected` summary when the changed-file
range cannot be classified.

The filters include the workflow and shared helper files themselves so a CI
change revalidates the affected check. The UI contract workflow additionally
covers client, service, endpoint-catalog, gateway, Compose and validator paths.
The test workflows include `.github/scripts/**` so a metrics-parser change
reruns the relevant result-processing suite.

Manual dispatch runs the selected workflow regardless of the path filter. A
workflow that is required by branch protection must be configured carefully:
GitHub may leave a required check pending when its path filter excludes a
pull request. Use required checks that match the repository's branch/path
policy, or retain a lightweight always-triggered gate when a branch rule
requires a check on every pull request.

The SE3090 assignment separately requires a workflow that restores, builds
and runs backend tests on every push and pull request to `main`. The
current backend source/test workflows have path filters, so this trigger
requirement is not yet met for unrelated changes. Preserve the per-area
workflows if useful, but add an always-triggered main-branch backend gate
before the final submission; document its passing hosted run.

## UI and API integration contract

`docs/contracts/ui-integration.json` is the source of truth for shared client
workflows. It maps one workflow ID to its React route, Flutter route and public
`/api/...` endpoint references. `docs/api/endpoint-catalog.json` and its
generated Markdown view are the source of truth for the complete route/API
catalog.

`ui-integration.yml` runs the endpoint-catalog validator, the React/Flutter
validator and its dependency-free tests. The client workflows run the same UI
validator for their affected client paths. The checks reject:

- a frontend route absent from the UI registry;
- an unregistered literal `/api/...` request;
- dynamic or otherwise unverifiable network targets;
- client calls to Auth, Agentic AI, PostgreSQL, another internal service or a
  Docker hostname; and
- `/api/v1`-style path versioning.

For any route or gateway change, update the endpoint-catalog JSON and
regenerate its Markdown view in the same change. Update the UI-registry JSON
when a client workflow, frontend route or its public API references change:

```bash
python .agents/scripts/validate_endpoint_catalog.py --write-markdown
python .agents/scripts/validate_endpoint_catalog.py
python scripts/validation/validate_ui_integrations.py
```

## Test discovery, metrics and artifacts

Authoritative tests stay beside their owning implementation:

- React: `apps/web/src` and optional `apps/web/e2e`;
- Flutter: `apps/mobile/test` and `apps/mobile/integration_test`;
- backend: service-local test projects under `services/<service>`; and
- Agentic AI: each service-local `tests/` directory under
  `services/ai`, `services/ai-agents` or `services/agents`.

Each test workflow preserves the runner exit code, prints an aggregate metrics
row, appends a summary table, uploads console/result artifacts and prints a
final `FAILED TEST CASES` list. Backend and Agentic AI workflows also retain
per-service rows and continue through discovered suites before failing the
workflow. Missing backend tests for a service source project are treated as a
failure; an entirely empty foundation suite is reported as zero tests until
that implementation exists.

`.github/scripts/report_test_metrics.py` parses JUnit, TRX and Flutter machine
results. Flutter output is normalized across the nested current protocol, the
legacy top-level `test`/`testID` protocol and line-delimited JSON lines that
contain arrays of events. Empty or missing error fields are safe and do not
crash the final failure report. Use `--require-results` when a real test suite
must produce parseable cases.

The helper tests are run by `repository-ci.yml`:

```bash
python -m unittest discover -s .github/scripts/tests -p "test_*.py"
```

## Docker workflows

Docker checks remain separate from source/test checks:

| Workflow | Trigger/dependency | Purpose |
|---|---|---|
| `docker-web-build.yml` | Supported work branches; web/Docker/Compose paths | Synchronizes the React lockfile and builds the web image. |
| `docker-backend-build.yml` | Supported work branches; service/Docker/Compose paths | Builds the public API first, then each discovered ASP.NET backend service. |
| `docker-stack-health.yml` | Successful Docker build workflows for a PR targeting `main` only | Checks the complete Compose network, public health endpoints and API/Auth Swagger routes. |

The web and backend image workflows start only when their relevant application,
service, Docker infrastructure, lockfile, Compose, global SDK or workflow
paths change on a supported work branch. Backend image errors are collected
across discovered services so later services are still checked before the
workflow fails.

The Docker backend convention is:

```text
services/<service-name>/
infrastructure/docker/<service-name>/Dockerfile
```

The workflow identifies ASP.NET services from `Program.cs` and project
metadata, requires `services/api`, verifies each matching Dockerfile and
builds the images from the repository root. Non-ASP.NET service directories
are reported as skipped rather than being silently treated as backend images.

The stack-health workflow is intentionally heavy. It is not a direct push
workflow: it listens for completed image workflows, proceeds only when the
originating run was a pull request targeting `main`, determines which required
image workflows were triggered for that exact comparison range, waits for
those runs to succeed, and then checks out the exact commit. It validates
`compose.yaml` or `docker-compose.yml`, starts the stack on host port `8080`,
checks `/health`, `/api/health`, the Swagger UI and both API/Auth OpenAPI documents,
and each discovered backend `/api/<service-name>/health`, prints Compose diagnostics on failure and always
tears the stack down.

## GitHub configuration synchronization

When a push changes `.github/**`, `github-config-sync.yml` copies only that
folder from the source branch onto each durable development branch except
`main` and `dev-backup`, using a temporary `github-sync/<target>/<run-id>`
branch. Its own `github-sync/**`, `docker-workflow-changes/**` and
`dev-backup-mistaken-commits/**` automation branches are excluded as sources
and targets because they are temporary or recovery refs. The workflow creates
a focused pull request, requests automatic squash merging and asks GitHub to
delete the temporary branch after merge. Existing open PRs for the same
temporary branch are reused on a retry. Pushes created by the standard sync
commit title are ignored to prevent a propagation loop.

For this to work, repository settings must permit GitHub Actions to create pull
requests and queue automatic merges. Required checks or review rules can still
leave a sync PR open; the workflow reports that condition rather than changing
branch protections.

## Local validation

From the repository root:

```bash
python .agents/scripts/validate_agent_resources.py
python .agents/scripts/validate_endpoint_catalog.py
python scripts/validation/validate_ui_integrations.py
python -m unittest discover -s .github/scripts/tests -p "test_*.py"
git diff --check
```

The GitHub runner is authoritative for Actions expression parsing, Docker,
Flutter and hosted SDK behavior. Local validation should still be run before a
pull request and its workflow results should be recorded in the PR template.
