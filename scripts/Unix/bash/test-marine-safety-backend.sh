#!/usr/bin/env bash
# Run every default Marine Safety backend test from any directory.
# Requires the .NET SDK version selected by the repository's global.json.
# The tests use local fixtures and service doubles; Docker is not required.

set -Eeuo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd -- "$script_dir/../../.." && pwd)"
test_project="$repo_root/services/marine-safety/tests/Blueverse.MarineSafety.Tests/Blueverse.MarineSafety.Tests.csproj"

if ! command -v dotnet >/dev/null 2>&1; then
  echo "Error: .NET SDK not found. Install the SDK version pinned in global.json." >&2
  exit 127
fi

if [[ ! -f "$test_project" ]]; then
  echo "Error: Marine Safety test project was not found:" >&2
  echo "  $test_project" >&2
  exit 2
fi

cd "$repo_root"
echo "Running all Marine Safety backend tests..."

# Detailed console output lists each test case and its result.
if dotnet test "$test_project" --configuration Release --logger "console;verbosity=detailed"; then
  echo "Marine Safety backend tests passed."
else
  test_exit_code=$?
  echo "Marine Safety backend tests failed (exit code $test_exit_code)." >&2
  exit "$test_exit_code"
fi
