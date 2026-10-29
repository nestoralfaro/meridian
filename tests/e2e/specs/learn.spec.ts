import { expect, expectNoSeriousA11yViolations, test } from './fixtures';

// Acceptance criteria from issue #66. Public pages: no sign-in.
test.describe('learn center', () => {
  test('visitors browse and read articles', async ({ page }) => {
    await page.goto('/');
    await page
      .getByRole('link', { name: 'Learn', exact: true })
      .first()
      .click();

    await expect(
      page.getByRole('heading', { level: 1, name: 'Learn' }),
    ).toBeFocused();
    await expect(page.getByRole('heading', { level: 2 })).toHaveCount(3);
    await expectNoSeriousA11yViolations(page);

    await page.getByRole('link', { name: 'What is asset allocation?' }).click();

    await expect(
      page.getByRole('heading', {
        level: 1,
        name: 'What is asset allocation?',
      }),
    ).toBeFocused();
    await expect(page).toHaveTitle('What is asset allocation? | Meridian');
    // Headings go h1 → h2 with no skipped levels.
    await expect(page.locator('article :is(h1, h2, h3)')).toHaveText([
      'What is asset allocation?',
      'The short version',
      'Why the mix matters',
      'Where you see it in Meridian',
      'Rebalancing',
    ]);
    await expectNoSeriousA11yViolations(page);
  });

  test('drafts are not published', async ({ page }) => {
    await page.goto('/learn/fees-draft');

    await expect(
      page.getByRole('heading', { level: 1, name: 'Article not found' }),
    ).toBeVisible();
  });
});
