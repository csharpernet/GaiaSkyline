const { test, expect } = require('@playwright/test');
const { loginAsOwner } = require('../helpers/admin');

// Prices admin (§8): the 12-month rates grid with bulk set/lock, CSV dry-run → apply, the setup page
// (seasons with overlap validation, promo codes with typed delete) and the quote preview whose total
// must match the public checkout quote API. All test ranges are far in the future and disjoint from
// every other spec's fixtures. Skipped unless E2E_SEAM=1.
test.describe('Admin prices', () => {
  test.skip(!process.env.E2E_SEAM, 'Set E2E_SEAM=1 and run the app with E2E:Enabled to run admin E2E.');

  const iso = (daysFromNow) => new Date(Date.now() + daysFromNow * 86400000).toISOString().slice(0, 10);

  test('bulk-set a price, lock it, and the admin preview matches the public quote', async ({ page, request }) => {
    await loginAsOwner(page);
    await page.goto('/admin/prices');
    await expect(page.getByRole('heading', { name: 'Prices', exact: true })).toBeVisible();
    await expect(page.locator('[data-rates-grid] > div')).toHaveCount(12);

    // Bulk-set 3 nights at €199 (the seeded minimum stay is 3, so the quote below uses 3 nights).
    await page.fill('#bulk-from', iso(300));
    await page.fill('#bulk-to', iso(302));
    await page.fill('#bulk-price', '199');
    await page.getByRole('button', { name: 'Set', exact: true }).click();
    await expect(page.locator('[data-toast]')).toContainText('Set €199');
    const cell = page.locator(`[data-rate-cell="${iso(300)}"]`);
    await expect(cell).toContainText('€199');
    await expect(cell).toContainText('Manual');

    // Lock the range → the cell shows the lock marker; imports can never overwrite it.
    await page.fill('#bulk-from', iso(300));
    await page.fill('#bulk-to', iso(302));
    await page.getByRole('button', { name: 'Lock', exact: true }).click();
    await expect(page.locator('[data-toast]')).toContainText('Locked 3');
    await expect(page.locator(`[data-rate-cell="${iso(300)}"]`)).toContainText('🔒');

    // Admin quote preview: 3 nights × €199 + fees, each night sourced "Manual".
    await page.fill('#qp-in', iso(300));
    await page.fill('#qp-out', iso(303));
    await page.getByRole('button', { name: 'Preview quote' }).click();
    await expect(page.locator('[data-quote-result]')).toBeVisible();
    await expect(page.locator('[data-quote-night]').first()).toContainText('Manual');
    const adminTotalText = await page.locator('[data-quote-total]').innerText();
    const adminTotal = Number(adminTotalText.replace('€', ''));

    // The public checkout quote must price the same stay identically.
    const response = await request.post('/api/quote', {
      data: { checkIn: iso(300), checkOut: iso(303), adults: 2, children: 0, infants: 0 },
    });
    expect(response.ok()).toBeTruthy();
    const quote = await response.json();
    expect(Number(quote.total)).toBeCloseTo(adminTotal, 2);
    expect(Number(quote.nightlySubtotal)).toBeCloseTo(199 * 3, 2);
  });

  test('CSV dry-run previews old vs new, then apply writes the rates', async ({ page }) => {
    await loginAsOwner(page);
    await page.goto('/admin/prices');

    const csv = `date,price,min_nights\n${iso(250)},145,2\n${iso(251)},150,\nbroken-row,10,`;
    await page.fill('textarea[name=csvText]', csv);
    await page.getByRole('button', { name: 'Preview changes' }).click();

    const previewPanel = page.locator('[data-csv-preview]');
    await expect(previewPanel).toBeVisible();
    await expect(previewPanel).toContainText('will change');
    await expect(previewPanel).toContainText('invalid');

    page.once('dialog', (dialog) => dialog.accept());
    await page.locator('[data-csv-apply]').click();
    await expect(page.locator('[data-toast]')).toContainText('Applied 2 rate(s)');
    await expect(page.locator(`[data-rate-cell="${iso(250)}"]`)).toContainText('€145');
  });

  test('seasons reject overlaps, promo codes round-trip with a typed delete', async ({ page }) => {
    await loginAsOwner(page);
    await page.goto('/admin/prices/setup');
    await expect(page.getByRole('heading', { name: /Seasons, fees/ })).toBeVisible();

    // Adding inside the seeded catch-all season carves it; a PARTIAL overlap is rejected with the
    // domain error naming the colliding season.
    const addSeason = page.locator('[data-add-season]');
    await addSeason.locator('input[name=start]').fill(iso(400));
    await addSeason.locator('input[name=end]').fill(iso(430));
    await addSeason.locator('input[name=price]').fill('175');
    await addSeason.getByRole('button', { name: 'Add season' }).click();
    await expect(page.locator('[data-toast]')).toContainText('Season added');

    await addSeason.locator('input[name=start]').fill(iso(410));
    await addSeason.locator('input[name=end]').fill(iso(440));
    await addSeason.locator('input[name=price]').fill('210');
    await addSeason.getByRole('button', { name: 'Add season' }).click();
    await expect(page.locator('[data-toast]')).toContainText('Overlaps');

    // Promo code: create, see it listed (normalized upper-case), then typed-DELETE it.
    const addPromo = page.locator('[data-add-promo]');
    await addPromo.locator('input[name=code]').fill('playw10');
    await addPromo.getByRole('button', { name: 'Add promo' }).click();
    await expect(page.locator('[data-toast]')).toContainText('Promo code added');
    const promoRow = page.locator('[data-promo-row="PLAYW10"]');
    await expect(promoRow).toBeVisible();

    page.once('dialog', (dialog) => dialog.accept('DELETE'));
    await promoRow.getByRole('button', { name: 'Delete' }).click();
    await expect(page.locator('[data-toast]')).toContainText('Promo code deleted');
    await expect(page.locator('[data-promo-row="PLAYW10"]')).toHaveCount(0);
  });
});
