#!/usr/bin/env bash
# Publishes the Server and the test agents that the mobile end-to-end suites drive.
#
# Each mobile job builds these itself rather than taking an artifact from the .NET chain. That
# looks wasteful but is not: this publish runs while the job is already busy provisioning an
# emulator or an Xcode toolchain, whereas an artifact dependency would serialise the whole mobile
# suite behind the slowest jobs in the pipeline.
set -euo pipefail

out="${1:-artifacts/mobile-e2e}"
mkdir -p "$out"

dotnet publish AgentUp.Server/AgentUp.Server.csproj \
  --configuration Release \
  --output "$out/server"

dotnet publish AgentUp.TestAgents/AgentUp.TestAgents.csproj \
  --configuration Release \
  --output "$out/test-agents"

server_dll="$(cd "$out/server" && pwd)/AgentUp.Server.dll"
test_agent="$(cd "$out/test-agents" && pwd)/AgentUp.TestAgents"

if [ ! -f "$server_dll" ]; then
  echo "Server publish did not produce $server_dll" >&2
  exit 1
fi
if [ ! -x "$test_agent" ]; then
  echo "Test agent publish did not produce an executable at $test_agent" >&2
  exit 1
fi

{
  echo "AGENTUP_E2E_SERVER_DLL=$server_dll"
  echo "AGENTUP_E2E_TEST_AGENT=$test_agent"
} >> "${GITHUB_ENV:-/dev/stdout}"

echo "Server:      $server_dll"
echo "Test agents: $test_agent"
