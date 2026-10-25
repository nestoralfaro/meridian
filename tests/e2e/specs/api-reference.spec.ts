import { expect, test } from './fixtures';

test.describe('API reference', () => {
  test('renders the versioned API from the OpenAPI contract', async ({
    page,
  }) => {
    const document = page.waitForResponse((r) =>
      r.url().includes('/openapi/v1.json'),
    );
    await page.goto('/docs');

    const response = await document;
    expect(
      response.status(),
      `OpenAPI document fetched from ${response.url()}`,
    ).toBe(200);
    // Scalar's mobile layout tucks operations behind a menu, so check the operation is rendered, not visible.
    await expect(
      page.getByText('Identifies the running build', { exact: false }).first(),
    ).toBeAttached();
  });
});
