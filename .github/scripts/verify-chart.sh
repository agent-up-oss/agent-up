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
# Capabilities are compiled into the Server, so an entry names only the id: there is no
# package version to pin.
unexpected = sorted({key for entry in modules for key in entry} - {"id"})
if unexpected:
    raise SystemExit(f"enabled.json entries carry {unexpected}; only an id is read")
print("Helm default enabled.json seeds the first-party capability ids.")
PY
