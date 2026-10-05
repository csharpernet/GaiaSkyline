const { test, expect } = require('@playwright/test');
const { loginAsOwner } = require('../helpers/admin');

// Settings admin (§12): property save, external calendars (masked URL, non-persisting test fetch,
// enable/disable → Connected/Manual badge), notification recipients + test email, pricing provider
// (encrypted key shown masked), and language toggles (disable → 404 + out of sitemap; re-enable).
// Skipped unless E2E_SEAM=1.
test.describe('Admin settings', () => {
  test.skip(!process.env.E2E_SEAM, 'Set E2E_SEAM=1 and run the app with E2E:Enabled to run admin E2E.');

  test('property, calendars, notifications and pricing round-trip', async ({ page, request, baseURL }) => {
    test.setTimeout(120000);
    await loginAsOwner(page);
    await page.goto('/admin/settings');
    await expect(page.getByRole('heading', { name: 'Settings' })).toBeVisible();

    // Property: saving the current values round-trips cleanly.
    await page.locator('[data-property-card]').getByRole('button', { name: 'Save property' }).click();
    await expect(page.locator('[data-toast]')).toContainText('Property details saved');

    // Calendar: add (disabled) with the E2E fixture feed → masked URL, then test-fetch parses 2 events.
    const calName = `E2E-Cal-${Date.now().toString().slice(-6)}`;
    const add = page.locator('[data-add-calendar]');
    await add.locator('input[name=name]').fill(calName);
    await add.locator('input[name=icsUrl]').fill(`${baseURL}/test/fixture.ics`);
    await add.getByRole('button', { name: 'Add calendar' }).click();
    await expect(page.locator('[data-toast]')).toContainText('added');
    const row = page.locator(`[data-calendar-row="${calName}"]`);
    await expect(row.locator('[data-masked-url]')).toContainText('••••');
    await expect(row.locator('[data-masked-url]')).not.toContainText('/test/');

    await row.getByRole('button', { name: 'Test fetch' }).click();
    await expect(page.locator('[data-test-fetch-result]')).toContainText('2 event(s)');
    await expect(page.locator('[data-test-fetch-result]')).toContainText('Nothing was saved');

    // Enable → Connected badge; disable again so the dashboard stays in manual mode for other specs.
    await page.locator(`[data-calendar-row="${calName}"]`).getByRole('button', { name: 'Enable' }).click();
    await expect(page.locator('[data-calendar-mode]')).toContainText('Connected');
    await page.locator(`[data-calendar-row="${calName}"]`).getByRole('button', { name: 'Disable' }).click();
    await expect(page.locator('[data-calendar-mode]')).toContainText('Manual mode');

    // Notifications: save a PM recipient, then the test email reaches it (captured by the seam).
    await page.fill('#n-pm', 'pm.e2e@example.com');
    await page.getByRole('button', { name: 'Save recipients' }).click();
    await expect(page.locator('[data-toast]')).toContainText('recipients saved');
    await page.locator('[data-send-test-email]').click();
    await expect(page.locator('[data-toast]')).toContainText('Test email sent');
    await expect
      .poll(async () => {
        const res = await request.get('/test/emails');
        if (!res.ok()) return false;
        return (await res.json()).some((e) => e.to === 'pm.e2e@example.com' && e.subject.includes('test'));
      }, { timeout: 15000 })
      .toBeTruthy();

    // Pricing: store a provider + secret key; the reloaded page shows the key masked only.
    await page.selectOption('#pr-provider', 'PriceLabs');
    await page.fill('#pr-key', 'pl_live_e2e_secret_7731');
    await page.fill('#pr-floor', '50');
    await page.locator('[data-pricing-card]').getByRole('button', { name: 'Save pricing settings' }).click();
    await expect(page.locator('[data-toast]')).toContainText('Pricing provider saved');
    await expect(page.locator('[data-adapter-note]')).toContainText('Adapter not yet available');
    await expect(page.locator('label[for=pr-key]')).toContainText('••••7731');
    const html = await page.content();
    expect(html).not.toContain('pl_live_e2e_secret_7731');

    // Back to manual mode so the dashboard pricing panel reads as before for other specs.
    await page.selectOption('#pr-provider', 'None');
    await page.locator('[data-pricing-card]').getByRole('button', { name: 'Save pricing settings' }).click();
    await expect(page.locator('[data-toast]')).toContainText('manual mode');
  });

  test('disabling a language removes it from the site, sitemap and hreflang; re-enabling restores it', async ({ page, request }) => {
    // Each language flip invalidates the public cache; the re-renders are instant on CI but can stall
    // on a degraded dev box (see docs/runbook.md "Local LocalDB health") — budget generously.
    test.setTimeout(240000);
    await loginAsOwner(page);
    await page.goto('/admin/settings');

    // Disable German.
    const languagesCard = page.locator('[data-languages-card]');
    await languagesCard.locator('input[value=de]').uncheck();
    page.once('dialog', (dialog) => dialog.accept());
    await languagesCard.getByRole('button', { name: 'Save languages' }).click();
    await expect(page.locator('[data-toast]')).toContainText('Languages saved');

    expect((await request.get('/de')).status()).toBe(404);
    const sitemap = await (await request.get('/sitemap.xml')).text();
    expect(sitemap).not.toContain('/de');
    const home = await (await request.get('/en')).text();
    expect(home).not.toContain('hreflang="de"');

    // Re-enable → German serves again.
    await page.goto('/admin/settings');
    await languagesCard.locator('input[value=de]').check();
    page.once('dialog', (dialog) => dialog.accept());
    await languagesCard.getByRole('button', { name: 'Save languages' }).click();
    await expect(page.locator('[data-toast]')).toContainText('Languages saved');
    expect((await request.get('/de')).status()).toBe(200);
  });
});
