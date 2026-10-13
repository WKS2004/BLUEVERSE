# Run every React Web test with the repository's Node.js test runner.
# Usage: .\scripts\Windows\Test-Web.ps1

$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$webDirectory = Join-Path $repositoryRoot 'apps/web'
$taskExitCode = 0

# Check the tools and Node version before starting the suite.
if (-not (Get-Command node -ErrorAction SilentlyContinue) -or
    -not (Get-Command npm.cmd -ErrorAction SilentlyContinue)) {
    [Console]::Error.WriteLine('Node.js and npm are required. Install Node.js 24 or newer, then try again.')
    exit 1
}
$nodeMajor = [int](& node -p "Number(process.versions.node.split('.')[0])")
if ($nodeMajor -lt 24) {
    [Console]::Error.WriteLine("This app's test runner requires Node.js 24 or newer (found $(& node --version)).")
    exit 1
}
if (-not (Test-Path -LiteralPath (Join-Path $webDirectory 'package-lock.json'))) {
    [Console]::Error.WriteLine('Could not find apps/web/package-lock.json; use a complete repository checkout.')
    exit 1
}

Push-Location $webDirectory
try {
    # Install the exact locked packages on a first run. Existing installs are reused.
    if (-not (Test-Path -LiteralPath (Join-Path $webDirectory 'node_modules'))) {
        Write-Host 'Installing the locked Web dependencies with npm ci...'
        & npm.cmd ci
        if ($LASTEXITCODE -ne 0) { $taskExitCode = $LASTEXITCODE }
    }

    if ($taskExitCode -eq 0) {
        Write-Host 'Running the complete React Web test suite (one test at a time to limit memory use).'
        # The spec reporter prints each test result and the final totals.
        & npm.cmd run test:ci -- --test-concurrency=1 --test-reporter=spec
        $taskExitCode = $LASTEXITCODE
    }
} finally {
    Pop-Location
}

if ($taskExitCode -eq 0) {
    Write-Host 'All React Web test cases passed.'
} else {
    [Console]::Error.WriteLine("React Web tests failed (exit code $taskExitCode).")
}
exit $taskExitCode
