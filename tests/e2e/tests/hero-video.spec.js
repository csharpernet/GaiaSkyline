const { test, expect } = require('@playwright/test');

// Public hero-video behaviour. A live hero is seeded via the E2E seam (placeholder /media URLs — these tests
// assert JS behaviour, not video bytes, so no FFmpeg is needed). Seeding happens here, after the Lighthouse
// step, so the Core Web Vitals run still measures the poster-only fallback. Skipped unless E2E_SEAM=1.
test.describe('Hero background video', () => {
  test.skip(!process.env.E2E_SEAM, 'Set E2E_SEAM=1 and run the app with E2E:Enabled to run this suite.');

  test.beforeEach(async ({ page }) => {
    const res = await page.request.post('/test/seed-hero');
    expect(res.ok()).toBeTruthy();
  });

  // The browser sets video.currentSrc from passive <source> selection even with preload="none", so the
  // reliable "poster only" signal is that hero-video.js never committed to playing (no data-hero-started)
  // and the pause/play toggle stays hidden.
  async function heroStarted(page) {
    return page.locator('#hero-video').evaluate((el) => el.hasAttribute('data-hero-started'));
  }

  test('prefers-reduced-motion shows the poster only — the video never starts', async ({ page }) => {
    await page.emulateMedia({ reducedMotion: 'reduce' });
    await page.goto('/en');

    await expect(page.locator('#hero-video video')).toHaveCount(1);
    // Give hero-video.js its load + idle window; with reduced motion it must bail out before playing.
    await page.waitForTimeout(1500);
    expect(await heroStarted(page)).toBe(false);
    await expect(page.locator('[data-hero-toggle]')).toBeHidden();
  });

  test('Save-Data shows the poster only', async ({ page }) => {
    await page.addInitScript(() => {
      try {
        Object.defineProperty(navigator, 'connection', {
          configurable: true,
          get: () => ({ saveData: true, effectiveType: '4g' }),
        });
      } catch (_) {
        /* some engines expose connection as non-configurable; the test is best-effort there */
      }
    });
    await page.goto('/en');

    // Confirm the override actually took effect before relying on the poster-only behaviour.
    expect(await page.evaluate(() => !!(navigator.connection && navigator.connection.saveData))).toBe(true);
    await page.waitForTimeout(1500);
    expect(await heroStarted(page)).toBe(false);
  });

  test('a portrait viewport selects a mobile rendition', async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto('/en');

    const video = page.locator('#hero-video video');
    await expect
      .poll(async () => video.evaluate((v) => v.currentSrc), {
        timeout: 15000,
        message: 'the video should select a mobile (portrait) rendition',
      })
      .toContain('mobile');
  });
});
