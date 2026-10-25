import {
  expect,
  expectNoSeriousA11yViolations,
  signInAs,
  test,
} from './fixtures';

// Acceptance criteria from issue #22.
test.describe('recent activity', () => {
  test('the dashboard shows the five most recent transactions, newest first', async ({
    page,
  }) => {
    await signInAs(page, 'Dana');

    const recent = page.locator('section.recent');
    const items = recent.locator('li');
    await expect(items).toHaveCount(5);
    const dates = await recent
      .locator('time')
      .evaluateAll((els) => els.map((el) => el.getAttribute('datetime') ?? ''));
    expect(dates).toEqual([...dates].sort().reverse());
    await expectNoSeriousA11yViolations(page);
  });

  test('"View all activity" opens the full history', async ({ page }) => {
    await signInAs(page, 'Dana');

    await page.getByRole('link', { name: 'View all activity' }).click();

    await expect(page).toHaveURL(/\/activity$/);
    await expect(
      page.getByRole('heading', { level: 1, name: 'Activity' }),
    ).toBeFocused();
    expect(await page.locator('mer-activity-list li').count()).toBeGreaterThan(
      5,
    );
    await expectNoSeriousA11yViolations(page);
  });

  test('a new client is told where transactions will appear', async ({
    page,
  }) => {
    await page.goto('/register');
    await page.getByLabel('Your name').fill('Activity Example');
    await page
      .getByLabel('Email', { exact: true })
      .fill(
        `run-${Date.now()}-${Math.random().toString(36).slice(2, 8)}@e2e.meridian.test`,
      );
    await page
      .getByLabel('Password', { exact: true })
      .fill('correct horse battery staple');
    await page.getByRole('button', { name: 'Create account' }).click();

    await expect(
      page.getByText('Your transactions will appear here'),
    ).toBeVisible();
  });
});
