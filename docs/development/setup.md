# Developer Setup

## Repository

Clone the repository and create a working branch.

## Environment

Copy:

```bash
cp .env.example .env
```

Never commit `.env`.

For local Compose, `.env` also defines the internal API-to-Auth destination. Keep it as `http://auth:8080`; clients must continue using the public gateway at `/api/...`.

The configured `ADMIN_EMAIL` and `ADMIN_PASSWORD` seed one administrator account. Normal registration creates users without automatically granting Admin privileges.

## Applications

The generated application projects are already located at:

```text
apps/web
apps/mobile
services/api
services/auth
```

They are intentionally still starter implementations: replace the template screens incrementally as v1 business components are implemented. Do not add business rules to the clients that contradict the API contract.

## Backend SDK

The repository uses:

```text
.NET SDK 10.0.400
```

as defined by `global.json`.

## Docker

Build from the repository root so Dockerfiles can access their expected project paths.

```bash
docker compose build
```

### Web lockfile (no local Node required)

The `package-lock.json` is generated inside the DHI Node 24 container so the
host needs no local Node.js installation.

**Linux / macOS / WSL2:**

```bash
bash scripts/bash/sync-web-lockfile.sh
```

**Windows (PowerShell, no WSL2 required):**

Open PowerShell with **Run as administrator**, then temporarily enable local
script execution before running either PowerShell script:

```powershell
Set-ExecutionPolicy RemoteSigned
```

Run the script:

```powershell
pwsh -File scripts\powershell\sync-web-lockfile.ps1
```

When it finishes, restore the restricted execution policy:

```powershell
Set-ExecutionPolicy Restricted
```

### Foundation verification

Run the structural verification script before committing infrastructure changes.

**Linux / macOS / WSL2:**

```bash
bash scripts/bash/verify-foundation.sh
```

**Windows (PowerShell, no WSL2 required):**

Open PowerShell with **Run as administrator**, then temporarily enable local
script execution:

```powershell
Set-ExecutionPolicy RemoteSigned
```

Run the verifier:

```powershell
pwsh -File scripts\powershell\verify-foundation.ps1
```

After execution completes, restore the restricted policy:

```powershell
Set-ExecutionPolicy Restricted
```

With the stack running, verify:

```text
GET http://localhost/api/health
GET http://localhost/api/auth/health
GET http://localhost/api/swagger/v1.json
GET http://localhost/api/auth/swagger/v1/swagger.json
```

Open the unified Swagger UI at:

```text
http://localhost/api/swagger
```

There is no public `/auth/...` or `/health` backend route. Auth requests must use `/api/auth/...` and are forwarded by the API service.
