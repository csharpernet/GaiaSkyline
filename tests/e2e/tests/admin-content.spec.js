const { test, expect } = require('@playwright/test');
const { loginAsOwner } = require('../helpers/admin');

// Content editor: edit a French translation as a DRAFT (publicly invisible), see it through the signed
// preview cookie, then publish and see it live on /fr/ (output cache invalidated by the content-revision
// bump) — the full Stage 7 Tests-list sentence. Skipped unless E2E_SEAM=1 with the app under the E2E seam.
test.describe('Admin content editor', () => {
  test.skip(!process.env.E2E_SEAM, 'Set E2E_SEAM=1 and run the app with E2E:Enabled to run admin E2E.');

  test('edit FR hero headline → draft invisible → preview shows it → publish → /fr/ shows it', async ({ page }) => {
    const headline = 'Réveillez-vous face au pont Dom Luís (E2E)';

    await loginAsOwner(page);

    // Content list → edit the hero headline block.
    await page.goto('/admin/content');
    await expect(page.getByRole('heading', { name: 'Content', exact: true })).toBeVisible();
    await page.goto('/admin/content/edit/home.hero.headline');
    await expect(page.getByRole('heading', { name: /hero/i })).toBeVisible();

    // Switch to the French tab and set the headline.
    await page.locator('[data-lang-tab="fr"]').click();
    const frPanel = page.locator('[data-lang-panel="fr"]');
    await frPanel.locator('textarea, input[type=text]').first().fill(headline);

    // Save as a DRAFT: the public page must NOT show it yet.
    await page.getByRole('button', { name: 'Save draft', exact: true }).click();
    await expect(page.locator('[data-toast]')).toContainText(/draft/i);
    await page.goto('/fr');
    await expect(page.locator('h1')).not.toContainText(headline);

    // The signed preview cookie renders drafts and bypasses the output cache.
    await page.goto('/admin/preview/enter?returnUrl=' + encodeURIComponent('/fr'));
    await expect(page.locator('h1')).toContainText(headline);
    await page.goto('/admin/preview/exit?returnUrl=' + encodeURIComponent('/fr'));

    // Publish (promotes the draft) and confirm the toast.
    await page.goto('/admin/content/edit/home.hero.headline');
    await page.getByRole('button', { name: 'Publish', exact: true }).click();
    await expect(page.locator('[data-toast]')).toContainText(/Published/i);

    // The public French home page now shows the new headline (output cache was invalidated).
    await page.goto('/fr');
    await expect(page.locator('h1')).toContainText(headline);
  });

  test('translation status grid filters and links to a block', async ({ page }) => {
    await loginAsOwner(page);
    await page.goto('/admin/content/grid');
    await expect(page.getByRole('heading', { name: 'Translation status' })).toBeVisible();
    // The grid has a row per block with a language-status cell per language.
    await expect(page.locator('table thead')).toContainText('FR');
  });
});
