#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -ne 1 ]; then
  echo "Usage: $0 <version|--check-only>" >&2
  exit 2
fi

version="$1"
prefix="agent-up"
artifact_dir="release-artifacts"
check_only=false

if [ "$version" = "--check-only" ]; then
  check_only=true
fi

if ! compgen -G "$artifact_dir/*" > /dev/null; then
  echo "No artifacts found in $artifact_dir" >&2
  exit 1
fi

expected_artifacts=(
  "agent-up-macos-osx-arm64.pkg"
  "agent-up-macos-osx-x64.pkg"
  "agent-up-nixos-pkgs.tar.gz"
  "agent-up-ubuntu-linux-x64.deb"
  "agent-up-windows-win-x64.exe"
)

for artifact in "${expected_artifacts[@]}"; do
  if [ ! -s "$artifact_dir/$artifact" ]; then
    echo "Missing expected release artifact: $artifact_dir/$artifact" >&2
    find "$artifact_dir" -maxdepth 2 -type f -print >&2
    exit 1
  fi
done

echo "Validated release artifacts:"
for artifact in "${expected_artifacts[@]}"; do
  stat -c "  %n (%s bytes)" "$artifact_dir/$artifact" 2>/dev/null \
    || stat -f "  %N (%z bytes)" "$artifact_dir/$artifact"
done

if [ "$check_only" = true ]; then
  exit 0
fi

manifest="$artifact_dir/manifest.json"
checksum_file="$artifact_dir/checksums.sha256"
(
  cd "$artifact_dir"
  printf "" > "$(basename "$checksum_file")"
  for artifact in "${expected_artifacts[@]}"; do
    sha256sum "$artifact" >> "$(basename "$checksum_file")"
  done
)

cat > "$manifest" <<JSON
{
  "version": "$version",
  "commit": "${GITHUB_SHA:-unknown}",
  "artifacts": [
    "agent-up-macos-osx-arm64.pkg",
    "agent-up-macos-osx-x64.pkg",
    "agent-up-nixos-pkgs.tar.gz",
    "agent-up-ubuntu-linux-x64.deb",
    "agent-up-windows-win-x64.exe"
  ],
  "checksums": "checksums.sha256"
}
JSON

release_artifacts=("${expected_artifacts[@]}" "manifest.json" "checksums.sha256")

bucket="${AGENTUP_RELEASE_BUCKET:?AGENTUP_RELEASE_BUCKET is required}"
endpoint="${AGENTUP_RELEASE_S3_ENDPOINT:?AGENTUP_RELEASE_S3_ENDPOINT is required}"
access_key="${AGENTUP_RELEASE_S3_ACCESS_KEY:?AGENTUP_RELEASE_S3_ACCESS_KEY is required}"
secret_key="${AGENTUP_RELEASE_S3_SECRET_KEY:?AGENTUP_RELEASE_S3_SECRET_KEY is required}"
alias_name="agentup-release"

mc alias set "$alias_name" "$endpoint" "$access_key" "$secret_key"
mc mb --ignore-existing "$alias_name/$bucket"

echo "Publishing release artifacts to $alias_name/$bucket/$prefix/releases/$version"
for artifact in "${release_artifacts[@]}"; do
  destination="$alias_name/$bucket/$prefix/releases/$version/$artifact"
  echo "Uploading $artifact_dir/$artifact -> $destination"
  mc cp "$artifact_dir/$artifact" "$destination"
done

echo "Publishing release artifacts to $alias_name/$bucket/$prefix/latest"
for artifact in "${release_artifacts[@]}"; do
  destination="$alias_name/$bucket/$prefix/latest/$artifact"
  echo "Uploading $artifact_dir/$artifact -> $destination"
  mc cp "$artifact_dir/$artifact" "$destination"
done
