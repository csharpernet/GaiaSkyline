const { test, expect } = require('@playwright/test');
const { loginAsOwner } = require('../helpers/admin');

// Payments admin (§9): Stripe status panel (gracefully unconfigured in E2E), the event-log browser
// empty state, and the Multibanco monitor with its typed manual cancel. The E2E seam seeds the
// dedicated hold GS-E2E01-M (AwaitingPayment + voucher, reset each startup) — never the magic-link
// fixture. Skipped unless E2E_SEAM=1.
test.describe('Admin payments', () => {
  test.skip(!process.env.E2E_SEAM, 'Set E2E_SEAM=1 and run the app with E2E:Enabled to run admin E2E.');

  test('shows Stripe status, event log and the Multibanco monitor', async ({ page }) => {
    await loginAsOwner(page);
    await page.goto('/admin/payments');

    await expect(page.getByRole('heading', { name: 'Payments' })).toBeVisible();
    // The summary text depends on whether a Stripe key is configured (CI: none; a dev box may have a
    // test key in User Secrets) — assert the panel renders and the webhook line is the fresh-DB "never".
    await expect(page.locator('[data-stripe-status]')).toContainText('Stripe');
    await expect(page.locator('[data-last-webhook]')).toContainText('never');

    const hold = page.locator('[data-multibanco-row="GS-E2E01-M"]');
    await expect(hold).toBeVisible();
    await expect(hold).toContainText('12345 / 123456789');
    await expect(hold).toContainText('€300.00');

    await expect(page.locator('[data-event-log]')).toBeVisible();
    await expect(page.locator('[data-no-events]')).toBeVisible();
    await expect(page.locator('[data-disputes]')).toContainText('Open disputes (0)');
  });

  test('typed CANCEL releases a Multibanco hold', async ({ page }) => {
    await loginAsOwner(page);
    await page.goto('/admin/payments');

    const hold = page.locator('[data-multibanco-row="GS-E2E01-M"]');
    await expect(hold).toBeVisible();

    page.once('dialog', (dialog) => dialog.accept('CANCEL'));
    await hold.getByRole('button', { name: 'Cancel hold' }).click();

    await expect(page.locator('[data-toast]')).toContainText('cancelled');
    await expect(page.locator('[data-multibanco-row="GS-E2E01-M"]')).toHaveCount(0);
  });
});
