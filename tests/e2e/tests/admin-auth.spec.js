const { test, expect } = require('@playwright/test');
const { loginAsOwner } = require('../helpers/admin');

// Requires the app to run with the E2E seam on (E2E:Enabled) and the Owner credentials in the
// environment — set by the CI browser-quality job. Skipped otherwise so a bare `npx playwright test`
// stays green. This is the carry-over from Stage 6 (owner password + TOTP), and the login helper it
// exercises is reused by every later admin E2E.
test.describe('Admin authentication', () => {
  test.skip(!process.env.E2E_SEAM, 'Set E2E_SEAM=1 and run the app with E2E:Enabled to run admin E2E.');

  test('owner signs in with password + TOTP and reaches the admin', async ({ page }) => {
    await loginAsOwner(page);

    // Past the login: the password field is gone and we are on the admin surface.
    await expect(page).toHaveURL(/\/admin$/);
    await expect(page.locator('#Password')).toHaveCount(0);
  });

  test('the admin is not reachable without signing in', async ({ page }) => {
    await page.goto('/admin');
    // Owner policy denies anonymous access: the cookie handler redirects to the login path.
    await expect(page).toHaveURL(/\/account\/login/);
    await expect(page.locator('#Password')).toBeVisible();
  });
});
