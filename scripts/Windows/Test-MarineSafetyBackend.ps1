# Run every default Marine Safety backend test from any directory.
# Requires the .NET SDK version selected by the repository's global.json.
# The tests use local fixtures and service doubles; Docker is not required.

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$testProject = Join-Path $repoRoot 'services\marine-safety\tests\Blueverse.MarineSafety.Tests\Blueverse.MarineSafety.Tests.csproj'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    [Console]::Error.WriteLine('The .NET SDK was not found. Install the SDK version pinned in global.json.')
    exit 127
}

if (-not (Test-Path -LiteralPath $testProject)) {
    [Console]::Error.WriteLine("Marine Safety test project was not found: $testProject")
    exit 2
}

Write-Host 'Running all Marine Safety backend tests...'
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
    Write-Host 'Marine Safety backend tests passed.'
} else {
    [Console]::Error.WriteLine("Marine Safety backend tests failed (exit code $testExitCode).")
    exit $testExitCode
}
