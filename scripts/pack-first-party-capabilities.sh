#!/usr/bin/env bash
set -euo pipefail

# Pack first-party capability projects into a local registry directory so CI and
# native tests can enable packages before any remote publish.

registry="${1:?local capability registry directory}"
root="$(cd "$(dirname "$0")/.." && pwd)"
configuration="${CONFIGURATION:-Release}"
mkdir -p "$registry/packages"

pack_one() {
  local project="$1"
  local staged
  staged="$(mktemp -d "${TMPDIR:-/tmp}/agent-up-pack.XXXXXX")"
  dotnet run --project "$root/$project" --configuration "$configuration" --no-launch-profile -- "$staged"
  python3 - "$staged" "$registry" <<'PY'
import json, pathlib, shutil, sys
staged = pathlib.Path(sys.argv[1])
registry = pathlib.Path(sys.argv[2])
manifest = json.loads((staged / "capability.json").read_text())
destination = registry / "packages" / manifest["id"] / manifest["version"]
destination.mkdir(parents=True, exist_ok=True)
for path in staged.iterdir():
    target = destination / path.name
    if path.is_file():
        shutil.copy2(path, target)
index_path = registry / "index.json"
packages = []
if index_path.exists():
    packages = json.loads(index_path.read_text()).get("packages", [])
packages = [
    entry for entry in packages
    if not (entry.get("id") == manifest["id"] and entry.get("version") == manifest["version"])
]
packages.append({
    "id": manifest["id"],
    "version": manifest["version"],
    "displayName": manifest.get("displayName", manifest["id"]),
    "publisher": manifest.get("publisher", "agent-up"),
    "kind": manifest.get("kind", ""),
})
packages.sort(key=lambda entry: (entry["id"], entry["version"]))
index_path.write_text(json.dumps({"schemaVersion": "1", "packages": packages}, indent=2) + "\n")
PY
  rm -rf "$staged"
}

pack_one AgentUp.Capabilities.Dotnet/AgentUp.Capabilities.Dotnet.csproj
pack_one AgentUp.Capabilities.Docker/AgentUp.Capabilities.Docker.csproj
pack_one AgentUp.Capabilities.Codex/AgentUp.Capabilities.Codex.csproj
pack_one AgentUp.Capabilities.Cursor/AgentUp.Capabilities.Cursor.csproj
pack_one AgentUp.Capabilities.Claude/AgentUp.Capabilities.Claude.csproj
echo "Packed first-party capabilities into $registry"
