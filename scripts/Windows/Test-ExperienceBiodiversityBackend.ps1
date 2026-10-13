# Run every default Experience & Biodiversity backend test from any directory.
# Requires the .NET SDK version selected by the repository's global.json.
# These tests use local test fixtures; Docker and a running API are not required.

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$testProject = Join-Path $repoRoot 'services\experience-biodiversity\tests\Blueverse.ExperienceBiodiversity.Tests\Blueverse.ExperienceBiodiversity.Tests.csproj'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    [Console]::Error.WriteLine('The .NET SDK was not found. Install the SDK version pinned in global.json.')
    exit 127
}

if (-not (Test-Path -LiteralPath $testProject)) {
    [Console]::Error.WriteLine("Experience & Biodiversity test project was not found: $testProject")
    exit 2
}

Write-Host 'Running all Experience & Biodiversity backend tests...'
Push-Location $repoRoot
try {
    # Detailed console output lists each test case and its result.
    & dotnet test $testProject --configuration Release --logger 'console;verbosity=detailed'
    $testExitCode = $LASTEXITCODE
}
finally {
    Pop-Location
}

if ($testExitCode -eq 0) {
    Write-Host 'Experience & Biodiversity backend tests passed.'
} else {
    [Console]::Error.WriteLine("Experience & Biodiversity backend tests failed (exit code $testExitCode).")
    exit $testExitCode
}
