const { test, expect } = require('@playwright/test');
const { loginAsOwner } = require('../helpers/admin');

// Media manager: drag-drop upload produces an asset, and alt text in all five languages makes it
// "ready for public". Skipped unless E2E_SEAM=1 with the app running under the E2E seam.
test.describe('Admin media manager', () => {
  test.skip(!process.env.E2E_SEAM, 'Set E2E_SEAM=1 and run the app with E2E:Enabled to run admin E2E.');

  // A small valid PNG; the upload pipeline resizes it to the responsive set (incl. AVIF).
  const PNG = Buffer.from(
    'iVBORw0KGgoAAAANSUhEUgAAABAAAAAQCAYAAAAf8/9hAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAAAdSURBVDhPY0iZ+vY/JZgBXYBUPGrAqAGjBgwWAwBvsOUfyEymUQAAAABJRU5ErkJggg==',
    'base64');

  test('upload an image then set alt text in all languages → ready for public', async ({ page }) => {
    await loginAsOwner(page);
    await page.goto('/admin/media');
    await expect(page.getByRole('heading', { name: 'Media', exact: true })).toBeVisible();

    const cards = page.locator('a[href^="/admin/media/"]');
    const before = await cards.count();

    // Upload via the hidden file input; the glue uploads then reloads, so the grid grows by one.
    await page.locator('#media-file-input').setInputFiles({ name: 'balcony.png', mimeType: 'image/png', buffer: PNG });
    await expect(cards).toHaveCount(before + 1, { timeout: 30000 });

    // Open the newest asset (first, ordered by upload time) — it has no alt yet.
    await cards.first().click();
    await expect(page.getByRole('heading', { name: 'Media asset' })).toBeVisible();
    await expect(page.getByText(/Needs alt/i)).toBeVisible();

    // Fill alt in every language and save.
    const inputs = page.locator('form[action$="/alt"] input[type="text"]');
    await expect(inputs).toHaveCount(5);
    for (let i = 0; i < 5; i++) {
      await inputs.nth(i).fill('A balcony view over the Douro and the Dom Luís I Bridge');
    }
    await page.getByRole('button', { name: /save alt text/i }).click();

    await expect(page.getByText(/Ready for public/i)).toBeVisible();
  });

  test('upload then soft-delete an unused image → it moves to the Deleted tab', async ({ page }) => {
    await loginAsOwner(page);
    await page.goto('/admin/media');

    const cards = page.locator('a[href^="/admin/media/"]');
    const before = await cards.count();
    await page.locator('#media-file-input').setInputFiles({ name: 'temp.png', mimeType: 'image/png', buffer: PNG });
    await expect(cards).toHaveCount(before + 1, { timeout: 30000 });

    await cards.first().click();
    await expect(page.getByRole('heading', { name: 'Media asset' })).toBeVisible();
    const assetPath = new URL(page.url()).pathname; // /admin/media/{id}

    // The delete form confirms via window.confirm (admin.js) — accept it.
    page.once('dialog', (d) => d.accept());
    await page.getByRole('button', { name: /delete asset/i }).click();
    await expect(page).toHaveURL(/\/admin\/media$/);

    // Gone from the default grid, present under Deleted.
    await expect(page.locator('a[href="' + assetPath + '"]')).toHaveCount(0);
    await page.goto('/admin/media?kind=deleted');
    await expect(page.locator('a[href="' + assetPath + '"]')).toBeVisible();
  });
});
