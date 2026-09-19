# Developer Setup

## Repository

Clone the repository and create a working branch.

## Environment

Copy:

```bash
cp .env.example .env
```

Never commit `.env`.

For local Compose, `.env` also defines the internal API-to-Auth destination.
Keep it as `http://auth:8080`; clients must continue using the public gateway
at `/api/...`. Replace the example database/admin passwords before using the
stack, even for local shared environments.

When the Auth implementation is present, the configured `ADMIN_EMAIL` and
`ADMIN_PASSWORD` seed one administrator account. Normal registration must not
automatically grant Admin privileges.

## Applications

The checked-in client projects are located at:

```text
apps/web
apps/mobile
```

Both clients are still generated starter implementations. The public API is
implemented in `services/api`; the internal `services/auth` project is still a
pending dependency for the complete Compose stack. Do not add business rules
to the clients that contradict the API contract.

## Backend SDK

The repository uses:

```text
.NET SDK 10.0.400
```

as defined by `global.json`.

## Docker

Build from the repository root so Dockerfiles can access their expected project paths.

The selected Docker Hardened Images require a registry login before the first
pull. Authenticate locally with a Docker PAT or organization access token:

```bash
docker login dhi.io
```

Use a read-only token where possible. Do not put the token in `.env`, a script,
or a committed configuration file.

```bash
docker compose --env-file .env config --quiet
docker compose build
```

The automated Docker workflows are documented in [Docker CI Workflows](ci.md). They use the same repository-root build context as local builds.

### Web lockfile (no local Node required)

The `package-lock.json` is generated inside the DHI Node 24 container so the
host needs no local Node.js installation.

The Docker Web Build workflow runs the same Unix synchronization script before
building the image. This ensures `npm ci` receives a lockfile generated from
the current `package.json`, even when the lockfile is missing or stale.

**Linux / macOS / WSL2:**

```bash
bash scripts/Unix/bash/sync-web-lockfile.sh
```

**Windows (PowerShell, no WSL2 required):**

Open PowerShell with **Run as administrator**, then temporarily enable local
script execution before running either PowerShell script:

```powershell
Set-ExecutionPolicy RemoteSigned
```

Run the script:

```powershell
pwsh -File scripts\Windows\powershell\sync-web-lockfile.ps1
```

When it finishes, restore the restricted execution policy:

```powershell
Set-ExecutionPolicy Restricted
```

### Foundation verification

Run the structural verification script before committing infrastructure changes.

**Linux / macOS / WSL2:**

```bash
bash scripts/Unix/bash/verify-foundation.sh
```

**Windows (PowerShell, no WSL2 required):**

Open PowerShell with **Run as administrator**, then temporarily enable local
script execution:

```powershell
Set-ExecutionPolicy RemoteSigned
```

Run the verifier:

```powershell
pwsh -File scripts\Windows\powershell\verify-foundation.ps1
```

After execution completes, restore the restricted policy:

```powershell
Set-ExecutionPolicy Restricted
```

The API can be smoke-tested independently of the full stack:

```bash
dotnet run --project services/api --launch-profile http
```

Then verify `http://localhost:5169/api/health` and
`http://localhost:5169/api/swagger`. For gateway verification, add the Auth
project, start the complete Compose stack, and verify:

```text
GET http://localhost/health
GET http://localhost/api/health
GET http://localhost/api/auth/health
GET http://localhost/api/swagger/v1.json
GET http://localhost/api/swagger/v1/swagger.json
GET http://localhost/api/auth/swagger/v1/swagger.json
```

Open the unified Swagger UI at:

```text
http://localhost/api/swagger
```

There is no public `/auth/...` or bare `/health` backend route. Auth requests
must use `/api/auth/...` and are forwarded by the API service.

Additional ASP.NET services follow the public gateway convention:

```text
GET http://localhost/api/<service-name>/health
```
