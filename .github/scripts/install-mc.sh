#!/usr/bin/env bash
set -euo pipefail

bin_dir="$RUNNER_TEMP/mc-bin"
mkdir -p "$bin_dir"

case "${RUNNER_OS:-Linux}" in
  Linux)
    url="https://dl.min.io/client/mc/release/linux-amd64/mc"
    output="$bin_dir/mc"
    ;;
  macOS)
    case "${RUNNER_ARCH:-$(uname -m)}" in
      ARM64|arm64)
        url="https://dl.min.io/client/mc/release/darwin-arm64/mc"
        ;;
      X64|x64|x86_64)
        url="https://dl.min.io/client/mc/release/darwin-amd64/mc"
        ;;
      *)
        echo "Unsupported macOS runner architecture: ${RUNNER_ARCH:-$(uname -m)}" >&2
        exit 1
        ;;
    esac
    output="$bin_dir/mc"
    ;;
  Windows)
    url="https://dl.min.io/client/mc/release/windows-amd64/mc.exe"
    output="$bin_dir/mc.exe"
    ;;
  *)
    echo "Unsupported runner OS: ${RUNNER_OS:-unknown}" >&2
    exit 1
    ;;
esac

curl -fsSL "$url" -o "$output"
chmod +x "$output" 2>/dev/null || true
echo "$bin_dir" >> "$GITHUB_PATH"
