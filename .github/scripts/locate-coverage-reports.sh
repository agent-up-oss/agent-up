#!/usr/bin/env bash
set -euo pipefail

# Writes one GITHUB_OUTPUT entry per requested test project with a comma-separated
# list of Cobertura reports. With no arguments, locates every known test project.

add_reports() {
  local output_name="$1"
  local test_project_name="$2"
  local search_root="artifacts/test-results/$test_project_name"
  local reports

  reports="$(find "$search_root" -type f -name 'coverage.cobertura.xml' -print | sort | paste -sd, -)"
  if [ -z "$reports" ]; then
    echo "No Cobertura coverage reports found for $test_project_name under $search_root" >&2
    return 1
  fi

  echo "$output_name=$reports" >> "$GITHUB_OUTPUT"
}

all_projects=(
  agentup_architecture_tests:AgentUp.Architecture.Tests
  agentup_cli_tests:AgentUp.CLI.Tests
  agentup_capabilities_abstractions_tests:AgentUp.Capabilities.Abstractions.Tests
  agentup_capabilities_common_tests:AgentUp.Capabilities.Common.Tests
  agentup_capabilities_claude_tests:AgentUp.Capabilities.Claude.Tests
  agentup_capabilities_codex_tests:AgentUp.Capabilities.Codex.Tests
  agentup_capabilities_cursor_tests:AgentUp.Capabilities.Cursor.Tests
  agentup_capabilities_docker_tests:AgentUp.Capabilities.Docker.Tests
  agentup_capabilities_dotnet_tests:AgentUp.Capabilities.Dotnet.Tests
  agentup_commit_policy_tests:AgentUp.CommitPolicy.Tests
  agentup_desktop_tests:AgentUp.Desktop.Tests
  agentup_server_tests:AgentUp.Server.Tests
  agentup_tests:AgentUp.Tests
  agentup_verification_tests:AgentUp.Verification.Tests
)

wanted=("$@")
for spec in "${all_projects[@]}"; do
  output_name="${spec%%:*}"
  test_project_name="${spec##*:}"
  if [ "${#wanted[@]}" -gt 0 ]; then
    skip=1
    for name in "${wanted[@]}"; do
      if [ "$name" = "$test_project_name" ]; then
        skip=0
        break
      fi
    done
    if [ "$skip" -eq 1 ]; then
      continue
    fi
  fi
  add_reports "$output_name" "$test_project_name"
done
