import { defineConfig, devices } from '@playwright/test';

/**
 * E2E against the REAL API: start it on 5081 with --seed in Development first (docs/runbooks/09-console.md).
 * The console dev server is started here if it is not already running; it proxies /api to 5081.
 */
/** E2E_BASE_URL points the suite at an already-running console (another port); default is `npm start` on 4200. */
const baseURL = process.env['E2E_BASE_URL'] ?? 'http://localhost:4200';

export default defineConfig({
  testDir: './e2e',
  timeout: 60_000,
  expect: { timeout: 10_000 },
  fullyParallel: false,
  workers: 1,
  reporter: [['list']],
  use: {
    baseURL,
    viewport: { width: 1440, height: 900 },
    deviceScaleFactor: 1,
    trace: 'retain-on-failure',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'], viewport: { width: 1440, height: 900 } } }],
  webServer: {
    command: 'npm start',
    url: baseURL,
    reuseExistingServer: true,
    timeout: 120_000,
  },
});
