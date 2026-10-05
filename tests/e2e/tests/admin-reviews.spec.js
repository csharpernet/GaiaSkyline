const { test, expect } = require('@playwright/test');
const { loginAsOwner } = require('../helpers/admin');

// Reviews admin (§10): add an off-platform review → it reaches the public reviews API; unpublish →
// it disappears; typo edit round-trips. Uses its own created review (the six seeded ones stay
// untouched). Skipped unless E2E_SEAM=1.
test.describe('Admin reviews', () => {
  test.skip(!process.env.E2E_SEAM, 'Set E2E_SEAM=1 and run the app with E2E:Enabled to run admin E2E.');

  test('add off-platform review → public API shows it; unpublish hides it; edit fixes a typo', async ({ page, request }) => {
    const guest = `Playwright${Date.now().toString().slice(-6)}`;
    const publicAuthors = async () => {
      const res = await request.get('/api/reviews');
      expect(res.ok()).toBeTruthy();
      return (await res.json()).map((r) => r.guestFirstName);
    };

    await loginAsOwner(page);
    await page.goto('/admin/reviews');
    await expect(page.getByRole('heading', { name: 'Reviews' })).toBeVisible();

    // Add + publish immediately.
    const add = page.locator('[data-add-review]');
    await add.locator('input[name=stayedOn]').fill('2026-08-15');
    await add.locator('input[name=source]').fill('Google');
    await add.locator('input[name=guestFirstName]').fill(guest);
    await add.locator('textarea[name=body]').fill('Stunning view and a flawless stay — tipo.');
    await add.getByRole('button', { name: 'Add review' }).click();
    await expect(page.locator('[data-toast]')).toContainText('published');
    expect(await publicAuthors()).toContain(guest);

    // Fix the typo through the inline edit.
    const row = page.locator('[data-review-row]', { hasText: guest });
    await row.getByText('Edit').click();
    await row.locator('textarea[name=body]').fill('Stunning view and a flawless stay — typo fixed.');
    await row.getByRole('button', { name: 'Save changes' }).click();
    await expect(page.locator('[data-toast]')).toContainText('Review updated');
    await expect(page.locator('[data-review-row]', { hasText: guest })).toContainText('typo fixed');

    // Unpublish → gone from the public API.
    await page.locator('[data-review-row]', { hasText: guest }).getByRole('button', { name: 'Unpublish' }).click();
    await expect(page.locator('[data-toast]')).toContainText('unpublished');
    expect(await publicAuthors()).not.toContain(guest);
  });
});
