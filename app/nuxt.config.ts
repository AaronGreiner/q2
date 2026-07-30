// https://nuxt.com/docs/api/configuration/nuxt-config
export default defineNuxtConfig({
  modules: [
    '@nuxt/ui',
    '@nuxt/eslint',
    '@nuxt/test-utils/module',
    '@sentry/nuxt/module',
  ],

  /**
   * Component names come from the file name, not from the folder path, so
   * `components/goals/GoalCard.vue` is `<GoalCard>` rather than
   * `<GoalsGoalCard>`. Folders stay free to group by feature (`goals/`, `ui/`)
   * without that grouping leaking into every template.
   *
   * The trade-off is that file names must be unique across the tree; the lint
   * rule `vue/multi-word-component-names` keeps them descriptive enough that
   * this is not a real constraint.
   */
  components: [
    { path: '~/components', pathPrefix: false },
  ],

  devtools: { enabled: true },

  css: ['~/assets/css/main.css'],

  /**
   * Everything the browser is allowed to know.
   *
   * Each key maps to an environment variable, so nothing has to be rebuilt to
   * point at a different API or Sentry project:
   *   apiBaseUrl              -> NUXT_PUBLIC_API_BASE_URL
   *   appEnv                  -> NUXT_PUBLIC_APP_ENV
   *   diagnosticsEnabled      -> NUXT_PUBLIC_DIAGNOSTICS_ENABLED
   *   sentry.dsn              -> NUXT_PUBLIC_SENTRY_DSN
   *   sentry.environment      -> NUXT_PUBLIC_SENTRY_ENVIRONMENT
   *   sentry.release          -> NUXT_PUBLIC_SENTRY_RELEASE
   *   sentry.enabled          -> NUXT_PUBLIC_SENTRY_ENABLED
   *   sentry.tracesSampleRate -> NUXT_PUBLIC_SENTRY_TRACES_SAMPLE_RATE
   *
   * SENTRY_AUTH_TOKEN is deliberately absent: it is a build-time CI secret and
   * must never reach the client bundle.
   */
  runtimeConfig: {
    public: {
      apiBaseUrl: 'http://localhost:5080',
      appEnv: 'local-development',
      diagnosticsEnabled: false,
      sentry: {
        dsn: '',
        environment: 'local-development',
        release: '',
        enabled: false,
        tracesSampleRate: 1,
      },
    },
  },

  // 'hidden' emits source maps for the Sentry upload but does not reference
  // them from the shipped bundles, so they are not served to browsers.
  sourcemap: { client: 'hidden', server: true },

  future: { compatibilityVersion: 4 },
  compatibilityDate: '2026-07-01',

  typescript: {
    strict: true,
    typeCheck: false,
  },

  eslint: {
    config: {
      stylistic: true,
    },
  },

  /**
   * Icons are bundled, never fetched.
   *
   * By default @nuxt/icon resolves an unknown icon through the Iconify API at
   * runtime. That means a network dependency on every render, icons that
   * silently disappear offline or behind a firewall, and a third party learning
   * which pages our users open. `scan` collects the icons actually referenced
   * in the source and bundles those.
   *
   * `@iconify-json/lucide` is a devDependency for exactly this reason.
   */
  icon: {
    /*
     * 'svg' renders the icon inline during SSR. The default 'css' mode injects
     * a mask rule from the client after hydration, which means every page load
     * shows a flash of missing icons and nothing renders at all without
     * JavaScript.
     */
    mode: 'svg',

    clientBundle: {
      // Collects the icons referenced literally in the source.
      scan: true,
      includeCustomCollections: true,

      /*
       * `scan` only sees literal strings. These names are produced at runtime
       * by goalStatusPresentation() in app/utils/goalDisplay.ts, so the scanner
       * cannot find them and they have to be listed here.
       *
       * If you add a status or return a new icon from a function, add it here
       * too — otherwise it silently falls back to a network lookup and logs
       * "failed to load icon" on every render.
       */
      icons: [
        'lucide:circle-dot',
        'lucide:circle-check',
        'lucide:archive',
        'lucide:clock-alert',
      ],
    },

    serverBundle: {
      collections: ['lucide'],
    },
  },

  sentry: {
    /*
     * Source maps are uploaded by the release workflow, never by a local build
     * or a fork PR — those have no SENTRY_AUTH_TOKEN and must still succeed.
     */
    sourceMapsUploadOptions: {
      enabled: Boolean(process.env.SENTRY_AUTH_TOKEN),
      org: process.env.SENTRY_ORG,
      project: process.env.SENTRY_PROJECT_APP,
      authToken: process.env.SENTRY_AUTH_TOKEN,
      telemetry: false,

      /*
       * The maps have to be uploaded under the same release the running app
       * reports, or Sentry has nothing to attach them to. Left to itself the
       * plugin guesses from git, which is not what the deployed service says.
       *
       * Undefined outside the release workflow, where the upload is disabled
       * anyway and the plugin's own detection is good enough.
       */
      release: {
        name: process.env.NUXT_PUBLIC_SENTRY_RELEASE,
      },

      sourcemaps: {
        // Remove the maps from the deployed artefact after uploading them.
        filesToDeleteAfterUpload: ['.output/**/*.map'],
      },
    },
    autoInjectServerSentry: 'top-level-import',
  },
})
