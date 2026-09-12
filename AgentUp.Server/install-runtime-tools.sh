#!/usr/bin/env bash
set -euo pipefail

# Runtime tools for the Helm Server image: git for source clones and Git review,
# plus the Codex, Cursor, and Claude ACP CLIs the workspace agent picker launches.
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

# Claude ACP requires Node >= 22; Ubuntu 24.04 only ships Node 18.
node_version=v22.23.2
curl -fsSL "https://nodejs.org/dist/${node_version}/node-${node_version}-linux-${ARCH}.tar.gz" \
  | tar -xz -C /usr/local --strip-components=1
hash -r
node --version
npm --version

prefix=/opt/agent-up
mkdir -p "$prefix/bin" "$prefix/npm" "$prefix/cursor-agent"

npm install --omit=dev --prefix "$prefix/npm" \
  @agentclientprotocol/codex-acp \
  @agentclientprotocol/claude-agent-acp

ln -sfn "$prefix/npm/node_modules/.bin/codex-acp" "$prefix/bin/codex-acp"
ln -sfn "$prefix/npm/node_modules/.bin/claude-agent-acp" "$prefix/bin/claude-agent-acp"

download_url="$(curl -fsSL -A 'curl/8.5.0' https://cursor.com/install \
  | grep -E '^[[:space:]]*DOWNLOAD_URL=' | head -n1 | cut -d= -f2- | tr -d '"')"
download_url="${download_url//\$\{OS\}/${OS}}"
download_url="${download_url//\$\{ARCH\}/${ARCH}}"
case "$download_url" in
  https://*) ;;
  *) download_url="" ;;
esac
if printf '%s' "$download_url" | grep -Eq '[[:space:];|&`$()]'; then
  download_url=""
fi
if [ -z "$download_url" ]; then
  echo "Could not resolve a Cursor Agent CLI download URL." >&2
  exit 1
fi

curl -fsSL "$download_url" | tar --strip-components=1 -xzf - -C "$prefix/cursor-agent"
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
