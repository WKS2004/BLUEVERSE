#!/usr/bin/env bash
# Run every default Experience & Biodiversity backend test from any directory.
# Requires the .NET SDK version selected by the repository's global.json.
# These tests use local test fixtures; Docker and a running API are not required.

set -Eeuo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd -- "$script_dir/../../.." && pwd)"
test_project="$repo_root/services/experience-biodiversity/tests/Blueverse.ExperienceBiodiversity.Tests/Blueverse.ExperienceBiodiversity.Tests.csproj"

if ! command -v dotnet >/dev/null 2>&1; then
  echo "Error: .NET SDK not found. Install the SDK version pinned in global.json." >&2
  exit 127
fi

if [[ ! -f "$test_project" ]]; then
  echo "Error: Experience & Biodiversity test project was not found:" >&2
  echo "  $test_project" >&2
  exit 2
fi

cd "$repo_root"
echo "Running all Experience & Biodiversity backend tests..."

# Detailed console output lists each test case and its result.
if dotnet test "$test_project" --configuration Release --logger "console;verbosity=detailed"; then
  echo "Experience & Biodiversity backend tests passed."
else
  test_exit_code=$?
  echo "Experience & Biodiversity backend tests failed (exit code $test_exit_code)." >&2
  exit "$test_exit_code"
fi
