const { defineConfig, devices } = require('@playwright/test');

// The app is started by the CI job (migrated + seeded against LocalDB) and reached over HTTP on
// port 5080; override with BASE_URL to run against another instance. See .github/workflows/ci.yml.
// Plain CommonJS (not .ts) so the config loads without a TypeScript transform on any supported Node.
module.exports = defineConfig({
  testDir: './tests',
  timeout: 60000,
  expect: { timeout: 10000 },
  retries: 0,
  reporter: [['list'], ['html', { open: 'never' }]],
  use: {
    baseURL: process.env.BASE_URL || 'http://localhost:5080',
    ignoreHTTPSErrors: true,
    trace: 'on-first-retry',
  },
  projects: [
    { name: 'chromium', use: { ...devices['Desktop Chrome'] } },
  ],
});
