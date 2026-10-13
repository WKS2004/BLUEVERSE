#!/usr/bin/env bash
# Run every Coastal Operations backend test from any directory.
# Requires the .NET SDK version selected by the repository's global.json.
# Docker is not required for the default tests; they use local test fixtures.
# Six PostgreSQL workflow tests run only when the connection variable is set.

set -Eeuo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd -- "$script_dir/../../.." && pwd)"
test_project="$repo_root/services/coastal-operations/tests/Blueverse.CoastalOperations.Tests/Blueverse.CoastalOperations.Tests.csproj"

if ! command -v dotnet >/dev/null 2>&1; then
  echo "Error: .NET SDK not found. Install the SDK version pinned in global.json." >&2
  exit 127
fi

if [[ ! -f "$test_project" ]]; then
  echo "Error: Coastal Operations test project was not found:" >&2
  echo "  $test_project" >&2
  exit 2
fi

cd "$repo_root"
echo "Running all Coastal Operations backend tests..."

# Set BLUEVERSE_CO_POSTGRES_TEST_CONNECTION to a dedicated database whose name
# starts with blueverse_co_test to include the six provider-specific cases.
# Never point this variable at a development or production database.
# Detailed console output lists each test case and its result.
if dotnet test "$test_project" --configuration Release --logger "console;verbosity=detailed"; then
  echo "Coastal Operations backend tests passed."
else
  test_exit_code=$?
  echo "Coastal Operations backend tests failed (exit code $test_exit_code)." >&2
  exit "$test_exit_code"
fi
