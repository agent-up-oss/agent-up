#!/usr/bin/env bash
# Whether a restored build is actually usable.
#
# A cache hit is not the same as having the app. A restore that brought back only some of the
# binaries leaves the runner reporting a missing one, which reads as a broken suite rather than
# as a cache that needs rebuilding - and the build it skipped is the very thing that would have
# fixed it. So every binary the platform needs is checked by name, and anything missing means
# build it after all.
#
# The names are asked of Detox rather than written down here. They were written down once, and
# when the app under test became AgentUp.Mobile.E2E.App this file went on naming AgentUp.Mobile:
# every check reported a miss, every run rebuilt an app it had already cached, and nothing failed
# to say so. A path that is read from the same configuration the build and the runner use cannot
# drift from them again.
set -euo pipefail

platform="${1:?Pass the platform to check: android or ios}"

case "$platform" in
  android) app="android.release" ;;
  ios) app="ios.release" ;;
  *)
    echo "Unknown platform '$platform'. Use android or ios." >&2
    exit 2
    ;;
esac

# Detox states each binary relative to its own project, so they are resolved from there and
# reported relative to the repository, which is where this runs and what the cache holds.
#
# A newline-separated list rather than an array, and read with `while read` rather than `mapfile`:
# this runs on the macOS runner too, whose /bin/bash is 3.2, where `mapfile` does not exist and
# `set -u` rejects an empty array. Nothing here may use anything newer than that shell has.
required="$(node -e '
  const path = require("node:path");
  const [project, name] = process.argv.slice(1);
  const app = require(path.resolve(project, ".detoxrc.js")).apps[name];
  if (!app) throw new Error(`No app "${name}" in ${project}/.detoxrc.js.`);
  for (const key of ["binaryPath", "testBinaryPath"]) {
    if (app[key]) console.log(path.relative(process.cwd(), path.resolve(project, app[key])));
  }
' AgentUp.Mobile.E2E "$app")"

if [ -z "$required" ]; then
  echo "Detox names no binaries for '$app', so there is nothing this could check." >&2
  exit 2
fi

missing=0

# An iOS app is a directory, so its presence proves nothing: the executable inside it is what a
# half-restored bundle is missing. The bundle says which file that is - and a bundle that cannot
# say is not one to hand to a simulator, so it is rebuilt rather than trusted. Nothing here is
# allowed to fail the step: the answer to "is this usable" is always yes or no, never an error.
if [ "$platform" = "ios" ]; then
  bundle="$(printf '%s\n' "$required" | head -n 1)"
  if [ -e "$bundle" ]; then
    if executable="$(plutil -extract CFBundleExecutable raw -o - "$bundle/Info.plist" 2>&1)" \
       && [ -n "$executable" ]; then
      required="$required
$bundle/$executable"
    else
      echo "UNREADABLE: $bundle/Info.plist does not say what the executable is ($executable)"
      missing=1
    fi
  fi
fi

# A here-string, so the loop runs in this shell and what it finds outlives it.
while IFS= read -r path; do
  [ -n "$path" ] || continue
  if [ -e "$path" ]; then
    echo "present: $path"
  else
    echo "MISSING: $path"
    missing=1
  fi
done <<< "$required"

if [ "$missing" -eq 0 ]; then
  echo "The restored $platform app is complete; the build is skipped."
  echo "usable=true" >> "${GITHUB_OUTPUT:-/dev/stdout}"
else
  echo "The restored $platform app is incomplete; building it."
  echo "usable=false" >> "${GITHUB_OUTPUT:-/dev/stdout}"
fi
