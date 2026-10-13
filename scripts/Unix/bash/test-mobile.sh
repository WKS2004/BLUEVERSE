#!/usr/bin/env bash
# Run every Flutter Mobile unit/widget test and any checked-in device tests.
# Usage: bash scripts/Unix/bash/test-mobile.sh

set -Eeuo pipefail

# Find the repository root so this script works from any current directory.
script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repository_root="$(cd -- "$script_dir/../../.." && pwd)"
mobile_directory="$repository_root/apps/mobile"

if ! command -v flutter >/dev/null 2>&1; then
  echo "Flutter is required. Install the Flutter SDK and add flutter to PATH." >&2
  exit 1
fi

if [[ ! -d "$mobile_directory/test" ]] || ! find "$mobile_directory/test" -type f -name '*_test.dart' -print -quit | grep -q .; then
  echo "No Flutter test files were found under apps/mobile/test." >&2
  exit 1
fi

cd "$mobile_directory"

# Resolve the versions in pubspec.lock before running the tests.
echo "Checking Flutter package dependencies..."
flutter pub get

test_status=0
echo "Running all Flutter unit/widget tests from apps/mobile/test."
# The expanded reporter shows test names and results as the suite runs.
if flutter test --no-pub --reporter expanded test; then
  echo "Flutter unit/widget tests passed."
else
  test_status=$?
  echo "Flutter unit/widget tests failed (exit code $test_status)." >&2
fi

# Device-level integration tests run too when this project has any.
if [[ -d integration_test ]] && find integration_test -type f -name '*_test.dart' -print -quit | grep -q .; then
  echo "Running Flutter device integration tests from apps/mobile/integration_test."
  if flutter test --no-pub --reporter expanded integration_test; then
    echo "Flutter integration tests passed."
  else
    integration_status=$?
    echo "Flutter integration tests failed (exit code $integration_status)." >&2
    test_status=1
  fi
else
  echo "No integration_test cases are present; skipping device-level tests."
fi

if (( test_status != 0 )); then
  echo "Flutter test run finished with failures." >&2
  exit "$test_status"
fi

echo "All available Flutter Mobile test cases passed."
