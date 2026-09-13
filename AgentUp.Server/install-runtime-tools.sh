#!/usr/bin/env bash
set -euo pipefail

# Runtime tools for the Helm Server image: git for source clones and Git review,
# plus the Codex, Cursor, and Claude ACP CLIs the workspace agent picker launches.
manifest_dir="${1:?runtime tools directory is required}"
# shellcheck disable=SC1091
source "$manifest_dir/artifacts.env"

export DEBIAN_FRONTEND=noninteractive

apt-get update
apt-get install -y --no-install-recommends \
  ca-certificates \
  curl \
  git \
  openssh-client
rm -rf /var/lib/apt/lists/*

os="$(uname -s)"
arch="$(uname -m)"
case "$os" in Linux*) OS=linux ;; Darwin*) OS=darwin ;; *) OS="" ;; esac
case "$arch" in x86_64|amd64) ARCH=x64 ;; arm64|aarch64) ARCH=arm64 ;; *) ARCH="" ;; esac
if [ -z "$OS" ] || [ -z "$ARCH" ]; then
  echo "Unsupported platform for bundled CLIs: $os/$arch" >&2
  exit 1
fi

sha_var="NODE_SHA256_${OS}_${ARCH}"
eval "node_sha=\"\${${sha_var}-}\""
if [ -z "$node_sha" ]; then
  echo "No pinned Node checksum for ${OS}/${ARCH}." >&2
  exit 1
fi

# Claude ACP requires Node >= 22; Ubuntu 24.04 only ships Node 18.
node_archive="$(mktemp)"
curl -fsSL "https://nodejs.org/dist/${NODE_VERSION}/node-${NODE_VERSION}-linux-${ARCH}.tar.gz" -o "$node_archive"
echo "${node_sha}  ${node_archive}" | sha256sum -c -
tar -xz -C /usr/local --strip-components=1 -f "$node_archive"
rm -f "$node_archive"
hash -r
node --version
npm --version

prefix=/opt/agent-up
mkdir -p "$prefix/bin" "$prefix/npm" "$prefix/cursor-agent"
cp "$manifest_dir/package.json" "$manifest_dir/package-lock.json" "$prefix/npm/"
npm ci --omit=dev --prefix "$prefix/npm"

ln -sfn "$prefix/npm/node_modules/.bin/codex-acp" "$prefix/bin/codex-acp"
ln -sfn "$prefix/npm/node_modules/.bin/claude-agent-acp" "$prefix/bin/claude-agent-acp"

sha_var="CURSOR_SHA256_${OS}_${ARCH}"
eval "cursor_sha=\"\${${sha_var}-}\""
if [ -z "$cursor_sha" ]; then
  echo "No pinned Cursor Agent CLI checksum for ${OS}/${ARCH}." >&2
  exit 1
fi

cursor_archive="$(mktemp)"
curl -fsSL "https://downloads.cursor.com/lab/${CURSOR_VERSION}/${OS}/${ARCH}/agent-cli-package.tar.gz" -o "$cursor_archive"
echo "${cursor_sha}  ${cursor_archive}" | sha256sum -c -
tar --strip-components=1 -xzf "$cursor_archive" -C "$prefix/cursor-agent"
rm -f "$cursor_archive"
if [ ! -x "$prefix/cursor-agent/cursor-agent" ]; then
  echo "Cursor Agent CLI archive did not contain an executable cursor-agent binary." >&2
  exit 1
fi
ln -sfn "$prefix/cursor-agent/cursor-agent" "$prefix/bin/agent"

chmod -R a+rX "$prefix"

mkdir -p /etc/agent-up
cat > /etc/agent-up/capabilities.json <<'EOF'
[
  { "id": "codex", "versions": ["bundled"], "command": "/opt/agent-up/bin/codex-acp", "arguments": [] },
  { "id": "cursor", "versions": ["bundled"], "command": "/opt/agent-up/bin/agent", "arguments": ["acp"] },
  { "id": "claude", "versions": ["bundled"], "command": "/opt/agent-up/bin/claude-agent-acp", "arguments": [] }
]
EOF
