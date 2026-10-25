#!/usr/bin/env bash
# Smoke-tests a running Meridian deployment. Used by CD against a new revision before it gets traffic,
# and runnable locally:  scripts/smoke-test.sh http://localhost:8080 "$(git rev-parse HEAD)"
#
# Usage: smoke-test.sh <base-url> <expected-commit-sha>
# Exit code: 0 when every check passes, 1 otherwise. Results are also appended to $GITHUB_STEP_SUMMARY when set.
set -uo pipefail

base="${1:?base URL required}"; base="${base%/}"
expected_commit="${2:?expected commit SHA required}"
failures=0
summary="${GITHUB_STEP_SUMMARY:-/dev/null}"

report() { echo "$1"; echo "- $1" >> "$summary"; }
pass() { report "✅ $1"; }
fail() { report "❌ $1"; failures=$((failures + 1)); }
# expect <actual> <wanted> <description>
expect() {
  if [ "$1" = "$2" ]; then pass "$3 → $1"; else fail "$3 → ${1:-none} (expected $2)"; fi
}
status_of() { curl -s -o /dev/null -w '%{http_code}' --max-time 15 "$base$1" || echo 000; }

# Wait until a path returns 200 (scale-from-zero cold start, database wake-up).
wait_for() {
  local path="$1" attempts="$2" code=000
  for _ in $(seq 1 "$attempts"); do
    code=$(status_of "$path")
    [ "$code" = "200" ] && break
    sleep 5
  done
  echo "$code"
}

echo "## Smoke test: $base" >> "$summary"

expect "$(wait_for /health/live 36)" 200 "/health/live"                        # up to 3 min (cold start)
expect "$(wait_for /health/ready 12)" 200 "/health/ready (database reachable)" # up to 1 min more

commit=$(curl -s --max-time 15 "$base/api/v1/version" | sed -n 's/.*"commit":"\([^"]*\)".*/\1/p')
expect "$commit" "$expected_commit" "/api/v1/version commit"

for path in / /dashboard; do
  body=$(curl -s --max-time 15 "$base$path")
  case "$body" in
    *"<mer-root"*) pass "$path → SPA served" ;;
    *) fail "$path → SPA not served" ;;
  esac
done

expect "$(status_of /api/v1/does-not-exist)" 404 "/api/v1/does-not-exist"

if [ "$failures" -gt 0 ]; then
  report "**$failures check(s) failed.**"
  exit 1
fi
report "**All checks passed.**"
