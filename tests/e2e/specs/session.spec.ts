import {
  expect,
  expectNoSeriousA11yViolations,
  signInAs,
  test,
} from './fixtures';

// Acceptance criteria from issue #18. Playwright's clock emulation fast-forwards the browser's timers,
// so the 13- and 15-minute idle thresholds run in seconds.
test.describe('session lifecycle', () => {
  test('signing out ends the session', async ({ page }) => {
    await signInAs(page, 'Dana');

    await page.getByRole('button', { name: 'Sign out' }).click();

    await expect(page).toHaveURL(/\/sign-in$/);
    await page.goto('/dashboard');
    await expect(page).toHaveURL(/\/sign-in\?returnUrl=%2Fdashboard$/);
  });

  test('a warning appears after 13 idle minutes, with focus on "Stay signed in"', async ({
    page,
  }) => {
    await page.clock.install();
    await signInAs(page, 'Dana');

    await page.clock.fastForward('13:00');

    const dialog = page.getByRole('dialog', { name: 'Are you still there?' });
    await expect(dialog).toBeVisible();
    await expect(dialog).toContainText("you'll be signed out in 2:00");
    await expect(
      page.getByRole('button', { name: 'Stay signed in' }),
    ).toBeFocused();
    await expectNoSeriousA11yViolations(page);
  });

  test('"Stay signed in" keeps the session', async ({ page }) => {
    await page.clock.install();
    await signInAs(page, 'Dana');
    await page.clock.fastForward('13:00');

    await page.getByRole('button', { name: 'Stay signed in' }).click();

    await expect(page.getByRole('dialog')).toBeHidden();
    await page.clock.fastForward('12:00');
    await expect(page).toHaveURL(/\/dashboard$/);
    await expect(page.getByText('Welcome back, Dana')).toBeVisible();
  });

  test('after 15 idle minutes the visitor is signed out and told why', async ({
    page,
  }) => {
    await page.clock.install();
    await signInAs(page, 'Dana');

    await page.clock.fastForward('13:00');
    await page.clock.fastForward('02:00');

    await expect(page).toHaveURL(/\/sign-in\?reason=idle$/);
    await expect(page.getByRole('status')).toContainText(
      'signed out after 15 minutes of inactivity',
    );
    await page.goto('/dashboard');
    await expect(page).toHaveURL(/\/sign-in\?returnUrl=%2Fdashboard$/);
  });
});
