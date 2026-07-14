#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -ne 3 ]; then
  echo "Usage: $0 <platform> <runtime-id> <artifact-dir>" >&2
  exit 2
fi

platform="$1"
rid="$2"
artifact_dir="$3"
work_dir="$(pwd)/artifacts/package-smoke/$platform-$rid"
port="$((45000 + (RANDOM % 10000)))"
server_pid=""

cleanup() {
  if [ -n "$server_pid" ]; then
    kill "$server_pid" 2>/dev/null || true
    wait "$server_pid" 2>/dev/null || true
  fi
}
trap cleanup EXIT

assert_file() {
  if [ ! -f "$1" ]; then
    echo "Expected file missing: $1" >&2
    exit 1
  fi
}

assert_executable() {
  assert_file "$1"
  if [ "${RUNNER_OS:-}" != "Windows" ] && [ ! -x "$1" ]; then
    echo "Expected executable missing execute bit: $1" >&2
    exit 1
  fi
}

assert_contains() {
  local file="$1"
  local expected="$2"

  assert_file "$file"
  if ! grep -Fq "$expected" "$file"; then
    echo "Expected '$file' to contain: $expected" >&2
    exit 1
  fi
}

extract_zip() {
  local archive="$1"
  local destination="$2"

  if command -v unzip >/dev/null 2>&1; then
    unzip -q "$archive" -d "$destination"
  else
    powershell.exe -NoProfile -Command "Expand-Archive -LiteralPath '$archive' -DestinationPath '$destination' -Force"
  fi
}

start_server_and_probe() {
  local server="$1"
  local data_dir="$work_dir/data"
  local url="http://localhost:$port"
  mkdir -p "$data_dir"

  chmod +x "$server" 2>/dev/null || true
  Storage__DataDirectory="$data_dir" ASPNETCORE_URLS="$url" "$server" > "$work_dir/server.log" 2>&1 &
  server_pid="$!"

  for _ in {1..60}; do
    if curl -fsS "$url/api/workspaces" > "$work_dir/workspaces.json"; then
      return 0
    fi

    if ! kill -0 "$server_pid" 2>/dev/null; then
      echo "Packaged server exited before becoming ready." >&2
      cat "$work_dir/server.log" >&2 || true
      exit 1
    fi

    sleep 1
  done

  echo "Packaged server did not become ready at $url" >&2
  cat "$work_dir/server.log" >&2 || true
  exit 1
}

smoke_cli_workspace() {
  local cli="$1"
  local repo="$work_dir/example-workspace"
  mkdir -p "$repo"

  git -C "$repo" init -q
  git -C "$repo" config user.email "ci@agent-up.local"
  git -C "$repo" config user.name "Agent-Up CI"
  cat > "$repo/agent-up.json" <<'JSON'
{
  "name": "Package Smoke Workspace",
  "applications": []
}
JSON
  git -C "$repo" add agent-up.json
  git -C "$repo" commit -q -m "Add smoke workspace"

  chmod +x "$cli" 2>/dev/null || true
  (cd "$repo" && AGENTUP_SERVER_URL="http://localhost:$port" "$cli" start) > "$work_dir/cli-start.log"
  (cd "$repo" && AGENTUP_SERVER_URL="http://localhost:$port" "$cli" status) > "$work_dir/cli-status.log"

  grep -Fq 'Started workspace "Package Smoke Workspace"' "$work_dir/cli-start.log"
  grep -Fq "Name:       Package Smoke Workspace" "$work_dir/cli-status.log"
  grep -Fq "State:      Running" "$work_dir/cli-status.log"
}

rm -rf "$work_dir"
mkdir -p "$work_dir"

case "$platform" in
  macos)
    archive="$artifact_dir/agent-up-macos-$rid.zip"
    assert_file "$archive"
    extract_zip "$archive" "$work_dir"
    assert_executable "$work_dir/Agent-Up.app/Contents/MacOS/AgentUp.Desktop"
    assert_executable "$work_dir/Agent-Up.app/Contents/Resources/server/AgentUp.Server"
    assert_executable "$work_dir/cli/AgentUp.CLI"
    assert_file "$work_dir/Agent-Up.app/Contents/Info.plist"
    assert_file "$work_dir/agent-up-server.plist"
    assert_file "$work_dir/install.sh"
    assert_file "$work_dir/uninstall.sh"
    assert_contains "$work_dir/agent-up-server.plist" "/Applications/Agent-Up.app/Contents/Resources/server/AgentUp.Server"
    assert_contains "$work_dir/install.sh" "launchctl bootstrap system"
    start_server_and_probe "$work_dir/Agent-Up.app/Contents/Resources/server/AgentUp.Server"
    smoke_cli_workspace "$work_dir/cli/AgentUp.CLI"
    ;;
  windows)
    archive="$artifact_dir/agent-up-windows-$rid.zip"
    assert_file "$archive"
    extract_zip "$archive" "$work_dir"
    assert_file "$work_dir/desktop/AgentUp.Desktop.exe"
    assert_file "$work_dir/server/AgentUp.Server.exe"
    assert_file "$work_dir/cli/AgentUp.CLI.exe"
    assert_file "$work_dir/tools/install-agent-up-server.ps1"
    assert_file "$work_dir/tools/uninstall-agent-up-server.ps1"
    assert_contains "$work_dir/tools/install-agent-up-server.ps1" "New-Service"
    assert_contains "$work_dir/tools/install-agent-up-server.ps1" "Start-Service"
    start_server_and_probe "$work_dir/server/AgentUp.Server.exe"
    smoke_cli_workspace "$work_dir/cli/AgentUp.CLI.exe"
    ;;
  ubuntu|nixos)
    archive="$artifact_dir/agent-up-$platform-$rid.tar.gz"
    assert_file "$archive"
    tar -xzf "$archive" -C "$work_dir"
    assert_executable "$work_dir/desktop/AgentUp.Desktop"
    assert_executable "$work_dir/server/AgentUp.Server"
    assert_executable "$work_dir/cli/AgentUp.CLI"
    assert_file "$work_dir/share/agent-up-server.service"
    assert_file "$work_dir/install.sh"
    assert_file "$work_dir/uninstall.sh"
    assert_contains "$work_dir/share/agent-up-server.service" "ExecStart=/opt/agent-up/server/AgentUp.Server"
    assert_contains "$work_dir/install.sh" "systemctl enable --now agent-up-server.service"
    if [ "$platform" = "nixos" ]; then
      assert_file "$work_dir/share/agent-up-nixos-module.nix"
      assert_contains "$work_dir/share/agent-up-nixos-module.nix" "systemd.services.agent-up-server"
    fi
    start_server_and_probe "$work_dir/server/AgentUp.Server"
    smoke_cli_workspace "$work_dir/cli/AgentUp.CLI"
    ;;
  *)
    echo "Unsupported platform: $platform" >&2
    exit 2
    ;;
esac

echo "Package smoke test passed for $platform/$rid"
