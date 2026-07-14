#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -ne 1 ]; then
  echo "Usage: $0 <version>" >&2
  exit 2
fi

version="$1"
bucket="${AGENTUP_RELEASE_BUCKET:?AGENTUP_RELEASE_BUCKET is required}"
prefix="${AGENTUP_RELEASE_PREFIX:-agent-up}"
artifact_dir="${AGENTUP_ARTIFACT_DIR:-release-artifacts}"
download_base="${AGENTUP_DOWNLOAD_BASE_URL:?AGENTUP_DOWNLOAD_BASE_URL is required}"
endpoint="${AGENTUP_RELEASE_S3_ENDPOINT:?AGENTUP_RELEASE_S3_ENDPOINT is required}"
access_key="${AGENTUP_RELEASE_S3_ACCESS_KEY:?AGENTUP_RELEASE_S3_ACCESS_KEY is required}"
secret_key="${AGENTUP_RELEASE_S3_SECRET_KEY:?AGENTUP_RELEASE_S3_SECRET_KEY is required}"
alias_name="${AGENTUP_RELEASE_S3_ALIAS:-agentup-release}"

if ! compgen -G "$artifact_dir/*" > /dev/null; then
  echo "No artifacts found in $artifact_dir" >&2
  exit 1
fi

manifest="$artifact_dir/manifest.json"
{
  echo "{"
  echo "  \"version\": \"$version\","
  echo "  \"artifacts\": ["
  first=true
  for file in "$artifact_dir"/*; do
    name="$(basename "$file")"
    [ "$name" = "manifest.json" ] && continue
    if [ "$first" = true ]; then
      first=false
    else
      echo ","
    fi
    printf '    { "name": "%s", "url": "%s/releases/%s/%s" }' "$name" "$download_base" "$version" "$name"
  done
  echo
  echo "  ]"
  echo "}"
} > "$manifest"

mc alias set "$alias_name" "$endpoint" "$access_key" "$secret_key"
mc mb --ignore-existing "$alias_name/$bucket"
mc mirror --overwrite --remove "$artifact_dir" "$alias_name/$bucket/$prefix/releases/$version"
mc mirror --overwrite --remove "$artifact_dir" "$alias_name/$bucket/$prefix/latest"
