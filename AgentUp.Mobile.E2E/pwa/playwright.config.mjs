import { defineConfig, devices } from '@playwright/test';

/**
 * The installable web build of the same client, served as real static files over real HTTP and
 * driven in a real browser.
 *
 * No retries: a retry would hide a flake rather than surface it, and this suite exists to make
 * instability visible.
 *
 * Scenarios do run side by side. Each brings up its own stack on ports the OS handed out, so
 * they share nothing and cannot collide, and running them one after another only made the suite
 * take four times as long as it needed to.
 */
export default defineConfig({
  testDir: '.',
  testMatch: '**/*.spec.mjs',
  fullyParallel: true,
  workers: 2,
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
