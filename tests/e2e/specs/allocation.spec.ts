import {
  expect,
  expectNoSeriousA11yViolations,
  signInAs,
  test,
} from './fixtures';

// Acceptance criteria from issue #21.
test.describe('asset allocation', () => {
  test('a donut chart shows stocks, bonds, cash and funds', async ({
    page,
  }) => {
    await signInAs(page, 'Dana');

    const chart = page.locator('mer-allocation-chart');
    await expect(
      chart.getByRole('heading', { name: 'Asset allocation' }),
    ).toBeVisible();
    await expect(chart.locator('svg circle.donut__segment')).toHaveCount(4);
  });

  test('screen-reader users get the same information as a table, totalling exactly 100%', async ({
    page,
  }) => {
    await signInAs(page, 'Dana');

    const table = page.getByRole('table', {
      name: 'Asset allocation by value',
    });
    await expect(table.getByRole('rowheader')).toHaveText([
      'Funds',
      'Bonds',
      'Cash',
      'Stocks',
    ]);
    await expect(table.getByRole('row', { name: /Funds/ })).toContainText(
      '70.82%',
    );
    await expect(table.getByRole('row', { name: /Stocks/ })).toContainText(
      '6.19%',
    );
    await expect(page.locator('mer-allocation-chart svg')).toHaveAttribute(
      'aria-hidden',
      'true',
    );
    await expectNoSeriousA11yViolations(page);
  });

  test('the chart stays accessible in the dark theme', async ({ page }) => {
    await page.emulateMedia({ colorScheme: 'dark' });
    await signInAs(page, 'Dana');

    await expect(
      page.getByRole('table', { name: 'Asset allocation by value' }),
    ).toBeVisible();
    await expectNoSeriousA11yViolations(page);
  });
});
