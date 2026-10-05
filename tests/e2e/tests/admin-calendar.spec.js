const { test, expect } = require('@playwright/test');
const { loginAsOwner } = require('../helpers/admin');

// Calendar admin (§7): month/week views with ICS hints, owner-block CRUD with the domain overlap
// error, conflict resolution and the duplicate-cleanup view. The E2E seam seeds (reset each startup):
// an OwnerUnavailable block at +120d, an ExternalBooking block at +130d, and an open conflict against
// the Confirmed fixture GS-E2E01-C (which occupies +90d..+93d). Skipped unless E2E_SEAM=1.
test.describe('Admin calendar', () => {
  test.skip(!process.env.E2E_SEAM, 'Set E2E_SEAM=1 and run the app with E2E:Enabled to run admin E2E.');

  const iso = (daysFromNow) => new Date(Date.now() + daysFromNow * 86400000).toISOString().slice(0, 10);

  test('month view shows seeded blocks with the right ICS-export hints and legend', async ({ page }) => {
    await loginAsOwner(page);

    // The two seeded blocks are selected by their seed notes — they may or may not share a month.
    await page.goto(`/admin/calendar?view=month&anchor=${iso(120)}`);
    await expect(page.locator('[data-calendar-legend]')).toContainText('Owner unavailable (in ICS export)');
    await expect(page.locator('[data-block-row]', { hasText: 'owner hold' })).toContainText('in ICS export');

    await page.goto(`/admin/calendar?view=month&anchor=${iso(130)}`);
    await expect(page.locator('[data-block-row]', { hasText: 'Hostify copy' })).toContainText('not exported');

    // Week view renders the same period style with a single row of taller cells.
    await page.goto(`/admin/calendar?view=week&anchor=${iso(130)}`);
    await expect(page.getByText(/Week of /)).toBeVisible();
  });

  test('adding a block over an active booking shows the domain error with the reference', async ({ page }) => {
    await loginAsOwner(page);
    await page.goto(`/admin/calendar?view=month&anchor=${iso(90)}`);

    await page.fill('#start', iso(90));
    await page.fill('#end', iso(92));
    await page.selectOption('#kind', 'external-booking');
    await page.getByRole('button', { name: 'Add block' }).click();

    await expect(page.locator('[data-toast]')).toContainText('GS-E2E01-C');
    await expect(page.locator('[data-toast]')).toContainText('overlap an active booking');
  });

  test('add, edit and typed-delete an owner block', async ({ page }) => {
    await loginAsOwner(page);
    await page.goto(`/admin/calendar?view=month&anchor=${iso(160)}`);

    await page.fill('#start', iso(160));
    await page.fill('#end', iso(162));
    await page.selectOption('#kind', 'owner-unavailable');
    await page.fill('#note', 'E2E temp block');
    await page.getByRole('button', { name: 'Add block' }).click();
    await expect(page.locator('[data-toast]')).toContainText('block added');
    const row = page.locator('[data-block-row]', { hasText: 'E2E temp block' });
    await expect(row).toBeVisible();

    // Edit the note through the inline form.
    await row.getByText('Edit').click();
    await row.locator('input[name=note]').fill('E2E temp block (edited)');
    await row.getByRole('button', { name: 'Save' }).click();
    await expect(page.locator('[data-toast]')).toContainText('Block updated');
    const edited = page.locator('[data-block-row]', { hasText: 'E2E temp block (edited)' });
    await expect(edited).toBeVisible();

    // Deleting requires typing DELETE into the prompt.
    page.once('dialog', (dialog) => dialog.accept('DELETE'));
    await edited.getByRole('button', { name: 'Delete' }).click();
    await expect(page.locator('[data-toast]')).toContainText('Block removed');
    await expect(page.locator('[data-block-row]', { hasText: 'E2E temp block' })).toHaveCount(0);
  });

  test('conflict resolution, manual-mode reimport and the duplicates page', async ({ page }) => {
    await loginAsOwner(page);
    await page.goto('/admin/calendar');

    // Manual mode: the re-import button is disabled with an explanation.
    await expect(page.locator('[data-force-reimport]')).toBeDisabled();
    await expect(page.locator('[data-reimport-explanation]')).toContainText('manual mode');

    // The seeded conflict row names the booking and resolves with a note.
    const conflict = page.locator('[data-conflict-row]', { hasText: 'GS-E2E01-C' });
    await expect(conflict).toBeVisible();
    await conflict.locator('input[name=note]').fill('Handled in Hostify');
    await conflict.getByRole('button', { name: 'Mark resolved' }).click();
    await expect(page.locator('[data-toast]')).toContainText('Conflict marked resolved');
    await expect(page.locator('[data-conflict-row]', { hasText: 'GS-E2E01-C' })).toHaveCount(0);

    // Duplicate-cleanup view loads (no duplicates until an iCal source is connected).
    await page.goto('/admin/calendar/duplicates');
    await expect(page.getByRole('heading', { name: 'Duplicate cleanup' })).toBeVisible();
    await expect(page.locator('[data-no-duplicates]')).toBeVisible();
  });
});
