#!/usr/bin/env bash
set -euo pipefail

# Runs the real OpenCode binary through the built-in OpenCode capability against the pinned
# in-process model, served by the test agents as an Ollama/OpenAI-compatible endpoint
# (test-model-server) that requires a bearer token. Two layers run: the loop kernel against the
# capability in process, and the full journey against a published Agent-Up Server process driven
# only over REST and the agent event stream. This is the slow ciOnly opencode-inference check; it
# shares the loop-inference job's GGUF cache and is not part of the fast Server/TestAgents suites.

root="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$root"

results="artifacts/test-results/opencode-inference"
publish="$root/artifacts/opencode-inference/test-agents"
server="$root/artifacts/opencode-inference/server"
tools="$root/artifacts/opencode-inference/opencode"
opencode_version="${AGENTUP_OPENCODE_VERSION:-1.19.6}"
mkdir -p "$results" "$publish" "$server" "$tools"

dotnet publish AgentUp.TestAgents/AgentUp.TestAgents.csproj --configuration Release --output "$publish"
dotnet publish AgentUp.Server/AgentUp.Server.csproj --configuration Release --output "$server"
npm install --prefix "$tools" --no-audit --no-fund "opencode-ai@$opencode_version"
opencode="$tools/node_modules/.bin/opencode"
"$opencode" --version

# The model server downloads and SHA-checks the pinned GGUF itself on first use, into the same
# cache the loop-inference job restores. Its first stdout line is the base URL it bound.
server_log="$results/model-server.log"
# The token is per run: the journey saves it in OpenCode's Settings and the model server refuses
# every request without it, so a passing run proves the token travelled the whole way.
api_key="opencode-inference-$RANDOM$RANDOM"
AGENTUP_TEST_AGENT=test-model-server "$publish/AgentUp.TestAgents" --port 0 --api-key "$api_key" >"$server_log" 2>&1 &
server_pid=$!
trap 'kill "$server_pid" 2>/dev/null || true' EXIT
for _ in $(seq 1 60); do
  endpoint="$(head -n 1 "$server_log" 2>/dev/null || true)"
  case "$endpoint" in http://*) break ;; esac
  sleep 1
done
case "${endpoint:-}" in
  http://*) echo "opencode-inference endpoint=$endpoint" ;;
  *) echo "test-model-server did not report its endpoint:" >&2; cat "$server_log" >&2; exit 1 ;;
esac

export AGENTUP_OPENCODE_INFERENCE_BINARY="$opencode"
export AGENTUP_OPENCODE_INFERENCE_ENDPOINT="$endpoint"
export AGENTUP_OPENCODE_INFERENCE_API_KEY="$api_key"
export AGENTUP_OPENCODE_INFERENCE_SERVER="$server/AgentUp.Server.dll"

# The published Server confines OpenCode with bubblewrap, so it has to be on its PATH, exactly as
# it is in the Server's own Nix shell. See run-agent-sandbox-e2e.sh for the AppArmor note.
if [ -e /proc/sys/kernel/apparmor_restrict_unprivileged_userns ] && command -v sudo >/dev/null; then
  sudo sysctl -w kernel.apparmor_restrict_unprivileged_userns=0
fi
bwrap_dir="$(nix-shell -p bubblewrap --run 'dirname "$(command -v bwrap)"')"
export PATH="$bwrap_dir:$PATH"

dotnet test "AgentUp.Server.Tests/AgentUp.Server.Tests.csproj" \
  --configuration Release \
  --results-directory "$results" \
  --settings "coverlet.runsettings" \
  --logger "trx;LogFileName=opencode-inference.trx" \
  --logger "console;verbosity=detailed" \
  --filter "Category=OpencodeInference" \
  --blame-hang \
  --blame-hang-timeout 20m
