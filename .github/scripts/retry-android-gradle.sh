#!/usr/bin/env bash
# Retry an Android Gradle invocation when the Gradle Plugin Portal fails to serve
# foojay-resolver-convention.
#
# Expo's React Native Gradle plugin resolves that convention plugin on every cold
# settings evaluation. The portal intermittently answers "could not resolve plugin
# artifact" on hosted runners, and Gradle caches the miss, so a later attempt in
# the same job would fail immediately unless we drop that negative cache first.
# configure-gradle-network.sh widens Gradle's own fetch retries inside one run;
# this wraps the whole command so a portal outage that outlives that window still
# costs a retry rather than the Mobile · Sign-in on Android or store-bundle job.
set -euo pipefail

max_attempts="${AGENTUP_GRADLE_RETRY_ATTEMPTS:-3}"
sleep_s="${AGENTUP_GRADLE_RETRY_SLEEP:-15}"
gradle_home="${GRADLE_USER_HOME:-$HOME/.gradle}"

if [ "$#" -eq 0 ]; then
  echo "usage: retry-android-gradle.sh <command> [args...]" >&2
  exit 2
fi

is_plugin_miss() {
  grep -Eq 'could not resolve plugin artifact|foojay-resolver-convention' "$1"
}

drop_plugin_miss_cache() {
  [ -d "$gradle_home/caches" ] || return 0
  # Gradle records "this module is absent" in the modules-2 metadata store
  # (resource-at-url.bin and friends), not in directories named after the
  # plugin. Leaving that store means the next attempt never asks the portal.
  # Downloaded files-2.1 jars stay; only the miss records and any plugin
  # marker paths go.
  rm -rf "$gradle_home/caches/modules-2"/metadata-*
  find "$gradle_home/caches" \( \
      -name '*foojay-resolver*' \
      -o -name '*foojay-resolver-convention*' \
    \) -exec rm -rf {} + 2>/dev/null || true
}

attempt=1
while [ "$attempt" -le "$max_attempts" ]; do
  echo "Android Gradle (attempt ${attempt}/${max_attempts}): $*"
  log="$(mktemp "${TMPDIR:-/tmp}/agent-up-gradle.XXXXXX")"
  set +e
  set +o pipefail
  "$@" 2>&1 | tee "$log"
  status=${PIPESTATUS[0]}
  set -e
  set -o pipefail
  if [ "$status" -eq 0 ]; then
    rm -f "$log"
    exit 0
  fi
  if ! is_plugin_miss "$log"; then
    rm -f "$log"
    exit "$status"
  fi
  if [ "$attempt" -eq "$max_attempts" ]; then
    rm -f "$log"
    exit "$status"
  fi
  echo "Plugin portal miss; dropping Gradle's negative cache and retrying."
  drop_plugin_miss_cache
  rm -f "$log"
  sleep "$sleep_s"
  attempt=$((attempt + 1))
done
