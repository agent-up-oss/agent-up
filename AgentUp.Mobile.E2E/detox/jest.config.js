/** @type {import('jest').Config} */
module.exports = {
  rootDir: '..',
  testMatch: ['<rootDir>/detox/**/*.test.js'],
  // Pasted-code iOS named waits sum to 540s (picker 60 + auth-required 60 + method 30 +
  // challenge 120 + open 60 + code field 30 + submit 30 + ready 150) plus returning from
  // the in-app browser. An envelope smaller than that kills the test with "Exceeded timeout"
  // instead of naming the wait that is actually stuck.
  testTimeout: 600_000,
  maxWorkers: 1,
  globalSetup: 'detox/runners/jest/globalSetup',
  globalTeardown: 'detox/runners/jest/globalTeardown',
  testEnvironment: 'detox/runners/jest/testEnvironment',
  reporters: ['default', ['jest-junit', { outputDirectory: 'artifacts', outputName: 'detox-results.xml' }]],
  verbose: true,
  // No retries. A retry hides a flake instead of surfacing it, which is the opposite of what this
  // suite is for: if something here is unstable, CI has to go red so the cause gets fixed.
  bail: false,
};
