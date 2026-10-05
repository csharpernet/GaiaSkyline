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

  test('edit a page meta title → the public page shows it', async ({ page }) => {
    await loginAsOwner(page);
    await page.goto('/admin/seo');

    const title = 'E2E Gallery Meta ' + Date.now();
    await page.locator('input[data-page="/gallery"][data-lang="en"][data-seo-field="title"]').fill(title);
    await page.getByRole('button', { name: /save page meta/i }).click();
    await expect(page).toHaveURL(/\/admin\/seo$/);

    const html = await (await page.request.get('/en/gallery')).text();
    expect(html).toContain(title);
  });

  test('the SEO page shows the CWV panel, sitemap preview and a /sitemap.xml link', async ({ page }) => {
    await loginAsOwner(page);
    await page.goto('/admin/seo');

    // Core Web Vitals panel always renders (shows "No field data yet" until samples arrive).
    await expect(page.getByRole('heading', { name: 'Core Web Vitals (field)' })).toBeVisible();

    // The warnings section always renders (its count may be zero).
    await expect(page.getByRole('heading', { name: /^Warnings/ })).toBeVisible();

    await expect(page.getByRole('heading', { name: 'Sitemap preview' })).toBeVisible();
    await expect(page.locator('a[href="/sitemap.xml"]')).toBeVisible();
    // A known page is listed with a per-language link (host-agnostic — the href ends with the path).
    await expect(page.locator('a[href$="/en/book"]')).toBeVisible();
  });

  test('uncheck Index for a page → the public page emits robots noindex, and re-checking clears it', async ({ page }) => {
    await loginAsOwner(page);
    await page.goto('/admin/seo');

    const indexBox = page.locator('input[data-robots="index"][data-page="/book"][data-lang="en"]');

    // Noindex it.
    await indexBox.uncheck();
    await page.getByRole('button', { name: /save page meta/i }).click();
    await expect(page).toHaveURL(/\/admin\/seo$/);
    // The checkbox reflects the stored state after the round-trip.
    await expect(page.locator('input[data-robots="index"][data-page="/book"][data-lang="en"]')).not.toBeChecked();

    let html = await (await page.request.get('/en/book')).text();
    expect(html).toContain('name="robots" content="noindex, nofollow"');

    // Back to indexable.
    await page.locator('input[data-robots="index"][data-page="/book"][data-lang="en"]').check();
    await page.getByRole('button', { name: /save page meta/i }).click();
    await expect(page).toHaveURL(/\/admin\/seo$/);

    html = await (await page.request.get('/en/book')).text();
    expect(html).not.toContain('name="robots" content="noindex');
  });
});
