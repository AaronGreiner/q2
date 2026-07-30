// @ts-check
import withNuxt from './.nuxt/eslint.config.mjs'

/**
 * ESLint is also the formatter here (`stylistic: true` in nuxt.config.ts), so
 * `bun run lint` covers both correctness and layout and there is no second
 * tool to keep in sync.
 */
export default withNuxt(
  {
    name: 'q2/ignores',
    ignores: [
      // Written by `bun run api:types`; regenerate rather than edit.
      'app/api/generated/**',
      '.nuxt/**',
      '.output/**',
      'coverage/**',
      'playwright-report/**',
      'test-results/**',
    ],
  },
  {
    name: 'q2/rules',
    rules: {
      // The API contract and the domain both use explicit types; `any` here
      // would silently opt a component out of the generated types.
      '@typescript-eslint/no-explicit-any': 'error',

      // Type-aware rules (consistent-type-imports and friends) are deliberately
      // not enabled: they need a full TypeScript program per lint run, which
      // makes linting several times slower, and `bun run typecheck` already
      // checks types properly with vue-tsc.

      // Components state their name, so devtools and test selectors are stable.
      'vue/multi-word-component-names': 'error',
      'vue/component-api-style': ['error', ['script-setup']],
      'vue/define-macros-order': ['error', { order: ['defineProps', 'defineEmits'] }],
      'vue/no-v-html': 'error',

      'no-console': ['error', { allow: ['warn', 'error'] }],
    },
  },
  {
    name: 'q2/file-routed-components',
    // Pages, layouts and the error page are named by the router, not by us:
    // `index.vue` and `[id].vue` are the framework's contract. The rule stays
    // on everywhere it can actually be followed, i.e. in components/.
    files: ['app/pages/**/*.vue', 'app/layouts/**/*.vue', 'app/error.vue', 'app/app.vue'],
    rules: {
      'vue/multi-word-component-names': 'off',
    },
  },
  {
    name: 'q2/tests',
    files: ['tests/**/*.ts', '**/*.spec.ts', '**/*.test.ts'],
    rules: {
      // Test files legitimately log while diagnosing a failure.
      'no-console': 'off',
    },
  },
)
