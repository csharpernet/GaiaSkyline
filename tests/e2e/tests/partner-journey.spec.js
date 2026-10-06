const { test, expect } = require('@playwright/test');
const { loginAsOwner } = require('../helpers/admin');

// The full influencer journey (Stage 8 Part A): apply on the public form → the owner approves (which
// sends the 7-day invite) → the partner finishes onboarding from the emailed link (password, terms,
// IBAN) → signs in and sees their dashboard + code → a ?ref= link 301-strips and sets the cookie → the
// owner records a phone booking citing the code → the commission appears for both sides.
// Self-contained: it applies with its own unique email, so the §11 fixtures stay untouched.
test.describe('Partner journey', () => {
  test.skip(!process.env.E2E_SEAM, 'Set E2E_SEAM=1 and run the app with E2E:Enabled to run this suite.');

  test('apply → approve → onboard → attribute a booking → commission visible', async ({ page, browser, request }) => {
    test.setTimeout(240_000);
    const unique = Date.now().toString(36);
    const partnerEmail = `journey-${unique}@partner.e2e`;
    const partnerPassword = `Journey-Pass-${unique}-12chars!`;

    // 1. Apply on the public form.
    await page.goto('/en/partners');
    await page.getByRole('link', { name: /apply/i }).click();
    await page.locator('#pa-name').fill('Journey Partner');
    await page.locator('#pa-email').fill(partnerEmail);
    await page.locator('#pa-social').fill('https://instagram.com/journey');
    await page.locator('#pa-audience').fill('5000');
    await page.getByLabel(/I agree/i).check();
    await page.getByRole('button', { name: 'Send application' }).click();
    await expect(page.locator('body')).toContainText(/your application is in/i);

    // 2. The owner approves it — approval creates the partner and emails the invite.
    await loginAsOwner(page);
    await page.goto('/admin/partners');
    const row = page.locator(`[data-application-row="${partnerEmail}"]`);
    await row.getByRole('button', { name: 'Approve' }).click();
    await expect(page.locator('[data-toast]')).toContainText(/invite email is on its way/i);

    // 3. Pull the invite link out of the captured email.
    let joinLink = null;
    for (let attempt = 0; attempt < 20 && !joinLink; attempt++) {
      const res = await request.get('/test/emails');
      if (res.ok()) {
        const emails = await res.json();
        const invite = emails.find((e) => e.to === partnerEmail && e.html && e.html.includes('/partners/join'));
        if (invite) {
          const match = invite.html.match(/href="([^"]*partners\/join[^"]*)"/);
          if (match) joinLink = match[1].replace(/&amp;/g, '&');
        }
      }
      if (!joinLink) await page.waitForTimeout(500);
    }
    expect(joinLink, 'invite email was captured').toBeTruthy();

    // 4. Onboard in a fresh browser context (the partner is not the owner). The email embeds the
    // configured public base URL, which differs from the test host locally — navigate by path.
    const joinPath = (() => {
      const url = new URL(joinLink);
      return url.pathname + url.search;
    })();
    const partnerContext = await browser.newContext();
    const partnerPage = await partnerContext.newPage();
    await partnerPage.goto(joinPath);
    await expect(partnerPage.locator('body')).toContainText(/your promo code is/i);
    const code = (await partnerPage.locator('strong.font-mono').first().textContent()).trim();
    expect(code).toMatch(/^JOURNEY\d{2,3}$/);

    await partnerPage.locator('#pj-password').fill(partnerPassword);
    await partnerPage.locator('#pj-iban').fill('PT50 0002 0123 1234 5678 9015 4');
    await partnerPage.locator('#pj-holder').fill('Journey Partner');
    await partnerPage.locator('#pj-tax').fill('123456789');
    await partnerPage.locator('#pj-country').fill('PT');
    await partnerPage.getByLabel(/I accept the partner terms/i).check();
    await partnerPage.getByRole('button', { name: 'Activate my partner account' }).click();
    await expect(partnerPage.locator('body')).toContainText(/your partner account is ready/i);

    // The link is single-use: opening it again shows the expired message.
    await partnerPage.goto(joinPath);
    await expect(partnerPage.locator('body')).toContainText(/expired or was already used/i);

    // 5. Partner signs in and sees the dashboard: code, link builder, empty bookings.
    await partnerPage.goto('/en/account/login');
    await partnerPage.getByLabel(/email/i).fill(partnerEmail);
    await partnerPage.getByLabel(/password/i).fill(partnerPassword);
    await partnerPage.getByRole('button', { name: /sign in|log in/i }).click();
    await partnerPage.goto('/en/partners/dashboard');
    await expect(partnerPage.locator('[data-partner-code]')).toHaveText(code);
    await expect(partnerPage.locator('[data-link-output]')).toHaveValue(new RegExp(`ref=${code}$`));

    // 6. A referral link 301s to the clean URL and sets the gs_ref cookie (ADR 0019).
    const refResponse = await partnerContext.request.get(`/en/gallery?ref=${code}`, { maxRedirects: 0 });
    expect(refResponse.status()).toBe(301);
    expect(refResponse.headers()['location']).toBe('/en/gallery');

    // 7. The owner records a phone booking citing the code (far-future dates, clear of all fixtures).
    const iso = (d) => {
      const date = new Date();
      date.setDate(date.getDate() + d);
      return date.toISOString().slice(0, 10);
    };
    await page.goto('/admin/bookings/new');
    await page.locator('input[name="CheckIn"]').fill(iso(450));
    await page.locator('input[name="CheckOut"]').fill(iso(453));
    await page.locator('input[name="GuestName"]').fill('Phone Guest');
    await page.locator('input[name="GuestEmail"]').fill(`phone-${unique}@example.com`);
    await page.locator('input[name="GuestPhone"]').fill('+351911222333');
    await page.locator('#PromoCode').fill(code);
    await page.locator('[data-create-manual-booking]').click();
    await expect(page.locator('[data-toast]')).toContainText(/booking/i);

    // 8. The commission is visible to both sides (created on confirmation, ADR 0020).
    await page.goto('/admin/partners/ledger');
    await expect(page.locator('body')).toContainText('Journey Partner');

    await partnerPage.goto('/en/partners/dashboard');
    await expect(partnerPage.locator('table tbody tr').first()).toContainText('Phone');
    await expect(partnerPage.locator('table tbody tr').first()).toContainText('Pending');

    await partnerContext.close();
  });
});
