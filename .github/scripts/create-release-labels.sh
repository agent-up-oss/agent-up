#!/usr/bin/env bash
# Creates the release labels required for customer-facing release notes.
# Run once after cloning or when setting up the repository.
set -euo pipefail

gh label create 'release-note' \
  --description 'Include this pull request in customer-facing release notes' \
  --color '0075ca' \
  --force

gh label create 'release:feature' \
  --description 'New customer-facing functionality' \
  --color '7057ff' \
  --force

gh label create 'release:fix' \
  --description 'Corrected customer-visible behavior' \
  --color 'd73a4a' \
  --force

gh label create 'release:improvement' \
  --description 'Improved existing customer-facing functionality' \
  --color '0052cc' \
  --force

gh label create 'release:breaking' \
  --description 'Requires customer action to migrate' \
  --color 'e4e669' \
  --force

echo "Release labels created."
