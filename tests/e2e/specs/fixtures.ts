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
  const blocking = results.violations.filter((v) => v.impact === 'serious' || v.impact === 'critical');
  const summary = blocking.map((v) => `${v.impact}: ${v.id} (${v.nodes.length}x) ${v.helpUrl}`);
  expect(summary, `axe violations on ${page.url()}`).toEqual([]);
}

export const test = base;
export { expect };
