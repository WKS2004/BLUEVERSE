#!/usr/bin/env bash
# Windows users without a WSL2 Linux distro: use the PowerShell equivalent.
#   pwsh -File scripts\powershell\sync-web-lockfile.ps1
set -euo pipefail

# BLUEVERSE Web Dependency Lockfile Synchronization
#
# Purpose:
#   Generate/update apps/web/package-lock.json from apps/web/package.json
#   without installing node_modules on the host.
#
# Usage:
#   ./scripts/bash/sync-web-lockfile.sh

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
WEB_DIR="$ROOT_DIR/apps/web"
PACKAGE_JSON="$WEB_DIR/package.json"
PACKAGE_LOCK="$WEB_DIR/package-lock.json"

echo "🌊 BLUEVERSE Web Dependency Lockfile Sync"
echo "========================================="

# ------------------------------------------------------------
# Verify frontend project
# ------------------------------------------------------------

if [[ ! -d "$WEB_DIR" ]]; then
    echo "❌ Frontend directory not found:"
    echo "   $WEB_DIR"
    exit 1
fi

if [[ ! -f "$PACKAGE_JSON" ]]; then
    echo "❌ package.json not found:"
    echo "   $PACKAGE_JSON"
    echo
    echo "Make sure the React frontend has been created under apps/web."
    exit 1
fi

echo "✅ Found package.json:"
echo "   $PACKAGE_JSON"

# ------------------------------------------------------------
# Generate/update package-lock.json
# ------------------------------------------------------------

echo
echo "📦 Synchronizing package-lock.json..."
echo "   Using DHI Node 24 image."
echo "   No node_modules will be installed on the host."

docker run --rm \
    -v "$WEB_DIR:/app" \
    -w /app \
    dhi.io/node:24-debian13-dev \
    npm install --package-lock-only

# ------------------------------------------------------------
# Verify generated lockfile
# ------------------------------------------------------------

if [[ ! -f "$PACKAGE_LOCK" ]]; then
    echo
    echo "❌ package-lock.json was not generated."
    exit 1
fi

echo
echo "✅ package-lock.json synchronized successfully:"
echo "   $PACKAGE_LOCK"
echo
echo "You can now run:"
echo
echo "   docker compose build"
echo
echo "or:"
echo
echo "   docker compose build --no-cache"
