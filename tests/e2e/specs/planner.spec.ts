import {
  expect,
  expectNoSeriousA11yViolations,
  signInAs,
  test,
} from './fixtures';

// Acceptance criteria from issue #64 (browser level). The model is unit-tested; the API is covered by scenarios.
test.describe('retirement planner', () => {
  test('runs a plan and shows the chance of success, a chart and the numbers', async ({
    page,
  }) => {
    await signInAs(page, 'Raj');
    await page.getByRole('link', { name: 'Planner' }).click();
    await expect(
      page.getByRole('heading', { level: 1, name: 'Retirement planner' }),
    ).toBeFocused();

    await page.getByLabel('Monthly saving until retirement').fill('2000');
    await page.getByRole('button', { name: 'Run the plan' }).click();

    const result = page.getByRole('heading', {
      level: 2,
      name: /\d+% chance your savings last to age 95/,
    });
    await expect(result).toBeVisible();
    await expect(page.locator('#planner-result')).toBeFocused();
    await expect(
      page.getByRole('img', { name: /Savings by age/ }),
    ).toBeVisible();

    await page.getByText('Show the numbers').click();
    await expect(page.getByRole('rowheader', { name: '95' })).toBeVisible();
    await expectNoSeriousA11yViolations(page);
  });

  test('the same plan gives the same answer', async ({ page }) => {
    await signInAs(page, 'Raj');
    await page.goto('/planner');
    const result = page.locator('#planner-result-heading');

    await page.getByRole('button', { name: 'Run the plan' }).click();
    await expect(result).toContainText('% chance');
    const first = await result.textContent();
    await page.getByRole('button', { name: 'Run the plan' }).click();

    await expect(result).toHaveText(first!);
  });

  test('impossible inputs are explained, not run', async ({ page }) => {
    await signInAs(page, 'Raj');
    await page.goto('/planner');

    await page.getByLabel('Retirement age').fill('30');
    await page.getByRole('button', { name: 'Run the plan' }).click();

    await expect(page.getByLabel('Retirement age')).toBeFocused();
    await expect(page.getByLabel('Retirement age')).toHaveAttribute(
      'aria-invalid',
      'true',
    );
    await expect(page.locator('#planner-result')).toHaveCount(0);
  });
});
