#!/usr/bin/env bash
set -euo pipefail

# Drives the Server loop kernel against the locally-inferring ACP test agent on
# CPU llama.cpp. Hard invariants fail the run with zero tolerance; pooled
# complete_task outcomes are compared to loop-coverage/baselines/cpu-inference.json.
# This is the slow ciOnly loop-inference check. It is not part of the test-kind
# watchdog or the fast Server/TestAgents suites.

root="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$root"

results="artifacts/test-results/loop-inference"
publish="$root/artifacts/loop-inference/test-agents"
models="${AGENTUP_LOOP_INFERENCE_MODEL_ROOT:-$HOME/.cache/agent-up/models}"
spec_sha="6a1a2eb6d15622bf3c96857206351ba97e1af16c30d7a74ee38970e434e9407e"
spec_file="qwen2.5-1.5b-instruct-q4_k_m.gguf"
spec_url="https://huggingface.co/Qwen/Qwen2.5-1.5B-Instruct-GGUF/resolve/main/qwen2.5-1.5b-instruct-q4_k_m.gguf"

mkdir -p "$results" "$publish" "$models"

dotnet publish AgentUp.TestAgents/AgentUp.TestAgents.csproj \
  --configuration Release \
  --output "$publish"

agent="$publish/AgentUp.TestAgents"
if [ ! -x "$agent" ]; then
  echo "Loop-inference publish did not produce an executable at $agent" >&2
  exit 1
fi

cached="$models/$spec_file"
hash_file() {
  sha256sum "$1" | awk '{print $1}'
}

if [ -f /tmp/models/"$spec_file" ] && [ ! -f "$cached" ]; then
  cp /tmp/models/"$spec_file" "$cached"
fi

if [ -f "$cached" ]; then
  actual="$(hash_file "$cached")"
  if [ "$actual" != "$spec_sha" ]; then
    echo "Cached GGUF hashed $actual, expected $spec_sha; removing." >&2
    rm -f "$cached"
  fi
fi

if [ ! -f "$cached" ]; then
  echo "Downloading pinned GGUF to $cached"
  curl -L --fail --retry 3 --retry-delay 2 -o "$cached.partial" "$spec_url"
  mv "$cached.partial" "$cached"
  actual="$(hash_file "$cached")"
  if [ "$actual" != "$spec_sha" ]; then
    rm -f "$cached"
    echo "Downloaded GGUF hashed $actual, expected $spec_sha." >&2
    exit 1
  fi
fi

export AGENTUP_LOOP_INFERENCE_AGENT="$agent"
export AGENTUP_LOOP_INFERENCE_MODEL_ROOT="$models"
export AGENTUP_LOOP_INFERENCE_SEED="${AGENTUP_LOOP_INFERENCE_SEED:-42}"
export AGENTUP_TEST_AGENT="test-agent-infer"

echo "loop-inference seed=${AGENTUP_LOOP_INFERENCE_SEED}"
echo "loop-inference agent=${AGENTUP_LOOP_INFERENCE_AGENT}"
echo "loop-inference model-root=${AGENTUP_LOOP_INFERENCE_MODEL_ROOT}"

dotnet test "AgentUp.Server.Tests/AgentUp.Server.Tests.csproj" \
  --configuration Release \
  --results-directory "$results" \
  --settings "coverlet.runsettings" \
  --logger "trx;LogFileName=loop-inference.trx" \
  --logger "console;verbosity=detailed" \
  --filter "Category=LoopInference" \
  --blame-crash \
  --blame-hang \
  --blame-hang-timeout 25m
