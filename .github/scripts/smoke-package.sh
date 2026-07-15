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
mounted_dmg=""

cleanup() {
  if [ -n "$server_pid" ]; then
    kill "$server_pid" 2>/dev/null || true
    wait "$server_pid" 2>/dev/null || true
  fi
  if [ -n "$mounted_dmg" ]; then
    hdiutil detach "$mounted_dmg" -quiet 2>/dev/null || true
  fi
}
trap cleanup EXIT

assert_file() {
  if [ ! -f "$1" ]; then
    echo "Expected file missing: $1" >&2
    exit 1
  fi
}

assert_symlink() {
  if [ ! -L "$1" ]; then
    echo "Expected symlink missing: $1" >&2
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

  if [ "${RUNNER_OS:-}" = "Windows" ] && command -v powershell.exe >/dev/null 2>&1; then
    local ps_archive="$archive"
    local ps_destination="$destination"
    if command -v cygpath >/dev/null 2>&1; then
      ps_archive="$(cygpath -w "$archive")"
      ps_destination="$(cygpath -w "$destination")"
    fi
    powershell.exe -NoProfile -Command "Expand-Archive -LiteralPath '$ps_archive' -DestinationPath '$ps_destination' -Force"
  elif command -v unzip >/dev/null 2>&1; then
    unzip -q "$archive" -d "$destination"
  else
    powershell.exe -NoProfile -Command "Expand-Archive -LiteralPath '$archive' -DestinationPath '$destination' -Force"
  fi
}

validate_package_contract() {
  dotnet run --project AgentUp.PackageSmoke/AgentUp.PackageSmoke.csproj --configuration Release -- \
    validate-package "$platform" "$rid" "$artifact_dir" "$work_dir"

  # shellcheck disable=SC1091
  . "$work_dir/package-smoke.env"

  if [ -z "${SERVER_PATH:-}" ] || [ -z "${CLI_PATH:-}" ]; then
    echo "Package validator did not report SERVER_PATH and CLI_PATH." >&2
    exit 1
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

smoke_cli_version() {
  local cli="$1"

  chmod +x "$cli" 2>/dev/null || true
  "$cli" --version > "$work_dir/cli-version.log"
  test -s "$work_dir/cli-version.log"
}

smoke_cli_workspace_from_path() {
  local cli_name="$1"
  local repo="$work_dir/example-workspace-nix"
  mkdir -p "$repo"

  git -C "$repo" init -q
  git -C "$repo" config user.email "ci@agent-up.local"
  git -C "$repo" config user.name "Agent-Up CI"
  cat > "$repo/agent-up.json" <<'JSON'
{
  "name": "Nix Package Smoke Workspace",
  "applications": []
}
JSON
  git -C "$repo" add agent-up.json
  git -C "$repo" commit -q -m "Add nix package smoke workspace"

  (cd "$repo" && AGENTUP_SERVER_URL="http://localhost:$port" "$cli_name" start) > "$work_dir/nix-cli-start.log"
  (cd "$repo" && AGENTUP_SERVER_URL="http://localhost:$port" "$cli_name" status) > "$work_dir/nix-cli-status.log"

  grep -Fq 'Started workspace "Nix Package Smoke Workspace"' "$work_dir/nix-cli-start.log"
  grep -Fq "Name:       Nix Package Smoke Workspace" "$work_dir/nix-cli-status.log"
  grep -Fq "State:      Running" "$work_dir/nix-cli-status.log"
}

rm -rf "$work_dir"
mkdir -p "$work_dir"

case "$platform" in
  macos)
    validate_package_contract
    smoke_cli_version "$CLI_PATH"
    start_server_and_probe "$SERVER_PATH"
    smoke_cli_workspace "$CLI_PATH"
    ;;
  windows)
    dotnet run --project AgentUp.PackageSmoke/AgentUp.PackageSmoke.csproj --configuration Release -- \
      validate-package "$platform" "$rid" "$artifact_dir" "$work_dir"
    ;;
  ubuntu)
    validate_package_contract
    smoke_cli_version "$CLI_PATH"
    start_server_and_probe "$SERVER_PATH"
    smoke_cli_workspace "$CLI_PATH"
    ;;
  nixos)
    archive="$artifact_dir/agent-up-nixos-pkgs.tar.gz"
    assert_file "$archive"
    tar -xzf "$archive" -C "$work_dir"
    assert_file "$work_dir/flake.nix"
    assert_file "$work_dir/flake.lock"
    assert_executable "$work_dir/package/opt/agent-up/desktop/AgentUp.Desktop"
    assert_executable "$work_dir/package/opt/agent-up/server/AgentUp.Server"
    assert_executable "$work_dir/package/opt/agent-up/cli/AgentUp.CLI"
    assert_file "$work_dir/package/opt/agent-up/logo.png"
    assert_contains "$work_dir/flake.nix" "packageFor = pkgs: pkgs.stdenv.mkDerivation"
    assert_contains "$work_dir/flake.nix" "agent-up = packageFor pkgs"
    assert_contains "$work_dir/flake.nix" "pkgs.autoPatchelfHook"
    assert_contains "$work_dir/flake.nix" "autoPatchelfIgnoreMissingDeps"
    assert_contains "$work_dir/flake.nix" "pkgs.lttng-ust"
    assert_contains "$work_dir/flake.nix" "wrapProgram"
    assert_contains "$work_dir/flake.nix" "packages = forAllSystems"
    assert_contains "$work_dir/flake.nix" "overlays.default"
    assert_contains "$work_dir/flake.nix" "nixosModules.default"
    assert_contains "$work_dir/flake.nix" "homeManagerModules.default"
    assert_contains "$work_dir/flake.nix" "options.services.agent-up"
    assert_contains "$work_dir/flake.nix" "options.programs.agent-up.enable"
    assert_contains "$work_dir/flake.nix" "xdg.desktopEntries.agent-up"
    assert_contains "$work_dir/flake.nix" "RestartSec = 5"
    nix flake show --extra-experimental-features "nix-command flakes" "path:$work_dir"
    nix build --extra-experimental-features "nix-command flakes" --no-link --print-out-paths "path:$work_dir#agent-up" > "$work_dir/nix-out-path.txt"
    nix_out="$(cat "$work_dir/nix-out-path.txt")"
    assert_executable "$nix_out/bin/agent-up"
    assert_executable "$nix_out/bin/agent-up-server"
    assert_executable "$nix_out/bin/agent-up-desktop"
    assert_file "$nix_out/opt/agent-up/logo.png"
    smoke_cli_version "$nix_out/bin/agent-up"
    start_server_and_probe "$nix_out/bin/agent-up-server"
    nix shell --extra-experimental-features "nix-command flakes" "path:$work_dir#agent-up" --command bash -lc 'command -v agent-up && command -v agent-up-server && command -v agent-up-desktop'
    nix shell --extra-experimental-features "nix-command flakes" "path:$work_dir#agent-up" --command bash -lc "$(declare -f smoke_cli_workspace_from_path); work_dir='$work_dir'; port='$port'; smoke_cli_workspace_from_path agent-up"
    ;;
  *)
    echo "Unsupported platform: $platform" >&2
    exit 2
    ;;
esac

echo "Package smoke test passed for $platform/$rid"
