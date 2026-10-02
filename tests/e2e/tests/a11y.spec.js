const { test, expect } = require('@playwright/test');
const AxeBuilder = require('@axe-core/playwright').default;

// Public pages that must have zero serious/critical accessibility violations (Stage 3 acceptance).
// Covers each page template plus a second language to catch lang-specific markup regressions.
const paths = [
  '/en',
  '/en/gallery',
  '/en/stories',
  '/en/stories/view-from-the-balcony-a-first-timers-guide-to-the-douro',
  '/en/book',
  '/pt-pt',
];

for (const path of paths) {
  test(`no serious or critical a11y violations: ${path}`, async ({ page }) => {
    await page.goto(path, { waitUntil: 'load' });

    const results = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
      .analyze();

    const blocking = results.violations.filter(
      (v) => v.impact === 'serious' || v.impact === 'critical',
    );

    const summary = blocking.map((v) => ({
      id: v.id,
      impact: v.impact,
      help: v.help,
      nodes: v.nodes.map((n) => ({ target: n.target, failureSummary: n.failureSummary })),
    }));

    expect(summary, JSON.stringify(summary, null, 2)).toEqual([]);
  });
}
