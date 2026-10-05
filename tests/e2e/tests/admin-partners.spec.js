const { test, expect } = require('@playwright/test');
const { loginAsOwner } = require('../helpers/admin');

// Partner applications (§11): the seeded pending applications (reset each startup) are approved and
// rejected with a note; decided rows keep their badge. Skipped unless E2E_SEAM=1.
test.describe('Admin partner applications', () => {
  test.skip(!process.env.E2E_SEAM, 'Set E2E_SEAM=1 and run the app with E2E:Enabled to run admin E2E.');

  test('approve one application and reject the other with a note', async ({ page }) => {
    await loginAsOwner(page);
    await page.goto('/admin/partners');
    await expect(page.getByRole('heading', { name: 'Partner applications' })).toBeVisible();

    const approveRow = page.locator('[data-application-row="approve@partner.e2e"]');
    await expect(approveRow).toContainText('Pending');
    await approveRow.getByRole('button', { name: 'Approve' }).click();
    await expect(page.locator('[data-toast]')).toContainText('approved');
    await expect(page.locator('[data-application-row="approve@partner.e2e"]')).toContainText('Approved');

    const rejectRow = page.locator('[data-application-row="reject@partner.e2e"]');
    await rejectRow.getByPlaceholder('Decision note (optional)').fill('Audience too small for now');
    page.once('dialog', (dialog) => dialog.accept());
    await rejectRow.getByRole('button', { name: 'Reject' }).click();
    await expect(page.locator('[data-toast]')).toContainText('rejected');
    const rejected = page.locator('[data-application-row="reject@partner.e2e"]');
    await expect(rejected).toContainText('Rejected');
    await expect(rejected).toContainText('Audience too small for now');
  });
});
