#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -ne 3 ]; then
  echo "Usage: $0 <platform> <runtime-id> <artifact-dir>" >&2
  exit 2
fi

platform="$1"
rid="$2"
artifact_dir="$3"
work_dir="$(pwd)/artifacts/service-smoke/$platform-$rid"
server_url="http://localhost:5000"
cli=""
uninstall_command=()

cleanup() {
  if [ "${#uninstall_command[@]}" -gt 0 ]; then
    "${uninstall_command[@]}" || true
  fi
}
trap cleanup EXIT

extract_zip() {
  local archive="$1"
  local destination="$2"

  if command -v unzip >/dev/null 2>&1; then
    unzip -q "$archive" -d "$destination"
  else
    powershell.exe -NoProfile -Command "Expand-Archive -LiteralPath '$archive' -DestinationPath '$destination' -Force"
  fi
}

wait_for_server() {
  for _ in {1..90}; do
    if curl -fsS "$server_url/api/workspaces" > "$work_dir/service-workspaces-before.json"; then
      return 0
    fi
    sleep 1
  done

  echo "Installed service did not become ready at $server_url" >&2
  exit 1
}

smoke_cli_workspace() {
  local repo="$work_dir/example-workspace"
  mkdir -p "$repo"

  git -C "$repo" init -q
  git -C "$repo" config user.email "ci@agent-up.local"
  git -C "$repo" config user.name "Agent-Up CI"
  cat > "$repo/agent-up.json" <<'JSON'
{
  "name": "Installed Service Smoke Workspace",
  "applications": []
}
JSON
  git -C "$repo" add agent-up.json
  git -C "$repo" commit -q -m "Add service smoke workspace"

  chmod +x "$cli" 2>/dev/null || true
  (cd "$repo" && AGENTUP_SERVER_URL="$server_url" "$cli" start) > "$work_dir/cli-start.log"
  (cd "$repo" && AGENTUP_SERVER_URL="$server_url" "$cli" status) > "$work_dir/cli-status.log"

  grep -Fq 'Started workspace "Installed Service Smoke Workspace"' "$work_dir/cli-start.log"
  grep -Fq "Name:       Installed Service Smoke Workspace" "$work_dir/cli-status.log"
  grep -Fq "State:      Running" "$work_dir/cli-status.log"
}

rm -rf "$work_dir"
mkdir -p "$work_dir"

case "$platform" in
  macos)
    extract_zip "$artifact_dir/agent-up-macos-$rid.zip" "$work_dir"
    cli="$work_dir/cli/AgentUp.CLI"
    uninstall_command=(sudo "$work_dir/uninstall.sh")
    sudo "$work_dir/install.sh"
    ;;
  windows)
    extract_zip "$artifact_dir/agent-up-windows-$rid.zip" "$work_dir"
    cli="$work_dir/cli/AgentUp.CLI.exe"
    uninstall_command=(powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$work_dir/tools/uninstall-agent-up-server.ps1")
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$work_dir/tools/install-agent-up-server.ps1"
    ;;
  ubuntu)
    tar -xzf "$artifact_dir/agent-up-ubuntu-$rid.tar.gz" -C "$work_dir"
    cli="$work_dir/cli/AgentUp.CLI"
    uninstall_command=(sudo "$work_dir/uninstall.sh")
    sudo "$work_dir/install.sh"
    ;;
  nixos)
    echo "Skipping installed-service smoke for NixOS because this CI job runs on Ubuntu with Nix, not a booted NixOS systemd host."
    exit 0
    ;;
  *)
    echo "Unsupported platform: $platform" >&2
    exit 2
    ;;
esac

wait_for_server
smoke_cli_workspace

echo "Installed service smoke test passed for $platform/$rid"
