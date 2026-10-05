const { test, expect } = require('@playwright/test');
const { loginAsOwner } = require('../helpers/admin');

// SEO redirects manager: add a rule and confirm the old path 301s to the new one. Skipped unless E2E_SEAM=1.
test.describe('Admin SEO redirects', () => {
  test.skip(!process.env.E2E_SEAM, 'Set E2E_SEAM=1 and run the app with E2E:Enabled to run admin E2E.');

  test('add a redirect → the old path 301s to the new one', async ({ page }) => {
    await loginAsOwner(page);
    await page.goto('/admin/seo');
    await expect(page.getByRole('heading', { name: 'SEO' })).toBeVisible();

    const from = '/en/e2e-old-' + Date.now();
    const to = '/en/book';
    await page.locator('#fromPath').fill(from);
    await page.locator('#toPath').fill(to);
    await page.getByRole('button', { name: /^add$/i }).click();

    await expect(page.locator('td', { hasText: from })).toBeVisible();

    const resp = await page.request.get(from, { maxRedirects: 0 });
    expect(resp.status()).toBe(301);
    expect(resp.headers()['location']).toBe(to);
  });
});
