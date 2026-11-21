#!/usr/bin/env bash
set -euo pipefail

# The Server confines every ACP agent with bubblewrap on Linux, so a job that starts agents through
# a published Server needs it on PATH, as the Server's Nix shell provides it. Ubuntu 24.04 runners
# also refuse unprivileged user namespaces to binaries without an AppArmor profile; lift that for
# the job.
sudo apt-get update -qq
sudo apt-get install -y -qq bubblewrap
if [ -e /proc/sys/kernel/apparmor_restrict_unprivileged_userns ]; then
  sudo sysctl -w kernel.apparmor_restrict_unprivileged_userns=0
fi
bwrap --version
