---
title: Mobile store release
---

# Mobile store release

Store binaries for `AgentUp.Mobile` ship from a dedicated workflow, not from
`ci.yml`. Dispatch [Deploy Mobile](https://github.com/themassiveone/agent-up/actions/workflows/deploy-mobile.yaml)
on the branch that already has a product `vX.Y.Z` tag from the normal release
pipeline.

The workflow does not run tests, does not call semantic-release, and does not
use `.releaserc.json`. Android compiles on Ubuntu. iOS compiles on macOS because
CocoaPods and `xcodebuild` cannot run on Linux.

## Inputs

| Input | Values | Effect |
|---|---|---|
| `channel` | `beta` or `prod` | Play internal vs production; TestFlight vs App Store Connect upload |
| `platforms` | `both`, `android`, or `ios` | Which store jobs run |

`prod` iOS uploads the IPA to App Store Connect with metadata and screenshots
skipped and does **not** submit for App Review. A human submits review from App
Store Connect until listing metadata exists.

## Versioning

The `version` job is the gate for every later job.

- Marketing version is the newest git tag reachable from `HEAD` that matches
  `vX.Y.Z`. Tags named `android-v*` and `ios-v*` are ignored so this pipeline
  cannot version from its own GitHub releases.
- Play `versionCode` and iOS `CFBundleVersion` are `GITHUB_RUN_NUMBER`.
- The job fails if this branch has no product tag. Ship a normal `ci.yml`
  release first.

Each successful platform job creates or updates a GitHub release whose tag is
`android-v<version>` or `ios-v<version>`, with the AAB or IPA attached. Re-running
the same marketing version replaces those assets; the stores still accept the
binary because the build number changed.

`ci.yml` ignores `android-v*` and `ios-v*` tag pushes so those releases do not
start the desktop CI.

## Job graph

```text
workflow_dispatch
  version (ubuntu)
    android (ubuntu)
    ios (macos-15)
```

Android failure does not cancel iOS, and the reverse. A second dispatch on the
same ref waits; it does not cancel an in-flight store upload.

## Signing

iOS uses Fastlane Match against the shared private
[certificates](https://github.com/MassiveCreationLab/certificates) repository
(`master`). Deploy lanes set `readonly: true` and
`force_for_new_certificates: false` so a release cannot mint a new Apple
certificate. New Agent-Up work creates **profiles only** for
`com.massivecreationlab.agentup`.

One-time bootstrap is [Mobile iOS certificates](https://github.com/themassiveone/agent-up/actions/workflows/mobile-ios-certs.yaml):

1. `init_ci` writes a writable deploy key from this repository onto the Match
   store.
2. `sync` creates development and App Store profiles while reusing the existing
   Apple certificates.

Android signing uses a Play **upload** keystore stored in GitHub secrets. That
keystore does not belong in the certificates git repo. Google Play App Signing
holds the app signing key.

## Expo identity

Store ID is `com.massivecreationlab.agentup` on both platforms.
[`AgentUp.Mobile/app.config.js`](../../AgentUp.Mobile/app.config.js) overlays
that identifier plus `AGENTUP_MOBILE_VERSION` and
`AGENTUP_MOBILE_VERSION_CODE` onto `app.json`. CI runs `expo prebuild` and does
not commit `android/` or `ios/`. Store `icon.png` and `adaptive-icon.png` under
`AgentUp.Mobile/assets/` are 1024px scales of
`AgentUp.Mobile/public/agent-up-icon-512.png`.

GitHub-hosted store jobs invoke Expo and Fastlane directly. Do not add public
Mobile npm scripts for those commands; local scripts still enter `shell.nix`.

## First-run console steps

These cannot be automated in the workflow:

1. Copy Match, App Store Connect, Play, and Android keystore secrets onto the
   Agent-Up GitHub repository. Names are listed in
   [CI Configuration](./ci-configuration.md).
2. Run Mobile iOS certificates `init_ci`, then `sync`, and confirm
   `AppStore_com.massivecreationlab.agentup.mobileprovision` landed in
   certificates without a new Apple certificate.
3. Create the App Store Connect app with bundle ID `com.massivecreationlab.agentup`.
4. Create the Play Console app with package `com.massivecreationlab.agentup`,
   enable Play App Signing, register the upload keystore, and grant the Play
   Developer API to the service account.
5. Dispatch Deploy Mobile with `channel: beta` after a product `vX.Y.Z` tag
   exists on the branch.
