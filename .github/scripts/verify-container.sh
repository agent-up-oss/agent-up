#!/usr/bin/env bash
set -euo pipefail

COMPONENT="$1"
REPOSITORY="$2"

IMAGE_BASE="docker.io/themassiveone/${REPOSITORY}"

if [ -z "${DOCKERHUB_USERNAME:-}" ] || [ -z "${DOCKERHUB_TOKEN:-}" ]; then
  echo "DOCKERHUB_USERNAME and DOCKERHUB_TOKEN are required to publish ${IMAGE_BASE}." >&2
  exit 1
fi

docker build . \
  -t "${IMAGE_BASE}:test" \
  -f "./${COMPONENT}/Dockerfile"

docker run --rm --user 1654 --entrypoint /bin/sh "${IMAGE_BASE}:test" -c '
  set -eu
  git --version >/dev/null
  test -x /opt/agent-up/bin/codex-acp
  test -x /opt/agent-up/bin/agent
  test -x /opt/agent-up/bin/claude-agent-acp
  /opt/agent-up/bin/codex-acp --version >/dev/null
  /opt/agent-up/bin/agent --version >/dev/null
  /opt/agent-up/bin/claude-agent-acp --version >/dev/null
  test -f /etc/agent-up/capabilities.json
'
