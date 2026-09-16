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
  agentup_browser_streaming_tests:AgentUp.Browser.Streaming.Tests
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
  agentup_audebug_tests:AgentUp.AUDebug.Tests
  agentup_installer_config_tests:AgentUp.InstallerConfig.Tests
  agentup_server_tests:AgentUp.Server.Tests
  agentup_tests:AgentUp.Tests
  agentup_tray_tests:AgentUp.Tray.Tests
  agentup_verification_tests:AgentUp.Verification.Tests
)

wanted=("$@")

# A requested project missing from all_projects used to be skipped in silence, which set
# no output. The Codecov step then ran with an empty "files" input and, because it is
# configured with fail_ci_if_error, failed the job with "Found 0 coverage files" - a long
# way from the actual mistake. Fail here instead, naming it.
unknown=()
for name in "${wanted[@]}"; do
  known=1
  for spec in "${all_projects[@]}"; do
    if [ "$name" = "${spec##*:}" ]; then
      known=0
      break
    fi
  done
  if [ "$known" -eq 1 ]; then
    unknown+=("$name")
  fi
done
if [ "${#unknown[@]}" -gt 0 ]; then
  echo "Unknown test project(s): ${unknown[*]}" >&2
  echo "Add them to all_projects in $0 with the output name the workflow reads." >&2
  exit 1
fi

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
