#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -ne 1 ]; then
  echo "Usage: $0 <version>" >&2
  exit 2
fi

version="$1"
artifact_dir="release-artifacts"
tag="v${version}"

if [ -z "${GITHUB_SHA:-}" ] || [ -z "${GITHUB_SERVER_URL:-}" ] || [ -z "${GITHUB_REPOSITORY:-}" ] || [ -z "${GITHUB_RUN_ID:-}" ]; then
  echo "GitHub Actions environment is incomplete" >&2
  exit 1
fi

required_artifacts=(
  "agent-up-macos-osx-arm64.pkg"
  "agent-up-macos-osx-x64.pkg"
  "agent-up-nixos-pkgs.tar.gz"
  "agent-up-ubuntu-linux-x64.deb"
  "agent-up-windows-win-x64.exe"
  "agent-up-windows-win-x64.msi"
  "agent-up-sbom.cdx.json"
  "au-check-linux-x64"
  "au-check-linux-arm64"
  "au-check-osx-arm64"
  "au-check-osx-x64"
  "au-check-win-x64.exe"
  "checksums.sha256"
  "manifest.json"
)

assets=()
for artifact in "${required_artifacts[@]}"; do
  path="$artifact_dir/$artifact"
  if [ ! -s "$path" ]; then
    echo "Missing expected prerelease asset: $path" >&2
    exit 1
  fi
  assets+=("$path")
done

for optional in "agent-up-ubuntu-linux-x64.deb.asc"; do
  path="$artifact_dir/$optional"
  if [ -s "$path" ]; then
    assets+=("$path")
  fi
done

notes="$(cat <<EOF
CI prerelease of ${version} from ${GITHUB_REF_NAME:-unknown} (${GITHUB_SHA}).
Workflow: ${GITHUB_SERVER_URL}/${GITHUB_REPOSITORY}/actions/runs/${GITHUB_RUN_ID}
EOF
)"

gh release create "$tag" "${assets[@]}" \
  --prerelease \
  --latest=false \
  --target "$GITHUB_SHA" \
  --title "$tag" \
  --notes "$notes"
