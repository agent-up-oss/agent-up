#!/usr/bin/env bash
set -euo pipefail

yq e ".version = \"0.0.0\" | .appVersion = \"0.0.0\"" -i chart/Chart.yaml
helm lint ./chart
helm package ./chart
