import { defineConfig, devices } from '@playwright/test'
import {
  apiBaseUrl,
  apiEnvironment,
  apiProject,
  appBaseUrl,
  appEnvironment,
  repoRoot,
} from './tests/e2e/support/e2eEnvironment'

/**
 * End-to-end tests against a real API, a real Nuxt server and a real browser.
 *
 * Ports differ from the development ones (5081/3001 instead of 5080/3000) so a
 * running `bun run dev` does not collide with a test run — and so a test can
 * never accidentally hit the development database.
 *
 * The database is created, migrated and seeded by globalSetup, and removed
 * again by globalTeardown. See tests/e2e/support/.
 */
export default defineConfig({
  testDir: './tests/e2e',
  testMatch: '**/*.spec.ts',
  outputDir: './test-results',

  // The suite shares one database, so tests run in sequence and each one is
  // responsible for leaving behind only data it can identify by name.
  fullyParallel: false,
  workers: 1,

  forbidOnly: Boolean(process.env.CI),
  retries: process.env.CI ? 1 : 0,
  timeout: 30_000,
  expect: { timeout: 10_000 },

  reporter: process.env.CI
    ? [['github'], ['html', { open: 'never' }]]
    : [['list'], ['html', { open: 'never' }]],

  globalSetup: './tests/e2e/support/globalSetup.ts',
  globalTeardown: './tests/e2e/support/globalTeardown.ts',

  use: {
    baseURL: appBaseUrl,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    video: 'off',
  },

  projects: [
    {
      // q2 is delivered as a Capacitor app, so the phone layout is the one that
      // has to hold — see app/AGENTS.md section 8. A desktop run would assert
      // the behaviour at a width no user of the shipped app ever sees.
      //
      // Pixel 7 rather than an iPhone descriptor because it is Chromium: CI
      // installs that one engine (`playwright install --with-deps chromium`),
      // and a WebKit descriptor would silently fall back to downloading a
      // second browser. The viewport is overridden to the 390 × 844 reference
      // the UI is designed against; everything else — touch, mobile emulation,
      // device scale factor — comes from the descriptor.
      name: 'mobile-chromium',
      use: {
        ...devices['Pixel 7'],
        viewport: { width: 390, height: 844 },
      },
    },
  ],

  webServer: [
    {
      // No build step: `dotnet run` is enough and keeps the loop short.
      command: `dotnet run --project ${apiProject} --no-launch-profile --verbosity quiet`,
      cwd: repoRoot,
      url: `${apiBaseUrl}/health`,
      env: apiEnvironment(),
      reuseExistingServer: false,
      timeout: 120_000,
      stdout: 'pipe',
      stderr: 'pipe',
    },
    {
      // The production build, so E2E exercises what actually ships.
      command: 'bun run build && bun run preview',
      cwd: import.meta.dirname,
      url: appBaseUrl,
      env: appEnvironment(),
      reuseExistingServer: false,
      timeout: 300_000,
      stdout: 'pipe',
      stderr: 'pipe',
    },
  ],
})
