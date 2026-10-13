$ErrorActionPreference = 'Stop'

# Resolve the repository and this agent's folder from the script location.
$taskRepoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskComponent = Join-Path $taskRepoRoot 'services/agents/marine-conditions'
$taskVenv = Join-Path $taskComponent '.venv'
$taskPython = Join-Path $taskVenv 'Scripts/python.exe'

# Prefer the Python launcher so a Windows Store `python` alias is not mistaken
# for an installed interpreter. Fall back only to a real Python on PATH.
$taskVersionCheck = 'import sys; raise SystemExit(0 if sys.version_info >= (3, 12) else 1)'
$taskPythonCommand = $null
$taskPythonArgs = @()
$taskLauncher = Get-Command py -ErrorAction SilentlyContinue
if ($taskLauncher) {
    & $taskLauncher.Source -3 -c $taskVersionCheck
    if ($LASTEXITCODE -eq 0) {
        $taskPythonCommand = $taskLauncher.Source
        $taskPythonArgs = @('-3')
    }
}
if (-not $taskPythonCommand) {
    foreach ($taskCandidate in @('python3', 'python')) {
        $taskCandidateCommand = Get-Command $taskCandidate -ErrorAction SilentlyContinue
        if (-not $taskCandidateCommand -or -not $taskCandidateCommand.Source) { continue }
        if ($taskCandidateCommand.Source -match '(?i)[\\/]WindowsApps[\\/]') { continue }
        & $taskCandidateCommand.Source -c $taskVersionCheck
        if ($LASTEXITCODE -eq 0) {
            $taskPythonCommand = $taskCandidateCommand.Source
            break
        }
    }
}
if (-not $taskPythonCommand) {
    Write-Error "Python 3.12 or newer is required. Install it or use the Windows 'py' launcher."
    exit 1
}
if (-not (Test-Path -LiteralPath (Join-Path $taskComponent 'tests'))) {
    Write-Error "Could not find the Marine Conditions tests under $taskComponent."
    exit 1
}

# Keep model credentials and network dispatch disabled while the suite runs.
# Empty keys also prevent Marine's dotenv loader from restoring a key from .env.
$taskSafeEnvironment = @{
    EXPERIENCE_BIODIVERSITY_GEMINI_ENABLED = 'false'
    MARINE_CONDITIONS_GEMINI_ENABLED = 'false'
    PLANNING_COORDINATION_GEMINI_ENABLED = 'false'
    SAFETY_OPERATIONS_GEMINI_ENABLED = 'false'
    AGENTIC_ENABLED = 'false'
    SAFETY_OPERATIONS_GEMINI_DATA_HANDLING_APPROVED = 'false'
    EXPERIENCE_BIODIVERSITY_GEMINI_API_KEY = ''
    MARINE_CONDITIONS_GEMINI_API_KEY = ''
    PLANNING_COORDINATION_GEMINI_API_KEY = ''
    SAFETY_OPERATIONS_GEMINI_API_KEY = ''
    GEMINI_API_KEY = ''
    PLANNING_COORDINATION_OPENAI_COMPATIBLE_API_KEY = ''
    PLANNING_COORDINATION_MODEL_PROVIDER = 'gemini'
    AGENTIC_SERVICE_KEY = ''
    EXPERIENCE_AGENT_URL = ''
    MARINE_AGENT_URL = ''
    SAFETY_OPERATIONS_AGENT_URL = ''
    EXPERIENCE_SERVICE_URL = ''
    PLANNER_SERVICE_URL = ''
    PUBLIC_API_URL = ''
    # Keep mocked tests' endpoint routing labels while blocking real egress.
    OPEN_METEO_WEATHER_URL = 'http://127.0.0.1:9/v1/weather'
    OPEN_METEO_MARINE_URL = 'http://127.0.0.1:9/v1/marine'
}
$taskOriginalEnvironment = @{}
foreach ($taskName in $taskSafeEnvironment.Keys) {
    $taskOriginalEnvironment[$taskName] = [Environment]::GetEnvironmentVariable($taskName, 'Process')
}

# Create a separate virtual environment for this agent if needed.
if (-not (Test-Path -LiteralPath $taskPython)) {
    & $taskPythonCommand @taskPythonArgs -m venv $taskVenv
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

Push-Location $taskComponent
$taskExitCode = 0
try {
    foreach ($taskName in $taskSafeEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($taskName, $taskSafeEnvironment[$taskName], 'Process')
    }

    # Install pytest directly; this service is not configured as a Python package.
    & $taskPython -m pip install --disable-pip-version-check 'pytest>=8.0.0' 'pytest-cov>=5.0.0'
    if ($LASTEXITCODE -ne 0) {
        $taskExitCode = $LASTEXITCODE
    } else {
        Write-Host 'Running all Marine Conditions Agent tests...'
        & $taskPython -m pytest tests -v
        $taskExitCode = $LASTEXITCODE
    }
} finally {
    # Return every process setting to its value before this script ran.
    foreach ($taskName in $taskSafeEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($taskName, $taskOriginalEnvironment[$taskName], 'Process')
    }
    Pop-Location
}
exit $taskExitCode
