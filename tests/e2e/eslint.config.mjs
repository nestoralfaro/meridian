// @ts-check
import playwright from 'eslint-plugin-playwright';
import tseslint from 'typescript-eslint';

export default tseslint.config(
  { ignores: ['node_modules/', 'test-results/', 'playwright-report/'] },
  {
    files: ['**/*.ts'],
    extends: [tseslint.configs.recommended, playwright.configs['flat/recommended']],
    rules: {
      // Sprint 1 retro: a non-waiting count() raced the page and blocked a deploy (#56 → #57).
      'playwright/prefer-web-first-assertions': 'error',
      'playwright/no-wait-for-timeout': 'error',
      'playwright/prefer-to-have-count': 'error',
      // The #56 bug exactly: prefer-web-first-assertions doesn't cover count() compared with
      // toBeGreaterThan(), so ban asserting on an awaited count() outright.
      'no-restricted-syntax': [
        'error',
        {
          selector:
            "CallExpression[callee.name='expect'] > AwaitExpression > CallExpression[callee.property.name='count']",
          message:
            'count() reads the page once without waiting. Use toHaveCount(), or expect(locator.nth(n)).toBeVisible().',
        },
      ],
      'playwright/no-force-option': 'error',
      'playwright/no-conditional-in-test': 'error',
      // Conditional skips with a reason (e.g. WebKit's Tab setting) are fine; unconditional ones aren't.
      'playwright/no-skipped-test': ['error', { allowConditional: true }],
    },
  },
);
