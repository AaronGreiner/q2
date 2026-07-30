import { fileURLToPath } from 'node:url'
import { defineVitestProject } from '@nuxt/test-utils/config'
import { defineConfig } from 'vitest/config'

/**
 * Two projects, because they cost very different amounts to run:
 *
 *  - `unit` — plain functions (display logic, error normalisation, the Sentry
 *    scrubber). No Nuxt, no DOM, milliseconds.
 *  - `component` — Vue components rendered in a real Nuxt environment, so
 *    auto-imports, Nuxt UI and runtime config behave as they do in the app.
 *
 * Run one with `bun run test:unit` / `bun run test:component`, or both with
 * `bun run test`. E2E lives in Playwright, not here.
 */
export default defineConfig({
  test: {
    projects: [
      {
        test: {
          name: 'unit',
          include: ['tests/unit/**/*.spec.ts'],
          environment: 'node',
        },
        resolve: {
          alias: {
            '~': fileURLToPath(new URL('./app', import.meta.url)),
            '@': fileURLToPath(new URL('./app', import.meta.url)),
          },
        },
      },
      await defineVitestProject({
        test: {
          name: 'component',
          include: ['tests/component/**/*.spec.ts'],
          environment: 'nuxt',
          environmentOptions: {
            nuxt: { domEnvironment: 'happy-dom' },
          },
        },
      }),
    ],
  },
})
