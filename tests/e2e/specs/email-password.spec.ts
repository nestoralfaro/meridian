import { expect, expectNoSeriousA11yViolations, test } from './fixtures';

// Acceptance criteria from issue #17 (browser level). Accounts use the e2e domain, which each demo reset removes.
const uniqueEmail = () =>
  `run-${Date.now()}-${Math.random().toString(36).slice(2, 8)}@e2e.meridian.test`;
const strongPassword = 'correct horse battery staple';

test.describe('email and password', () => {
  test('a new client registers and lands on their dashboard', async ({
    page,
  }) => {
    await page.goto('/register');
    await expectNoSeriousA11yViolations(page);

    await page.getByLabel('Your name').fill('Avery Stone');
    await page.getByLabel('Email', { exact: true }).fill(uniqueEmail());
    await page.getByLabel('Password', { exact: true }).fill(strongPassword);
    await page.getByRole('button', { name: 'Create account' }).click();

    await expect(page).toHaveURL(/\/dashboard$/);
    await expect(page.getByText('Welcome back, Avery')).toBeVisible();
  });

  test('a weak password is rejected with guidance, announced and focused', async ({
    page,
  }) => {
    await page.goto('/register');
    await page.getByLabel('Your name').fill('Avery Stone');
    await page.getByLabel('Email', { exact: true }).fill(uniqueEmail());
    await page.getByLabel('Password', { exact: true }).fill('password1');
    await page.getByRole('button', { name: 'Create account' }).click();

    await expect(page.getByRole('alert')).toContainText(
      'at least 12 characters',
    );
    const password = page.getByLabel('Password', { exact: true });
    await expect(password).toHaveAttribute('aria-invalid', 'true');
    await expect(password).toBeFocused();
    await expectNoSeriousA11yViolations(page);
  });

  test('a common password is rejected by the server with an explanation', async ({
    page,
  }) => {
    await page.goto('/register');
    await page.getByLabel('Your name').fill('Avery Stone');
    await page.getByLabel('Email', { exact: true }).fill(uniqueEmail());
    await page.getByLabel('Password', { exact: true }).fill('password1234');
    await page.getByRole('button', { name: 'Create account' }).click();

    await expect(page.getByRole('alert')).toContainText('too common');
    await expect(page).toHaveURL(/\/register$/);
  });

  test('a registered client signs in with their password', async ({
    page,
    browser,
  }) => {
    const email = uniqueEmail();
    await page.goto('/register');
    await page.getByLabel('Your name').fill('Riley Park');
    await page.getByLabel('Email', { exact: true }).fill(email);
    await page.getByLabel('Password', { exact: true }).fill(strongPassword);
    await page.getByRole('button', { name: 'Create account' }).click();
    await expect(page).toHaveURL(/\/dashboard$/);

    const fresh = await browser.newPage(); // a different browser session, not signed in
    await fresh.goto('/sign-in');
    await fresh.getByLabel('Email', { exact: true }).fill(email);
    await fresh.getByLabel('Password', { exact: true }).fill(strongPassword);
    await fresh.getByRole('button', { name: 'Sign in', exact: true }).click();

    await expect(fresh).toHaveURL(/\/dashboard$/);
    await expect(fresh.getByText('Welcome back, Riley')).toBeVisible();
    await fresh.close();
  });

  test('a failed sign-in gives one generic message', async ({ page }) => {
    await page.goto('/sign-in');
    await page.getByLabel('Email', { exact: true }).fill(uniqueEmail());
    await page
      .getByLabel('Password', { exact: true })
      .fill('not the right password');
    await page.getByRole('button', { name: 'Sign in', exact: true }).click();

    await expect(page.getByRole('alert')).toHaveText(
      'Sign-in failed. Check your email and password, or try again later.',
    );
    await expectNoSeriousA11yViolations(page);
  });
});
