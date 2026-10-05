const { test, expect } = require('@playwright/test');
const { loginAsOwner } = require('../helpers/admin');

// Stories editor: create + publish a story, see it on the public stories page, then change its slug and
// confirm the old URL 301-redirects to the new one. Skipped unless E2E_SEAM=1 with the app under the seam.
test.describe('Admin stories editor', () => {
  test.skip(!process.env.E2E_SEAM, 'Set E2E_SEAM=1 and run the app with E2E:Enabled to run admin E2E.');

  test('create → publish → shows on /en/stories, then a slug change 301s the old URL', async ({ page }) => {
    await loginAsOwner(page);
    await page.goto('/admin/stories/create');
    await expect(page.getByRole('heading', { name: 'New story' })).toBeVisible();

    const title = 'E2E Story ' + Date.now();
    await page.locator('input[name="Translations[0].Title"]').fill(title); // EN is the first tab
    // The cover radio is visually hidden (sr-only); click its wrapping label to select it.
    await page.locator('label:has(input[name="CoverMediaAssetId"])').first().click();
    await page.locator('input[name="IsPublished"]').check();
    await page.getByRole('button', { name: /create story/i }).click();

    await expect(page).toHaveURL(/\/admin\/stories\/[0-9a-f-]{36}$/i);
    const editUrl = page.url();
    const oldSlug = await page.locator('input[name="Slug"]').inputValue();
    expect(oldSlug).toBeTruthy();

    // The published story shows on the public stories index.
    await page.goto('/en/stories');
    await expect(page.getByText(title)).toBeVisible();
    expect((await page.request.get(`/en/stories/${oldSlug}`)).ok()).toBeTruthy();

    // Change the slug; the old URL must 301 to the new one.
    await page.goto(editUrl);
    const newSlug = 'e2e-renamed-' + Date.now();
    await page.locator('input[name="Slug"]').fill(newSlug);
    await page.getByRole('button', { name: /save story/i }).click();
    await expect(page).toHaveURL(editUrl);

    const redirect = await page.request.get(`/en/stories/${oldSlug}`, { maxRedirects: 0 });
    expect(redirect.status()).toBe(301);
    expect(redirect.headers()['location']).toContain(newSlug);
    expect((await page.request.get(`/en/stories/${newSlug}`)).ok()).toBeTruthy();
  });
});
