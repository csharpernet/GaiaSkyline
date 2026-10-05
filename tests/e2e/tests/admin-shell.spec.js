const { test, expect } = require('@playwright/test');
const { loginAsOwner } = require('../helpers/admin');

// The admin shell (sidebar + top bar) and the audit log. Reuses the 7A owner-login helper. Skipped
// unless E2E_SEAM=1 (CI browser-quality job runs the app with E2E:Enabled).
test.describe('Admin shell', () => {
  test.skip(!process.env.E2E_SEAM, 'Set E2E_SEAM=1 and run the app with E2E:Enabled to run admin E2E.');

  test('dashboard shows the sidebar and navigates to the audit log', async ({ page }) => {
    await loginAsOwner(page);

    const sidebar = page.locator('aside');
    await expect(sidebar.getByRole('link', { name: 'Dashboard' })).toBeVisible();
    await expect(sidebar).toContainText('Audit log');
    // Stage 7 shipped every section: nothing renders as a disabled "soon" item any more.
    await expect(sidebar).not.toContainText('soon');
    await expect(sidebar.getByRole('link', { name: 'Settings' })).toBeVisible();

    await sidebar.getByRole('link', { name: 'Audit log' }).click();
    await expect(page).toHaveURL(/\/admin\/audit/);
    await expect(page.getByRole('heading', { name: 'Audit log' })).toBeVisible();
    // Our own login just wrote audit events, so the table is populated.
    await expect(page.locator('table')).toBeVisible();
  });

  test('the ? shortcut opens the keyboard help overlay', async ({ page }) => {
    await loginAsOwner(page);
    await page.keyboard.press('?');
    await expect(page.locator('#admin-help')).toBeVisible();
    await page.keyboard.press('Escape');
    await expect(page.locator('#admin-help')).toBeHidden();
  });
});
