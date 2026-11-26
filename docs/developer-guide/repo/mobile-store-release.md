---
title: Mobile store release
---

# Mobile store release

<DocWhat>
Mobile store binaries are built by `ci.yml` on every run and shipped by its Mobile release job. This page is the Android and iOS store-build and store-submit contract.
</DocWhat>

Two Mobile legs build the full client for the stores on every pull request and push:

- **Mobile · Android store bundle** prebuilds, builds, signs and validates the Play upload AAB.
- **Mobile · iOS store archive** prebuilds, builds, signs through Match and validates the App Store IPA.

Each uploads the binary, its checksum and a `release.json` manifest to GitHub Actions artifacts (`mobile-android`, `mobile-ios`) with 1-day retention.

A store leg records a declared skip, not a pass, where GitHub withholds its signing secrets: Dependabot and pull requests from forks. The Behavior matrix lists it as skipped with that reason. Any other run that lacks the secrets fails.

Mobile release uploads exactly the binaries those legs built. It sits beside Release, needs the same two jobs (Behavior matrix and Plan), and runs only on a `workflow_dispatch` that names a mobile channel. Release runs only on a push, so one run never does both.

Mobile typechecking, provider and script tests run in Source · TypeScript suites. The web export and `Examples/browser-sso` Playwright run in Mobile · Browser SSO front door; pin `@playwright/test` to 1.61.1 so Chromium installs on `ubuntu-latest` (26.04). SDK 58 custom `src/app/+html.tsx` must render `bodyNodes` from `useServerDocumentContext()` so the static HTML hydrates. Dependency installation runs the design-system generator with `--check` so stale committed outputs fail instead of being rewritten. It does not call semantic-release or use `.releaserc.json`. Android compiles on Ubuntu, with Gradle's repository retry window widened by `.github/scripts/configure-gradle-network.sh` because the Gradle Plugin Portal intermittently fails to serve the React Native plugin's `foojay-resolver-convention` on hosted runners. iOS compiles on `macos-latest` with Xcode 26.4 because CocoaPods and `xcodebuild` cannot run on Linux, Expo SDK 58 requires the iOS 27 SDK that Xcode 16 does not ship, and the `macos-26` label sits in a thinner hosted pool that GitHub cancels after about fifteen minutes with no runner.

## Inputs

Dispatch `ci.yml` with:

| Input | Values | Effect |
|---|---|---|
| `mobile-channel` | `none`, `beta` or `prod` | `none` runs the matrix and ships nothing. `beta` is Play internal and TestFlight; `prod` is Play production and App Store Connect |
| `mobile-platforms` | `both`, `android`, or `ios` | Which Mobile release legs run. The store leg of a platform the dispatch does not ship records a declared skip |

`prod` iOS uploads the IPA to App Store Connect with metadata and screenshots
skipped and does **not** submit for App Review. A human submits review from App
Store Connect until listing metadata exists.

## Versioning

- On a dispatch that names a mobile channel, the store legs take the marketing version from the newest git tag reachable from `HEAD` that matches `vX.Y.Z`. Tags named `android-v*` and `ios-v*` are ignored, so this pipeline cannot version from its own GitHub releases. The legs fail if the branch has no product tag; ship a normal release first.
- On every other run, the marketing version is `0.0.0`, so the store build needs no product tag.
- Play `versionCode` and iOS `CFBundleVersion` are `GITHUB_RUN_NUMBER` of `ci.yml`. Its counter is far above the one the separate Mobile CI workflow used, so build numbers keep increasing.

Mobile release reads the version, build number and source tag from the `release.json` the store leg wrote, and checks the binary against its checksum before any upload.

Each successful Mobile release leg creates or updates a GitHub release
whose tag is `android-v<version>` or `ios-v<version>`, with the AAB or IPA
attached. Re-running the same marketing version replaces those assets; the stores
still accept the binary because the build number changed.

`ci.yml` ignores `android-v*` and `ios-v*` tag pushes. A `tags-ignore` filter without a `branches` filter would skip branch
pushes entirely, so CI would never start on this branch.

## Job graph

```text
pull_request, push, or workflow_dispatch
  Mobile · Android store bundle (ubuntu)  ─┐
  Mobile · iOS store archive (macos-latest) ├─ Behavior matrix ─ Mobile release (android) → Google Play
  every other leg ─────────────────────────┘                    Mobile release (ios) → App Store Connect
```

Android failure does not cancel iOS, and the reverse. A dispatch never cancels and is never cancelled: a second dispatch on the same ref waits, so a store upload is never cut off. A newer push on the same branch cancels an in-flight branch run, never a dispatch.

## Signing

iOS uses Fastlane Match against the shared private
[certificates](https://github.com/MassiveCreationLab/certificates) repository
(`master`). Deploy lanes set `readonly: true` and
`force_for_new_certificates: false` so a release cannot mint a new Apple
certificate. New Agent-Up work creates **profiles only** for
`net.themassiveone.agent-up.ios`.

One-time bootstrap is [Mobile iOS certificates](https://github.com/themassiveone/agent-up/actions/workflows/mobile-ios-certs.yaml):

1. `init_ci` writes a writable deploy key from this repository onto the Match
   store.
2. `sync` creates development and App Store profiles while reusing the existing
   Apple certificates.

Android signing uses a Play **upload** keystore stored in GitHub secrets. That
keystore does not belong in the certificates git repo. Google Play App Signing
holds the app signing key.

## Expo identity

iOS is `net.themassiveone.agent-up.ios`. Android is
`net.themassiveone.agentup.android`.
[`AgentUp.Mobile/app.config.js`](../../../AgentUp.Mobile/app.config.js) overlays
those identifiers plus `AGENTUP_MOBILE_VERSION` and
`AGENTUP_MOBILE_VERSION_CODE` onto `app.json`. Play `package_name` must match the
Android package baked at prebuild; a different id is a hard Play API failure.
CI runs `expo prebuild` and does not commit `android/` or `ios/`. Store
`icon.png` and `adaptive-icon.png` under `AgentUp.Mobile/assets/` are 1024px
scales of `AgentUp.Mobile/public/agent-up-icon-512.png`.

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
   `AppStore_net.themassiveone.agent-up.ios.mobileprovision` landed in
   certificates without a new Apple certificate.
3. Create the App Store Connect app with bundle ID `net.themassiveone.agent-up.ios`.
4. Create the Play Console app with package `net.themassiveone.agentup.android`,
   enable Play App Signing, register the upload keystore, and grant the Play
   Developer API to the service account.
5. Dispatch Agent-Up CI with `mobile-channel: beta` after a product `vX.Y.Z` tag exists
   on the branch.
