import {
  expect,
  expectNoSeriousA11yViolations,
  signInAs,
  test,
} from './fixtures';

// Acceptance criteria from issue #62 (browser level). Paging is covered by the API scenarios and unit tests:
// no demo account holds more than one page of positions.
test.describe('positions', () => {
  test('an account on the dashboard opens its holdings, largest first', async ({
    page,
  }) => {
    await signInAs(page, 'Dana');

    await page.getByRole('link', { name: 'Individual Brokerage' }).click();

    await expect(page).toHaveURL(/\/accounts\/[0-9a-f-]{36}$/);
    await expect(
      page.getByRole('heading', { level: 1, name: 'Individual Brokerage' }),
    ).toBeFocused();
    const rows = page.getByTestId('position');
    await expect(rows.getByRole('rowheader')).toHaveText(['MDTOT', 'ACME']);
    await expect(
      page.getByRole('columnheader', { name: 'Market value' }),
    ).toHaveAttribute('aria-sort', 'descending');
    await expectNoSeriousA11yViolations(page);
  });

  test('sorting by symbol reorders on the server and is announced', async ({
    page,
  }) => {
    await signInAs(page, 'Raj');
    await page.getByRole('link', { name: 'Individual Brokerage' }).click();
    const symbols = page.getByTestId('position').getByRole('rowheader');
    await expect(symbols).toHaveText(['MDTOT', 'MDINT', 'ACME', 'GLBX']);

    await page.getByRole('button', { name: 'Symbol' }).click();

    await expect(symbols).toHaveText(['ACME', 'GLBX', 'MDINT', 'MDTOT']);
    await expect(
      page.getByRole('columnheader', { name: 'Symbol' }),
    ).toHaveAttribute('aria-sort', 'ascending');
    await expect(page.getByRole('status')).toContainText(
      'sorted by symbol, ascending',
    );
  });

  test('filtering by name narrows the list', async ({ page }) => {
    await signInAs(page, 'Raj');
    await page.getByRole('link', { name: 'Individual Brokerage' }).click();
    await expect(page.getByTestId('position')).toHaveCount(4);

    await page.getByLabel('Filter by symbol or name').fill('index');

    await expect(
      page.getByTestId('position').getByRole('rowheader'),
    ).toHaveText(['MDTOT', 'MDINT']);

    await page.getByLabel('Filter by symbol or name').fill('nothing here');
    await expect(
      page.getByText('No positions match “nothing here”.'),
    ).toBeVisible();
  });

  test("another client's account looks like a missing one", async ({
    page,
  }) => {
    await signInAs(page, 'Dana');

    await page.goto('/accounts/00000000-0000-4000-8000-000000000000');

    await expect(
      page.getByText("We couldn't find that account."),
    ).toBeVisible();
    await expectNoSeriousA11yViolations(page);
  });
});
