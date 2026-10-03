const { expect } = require('@playwright/test');
const { computeTotp } = require('./totp');

// Credentials for the E2E Owner seeded when the app runs with E2E:Enabled (see E2ESeeder.cs). The
// defaults match the seeder's defaults; CI overrides OWNER_PASSWORD with an ephemeral per-run value.
const OWNER_EMAIL = process.env.OWNER_EMAIL || 'owner.e2e@gaiaskyline.test';
const OWNER_PASSWORD = process.env.OWNER_PASSWORD || '';
const OWNER_TOTP_KEY = process.env.OWNER_TOTP_KEY || 'JBSWY3DPEHPK3PXP';

// Signs the Owner in through the real /admin flow: password, then an authenticator code. Reused by
// every admin E2E. Retries the TOTP once in case the code rolled over at a 30s boundary.
async function loginAsOwner(page) {
  await page.goto('/admin/login');
  await page.fill('#Email', OWNER_EMAIL);
  await page.fill('#Password', OWNER_PASSWORD);
  await page.click('button[type=submit]');

  await expect(page).toHaveURL(/\/admin\/two-factor/);
  await page.fill('#Code', computeTotp(OWNER_TOTP_KEY));
  await page.click('button[type=submit]');

  if (/\/admin\/two-factor/.test(page.url())) {
    await page.fill('#Code', computeTotp(OWNER_TOTP_KEY));
    await page.click('button[type=submit]');
  }

  await expect(page).toHaveURL(/\/admin$/);
}

module.exports = { loginAsOwner, OWNER_EMAIL, OWNER_PASSWORD, OWNER_TOTP_KEY };
