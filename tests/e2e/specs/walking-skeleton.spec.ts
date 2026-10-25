import {
  expect,
  expectNoSeriousA11yViolations,
  signInAs,
  test,
} from './fixtures';

test.describe('walking skeleton', () => {
  test('landing page renders the portal shell and is accessible', async ({
    page,
  }) => {
    await page.goto('/');

    await expect(page).toHaveTitle('Meridian Client Portal');
    await expect(
      page.getByRole('link', { name: 'Meridian home' }),
    ).toBeVisible();
    await expect(
      page.getByRole('heading', { level: 1, name: 'Welcome to Meridian' }),
    ).toBeVisible();
    await expect(page.getByRole('main')).toBeVisible();
    await expect(page.getByTestId('demo-disclaimer')).toContainText(
      'Fictional demo',
    );
    await expectNoSeriousA11yViolations(page);
  });

  test('the SPA talks to the API on the same origin', async ({ page }) => {
    const version = page.waitForResponse(
      (r) => r.url().endsWith('/api/v1/version') && r.ok(),
    );
    await page.goto('/');
    const body = (await (await version).json()) as { commit: string };

    await expect(page.getByTestId('build-info')).toHaveText(
      `Build ${body.commit.slice(0, 7)}`,
    );
  });

  test('client-side navigation reaches the dashboard once signed in', async ({
    page,
  }) => {
    await signInAs(page, 'Dana');
    await page.getByRole('link', { name: 'Home' }).first().click();
    await expect(
      page.getByRole('heading', { level: 1, name: 'Welcome to Meridian' }),
    ).toBeVisible();
    await page.getByRole('link', { name: 'Go to your dashboard' }).click();

    await expect(page).toHaveURL(/\/dashboard$/);
    await expect(page).toHaveTitle('Dashboard | Meridian');
    await expect(
      page.getByRole('heading', { level: 1, name: 'Dashboard' }),
    ).toBeVisible();
    await expectNoSeriousA11yViolations(page);
  });

  test('a deep link is served by the SPA (which asks anonymous visitors to sign in)', async ({
    page,
  }) => {
    const response = await page.goto('/dashboard');

    expect(response?.status()).toBe(200);
    await expect(
      page.getByRole('heading', { level: 1, name: 'Sign in' }),
    ).toBeVisible();
  });

  test('unknown routes show an accessible not-found page', async ({ page }) => {
    await page.goto('/no-such-page');

    await expect(page).toHaveTitle('Page not found | Meridian');
    await expect(
      page.getByRole('heading', { level: 1, name: 'Page not found' }),
    ).toBeVisible();
    await expectNoSeriousA11yViolations(page);
  });
});
