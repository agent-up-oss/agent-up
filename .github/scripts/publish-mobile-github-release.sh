#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -lt 6 ]; then
  echo "Usage: $0 <android|ios> <version> <version-code> <channel> <source-tag> <file> [file...]" >&2
  exit 2
fi

platform="$1"
version="$2"
version_code="$3"
channel="$4"
source_tag="$5"
shift 5

case "$platform" in
  android|ios) ;;
  *)
    echo "Platform must be android or ios" >&2
    exit 2
    ;;
esac

if [ -z "${GITHUB_SHA:-}" ] || [ -z "${GITHUB_SERVER_URL:-}" ] || [ -z "${GITHUB_REPOSITORY:-}" ] || [ -z "${GITHUB_RUN_ID:-}" ]; then
  echo "GitHub Actions environment is incomplete" >&2
  exit 1
fi

for file in "$@"; do
  if [ ! -s "$file" ]; then
    echo "Missing release asset: $file" >&2
    exit 1
  fi
done

tag="${platform}-v${version}"
title="${platform} v${version}"
notes="$(cat <<EOF
Channel: ${channel}
Product version: ${version} (from ${source_tag})
Store build: ${version_code}
Workflow: ${GITHUB_SERVER_URL}/${GITHUB_REPOSITORY}/actions/runs/${GITHUB_RUN_ID}
EOF
)"

if gh release view "$tag" >/dev/null 2>&1; then
  gh release upload "$tag" --clobber "$@"
  gh release edit "$tag" --title "$title" --notes "$notes"
else
  gh release create "$tag" "$@" --title "$title" --notes "$notes" --target "$GITHUB_SHA"
fi
