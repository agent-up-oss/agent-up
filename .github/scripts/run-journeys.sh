#!/usr/bin/env bash
# Replays every QA flow of one client surface against an isolated real Server, through
# au-debug validate. The flows are the first suite on docs/developer-guide/verification/qa-coverage.md:
# Desktop flows live in .agent-up/validation-flows.json, Mobile flows in AgentUp.Mobile.E2E/pwa/flows.
#
# One flow per invocation. Each one then starts from a fresh Server data directory, a stuck flow
# spends its own au-debug budget rather than the suite's, and the report names the flow that broke.
# Every flow runs even after one fails, so a run shows every journey that drifted, not the first.
#
#   ./.github/scripts/run-journeys.sh desktop|mobile
set -uo pipefail

surface="${1:-}"
root="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$root"

case "$surface" in
  desktop)
    flows="$(node -e '
      const flows = require("./.agent-up/validation-flows.json").flows;
      console.log(flows.filter(flow => flow.surface === "Desktop").map(flow => flow.id).join(" "));')"
    out="artifacts/desktop-validation"
    ;;
  mobile)
    flows="$(node -e '
      const { readdirSync, readFileSync } = require("node:fs");
      const dir = "AgentUp.Mobile.E2E/pwa/flows";
      console.log(readdirSync(dir).filter(name => name.endsWith(".json"))
        .map(name => JSON.parse(readFileSync(`${dir}/${name}`, "utf8")).id).sort().join(" "));')"
    out="artifacts/mobile-validation"
    ;;
  *)
    echo "Usage: $0 desktop|mobile" >&2
    exit 2
    ;;
esac

if [ -z "$flows" ]; then
  echo "No $surface flows found. A surface with no journeys must be a declared gap, not a green job." >&2
  exit 1
fi

set -e
echo "Building what au-debug validate starts"
dotnet build AgentUp.AUDebug/AgentUp.AUDebug.csproj
dotnet build AgentUp.Server/AgentUp.Server.csproj
# The fixture puts test-agent-scripted on the isolated Server's PATH from this build output.
dotnet build AgentUp.TestAgents/AgentUp.TestAgents.csproj
if [ "$surface" = desktop ]; then
  dotnet build AgentUp.Desktop/AgentUp.Desktop.csproj
else
  ./.github/scripts/install-mobile-deps.sh AgentUp.Mobile
  (cd AgentUp.Mobile && node scripts/export-web.mjs)
  npm --prefix AgentUp.Mobile.E2E install --no-audit --no-fund
  (cd AgentUp.Mobile.E2E && npx playwright install --with-deps chromium)
fi
set +e

rm -rf "$out"
failed=()
for flow in $flows; do
  echo "::group::$surface journey $flow"
  if [ "$surface" = desktop ]; then
    LIBGL_ALWAYS_SOFTWARE=1 \
      GALLIUM_DRIVER=llvmpipe \
      WEBKIT_DISABLE_COMPOSITING_MODE=1 \
      WEBKIT_DISABLE_DMABUF_RENDERER=1 \
      WEBKIT_DISABLE_SANDBOX_THIS_IS_DANGEROUS=1 \
      dbus-run-session xvfb-run -a \
      --server-args="-screen 0 1440x900x24 -ac +extension GLX +render -noreset" \
      ./au-debug validate --surface desktop --flow "$flow" --timeout 600
  else
    ./au-debug validate --surface mobile --flow "$flow" --timeout 600
  fi
  status=$?
  echo "::endgroup::"
  if [ "$status" -ne 0 ]; then
    echo "::error title=$surface journey $flow::au-debug validate exited $status; see $out/$flow"
    failed+=("$flow")
  fi
done

node - "$surface" "$out" $flows <<'NODE'
const { existsSync, readFileSync, appendFileSync } = require('node:fs');
const [surface, out, ...flows] = process.argv.slice(2);
const lines = [`### ${surface === 'desktop' ? 'Desktop' : 'Mobile'} journeys on a real Server`, '', '| | Flow | Steps | First failing step |', '|---|---|---|---|'];
for (const flow of flows) {
  const file = `${out}/${flow}/report.json`;
  if (!existsSync(file)) { lines.push(`| ⚠️ | ${flow} | | no report |`); continue; }
  const report = JSON.parse(readFileSync(file, 'utf8'));
  const steps = report.steps ?? [];
  const failing = steps.find(step => step.passed === false);
  lines.push(`| ${report.passed ? '✅' : '❌'} | ${flow} | ${steps.filter(step => step.passed).length}/${steps.length} | ${failing ? failing.id : ''} |`);
}
const text = `${lines.join('\n')}\n`;
process.stdout.write(text);
if (process.env.GITHUB_STEP_SUMMARY) appendFileSync(process.env.GITHUB_STEP_SUMMARY, text);
NODE

if [ "${#failed[@]}" -gt 0 ]; then
  echo "Failed $surface journeys: ${failed[*]}" >&2
  exit 1
fi
