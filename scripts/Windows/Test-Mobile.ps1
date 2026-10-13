# Run every Flutter Mobile unit/widget test and any checked-in device tests.
# Usage: .\scripts\Windows\Test-Mobile.ps1

$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$mobileDirectory = Join-Path $repositoryRoot 'apps/mobile'
$unitTestFiles = @(Get-ChildItem (Join-Path $mobileDirectory 'test') -Filter '*_test.dart' -File -Recurse -ErrorAction SilentlyContinue)
$integrationDirectory = Join-Path $mobileDirectory 'integration_test'
$integrationTestFiles = @()
if (Test-Path -LiteralPath $integrationDirectory) {
    $integrationTestFiles = @(Get-ChildItem $integrationDirectory -Filter '*_test.dart' -File -Recurse -ErrorAction SilentlyContinue)
}
$dependencyExitCode = 0
$unitTestExitCode = 0
$integrationTestExitCode = 0

if (-not (Get-Command flutter -ErrorAction SilentlyContinue)) {
    [Console]::Error.WriteLine('Flutter is required. Install the Flutter SDK and add flutter to PATH.')
    exit 1
}
if ($unitTestFiles.Count -eq 0) {
    [Console]::Error.WriteLine('No Flutter test files were found under apps/mobile/test.')
    exit 1
}
# Fetch packages using the Flutter SDK's normal package cache.
Write-Host 'Checking Flutter package dependencies...'
Push-Location $mobileDirectory
try {
    & flutter pub get
    $dependencyExitCode = $LASTEXITCODE
} finally { Pop-Location }

if ($dependencyExitCode -eq 0) {
    Write-Host 'Running all Flutter unit/widget tests from apps/mobile/test.'
    # The expanded reporter shows test names and results as the suite runs.
    Push-Location $mobileDirectory
    try {
        & flutter test --no-pub --reporter expanded test
        $unitTestExitCode = $LASTEXITCODE
    } finally { Pop-Location }
    if ($unitTestExitCode -eq 0) { Write-Host 'Flutter unit/widget tests passed.' }
    else { [Console]::Error.WriteLine("Flutter unit/widget tests failed (exit code $unitTestExitCode).") }
}

# Device-level integration tests run too when this project has any.
if ($dependencyExitCode -eq 0 -and $integrationTestFiles.Count -gt 0) {
    Write-Host 'Running Flutter device integration tests from apps/mobile/integration_test.'
    Push-Location $mobileDirectory
    try {
        & flutter test --no-pub --reporter expanded integration_test
        $integrationTestExitCode = $LASTEXITCODE
    } finally { Pop-Location }
    if ($integrationTestExitCode -eq 0) { Write-Host 'Flutter integration tests passed.' }
    else { [Console]::Error.WriteLine("Flutter integration tests failed (exit code $integrationTestExitCode).") }
} elseif ($integrationTestFiles.Count -eq 0) {
    Write-Host 'No integration_test cases are present; skipping device-level tests.'
}

$taskExitCode = 0
if ($dependencyExitCode -ne 0) { $taskExitCode = $dependencyExitCode }
elseif ($unitTestExitCode -ne 0) { $taskExitCode = $unitTestExitCode }
elseif ($integrationTestExitCode -ne 0) { $taskExitCode = $integrationTestExitCode }

if ($taskExitCode -eq 0) {
    Write-Host 'All available Flutter Mobile test cases passed.'
} else {
    [Console]::Error.WriteLine("Flutter test run finished with failures (exit code $taskExitCode).")
}
exit $taskExitCode
