const { test, expect } = require('@playwright/test');
const { loginAsOwner } = require('../helpers/admin');

// Bookings admin: list + filter, then a detail status action and a notes save. Skipped unless E2E_SEAM=1.
// The E2E seam seeds GS-E2E01 (AwaitingPayment) and GS-E2E01-C (Confirmed) on every startup.
test.describe('Admin bookings', () => {
  test.skip(!process.env.E2E_SEAM, 'Set E2E_SEAM=1 and run the app with E2E:Enabled to run admin E2E.');

  test('list shows the seeded bookings and filters by status', async ({ page }) => {
    await loginAsOwner(page);
    await page.goto('/admin/bookings');
    await expect(page.getByRole('heading', { name: 'Bookings' })).toBeVisible();

    await expect(page.getByRole('link', { name: 'GS-E2E01-C' })).toBeVisible();
    await expect(page.getByRole('link', { name: 'GS-E2E01', exact: true })).toBeVisible();

    // Filter to Confirmed → the confirmed booking stays, the awaiting one drops out.
    await page.selectOption('#status', 'Confirmed');
    await page.getByRole('button', { name: 'Filter' }).click();
    await expect(page).toHaveURL(/status=Confirmed/);
    await expect(page.getByRole('link', { name: 'GS-E2E01-C' })).toBeVisible();
    await expect(page.getByRole('link', { name: 'GS-E2E01', exact: true })).toHaveCount(0);
  });

  test('detail allows a check-in transition and saving notes', async ({ page }) => {
    await loginAsOwner(page);
    await page.goto('/admin/bookings');
    // GS-E2E01-B is the dedicated, already-synced fixture for this destructive test, so mutating it never
    // affects the dashboard manual-sync to-do (which keys off Confirmed/Cancelled unsynced bookings).
    await page.getByRole('link', { name: 'GS-E2E01-B' }).click();
    await expect(page).toHaveURL(/\/admin\/bookings\/[0-9a-fA-F-]{36}$/);

    // A Confirmed booking can be marked checked in.
    await page.getByRole('button', { name: 'Mark checked in' }).click();
    await expect(page.locator('[data-toast]')).toContainText('checked in');

    // Saving internal notes.
    await page.fill('textarea[name=notes]', 'Guest requested a late checkout');
    await page.getByRole('button', { name: 'Save notes' }).click();
    await expect(page.locator('[data-toast]')).toContainText('Notes saved');
  });
});
