const { test, expect } = require('@playwright/test');
const { loginAsOwner } = require('../helpers/admin');

// Bookings admin: list + filter, then a detail status action and a notes save. Skipped unless E2E_SEAM=1.
// The E2E seam seeds GS-E2E01 (AwaitingPayment) and GS-E2E01-C (Confirmed) on every startup.
test.describe('Admin bookings', () => {
  test.skip(!process.env.E2E_SEAM, 'Set E2E_SEAM=1 and run the app with E2E:Enabled to run admin E2E.');

  test('list shows the seeded bookings and filters by status', async ({ page }) => {
    await loginAsOwner(page);
    await page.goto('/admin/bookings');
    await expect(page.getByRole('heading', { name: 'Bookings' })).toBeVisible();

    await expect(page.getByRole('link', { name: 'GS-E2E01-C' })).toBeVisible();
    await expect(page.getByRole('link', { name: 'GS-E2E01', exact: true })).toBeVisible();

    // Filter to Confirmed → the confirmed booking stays, the awaiting one drops out.
    await page.selectOption('#status', 'Confirmed');
    await page.getByRole('button', { name: 'Filter' }).click();
    await expect(page).toHaveURL(/status=Confirmed/);
    await expect(page.getByRole('link', { name: 'GS-E2E01-C' })).toBeVisible();
    await expect(page.getByRole('link', { name: 'GS-E2E01', exact: true })).toHaveCount(0);
  });

  test('manual booking: create with quote preview, resend confirmation, typed cancel', async ({ page, request }) => {
    // Emails go through Hangfire, whose queue poll interval is 15 s — two email waits plus page
    // round-trips do not fit the default 60 s budget, so this one test gets its own.
    test.setTimeout(180000);
    const iso = (daysFromNow) => new Date(Date.now() + daysFromNow * 86400000).toISOString().slice(0, 10);
    const guestEmail = `walkin.e2e.${Date.now()}@example.com`;
    const emailsTo = async () => {
      const res = await request.get('/test/emails');
      if (!res.ok()) return 0;
      return (await res.json()).filter((e) => e.to === guestEmail).length;
    };

    await loginAsOwner(page);
    await page.goto('/admin/bookings/new');

    // Stay dates far beyond every seeded fixture so occupancy never clashes across specs.
    await page.fill('#CheckIn', iso(200));
    await page.fill('#CheckOut', iso(203));
    await page.fill('#GuestName', 'Walk In E2E');
    await page.fill('#GuestEmail', guestEmail);
    await page.fill('#GuestPhone', '+351911222333');

    // Server-side quote preview renders the breakdown before anything is created.
    await page.getByRole('button', { name: 'Preview price' }).click();
    await expect(page.locator('[data-quote-preview]')).toBeVisible();
    await expect(page.locator('[data-quote-total]')).toContainText('€');

    await page.getByRole('button', { name: 'Create confirmed booking' }).click();
    await expect(page.locator('[data-toast]')).toContainText('Manual booking created');
    await expect(page).toHaveURL(/\/admin\/bookings\/[0-9a-fA-F-]{36}$/);
    const detailUrl = page.url();
    await expect(page.locator('body')).toContainText('cash');
    await expect(page.locator('body')).toContainText('Confirmed');
    await expect(page.locator('[data-booking-audit]')).toContainText('booking.manual-create');

    // The guest confirmation email was sent (captured by the E2E seam), and "Resend" sends another.
    // Generous polls: Hangfire picks queued jobs up on a 15 s interval.
    await expect.poll(emailsTo, { timeout: 45000, intervals: [1000] }).toBeGreaterThan(0);
    const before = await emailsTo();
    await page.getByRole('button', { name: 'Resend confirmation' }).click();
    await expect(page.locator('[data-toast]')).toContainText('resent', { timeout: 20000 });
    await expect.poll(emailsTo, { timeout: 45000, intervals: [1000] }).toBeGreaterThan(before);

    // The new payment-method filter finds the cash booking.
    await page.goto('/admin/bookings?payment=cash');
    await expect(page.locator('tbody')).toContainText('Walk In E2E');

    // Cancel requires typing CANCEL into the confirmation prompt; €0 refund (manual payment, no Stripe).
    await page.goto(detailUrl);
    page.once('dialog', (dialog) => dialog.accept('CANCEL'));
    await page.getByRole('button', { name: 'Cancel booking' }).click();
    await expect(page.locator('[data-toast]')).toContainText('cancelled');
    await expect(page.locator('body')).toContainText('Cancelled');
  });

  test('detail allows a check-in transition and saving notes', async ({ page }) => {
    await loginAsOwner(page);
    await page.goto('/admin/bookings');
    // GS-E2E01-B is the dedicated, already-synced fixture for this destructive test, so mutating it never
    // affects the dashboard manual-sync to-do (which keys off Confirmed/Cancelled unsynced bookings).
    await page.getByRole('link', { name: 'GS-E2E01-B' }).click();
    await expect(page).toHaveURL(/\/admin\/bookings\/[0-9a-fA-F-]{36}$/);

    // A Confirmed booking can be marked checked in.
    await page.getByRole('button', { name: 'Mark checked in' }).click();
    await expect(page.locator('[data-toast]')).toContainText('checked in');

    // Saving internal notes.
    await page.fill('textarea[name=notes]', 'Guest requested a late checkout');
    await page.getByRole('button', { name: 'Save notes' }).click();
    await expect(page.locator('[data-toast]')).toContainText('Notes saved');
  });
});
