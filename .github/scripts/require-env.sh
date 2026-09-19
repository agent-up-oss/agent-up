#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -lt 1 ]; then
  echo "Usage: $0 NAME [NAME...]" >&2
  exit 2
fi

missing=0
for name in "$@"; do
  if [ -z "${!name:-}" ]; then
    echo "Missing required environment value: $name" >&2
    missing=1
  fi
done

if [ "$missing" -ne 0 ]; then
  exit 1
fi
