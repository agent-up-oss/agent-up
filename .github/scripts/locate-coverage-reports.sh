#!/usr/bin/env bash
set -euo pipefail

# Writes one GITHUB_OUTPUT entry per test project with a comma-separated list of
# Cobertura reports. Missing reports fail the coverage job instead of uploading empty flags.

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

add_reports agentup_architecture_tests AgentUp.Architecture.Tests
add_reports agentup_cli_tests AgentUp.CLI.Tests
add_reports agentup_capabilities_abstractions_tests AgentUp.Capabilities.Abstractions.Tests
add_reports agentup_capabilities_common_tests AgentUp.Capabilities.Common.Tests
add_reports agentup_capabilities_claude_tests AgentUp.Capabilities.Claude.Tests
add_reports agentup_capabilities_codex_tests AgentUp.Capabilities.Codex.Tests
add_reports agentup_capabilities_cursor_tests AgentUp.Capabilities.Cursor.Tests
add_reports agentup_capabilities_docker_tests AgentUp.Capabilities.Docker.Tests
add_reports agentup_capabilities_dotnet_tests AgentUp.Capabilities.Dotnet.Tests
add_reports agentup_commit_policy_tests AgentUp.CommitPolicy.Tests
add_reports agentup_desktop_tests AgentUp.Desktop.Tests
add_reports agentup_server_tests AgentUp.Server.Tests
add_reports agentup_tests AgentUp.Tests
add_reports agentup_verification_tests AgentUp.Verification.Tests
