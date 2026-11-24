#!/usr/bin/env bash
# Desktop · Showcase and Mobile · Showcase: compares the screens just captured with the copy the
# design system documents for each, and writes the report to the run summary. The step that runs
# this is continue-on-error while the comparison is a declared gap in .github/behavior-matrix.json.
set -euo pipefail

surface="${1:?usage: compare-product-screens.sh desktop|mobile}"
root="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$root"

report="${RUNNER_TEMP:-$root/artifacts}/compare-$surface.txt"
mkdir -p "$(dirname "$report")"
status=0
./au-debug screens compare "$surface" 2>&1 | tee "$report" || status=$?

if [ -n "${GITHUB_STEP_SUMMARY:-}" ]; then
  {
    echo "### $surface screens against the design system"
    echo
    echo '```'
    cat "$report"
    echo '```'
  } >> "$GITHUB_STEP_SUMMARY"
fi
exit "$status"
