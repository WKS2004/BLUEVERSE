#Requires -Version 5.1
<#
.SYNOPSIS
    BLUEVERSE foundation structure verification (Windows / PowerShell).

.DESCRIPTION
    Verifies that all required root files, directories, and generated project
    files are present, and that key configuration patterns exist in
    appsettings.json and nginx.conf.

    This script is the PowerShell equivalent of scripts/bash/verify-foundation.sh
    and produces identical output / exit codes so it can be used on Windows
    machines that do not have a Linux WSL2 distribution installed.

    Docker is still required to run the stack, but this script itself has
    no dependency on Docker, bash, or WSL2.

.EXAMPLE
    pwsh -File scripts\powershell\verify-foundation.ps1
    # or from within PowerShell:
    .\scripts\powershell\verify-foundation.ps1

    Run from an elevated Administrator PowerShell session:
        Set-ExecutionPolicy RemoteSigned
        pwsh -File scripts\powershell\verify-foundation.ps1
        Set-ExecutionPolicy Restricted
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$Principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $Principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'This script must be run from an elevated Administrator PowerShell session.'
}

# ---------------------------------------------------------------------------
# Resolve repository root (parent of the scripts\ folder)
# ---------------------------------------------------------------------------
$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..'))
Push-Location $RepoRoot

$Failed = $false

function Fail {
    param([string]$Message)
    Write-Host "MISSING: $Message" -ForegroundColor Red
    $script:Failed = $true
}

# ---------------------------------------------------------------------------
# 1. Required root files
# ---------------------------------------------------------------------------
$RequiredFiles = @(
    'README.md'
    'PROJECT_REQUIREMENTS.md'
    'AGENTS.md'
    'LICENSE.md'
    'compose.yaml'
    'render.yaml'
    'global.json'
    '.gitignore'
    '.dockerignore'
    '.env.example'
)

foreach ($file in $RequiredFiles) {
    if (-not (Test-Path $file -PathType Leaf)) {
        Fail $file
    }
}

# ---------------------------------------------------------------------------
# 2. Required directories
# ---------------------------------------------------------------------------
$RequiredDirs = @(
    '.github'
    '.agents'
    'docs'
    'infrastructure/docker'
    'apps/web'
    'apps/mobile'
    'services/api'
    'services/auth'
)

foreach ($dir in $RequiredDirs) {
    if (-not (Test-Path $dir -PathType Container)) {
        Fail "directory: $dir"
    }
}

# ---------------------------------------------------------------------------
# 3. Generated project files
# ---------------------------------------------------------------------------
$ProjectFiles = @(
    'services/api/Blueverse.Api.csproj'
    'services/api/Controllers/HealthController.cs'
    'services/auth/Blueverse.Auth.csproj'
    'services/auth/Controllers/HealthControllers.cs'
    'apps/web/package.json'
    'apps/mobile/pubspec.yaml'
)

foreach ($file in $ProjectFiles) {
    if (-not (Test-Path $file -PathType Leaf)) {
        Fail "generated project file: $file"
    }
}

# ---------------------------------------------------------------------------
# 4. Configuration pattern checks
# ---------------------------------------------------------------------------

# appsettings.json must contain the Auth catch-all proxy route
$AppSettings = Get-Content 'services/api/appsettings.json' -Raw
if ($AppSettings -notmatch [regex]::Escape('"Path": "/api/auth/{**catch-all}"')) {
    Fail 'services/api/appsettings.json: missing Auth catch-all route pattern'
}

# nginx.conf must contain the /api/ location block
$NginxConf = Get-Content 'infrastructure/docker/edge-nginx/nginx.conf' -Raw
if ($NginxConf -notmatch 'location /api/') {
    Fail 'infrastructure/docker/edge-nginx/nginx.conf: missing "location /api/" block'
}

# nginx.conf must NOT expose /auth/ or a bare /health route directly
if ($NginxConf -match 'location /auth/' -or $NginxConf -match 'location = /health') {
    Fail 'infrastructure/docker/edge-nginx/nginx.conf: must not expose /auth/ or = /health directly'
}

# ---------------------------------------------------------------------------
# Result
# ---------------------------------------------------------------------------
Pop-Location

if ($Failed) {
    Write-Host ''
    Write-Host 'BLUEVERSE foundation verification FAILED. See above for details.' -ForegroundColor Red
    exit 1
}

Write-Host ''
Write-Host 'BLUEVERSE foundation structure: OK' -ForegroundColor Green
Write-Host 'Generated web, mobile, API and Auth projects are present.' -ForegroundColor Green
