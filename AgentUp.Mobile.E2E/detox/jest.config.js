/** @type {import('jest').Config} */
module.exports = {
  rootDir: '..',
  testMatch: ['<rootDir>/detox/**/*.test.js'],
  testTimeout: 300_000,
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
