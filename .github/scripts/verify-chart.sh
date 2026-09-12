#!/usr/bin/env bash
set -euo pipefail

yq e ".version = \"0.0.0\" | .appVersion = \"0.0.0\"" -i chart/Chart.yaml
helm lint ./chart
helm package ./chart

rendered="$(mktemp)"
trap 'rm -f "$rendered"' EXIT
helm template agent-up ./chart \
  --set server.adminPassword=verify \
  --show-only templates/server/capabilities-configmap.yaml > "$rendered"

python3 - "$rendered" <<'PY'
import json
import sys
from pathlib import Path

text = Path(sys.argv[1]).read_text()
marker = "capabilities.json: |"
start = text.find(marker)
if start < 0:
    raise SystemExit("capabilities ConfigMap is missing capabilities.json")

body = []
for line in text[start + len(marker):].splitlines()[1:]:
    if line.startswith("    "):
        body.append(line[4:])
        continue
    if line.strip() == "":
        continue
    break

inventory = json.loads("\n".join(body))
by_id = {entry["id"]: entry for entry in inventory}
expected = {
    "codex": "/opt/agent-up/bin/codex-acp",
    "cursor": "/opt/agent-up/bin/agent",
    "claude": "/opt/agent-up/bin/claude-agent-acp",
}
if set(by_id) != set(expected):
    raise SystemExit(f"default inventory ids {sorted(by_id)} != {sorted(expected)}")
for capability_id, command in expected.items():
    if by_id[capability_id].get("command") != command:
        raise SystemExit(f"{capability_id} command is {by_id[capability_id].get('command')!r}, expected {command!r}")
if by_id["cursor"].get("arguments") != ["acp"]:
    raise SystemExit(f"cursor arguments are {by_id['cursor'].get('arguments')!r}, expected ['acp']")
print("Helm default capability inventory enables bundled Codex, Cursor, and Claude ACP commands.")
PY
