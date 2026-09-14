#Requires -Version 5.1
<#
.SYNOPSIS
    BLUEVERSE Web Dependency Lockfile Synchronization (Windows / PowerShell).

.DESCRIPTION
    Generates or updates apps/web/package-lock.json from apps/web/package.json
    without installing node_modules on the host machine.

This script is the PowerShell equivalent of scripts/bash/sync-web-lockfile.sh
    and can be run on Windows machines that do not have a Linux WSL2
    distribution installed.

    Docker Desktop is required - the lockfile generation runs inside the
    DHI Node 24 container so the host needs no local Node.js installation.

.EXAMPLE
    pwsh -File scripts\powershell\sync-web-lockfile.ps1
    # or from within PowerShell:
    .\scripts\powershell\sync-web-lockfile.ps1

    Run from an elevated Administrator PowerShell session:
        Set-ExecutionPolicy RemoteSigned
        pwsh -File scripts\powershell\sync-web-lockfile.ps1
        Set-ExecutionPolicy Restricted
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$Principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $Principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'This script must be run from an elevated Administrator PowerShell session.'
}

# ---------------------------------------------------------------------------
# Resolve paths
# ---------------------------------------------------------------------------
$RepoRoot    = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$WebDir      = Join-Path $RepoRoot 'apps\web'
$PackageJson = Join-Path $WebDir 'package.json'
$PackageLock = Join-Path $WebDir 'package-lock.json'

Write-Host ''
Write-Host 'BLUEVERSE Web Dependency Lockfile Sync' -ForegroundColor Cyan
Write-Host '=======================================' -ForegroundColor Cyan

# ---------------------------------------------------------------------------
# Verify frontend project
# ---------------------------------------------------------------------------
if (-not (Test-Path $WebDir -PathType Container)) {
    Write-Host ''
    Write-Host "ERROR: Frontend directory not found:" -ForegroundColor Red
    Write-Host "   $WebDir" -ForegroundColor Red
    exit 1
}

if (-not (Test-Path $PackageJson -PathType Leaf)) {
    Write-Host ''
    Write-Host "ERROR: package.json not found:" -ForegroundColor Red
    Write-Host "   $PackageJson" -ForegroundColor Red
    Write-Host ''
    Write-Host 'Make sure the React frontend has been created under apps/web.'
    exit 1
}

Write-Host ''
Write-Host 'Found package.json:' -ForegroundColor Green
Write-Host "   $PackageJson"

# ---------------------------------------------------------------------------
# Verify Docker is available
# ---------------------------------------------------------------------------
if (-not (Get-Command 'docker' -ErrorAction SilentlyContinue)) {
    Write-Host ''
    Write-Host 'ERROR: docker is not available in PATH.' -ForegroundColor Red
    Write-Host 'Install Docker Desktop and ensure it is running before continuing.'
    exit 1
}

# ---------------------------------------------------------------------------
# Keep the native Windows path and pass it through Docker's --mount syntax.
# This avoids ambiguity around drive-letter colons and works with Docker
# Desktop from both Windows PowerShell and PowerShell 7.
# ---------------------------------------------------------------------------

# ---------------------------------------------------------------------------
# Generate / update package-lock.json via Docker
# ---------------------------------------------------------------------------
Write-Host ''
Write-Host 'Synchronizing package-lock.json...' -ForegroundColor Cyan
Write-Host '   Using DHI Node 24 image.'
Write-Host '   No node_modules will be installed on the host.'
Write-Host ''

docker run --rm `
    --mount "type=bind,source=$WebDir,target=/app" `
    -w /app `
    dhi.io/node:24-debian13-dev `
    npm install --package-lock-only

if ($LASTEXITCODE -ne 0) {
    Write-Host ''
    Write-Host 'ERROR: docker run exited with code' $LASTEXITCODE -ForegroundColor Red
    exit $LASTEXITCODE
}

# ---------------------------------------------------------------------------
# Verify generated lockfile
# ---------------------------------------------------------------------------
if (-not (Test-Path $PackageLock -PathType Leaf)) {
    Write-Host ''
    Write-Host 'ERROR: package-lock.json was not generated.' -ForegroundColor Red
    exit 1
}

Write-Host ''
Write-Host 'package-lock.json synchronized successfully:' -ForegroundColor Green
Write-Host "   $PackageLock"
Write-Host ''
Write-Host 'You can now run:'
Write-Host ''
Write-Host '   docker compose build'
Write-Host ''
Write-Host 'or:'
Write-Host ''
Write-Host '   docker compose build --no-cache'
