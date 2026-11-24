#!/usr/bin/env bash
# Mobile · Android store bundle and Mobile · Sign-in on Android: lets Gradle ride out a transient
# repository error instead of failing the build on it.
#
# Expo's React Native Gradle plugin resolves foojay-resolver-convention from the Gradle Plugin
# Portal on every cold build, and the portal intermittently fails that fetch on hosted runners -
# "could not resolve plugin artifact", with nothing wrong on our side. Gradle's own retry gives up
# within about a second. These go into the Gradle user home as system properties, so they reach
# the build JVM however Gradle is launched (fastlane, Detox, gradlew), and they only widen how long
# Gradle keeps retrying a fetch that failed; a fetch that succeeds is unchanged.
set -euo pipefail

gradle_home="${GRADLE_USER_HOME:-$HOME/.gradle}"
mkdir -p "$gradle_home"
{
  # Six attempts with a doubling backoff from two seconds spans about a minute.
  echo "systemProp.org.gradle.internal.repository.max.retries=6"
  echo "systemProp.org.gradle.internal.repository.max.tentatives=6"
  echo "systemProp.org.gradle.internal.repository.initial.backoff=2000"
} >> "$gradle_home/gradle.properties"
echo "Gradle repository retries configured in $gradle_home/gradle.properties"
