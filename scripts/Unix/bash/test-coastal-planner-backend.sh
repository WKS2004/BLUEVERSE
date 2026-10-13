#!/usr/bin/env bash
# Run the Coastal Planner backend tests from any directory.
# Requires the .NET SDK version selected by the repository's global.json.
# Docker is not required for the default suite; it uses local test fixtures.
# The optional PostgreSQL test runs only when its dedicated password is set.

set -Eeuo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd -- "$script_dir/../../.." && pwd)"
test_project="$repo_root/services/coastal-planner/tests/Blueverse.CoastalPlanner.Tests/Blueverse.CoastalPlanner.Tests.csproj"

if ! command -v dotnet >/dev/null 2>&1; then
  echo "Error: .NET SDK not found. Install the SDK version pinned in global.json." >&2
  exit 127
fi

if [[ ! -f "$test_project" ]]; then
  echo "Error: Coastal Planner test project was not found:" >&2
  echo "  $test_project" >&2
  exit 2
fi

cd "$repo_root"
test_arguments=("$test_project" --configuration Release --logger "console;verbosity=detailed")

# This test creates and removes its own uniquely named database. Use a dedicated
# test PostgreSQL account/database server, never a development or production one.
if [[ -n "${BLUEVERSE_PLANNER_POSTGRES_PASSWORD:-}" ]]; then
  echo "Planner PostgreSQL test enabled by BLUEVERSE_PLANNER_POSTGRES_PASSWORD."
  test_arguments+=(--property:EnablePostgresPlannerTests=true)
else
  echo "Planner PostgreSQL test is not enabled; running the default suite."
fi

echo "Running Coastal Planner backend tests..."

# Detailed console output lists each test case and its result.
if dotnet test "${test_arguments[@]}"; then
  echo "Coastal Planner backend tests passed."
else
  test_exit_code=$?
  echo "Coastal Planner backend tests failed (exit code $test_exit_code)." >&2
  exit "$test_exit_code"
fi
