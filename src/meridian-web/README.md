# meridian-web

The Meridian Client Portal single-page application: Angular (standalone components, signals, zoneless change detection), strict TypeScript, SCSS.

In production it is built and served by the ASP.NET Core host (`src/Meridian.Api`) from the same origin as the API. See the repository [README](../../README.md) for commands and [ADRs](../../docs/adr) for decisions.

| Concern     | Tooling                                                                 |
| ----------- | ----------------------------------------------------------------------- |
| Lint        | `angular-eslint` (TypeScript + template rules, including accessibility) |
| Format      | Prettier (`.prettierrc`)                                                |
| Unit tests  | Vitest via `ng test`                                                    |
| Bundle size | Budgets in `angular.json` (initial: warn 500 kB, error 1 MB)            |

Component selectors use the `mer-` prefix (enforced by lint).
