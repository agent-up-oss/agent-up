#!/usr/bin/env bash
set -euo pipefail

COMPONENT="$1"
REPOSITORY="$2"
VERSION="$3"

IMAGE_BASE="docker.io/themassiveone/${REPOSITORY}"

if [ -z "${DOCKERHUB_USERNAME:-}" ] || [ -z "${DOCKERHUB_TOKEN:-}" ]; then
  echo "DOCKERHUB_USERNAME and DOCKERHUB_TOKEN are required to publish ${IMAGE_BASE}." >&2
  exit 1
fi

echo "$DOCKERHUB_TOKEN" | docker login -u "$DOCKERHUB_USERNAME" --password-stdin

docker build . \
  -t "${IMAGE_BASE}:${VERSION}" \
  -f "./${COMPONENT}/Dockerfile"

docker push "${IMAGE_BASE}:${VERSION}"

if [ "${AGENTUP_PUBLISH_LATEST:-1}" = "1" ]; then
  docker tag "${IMAGE_BASE}:${VERSION}" "${IMAGE_BASE}:latest"
  docker push "${IMAGE_BASE}:latest"
fi
