/**
 * Detox drives the real app on a real simulator and a real emulator.
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
      binaryPath: '../AgentUp.Mobile/ios/build/Build/Products/Release-iphonesimulator/AgentUp.app',
      build:
        'xcodebuild -workspace ../AgentUp.Mobile/ios/AgentUp.xcworkspace -scheme AgentUp ' +
        '-configuration Release -sdk iphonesimulator -derivedDataPath ../AgentUp.Mobile/ios/build ' +
        '-quiet CODE_SIGNING_ALLOWED=NO',
    },
    'android.release': {
      type: 'android.apk',
      binaryPath: '../AgentUp.Mobile/android/app/build/outputs/apk/release/app-release.apk',
      testBinaryPath:
        '../AgentUp.Mobile/android/app/build/outputs/apk/androidTest/release/app-release-androidTest.apk',
      build:
        'cd ../AgentUp.Mobile/android && ./gradlew assembleRelease assembleAndroidTest ' +
        '-DtestBuildType=release',
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
