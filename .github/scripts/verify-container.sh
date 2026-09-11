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
