#!/usr/bin/env bash
# Holds one implementation of the client contract to AgentUp.FakeServer/parity.json.
#
#   server        a published Server process with a registered probe workspace, over HTTP
#   demo-desktop  Desktop's in-process Demo (DemoParityTests)
#   demo-mobile   Mobile's in-process Demo (FakeBackendParity.test.ts)
#
# After a deliberate Server change, re-record its answers and then decide, per changed read,
# whether the Demo answers it too:
#   ./.github/scripts/run-backend-parity.sh server --record
set -euo pipefail

leg="${1:-}"
if [ "$#" -gt 0 ]; then shift; fi
root="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$root"

case "$leg" in
  server)
    node .github/scripts/backend-parity.mjs check

    work="$root/artifacts/backend-parity"
    rm -rf "$work"
    mkdir -p "$work/data"
    dotnet publish AgentUp.Server/AgentUp.Server.csproj --configuration Release --output "$work/server"

    # The probe workspace: a repository with one commit, one untracked file so a diff exists to
    # read, and one application, the fixture parity.json names for the Server.
    repo="$work/workspace"
    mkdir -p "$repo"
    git -C "$repo" init -q
    git -C "$repo" config user.email "ci@agent-up.invalid"
    git -C "$repo" config user.name "Agent-Up CI"
    printf '# probe\n' > "$repo/README.md"
    git -C "$repo" add README.md
    git -C "$repo" commit -q -m "probe"
    printf 'probe\n' > "$repo/probe.txt"

    port="$((45000 + (RANDOM % 10000)))"
    url="http://127.0.0.1:$port"
    AGENTUP_AUTH_DISABLED=true \
      AGENTUP_SKIP_DEV_AGENT_BOOTSTRAP=1 \
      Storage__DataDirectory="$work/data" \
      ASPNETCORE_URLS="$url" \
      dotnet "$work/server/AgentUp.Server.dll" > "$work/server.log" 2>&1 &
    server_pid=$!
    trap 'kill "$server_pid" 2>/dev/null || true' EXIT

    ready=false
    for _ in $(seq 1 90); do
      if curl -fsS "$url/api/connection" > /dev/null 2>&1; then ready=true; break; fi
      if ! kill -0 "$server_pid" 2>/dev/null; then break; fi
      sleep 1
    done
    if [ "$ready" != true ]; then
      echo "The published Server did not answer $url/api/connection." >&2
      cat "$work/server.log" >&2
      exit 1
    fi

    commit="$(git -C "$repo" rev-parse HEAD)"
    branch="$(git -C "$repo" rev-parse --abbrev-ref HEAD)"
    registration="$(node -e '
      const [repo, branch, commit] = process.argv.slice(1);
      process.stdout.write(JSON.stringify({ displayName: "Parity probe", repositoryPath: repo, worktreePath: repo,
        branch, commit, applications: [{ name: "Web", command: "node --version" }] }));' "$repo" "$branch" "$commit")"
    workspace="$(curl -fsS -X POST "$url/api/workspaces" -H 'content-type: application/json' -d "$registration" \
      | node -e 'let text = ""; process.stdin.on("data", chunk => text += chunk).on("end", () => process.stdout.write(JSON.parse(text).id));')"

    node .github/scripts/backend-parity.mjs server --url "$url" --workspace "$workspace" \
      --application Web --changed-file probe.txt "$@"
    ;;
  demo-desktop)
    dotnet test AgentUp.Desktop.Tests/AgentUp.Desktop.Tests.csproj \
      --configuration Release \
      --filter "Category=DemoParity" \
      --logger "console;verbosity=normal"
    ;;
  demo-mobile)
    ./.github/scripts/install-mobile-deps.sh AgentUp.Mobile
    (cd AgentUp.Mobile && npx tsx --test src/features/fake-server/services/FakeBackendParity.test.ts)
    ;;
  *)
    echo "Usage: $0 server|demo-desktop|demo-mobile [--record]" >&2
    exit 2
    ;;
esac
