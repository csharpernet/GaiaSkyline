const { test, expect } = require('@playwright/test');

// Always-on checks: the public /book page works without Stripe (calendars + live quote via the APIs).
test.describe('Book page', () => {
    test('renders the calendars, guest picker and reserve control', async ({ page }) => {
        await page.goto('/en/book', { waitUntil: 'load' });

        await expect(page.locator('#booking-form')).toBeVisible();
        // Flatpickr enhances the date inputs into inline calendars.
        await expect(page.locator('.flatpickr-calendar').first()).toBeVisible();
        await expect(page.locator('#reserve')).toBeVisible();
        await expect(page.locator('#quote-panel')).toBeVisible();
    });

    test('picking dates produces a live quote and enables Reserve', async ({ page }) => {
        await page.goto('/en/book', { waitUntil: 'load' });

        // Pick the first two selectable (not disabled, not prev-month) days in each calendar.
        var calendars = page.locator('.flatpickr-calendar');
        await calendars.nth(0).locator('.flatpickr-day:not(.flatpickr-disabled):not(.prevMonthDay)').nth(10).click();
        await calendars.nth(1).locator('.flatpickr-day:not(.flatpickr-disabled):not(.prevMonthDay)').nth(15).click();

        // The quote panel fills in a total (or a clear minimum-nights message).
        await expect(page.locator('#quote-panel')).toContainText(/€|night/i, { timeout: 10000 });
    });
});

// Stripe test-mode flows. These require the app to run with Stripe test keys (and, for confirmation,
// a reachable webhook / the Stripe CLI). They are skipped unless STRIPE_E2E=1 so CI stays green until
// the keys are configured as CI secrets. See docs/runbook.md for the manual procedure and test cards.
test.describe('Checkout (Stripe test mode)', () => {
    test.skip(!process.env.STRIPE_E2E, 'Set STRIPE_E2E=1 with Stripe test keys to run the payment flows.');

    const CHECKOUT = '/en/book/checkout?checkIn=2027-07-01&checkOut=2027-07-06&adults=2&children=0&infants=0';

    async function fillGuest(page) {
        await page.goto(CHECKOUT, { waitUntil: 'load' });
        await page.fill('#name', 'Test Guest');
        await page.fill('#email', 'test.guest@example.com');
        await page.fill('#phone', '+351 912 345 678');
    }

    async function fillCard(page, number) {
        var frame = page.frameLocator('iframe[title*="Secure payment"], iframe[name^="__privateStripeFrame"]').first();
        await frame.locator('[name="number"]').fill(number);
        await frame.locator('[name="expiry"]').fill('12 / 34');
        await frame.locator('[name="cvc"]').fill('123');
    }

    test('card 4242 happy path reaches the confirmation page', async ({ page }) => {
        await fillGuest(page);
        await fillCard(page, '4242 4242 4242 4242');
        await page.click('#pay');
        await page.waitForURL(/\/book\/confirmation\//, { timeout: 30000 });
        await expect(page.locator('#confirmation')).toBeVisible();
    });

    test('3DS card 4000 0025 0000 3155 completes the authentication', async ({ page }) => {
        await fillGuest(page);
        await fillCard(page, '4000 0025 0000 3155');
        await page.click('#pay');
        // Stripe shows a 3DS challenge iframe; the test helper confirms it.
        var challenge = page.frameLocator('iframe[name^="__privateStripeFrame"]')
            .frameLocator('iframe[name="stripe-challenge-frame"]');
        await challenge.getByRole('button', { name: /complete/i }).click({ timeout: 20000 });
        await page.waitForURL(/\/book\/confirmation\//, { timeout: 30000 });
    });

    test('declined card 4000 0000 0000 0002 shows an error, then a good card succeeds', async ({ page }) => {
        await fillGuest(page);
        await fillCard(page, '4000 0000 0000 0002');
        await page.click('#pay');
        await expect(page.locator('#payment-error')).toContainText(/declin/i, { timeout: 30000 });

        await fillCard(page, '4242 4242 4242 4242');
        await page.click('#pay');
        await page.waitForURL(/\/book\/confirmation\//, { timeout: 30000 });
    });
});
