const { test, expect } = require('@playwright/test');
const { loginAsOwner } = require('../helpers/admin');

// The hero-video admin panel renders and exposes the upload form. The full upload -> transcode -> live flow
// (which needs FFmpeg) is covered in 7E-4c. Skipped unless E2E_SEAM=1 with the app under the E2E seam.
test.describe('Admin hero video panel', () => {
  test.skip(!process.env.E2E_SEAM, 'Set E2E_SEAM=1 and run the app with E2E:Enabled to run admin E2E.');

  test('the hero panel renders with the upload form', async ({ page }) => {
    await loginAsOwner(page);
    await page.goto('/admin/media/hero');

    await expect(page.getByRole('heading', { name: 'Hero background video' })).toBeVisible();
    await expect(page.locator('#hero-upload-form')).toBeVisible();
    await expect(page.locator('#hero-file')).toBeVisible();
    await expect(page.getByRole('button', { name: /upload & process/i })).toBeVisible();

    // Reached from the media library.
    await page.goto('/admin/media');
    await expect(page.getByRole('link', { name: /manage hero video/i })).toBeVisible();
  });
});
