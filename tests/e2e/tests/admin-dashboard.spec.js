const { test, expect } = require('@playwright/test');
const { loginAsOwner } = require('../helpers/admin');

// Dashboard: KPIs, calendar legend, and the manual Hostify-sync to-do. The E2E seam seeds a confirmed
// booking (reset each startup) that appears as an overdue to-do item. Skipped unless E2E_SEAM=1.
test.describe('Admin dashboard', () => {
  test.skip(!process.env.E2E_SEAM, 'Set E2E_SEAM=1 and run the app with E2E:Enabled to run admin E2E.');

  test('shows KPIs + calendar and "Done in Hostify" clears a manual-sync item', async ({ page }) => {
    await loginAsOwner(page);
    await expect(page.getByRole('heading', { name: 'Dashboard' })).toBeVisible();

    // KPI cards + calendar legend render.
    await expect(page.getByText('Occupancy', { exact: true })).toBeVisible();
    await expect(page.getByText('Revenue (MTD)', { exact: true })).toBeVisible();
    await expect(page.getByText('Direct booking', { exact: true })).toBeVisible();

    // The seeded confirmed booking is on the manual-sync to-do.
    await expect(page.getByRole('heading', { name: /Mirror in Hostify/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /done in hostify/i })).toBeVisible();

    // Mark it done → redirect to /admin and the to-do item is gone.
    await page.locator('form[action*="/sync/"]').first().getByRole('button', { name: /done in hostify/i }).click();
    await expect(page).toHaveURL(/\/admin$/);
    await expect(page.getByRole('heading', { name: /Mirror in Hostify/i })).toHaveCount(0);
  });
});
