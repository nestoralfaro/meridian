import { defineConfig } from '@hey-api/openapi-ts';

/**
 * Generates TypeScript types from the committed API contract (openapi/meridian-v1.json).
 * Types only: services call the API with Angular's httpResource (ADR-0007). CI regenerates and fails on drift.
 */
export default defineConfig({
  input: '../../openapi/meridian-v1.json',
  output: { path: 'src/app/core/api', clean: true },
  plugins: ['@hey-api/typescript'],
});
