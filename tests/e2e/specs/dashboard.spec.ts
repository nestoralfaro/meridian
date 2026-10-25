import {
  expect,
  expectNoSeriousA11yViolations,
  signInAs,
  test,
} from './fixtures';

// Acceptance criteria from issue #20 (demo data: Dana's holdings are fixed, so the figures are exact).
test.describe('accounts overview', () => {
  test("Dana sees each account's balance and the total", async ({ page }) => {
    await signInAs(page, 'Dana');

    const accounts = page.getByTestId('account');
    await expect(accounts).toHaveCount(2);
    await expect(accounts.nth(0)).toContainText('Individual Brokerage');
    await expect(accounts.nth(0)).toContainText('$12,480.55');
    await expect(accounts.nth(1)).toContainText('Roth IRA');
    await expect(accounts.nth(1)).toContainText('$8,301.20');
    await expect(page.getByTestId('total-value')).toHaveText('$20,781.75');
    await expectNoSeriousA11yViolations(page);
  });

  test('day changes are signed and described in words', async ({ page }) => {
    await signInAs(page, 'Dana');

    const brokerage = page.getByTestId('account').nth(0);
    await expect(brokerage).toContainText('+$75.50');
    await expect(brokerage.locator('.change')).toHaveAttribute(
      'aria-label',
      /^Up \$75\.50 \(0\.61%\) today$/,
    );
  });

  test("the next person to sign in never sees the previous person's accounts", async ({
    page,
  }) => {
    await signInAs(page, 'Dana');
    await expect(page.getByTestId('total-value')).toHaveText('$20,781.75');
    await page.getByRole('button', { name: 'Sign out' }).click();

    await signInAs(page, 'Raj');

    await expect(page.getByTestId('account').first()).toBeVisible();
    await expect(page.getByText('Roth IRA')).toHaveCount(0); // Dana's account type; Raj has none
    await expect(page.getByTestId('total-value')).not.toHaveText('$20,781.75');
  });

  test('a new client with no accounts gets a friendly empty state', async ({
    page,
  }) => {
    await page.goto('/register');
    await page.getByLabel('Your name').fill('Empty Example');
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
      page.getByText("You don't have any accounts yet"),
    ).toBeVisible();
    await expectNoSeriousA11yViolations(page);
  });
});
