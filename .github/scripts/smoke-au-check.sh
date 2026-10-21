#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -ne 1 ]; then
  echo "Usage: $0 <published-au-check>" >&2
  exit 2
fi

binary="$1"
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$root"
chmod +x "$binary"

plan_dir="$(mktemp -d)"
"$binary" plan > "$plan_dir/au-check.txt"
dotnet run --project AgentUp.CLI --configuration Release --no-launch-profile -- verify plan > "$plan_dir/cli.txt"
diff -u "$plan_dir/cli.txt" "$plan_dir/au-check.txt"

"$binary" guard > "$plan_dir/guard.txt"
echo "au-check guard:"
cat "$plan_dir/guard.txt"
