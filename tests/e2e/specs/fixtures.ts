import AxeBuilder from '@axe-core/playwright';
import { expect, test as base, type Page } from '@playwright/test';

/** WCAG 2.2 AA rule tags checked on every page. */
const WCAG_TAGS = ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'];

/**
 * Scans the current page with axe and fails on any serious or critical violation.
 * Every test that visits a page should call this, so accessibility is checked everywhere, not in one dedicated test.
 */
export async function expectNoSeriousA11yViolations(page: Page): Promise<void> {
  const results = await new AxeBuilder({ page }).withTags(WCAG_TAGS).analyze();
  const blocking = results.violations.filter(
    (v) => v.impact === 'serious' || v.impact === 'critical',
  );
  const summary = blocking.map(
    (v) => `${v.impact}: ${v.id} (${v.nodes.length}x) ${v.helpUrl}`,
  );
  expect(summary, `axe violations on ${page.url()}`).toEqual([]);
}

/** Signs in through the UI, as a visitor would, and waits for the dashboard. */
export async function signInAs(
  page: Page,
  persona: 'Dana' | 'Raj',
): Promise<void> {
  await page.goto('/sign-in');
  await page.getByRole('button', { name: `Sign in as ${persona}` }).click();
  await expect(
    page.getByRole('heading', { level: 1, name: 'Dashboard' }),
  ).toBeVisible();
}

export const test = base;
export { expect };
