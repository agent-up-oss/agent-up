import { defineConfig, devices } from '@playwright/test';

/**
 * The installable web build of the same client, served as real static files over real HTTP and
 * driven in a real browser.
 *
 * Pinned, serial, and with no retries: a retry would hide a flake rather than surface it, and
 * this suite exists to make instability visible.
 */
export default defineConfig({
  testDir: '.',
  testMatch: '**/*.spec.mjs',
  fullyParallel: false,
  workers: 1,
  retries: 0,
  timeout: 180_000,
  expect: { timeout: 30_000 },
  reporter: [['list'], ['junit', { outputFile: '../artifacts/pwa-results.xml' }]],
  use: {
    ...devices['Pixel 7'],
    // The subject is a phone-shaped client, so it is driven at phone size.
    trace: 'retain-on-failure',
    video: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: [{ name: 'installable-web', use: { ...devices['Pixel 7'] } }],
});
