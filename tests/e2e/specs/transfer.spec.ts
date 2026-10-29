import type { Locator } from '@playwright/test';
import {
  expect,
  expectNoSeriousA11yViolations,
  signInAs,
  test,
} from './fixtures';

// Acceptance criteria from issue #63 (browser level). Idempotency, concurrency and ownership are covered by
// the API scenarios. This uses Raj's savings so Dana's figures, which other specs assert, never change.
test.describe('move money', () => {
  test('review, then confirm, a transfer between own accounts', async ({
    page,
  }) => {
    await signInAs(page, 'Raj');
    await page.getByRole('link', { name: 'Move money' }).click();
    await expect(
      page.getByRole('heading', { level: 1, name: 'Move money' }),
    ).toBeFocused();
    await expectNoSeriousA11yViolations(page);

    await choose(page.getByLabel('From'), 'High-Yield Savings');
    await choose(page.getByLabel('To'), 'Individual Brokerage');
    await page.getByLabel('Amount (USD)').fill('1.00');
    await page.getByRole('button', { name: 'Review transfer' }).click();

    await expect(
      page.getByRole('heading', { name: 'Review your transfer' }),
    ).toBeFocused();
    await expect(page.locator('.review')).toContainText('$1.00');
    await expectNoSeriousA11yViolations(page);

    await page.getByRole('button', { name: 'Confirm transfer' }).click();

    await expect(
      page.getByRole('heading', { name: 'Transfer complete' }),
    ).toBeFocused();
    await expect(page.getByRole('status')).toContainText(
      '$1.00 moved from High-Yield Savings to Individual Brokerage.',
    );

    await page.getByRole('link', { name: 'Back to dashboard' }).click();
    await expect(
      page
        .locator('section.recent li')
        .filter({ hasText: 'Transfer from High-Yield Savings' })
        .first(),
    ).toBeVisible();
  });

  test('problems are explained next to the fields, before review', async ({
    page,
  }) => {
    await signInAs(page, 'Raj');
    await page.goto('/transfer');

    await choose(page.getByLabel('From'), 'High-Yield Savings');
    await choose(page.getByLabel('To'), 'High-Yield Savings');
    await page.getByLabel('Amount (USD)').fill('lots');
    await page.getByRole('button', { name: 'Review transfer' }).click();

    await expect(page.getByLabel('To')).toBeFocused();
    await expect(page.getByLabel('To')).toHaveAttribute('aria-invalid', 'true');
    await expect(
      page.getByText('Choose a different account to move money to.'),
    ).toHaveCount(2); // summary + field
    await expect(page.getByLabel('Amount (USD)')).toHaveAttribute(
      'aria-invalid',
      'true',
    );
    await expectNoSeriousA11yViolations(page);
  });
});

/** Option labels include live balances, so pick the account by name. */
async function choose(select: Locator, accountName: string): Promise<void> {
  const value = await select
    .locator('option', { hasText: accountName })
    .getAttribute('value');
  await select.selectOption(value!);
}
