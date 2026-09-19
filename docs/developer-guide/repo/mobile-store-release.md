---
title: Mobile store release
---

# Mobile store release

Store binaries for `AgentUp.Mobile` ship from
[Mobile CI](https://github.com/themassiveone/agent-up/actions/workflows/mobile-ci.yaml),
not from `ci.yml`.

A path-filtered `push` smoke-builds and signs Android and iOS. It does not upload
to Play or App Store Connect and does not create `android-v*` / `ios-v*` GitHub
releases. Dispatch the same workflow on the branch that already has a product
`vX.Y.Z` tag to publish.

After validation, each platform job uploads the signed AAB or IPA plus checksum
to GitHub Actions artifacts (`mobile-android`, `mobile-ios`) with 1-day
retention, including on smoke, before any Play or App Store Connect submit.

The workflow runs Mobile typechecking, provider and script tests, and a web
export in a dedicated Ubuntu job before either native build. Dependency
installation runs the design-system generator with `--check` so stale committed
outputs fail instead of being rewritten. It does not call
semantic-release or use `.releaserc.json`. Android compiles on Ubuntu. iOS
compiles on `macos-26` with Xcode 26.4 because CocoaPods and `xcodebuild` cannot
run on Linux, and Expo SDK 57 rejects Xcode 16.

## Inputs

| Input | Values | Effect |
|---|---|---|
| `channel` | `beta` or `prod` | Play internal vs production; TestFlight vs App Store Connect upload |
| `platforms` | `both`, `android`, or `ios` | Which store jobs run |

These inputs apply only to `workflow_dispatch`. A path-filtered `push` always
builds both platforms and never publishes.

`prod` iOS uploads the IPA to App Store Connect with metadata and screenshots
skipped and does **not** submit for App Review. A human submits review from App
Store Connect until listing metadata exists.

## Versioning

The `version` job is the gate for every later job.

- On `workflow_dispatch`, marketing version is the newest git tag reachable from
  `HEAD` that matches `vX.Y.Z`. Tags named `android-v*` and `ios-v*` are ignored
  so this pipeline cannot version from its own GitHub releases. The job fails if
  this branch has no product tag; ship a normal `ci.yml` release first.
- On `push`, marketing version is `0.0.0` so the smoke does not need a product
  tag.
- Play `versionCode` and iOS `CFBundleVersion` are `GITHUB_RUN_NUMBER`.

Each successful **dispatched** platform job creates or updates a GitHub release
whose tag is `android-v<version>` or `ios-v<version>`, with the AAB or IPA
attached. Re-running the same marketing version replaces those assets; the stores
still accept the binary because the build number changed.

`ci.yml` listens to every branch push and ignores `android-v*` and `ios-v*` tag
pushes. A `tags-ignore` filter without a `branches` filter would skip branch
pushes entirely, so desktop CI would never start on this branch.

## Job graph

```text
push (path-filtered) or workflow_dispatch
  version (ubuntu)
    tests (ubuntu)
      android (ubuntu)
      ios (macos-26)
```

Push runs when `AgentUp.Mobile`, `AgentUp.Chat`, `AgentUp.AgentAuth`,
`AgentUp.ServerClient`, `AgentUp.DesignSystem`, `AgentUp.WebAudit`, this
workflow, the iOS certs workflow, the mobile helper scripts, Fastlane,
`Gemfile`, `Gemfile.lock`, or `.ruby-version` change. Changing only `ci.yml`
does not start Mobile CI.

The Android and iOS jobs both require the shared Mobile test job to pass. Android
failure does not cancel iOS, and the reverse. A second dispatch on the same ref
waits; it does not cancel an in-flight store upload. A newer push on the same ref
cancels an in-flight smoke, not a dispatch.

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
[`AgentUp.Mobile/app.config.js`](../../../AgentUp.Mobile/app.config.js) overlays
that identifier plus `AGENTUP_MOBILE_VERSION` and
`AGENTUP_MOBILE_VERSION_CODE` onto `app.json`. CI runs `expo prebuild` and does
not commit `android/` or `ios/`. Store `icon.png` and `adaptive-icon.png` under
`AgentUp.Mobile/assets/` are 1024px scales of
`AgentUp.Mobile/public/agent-up-icon-512.png`.

GitHub-hosted store jobs invoke Expo and Fastlane directly. Fastlane, `Gemfile`,
`Gemfile.lock`, and `.ruby-version` live at the repository root; `expo prebuild`
still runs in `AgentUp.Mobile`. Do not add public Mobile npm scripts for those
commands; local scripts still enter `shell.nix`.

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
5. Dispatch Mobile CI with `channel: beta` after a product `vX.Y.Z` tag exists
   on the branch.
