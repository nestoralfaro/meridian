import { expect, expectNoSeriousA11yViolations, test } from './fixtures';

// Acceptance criteria from issue #23.
test.describe('app shell', () => {
  test('mobile: primary navigation is a bottom bar and nothing scrolls horizontally', async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto('/');

    const nav = page.getByRole('navigation', { name: 'Primary' });
    await expect(nav).toBeVisible();
    const box = await nav.boundingBox();
    expect(box, 'primary navigation is rendered').not.toBeNull();
    expect(box!.y + box!.height).toBeGreaterThanOrEqual(844 - 1); // docked to the bottom edge
    expect(await hasHorizontalScroll(page)).toBe(false);
    await expectNoSeriousA11yViolations(page);
  });

  test('desktop: primary navigation sits in the header', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 800 });
    await page.goto('/');

    const nav = page.getByRole('navigation', { name: 'Primary' });
    const box = await nav.boundingBox();
    expect(box!.y).toBeLessThan(120);
  });

  test('keyboard: the first Tab reaches "Skip to main content", which moves focus to the main region', async ({ page, browserName }) => {
    test.skip(browserName === 'webkit', 'Desktop Safari does not Tab to links by default (system setting)');
    await page.goto('/');

    await page.keyboard.press('Tab');
    const skip = page.getByRole('link', { name: 'Skip to main content' });
    await expect(skip).toBeFocused();
    await expect(skip).toBeVisible(); // visible when focused

    await page.keyboard.press('Enter');
    await expect(page.locator('#main-content')).toBeFocused();
  });

  test('navigation moves focus to the new page heading and updates the title', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 800 });
    await page.goto('/');

    await page.getByRole('navigation', { name: 'Primary' }).getByRole('link', { name: 'Dashboard' }).click();

    await expect(page).toHaveTitle('Dashboard | Meridian');
    await expect(page.getByRole('heading', { level: 1, name: 'Dashboard' })).toBeFocused();
    await expect(page.getByRole('navigation', { name: 'Primary' }).getByRole('link', { name: 'Dashboard' })).toHaveAttribute('aria-current', 'page');
  });

  test('dark theme meets the same accessibility bar on every page', async ({ page }) => {
    await page.emulateMedia({ colorScheme: 'dark' });
    for (const path of ['/', '/dashboard', '/no-such-page']) {
      await page.goto(path);
      await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
      await expectNoSeriousA11yViolations(page);
    }
  });

  test('200% zoom: content stays available without horizontal scrolling', async ({ page }) => {
    // A 1280px-wide window at 200% zoom lays out like a 640px viewport.
    await page.setViewportSize({ width: 640, height: 400 });
    for (const path of ['/', '/dashboard', '/no-such-page']) {
      await page.goto(path);
      await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
      expect(await hasHorizontalScroll(page), `horizontal scroll on ${path}`).toBe(false);
    }
  });
});

async function hasHorizontalScroll(page: import('@playwright/test').Page): Promise<boolean> {
  return page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth);
}
