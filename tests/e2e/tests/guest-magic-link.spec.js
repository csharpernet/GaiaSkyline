const { test, expect } = require('@playwright/test');

// The guest magic-link → my-booking → cancel flow (Stage 6 carry-over). Requires the E2E seam: a known
// booking is seeded and outbound email is captured in memory, exposed at /test/emails. Skipped unless
// E2E_SEAM=1 (set by the CI browser-quality job running the app with E2E:Enabled).
//
// This test cancels the seeded booking. E2ESeeder resets it to AwaitingPayment on every app startup, so
// it passes on each fresh run (CI runs each test once against a freshly started app); to re-run locally
// against the same instance, restart the app first.
const BOOKING_REF = process.env.E2E_BOOKING_REF || 'GS-E2E01';
const GUEST_EMAIL = process.env.E2E_GUEST_EMAIL || 'guest.e2e@example.com';

test.describe('Guest magic link', () => {
  test.skip(!process.env.E2E_SEAM, 'Set E2E_SEAM=1 and run the app with E2E:Enabled to run guest E2E.');

  test('request a magic link, open the booking and cancel it', async ({ page, request }) => {
    // 1. Request the link.
    await page.goto('/en/account/magic-link');
    await page.fill('#Reference', BOOKING_REF);
    await page.fill('#Email', GUEST_EMAIL);
    await page.click('button[type=submit]');
    await expect(page.locator('body')).toContainText(/check your email|sent an access link/i);

    // 2. Read the captured email and extract the single-use consume link.
    let link = null;
    for (let attempt = 0; attempt < 20 && !link; attempt++) {
      const res = await request.get('/test/emails');
      if (res.ok()) {
        const emails = await res.json();
        const magic = emails.find((e) => e.html && e.html.includes('magic-link/consume'));
        if (magic) {
          const match = magic.html.match(/href="([^"]*magic-link\/consume[^"]*)"/);
          if (match) link = match[1].replace(/&amp;/g, '&');
        }
      }
      if (!link) await page.waitForTimeout(500);
    }
    expect(link, 'magic-link email was captured').toBeTruthy();

    // 3. Consume it → the booking page (booking-scoped cookie granted).
    await page.goto(link);
    await expect(page).toHaveURL(new RegExp(`/my/booking/${BOOKING_REF}`, 'i'));
    await expect(page.locator('body')).toContainText(BOOKING_REF);

    // 4. Cancel the booking.
    await page.getByRole('button', { name: /cancel booking/i }).click();
    await expect(page).toHaveURL(new RegExp(`/my/booking/${BOOKING_REF}`, 'i'));
    await expect(page.getByText(/^cancelled$/i)).toBeVisible();
  });
});
