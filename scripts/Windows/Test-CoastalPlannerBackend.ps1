# Run the Coastal Planner backend tests from any directory.
# Requires the .NET SDK version selected by the repository's global.json.
# Docker is not required for the default suite; it uses local test fixtures.
# The optional PostgreSQL test runs only when its dedicated password is set.

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$testProject = Join-Path $repoRoot 'services\coastal-planner\tests\Blueverse.CoastalPlanner.Tests\Blueverse.CoastalPlanner.Tests.csproj'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    [Console]::Error.WriteLine('The .NET SDK was not found. Install the SDK version pinned in global.json.')
    exit 127
}

if (-not (Test-Path -LiteralPath $testProject)) {
    [Console]::Error.WriteLine("Coastal Planner test project was not found: $testProject")
    exit 2
}

$testArguments = @($testProject, '--configuration', 'Release', '--logger', 'console;verbosity=detailed')

# This test creates and removes its own uniquely named database. Use a dedicated
# test PostgreSQL account/database server, never a development or production one.
if (-not [string]::IsNullOrWhiteSpace($env:BLUEVERSE_PLANNER_POSTGRES_PASSWORD)) {
    Write-Host 'Planner PostgreSQL test enabled by BLUEVERSE_PLANNER_POSTGRES_PASSWORD.'
    $testArguments += '-p:EnablePostgresPlannerTests=true'
} else {
    Write-Host 'Planner PostgreSQL test is not enabled; running the default suite.'
}

Write-Host 'Running Coastal Planner backend tests...'
Push-Location $repoRoot
try {
    # Detailed console output lists each test case and its result.
    & dotnet test @testArguments
    $testExitCode = $LASTEXITCODE
}
finally {
    Pop-Location
}

if ($testExitCode -eq 0) {
    Write-Host 'Coastal Planner backend tests passed.'
} else {
    [Console]::Error.WriteLine("Coastal Planner backend tests failed (exit code $testExitCode).")
    exit $testExitCode
}
