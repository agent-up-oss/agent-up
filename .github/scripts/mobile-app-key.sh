#!/usr/bin/env bash
# What the mobile app under test is built from.
#
# Two keys, because two things are worth reusing for different lengths of time.
#
# `dependencies` covers the dependency set and the app's native configuration: what the compiled
# pods and their object files are a function of. It changes only when a dependency does, so a
# change to the client relinks rather than recompiling the world.
#
# `key` covers those plus every source file that ends up bundled into the app. An exact hit means
# the app the previous run compiled is the app this run would compile, so the build is skipped
# outright - which is most runs of this workflow, because the Server and the test agents are in
# its paths filter and neither of them is in the bundle.
#
# Content, not timestamps: git already knows the blob hash of every tracked file, so this is both
# exact and nearly free.
set -euo pipefail

sum() {
  if command -v shasum >/dev/null 2>&1; then shasum -a 256; else sha256sum; fi
}

hash_of() {
  git ls-files -s -- "$@" | sum | cut -c1-16
}

# The app under test is the chat harness, so this is its dependency set and its sources - plus
# every module it mounts, because a change in any of them changes the bundle, and .detoxrc.js,
# which holds the command that compiles it. A binary is a function of what goes into it and of how
# it was built, and leaving the second one out is how a cache starts handing back an app nobody
# would get by building.
dependencies="$(hash_of AgentUp.Mobile.E2E.App/package-lock.json AgentUp.Mobile.E2E.App/package.json AgentUp.Mobile.E2E.App/app.json)"
contents="$(hash_of AgentUp.Mobile.E2E.App AgentUp.Chat AgentUp.AgentAuth AgentUp.ServerClient AgentUp.DesignSystem AgentUp.Mobile.E2E/.detoxrc.js)"

{
  echo "dependencies=$dependencies"
  echo "key=$contents"
} >> "${GITHUB_OUTPUT:-/dev/stdout}"

echo "Dependency set: $dependencies"
echo "App contents:   $contents"
