import {
  expect,
  expectNoSeriousA11yViolations,
  signInAs,
  test,
} from './fixtures';

// Acceptance criteria from issue #16 (browser level).
test.describe('demo sign-in', () => {
  test('one click signs in as Dana and lands on her dashboard', async ({
    page,
  }) => {
    await page.goto('/sign-in');
    await expectNoSeriousA11yViolations(page);

    await page.getByRole('button', { name: 'Sign in as Dana' }).click();

    await expect(page).toHaveURL(/\/dashboard$/);
    await expect(page.getByText('Welcome back, Dana')).toBeVisible();
    await expectNoSeriousA11yViolations(page);
  });

  test('the session is an HttpOnly, SameSite=Strict cookie (Secure over HTTPS) and nothing is kept in web storage', async ({
    page,
    context,
    baseURL,
  }) => {
    await signInAs(page, 'Dana');

    const session = (await context.cookies()).find(
      (c) => c.name === 'meridian.session',
    );
    expect(session, 'session cookie').toBeDefined();
    expect(session!.httpOnly).toBe(true);
    // Deployed environments are HTTPS and must mark it Secure; the local compose stack is plain HTTP.
    expect(session!.secure).toBe(baseURL!.startsWith('https://'));
    expect(session!.sameSite).toBe('Strict');

    const storage = await page.evaluate(() => ({
      local: Object.keys(localStorage),
      session: Object.keys(sessionStorage),
    }));
    expect(storage).toEqual({ local: [], session: [] });
  });

  test('the dashboard asks visitors to sign in, then takes them back', async ({
    page,
  }) => {
    await page.goto('/dashboard');

    await expect(page).toHaveURL(/\/sign-in\?returnUrl=%2Fdashboard$/);
    await expect(page).toHaveTitle('Sign in | Meridian');

    await page.getByRole('button', { name: 'Sign in as Raj' }).click();

    await expect(page).toHaveURL(/\/dashboard$/);
    await expect(page.getByText('Welcome back, Raj')).toBeVisible();
  });

  test('a refresh keeps the visitor signed in', async ({ page }) => {
    await signInAs(page, 'Dana');

    await page.reload();

    await expect(page.getByText('Welcome back, Dana')).toBeVisible();
  });
});
