#!/usr/bin/env bash
# Windows users without a WSL2 Linux distro: use the PowerShell equivalent.
#   pwsh -File scripts\powershell\verify-foundation.ps1
set -euo pipefail

required=(
  README.md
  PROJECT_REQUIREMENTS.md
  AGENTS.md
  LICENSE.md
  compose.yaml
  render.yaml
  global.json
  .gitignore
  .dockerignore
  .env.example
)

for file in "${required[@]}"; do
  [[ -f "$file" ]] || { echo "Missing: $file"; exit 1; }
done

for dir in .github .agents docs infrastructure/docker apps/web apps/mobile services/api services/auth; do
  [[ -d "$dir" ]] || { echo "Missing directory: $dir"; exit 1; }
done

for file in \
  services/api/Blueverse.Api.csproj \
  services/api/Controllers/HealthController.cs \
  services/auth/Blueverse.Auth.csproj \
  services/auth/Controllers/HealthControllers.cs \
  apps/web/package.json \
  apps/mobile/pubspec.yaml; do
  [[ -f "$file" ]] || { echo "Missing generated project file: $file"; exit 1; }
done

grep -q '"Path": "/api/auth/{\*\*catch-all}"' services/api/appsettings.json
grep -q 'location /api/' infrastructure/docker/edge-nginx/nginx.conf
! grep -qE 'location /auth/|location = /health' infrastructure/docker/edge-nginx/nginx.conf

echo "BLUEVERSE foundation structure: OK"
echo "Generated web, mobile, API and Auth projects are present."
