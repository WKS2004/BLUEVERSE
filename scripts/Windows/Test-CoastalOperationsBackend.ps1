# Run every Coastal Operations backend test from any directory.
# Requires the .NET SDK version selected by the repository's global.json.
# Docker is not required for the default tests; they use local test fixtures.
# Six PostgreSQL workflow tests run only when the connection variable is set.

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$testProject = Join-Path $repoRoot 'services\coastal-operations\tests\Blueverse.CoastalOperations.Tests\Blueverse.CoastalOperations.Tests.csproj'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    [Console]::Error.WriteLine('The .NET SDK was not found. Install the SDK version pinned in global.json.')
    exit 127
}

if (-not (Test-Path -LiteralPath $testProject)) {
    [Console]::Error.WriteLine("Coastal Operations test project was not found: $testProject")
    exit 2
}

Write-Host 'Running all Coastal Operations backend tests...'
Push-Location $repoRoot
try {
    # Set BLUEVERSE_CO_POSTGRES_TEST_CONNECTION to a dedicated database whose
    # name starts with blueverse_co_test to include the six provider-specific cases.
    # Never point this variable at a development or production database.
    # Detailed console output lists each test case and its result.
    & dotnet test $testProject --configuration Release --logger 'console;verbosity=detailed'
    $testExitCode = $LASTEXITCODE
}
finally {
    Pop-Location
}

if ($testExitCode -eq 0) {
    Write-Host 'Coastal Operations backend tests passed.'
} else {
    [Console]::Error.WriteLine("Coastal Operations backend tests failed (exit code $testExitCode).")
    exit $testExitCode
}
