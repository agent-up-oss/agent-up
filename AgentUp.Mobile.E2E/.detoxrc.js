/**
 * Detox drives the chat harness app on a real simulator and a real emulator.
 *
 * The app is AgentUp.Mobile.E2E.App: the real chat module and the real sign-in module, mounted
 * with nothing around them. Everything these tests exercise is code the real client runs; what is
 * missing is only the shell, the sidebar and the workspace navigation, none of which has anything
 * to do with signing an agent in.
 *
 * Detox rather than a black-box driver on purpose: it synchronises with the React Native runtime,
 * so it waits on pending timers, in-flight requests, and animations instead of guessing. For a
 * suite whose entire subject is asynchronous sign-in, that difference is the flakiness question.
 *
 * Every version here is pinned. A floating simulator runtime or system image is a test that
 * changes underneath you.
 */
module.exports = {
  testRunner: {
    args: { $0: 'jest', config: 'detox/jest.config.js' },
    jest: { setupTimeout: 300_000 },
  },
  apps: {
    'ios.release': {
      type: 'ios.app',
      binaryPath: '../AgentUp.Mobile.E2E.App/ios/build/Build/Products/Release-iphonesimulator/AgentUpChatHarness.app',
      // Same reason as Android: the chat mounts on launch and never goes idle (event stream,
      // reconnect timers, main-queue work). Waiting for idle during launchApp cancelled the first
      // getAgent (HTTP 499) and left the picker without buttons.
      launchArgs: { detoxEnableSynchronization: 0 },
      build:
        'xcodebuild -workspace ../AgentUp.Mobile.E2E.App/ios/AgentUpChatHarness.xcworkspace ' +
        '-scheme AgentUpChatHarness -configuration Release -sdk iphonesimulator ' +
        '-derivedDataPath ../AgentUp.Mobile.E2E.App/ios/build -quiet CODE_SIGNING_ALLOWED=NO',
    },
    'android.release': {
      type: 'android.apk',
      binaryPath: '../AgentUp.Mobile.E2E.App/android/app/build/outputs/apk/release/app-release.apk',
      // Detox's Android idling resources are built at startup, and the network one reads React
      // Native's OkHttp client by reflecting for a field the New Architecture no longer has. It
      // gets null, dereferences it, and the app dies before a single test body runs:
      //
      //   java.lang.NullPointerException
      //     at ...idlingresources.network.NetworkIdlingResource.<init>(NetworkIdlingResource.kt:24)
      //
      // Detox 20.51.4 compiles that constructor to the same bytes, so upgrading is not the answer.
      // This argument is read before any idling resource is built - it is the one way past it from
      // outside Detox - and nothing here wants those resources anyway: every wait in these suites
      // is on an element being visible or on Server state, never on Detox's idea of idle. iOS
      // launches the same way: the chat mounts on launch and never goes idle, so waiting for
      // synchronisation cancelled getAgent (HTTP 499) and left the picker without buttons.
      launchArgs: { detoxEnableSynchronization: 0 },
      testBinaryPath:
        '../AgentUp.Mobile.E2E.App/android/app/build/outputs/apk/androidTest/release/app-release-androidTest.apk',
      // --build-cache and --parallel: a cold build of this takes the better part of an hour on a
      // hosted runner, and the cache the CI job persists is only consulted when it is asked for.
      build:
        'cd ../AgentUp.Mobile.E2E.App/android && ./gradlew assembleRelease assembleAndroidTest ' +
        '-DtestBuildType=release --build-cache --parallel',
    },
  },
  devices: {
    simulator: {
      type: 'ios.simulator',
      // The device type is resolved from what the runner image actually ships and passed in, then
      // logged by the job that chose it. Pinning a runtime version here instead was worse than
      // not pinning: when the image moved on, Detox could not find the device at all.
      device: { type: process.env.AGENTUP_E2E_IOS_DEVICE || 'iPhone 16' },
    },
    emulator: {
      type: 'android.emulator',
      device: { avdName: 'agent_up_e2e' },
    },
  },
  configurations: {
    'ios.sim.release': { device: 'simulator', app: 'ios.release' },
    'android.emu.release': { device: 'emulator', app: 'android.release' },
  },
  behavior: {
    init: { exposeGlobals: true },
    cleanup: { shutdownDevice: false },
  },
  artifacts: {
    rootDir: 'artifacts',
    plugins: {
      // A failure on a runner you cannot attach to is only as debuggable as what it left behind.
      screenshot: { shouldTakeAutomaticSnapshots: true, takeWhen: { testDone: true } },
      log: { enabled: true },
    },
  },
};
