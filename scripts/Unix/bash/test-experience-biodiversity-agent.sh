#!/usr/bin/env bash
set -Eeuo pipefail

# Find this component from the script location so it works from any directory.
SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd -- "$SCRIPT_DIR/../../.." && pwd)"
COMPONENT_DIR="$REPO_ROOT/services/agents/experience-biodiversity"

# Prefer the Windows Python launcher when available. This avoids mistaking the
# Microsoft Store's `python` alias for an installed Python interpreter.
PYTHON_CHECK='import sys; raise SystemExit(0 if sys.version_info >= (3, 12) else 1)'
PYTHON_COMMAND=()
if command -v py >/dev/null 2>&1 && py -3 -c "$PYTHON_CHECK" >/dev/null 2>&1; then
    PYTHON_COMMAND=("$(command -v py)" -3)
else
    for candidate in python3 python; do
        if command -v "$candidate" >/dev/null 2>&1; then
            candidate_path="$(command -v "$candidate")"
            case "$candidate_path" in
                *WindowsApps*|*windowsapps*) continue ;;
            esac
            if "$candidate_path" -c "$PYTHON_CHECK" >/dev/null 2>&1; then
                PYTHON_COMMAND=("$candidate_path")
                break
            fi
        fi
    done
fi

if [[ ${#PYTHON_COMMAND[@]} -eq 0 ]]; then
    echo "Python 3.12 or newer is required. Install it or use the Windows 'py' launcher." >&2
    exit 1
fi

if [[ ! -d "$COMPONENT_DIR/tests" ]]; then
    echo "Could not find the Experience & Biodiversity tests at: $COMPONENT_DIR/tests" >&2
    exit 1
fi

# Keep this agent's test packages separate from the other services.
VENV_DIR="$COMPONENT_DIR/.venv"
if [[ ! -d "$VENV_DIR" ]]; then
    "${PYTHON_COMMAND[@]}" -m venv "$VENV_DIR"
fi

# Support both Unix Python and Git Bash on Windows.
if [[ -x "$VENV_DIR/bin/python" ]]; then
    VENV_PYTHON="$VENV_DIR/bin/python"
elif [[ -x "$VENV_DIR/Scripts/python.exe" ]]; then
    VENV_PYTHON="$VENV_DIR/Scripts/python.exe"
else
    echo "The virtual environment was not created correctly at: $VENV_DIR" >&2
    exit 1
fi

cd "$COMPONENT_DIR"

# Disable model and agent dispatch for every suite, and hide inherited keys.
# Marine's settings loader reads .env when variables are absent, so use empty
# key values instead of deleting these names from the environment.
export EXPERIENCE_BIODIVERSITY_GEMINI_ENABLED=false
export MARINE_CONDITIONS_GEMINI_ENABLED=false
export PLANNING_COORDINATION_GEMINI_ENABLED=false
export SAFETY_OPERATIONS_GEMINI_ENABLED=false
export AGENTIC_ENABLED=false
export SAFETY_OPERATIONS_GEMINI_DATA_HANDLING_APPROVED=false
export EXPERIENCE_BIODIVERSITY_GEMINI_API_KEY=''
export MARINE_CONDITIONS_GEMINI_API_KEY=''
export PLANNING_COORDINATION_GEMINI_API_KEY=''
export SAFETY_OPERATIONS_GEMINI_API_KEY=''
export GEMINI_API_KEY=''
export PLANNING_COORDINATION_OPENAI_COMPATIBLE_API_KEY=''
export PLANNING_COORDINATION_MODEL_PROVIDER=gemini
export AGENTIC_SERVICE_KEY=''
export EXPERIENCE_AGENT_URL=''
export MARINE_AGENT_URL=''
export SAFETY_OPERATIONS_AGENT_URL=''
export EXPERIENCE_SERVICE_URL=''
export PLANNER_SERVICE_URL=''
export PUBLIC_API_URL=''
export OPEN_METEO_WEATHER_URL=http://127.0.0.1:9
export OPEN_METEO_MARINE_URL=http://127.0.0.1:9

# Install the test tools declared by this agent's pyproject.toml.
"$VENV_PYTHON" -m pip install --disable-pip-version-check --editable '.[test]'

echo "Running all Experience & Biodiversity Agent tests..."
"$VENV_PYTHON" -m pytest tests -v
