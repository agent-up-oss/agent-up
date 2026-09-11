#!/usr/bin/env bash
set -euo pipefail

VERSION="$1"

if [ -z "${DOCKERHUB_USERNAME:-}" ] || [ -z "${DOCKERHUB_TOKEN:-}" ]; then
  echo "DOCKERHUB_USERNAME and DOCKERHUB_TOKEN are required to publish agent-up-helm." >&2
  exit 1
fi

yq e ".version = \"$VERSION\" | .appVersion = \"$VERSION\"" -i chart/Chart.yaml
helm package ./chart
echo "$DOCKERHUB_TOKEN" | helm registry login registry-1.docker.io -u "$DOCKERHUB_USERNAME" --password-stdin
helm push "agent-up-helm-${VERSION}.tgz" oci://registry-1.docker.io/themassiveone
