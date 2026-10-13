#!/usr/bin/env bash
# Run every React Web test with the repository's Node.js test runner.
# Usage: bash scripts/Unix/bash/test-web.sh

set -Eeuo pipefail

# Find the repository root so this script works from any current directory.
script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repository_root="$(cd -- "$script_dir/../../.." && pwd)"
web_directory="$repository_root/apps/web"

if ! command -v node >/dev/null 2>&1 || ! command -v npm >/dev/null 2>&1; then
  echo "Node.js and npm are required. Install Node.js 24 or newer, then try again." >&2
  exit 1
fi

node_major="$(node -p "Number(process.versions.node.split('.')[0])")"
if (( node_major < 24 )); then
  echo "This app's test runner requires Node.js 24 or newer (found $(node --version))." >&2
  exit 1
fi

if [[ ! -f "$web_directory/package-lock.json" ]]; then
  echo "Could not find apps/web/package-lock.json; run this script from a complete repository checkout." >&2
  exit 1
fi

cd "$web_directory"

# Install the exact locked packages on a first run. Existing installs are reused.
if [[ ! -d node_modules ]]; then
  echo "Installing the locked Web dependencies with npm ci..."
  npm ci
fi

echo "Running the complete React Web test suite (one test at a time to limit memory use)."
# The spec reporter prints each test result and the final totals in the terminal.
npm run test:ci -- --test-concurrency=1 --test-reporter=spec
