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
marker = "enabled.json: |"
start = text.find(marker)
if start < 0:
    raise SystemExit("capabilities ConfigMap is missing enabled.json")

body = []
for line in text[start + len(marker):].splitlines()[1:]:
    if line.startswith("    "):
        body.append(line[4:])
        continue
    if line.strip() == "":
        continue
    break

enabled = json.loads("\n".join(body))
modules = enabled.get("modules") or []
by_id = {entry["id"]: entry for entry in modules}
expected = {"dotnet", "docker", "codex", "cursor", "claude"}
if set(by_id) != expected:
    raise SystemExit(f"default enabled ids {sorted(by_id)} != {sorted(expected)}")
for capability_id in expected:
    if not by_id[capability_id].get("version"):
        raise SystemExit(f"{capability_id} is missing a version")
print("Helm default enabled.json seeds first-party capability packages.")
PY
