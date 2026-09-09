// https://nuxt.com/docs/api/configuration/nuxt-config
import { de } from './app/i18n/messages'
import { installedThemeColor } from './app/utils/themeColors'

export default defineNuxtConfig({
  modules: [
    '@nuxt/ui',
    '@nuxt/eslint',
    '@nuxt/test-utils/module',
    '@sentry/nuxt/module',
    '@vite-pwa/nuxt',
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
   * q2 opens dark.
   *
   * `preference` is what somebody who has never chosen gets, and `fallback` is
   * what the no-flash inline script paints before it knows anything — both are
   * dark, so the first frame of a first launch is already the app rather than a
   * white rectangle that turns black. Choosing "System" in the settings still
   * works and still follows the operating system; it is simply no longer the
   * unchosen middle.
   *
   * The server holds the same default (UserSettings.Theme) and wins once the
   * settings request comes back. These two only decide what is on screen before
   * that, which is the frame people actually notice.
   */
  colorMode: {
    preference: 'dark',
    fallback: 'dark',
  },

  /**
   * Everything the browser is allowed to know.
   *
   * Each key maps to an environment variable, so nothing has to be rebuilt to
   * point at a different API or Sentry project:
   *   apiBaseUrl              -> NUXT_PUBLIC_API_BASE_URL
   *   appEnv                  -> NUXT_PUBLIC_APP_ENV
   *   diagnosticsEnabled      -> NUXT_PUBLIC_DIAGNOSTICS_ENABLED
   *   demoEmail               -> NUXT_PUBLIC_DEMO_EMAIL
   *   demoPassword            -> NUXT_PUBLIC_DEMO_PASSWORD
   *   sentry.dsn              -> NUXT_PUBLIC_SENTRY_DSN
   *   sentry.environment      -> NUXT_PUBLIC_SENTRY_ENVIRONMENT
   *   sentry.release          -> NUXT_PUBLIC_SENTRY_RELEASE
   *   sentry.enabled          -> NUXT_PUBLIC_SENTRY_ENABLED
   *   sentry.tracesSampleRate -> NUXT_PUBLIC_SENTRY_TRACES_SAMPLE_RATE
   *   sentry.profileSessionSampleRate -> NUXT_PUBLIC_SENTRY_PROFILE_SESSION_SAMPLE_RATE
   *
   * SENTRY_AUTH_TOKEN is deliberately absent: it is a build-time CI secret and
   * must never reach the client bundle.
   */
  runtimeConfig: {
    public: {
      apiBaseUrl: 'http://localhost:5080',
      appEnv: 'local-development',
      diagnosticsEnabled: false,

      /*
       * The seeded account the sign-in screen offers to fill in for you.
       *
       * Empty by default, and the button only exists when both are set — which
       * is what keeps it out of Staging and Production without a second flag to
       * forget. It is not a secret: it is the documented credential of a
       * synthetic seed profile, and those only ever run against a database
       * built by `bun run dev`, `test:manual:start` or the E2E suite.
       */
      demoEmail: '',
      demoPassword: '',
      sentry: {
        dsn: '',
        environment: 'local-development',
        release: '',
        enabled: false,
        tracesSampleRate: 1,

        // A sampled session is profiled only while a sampled root span runs;
        // see profileLifecycle in sentry.client.config.ts.
        profileSessionSampleRate: 1,
      },
    },
  },

  /**
   * What lets Sentry's browser profiling actually run.
   *
   * The JS Self-Profiling API is gated behind a document policy: a page may
   * only sample its own stack when it was *served* with this header, and there
   * is no way to opt in from script afterwards. Without it
   * `browserProfilingIntegration()` in sentry.client.config.ts initialises,
   * finds no profiler, and silently produces nothing.
   *
   * It grants the page a capability over itself and discloses nothing — the
   * samples are stacks of our own bundle. Only Chromium implements it; other
   * browsers ignore both the header and the integration.
   */
  routeRules: {
    '/**': { headers: { 'Document-Policy': 'js-profiling' } },
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
       * `scan` only sees literal strings, and two sets of icon names never
       * appear as one:
       *
       *  - a goal's icon comes from the API (GoalIcons in the backend), so the
       *    scanner cannot know it. The list below must stay in step with
       *    api/src/Q2.Api/Features/Goals/Goal.cs — the server validates against
       *    its list, this bundles against ours, and an icon in one but not the
       *    other renders as nothing at all;
       *  - the rest are produced by the presentation helpers in
       *    app/utils/display.ts.
       *
       * Anything missing here silently falls back to a network lookup and logs
       * "failed to load icon" on every render.
       */
      icons: [
        // GoalIcons, server-side.
        'lucide:target',
        'lucide:medal',
        'lucide:book-open',
        'lucide:sunrise',
        'lucide:droplet',
        'lucide:flame',
        'lucide:trophy',
        'lucide:sparkles',
        'lucide:calendar',
        'lucide:alarm-clock',
        'lucide:hand-heart',
        'lucide:users',

        // ConversationIcons, server-side — a group chat's avatar. Must stay in
        // step with api/src/Q2.Api/Features/Chats/Conversation.cs.
        'lucide:footprints',
        'lucide:sprout',
        'lucide:party-popper',

        // Badges and statuses, chosen at runtime by display.ts.
        'lucide:circle-dot',
        'lucide:circle-check',
        'lucide:archive',
        'lucide:clock-alert',
        'lucide:repeat',
        'lucide:message-circle',
        'lucide:user',
        'lucide:lock',
        'lucide:circle-help',

        // The three kinds of kudos, chosen at runtime by display.ts.
        'lucide:biceps-flexed',

        // Named in the message catalogue rather than in a template, which is
        // where the scanner does look — but these are the only icons whose one
        // and only mention is a value in app/i18n/messages.ts, so they are
        // listed here the way every other indirect icon is.
        'lucide:check',
        'lucide:plus',
        'lucide:megaphone',
        'lucide:send',
        'lucide:undo-2',
        'lucide:user-check',
        'lucide:user-minus',
        'lucide:log-out',
        'lucide:pencil',
        'lucide:circle-alert',
        'lucide:search',
        'lucide:image-off',

        // The photo sheet's own icons. They are in templates, so the scanner
        // would find them — but it only scans app/, and these are worth
        // listing beside the ones it cannot see rather than being the one set
        // that silently falls back to a network lookup after a refactor.
        'lucide:camera',
        'lucide:camera-off',
        'lucide:image',
        'lucide:switch-camera',
        'lucide:rotate-ccw',
        'lucide:trash-2',

        // Stage 4: the photograph, the wait, and the verdict.
        'lucide:hourglass',
        'lucide:gavel',
        'lucide:circle-check-big',

        // Stage 5: the warning, and the bell it lives behind.
        'lucide:bell',

        // Stage 7: the daily challenge — the prompt, the covered room, and
        // the toast that says you are in.
        'lucide:zap',
        'lucide:eye-off',
        'lucide:package-open',

        // Stage 8: reporting, blocking, the invite link and the way out.
        'lucide:flag',
        'lucide:shield',
        'lucide:shield-off',
        'lucide:link',
        'lucide:share-2',
        'lucide:user-x',
        'lucide:ellipsis',

        // Stage 9: notifications, and the hours they stay away.
        'lucide:bell-off',
        'lucide:moon-star',
      ],
    },

    serverBundle: {
      collections: ['lucide'],
    },
  },

  /**
   * q2 as an installable application.
   *
   * This is the web half of what [next-steps.md](../docs/next-steps.md) item
   * 15 will finish with Capacitor: the same UI, installed from the browser
   * rather than from a store. It buys the standalone window, the icon on the
   * home screen and a start that does not wait for the network — and it
   * deliberately buys nothing else. See docs/adr/0012-installable-pwa.md.
   *
   * `injectManifest` rather than the default `generateSW`: what to cache is
   * the one interesting decision here and it is not a Workbox preset, so the
   * worker is written out in service-worker/sw.ts and the module only
   * substitutes the precache list into it.
   */
  pwa: {
    strategies: 'injectManifest',

    // Resolved against Nuxt's srcDir (app/app), so this is app/service-worker.
    // The worker lives outside the application source because it is not part
    // of it: different global scope, different lib, its own tsconfig.
    srcDir: '../service-worker',
    filename: 'sw.ts',

    // Our worker calls skipWaiting itself; this makes the registration agree
    // with it instead of waiting for a reload that will never be asked for.
    registerType: 'autoUpdate',

    /*
     * Adds the Nitro route rules for /sw.js and /manifest.webmanifest.
     *
     * The one that matters is `Cache-Control: max-age=0, must-revalidate` on
     * the worker. Without it Nitro sends no cache header at all, which leaves
     * the browser free to guess a freshness lifetime from Last-Modified — and
     * a stale worker is a deployment that quietly does not arrive. The 24-hour
     * cap in the specification is a backstop, not a plan.
     */
    registerWebManifestInRouteRules: true,

    manifest: {
      id: '/',
      name: `${de.app.name} (q2)`,
      short_name: de.app.name,
      description: de.app.description,

      // The manifest cannot be translated per person — it is read once, at
      // install time, by the operating system. German is what q2 speaks by
      // default (docs/adr/0010-german-first-interface.md).
      lang: 'de',
      dir: 'ltr',

      start_url: '/',
      scope: '/',

      // No browser chrome. The app draws its own header and tab bar, and both
      // already account for the safe areas a notch leaves behind.
      display: 'standalone',

      /*
       * Orientation is deliberately unset. A phone layout is what q2 is
       * designed for, but locking rotation would also lock out somebody using
       * the device in a stand or mounted sideways, and the layout survives it.
       */

      /*
       * The dark value of both, because dark is what q2 opens in. A manifest
       * holds one colour and is read before anything has rendered, so it
       * cannot follow the scheme — the theme-color metas in app/app.vue do
       * that afterwards, and they still offer both.
       */
      background_color: installedThemeColor,
      theme_color: installedThemeColor,

      icons: [
        { src: '/pwa-192x192.png', sizes: '192x192', type: 'image/png', purpose: 'any' },
        { src: '/pwa-512x512.png', sizes: '512x512', type: 'image/png', purpose: 'any' },

        // Cropped by the platform to its own shape; drawn with the safe zone
        // that needs. See app/scripts/generate-icons.ts.
        { src: '/maskable-512x512.png', sizes: '512x512', type: 'image/png', purpose: 'maskable' },
      ],
    },

    injectManifest: {
      // The worker is one self-contained script. Choosing IIFE makes that
      // explicit to Vite and avoids the deprecated inlineDynamicImports path.
      rollupFormat: 'iife',

      /*
       * The precache list, and the reason it names extensions instead of
       * saying "everything".
       *
       * These are the files that are identical for every person and carry a
       * content hash in their name, so a cached copy is always a correct copy.
       * No HTML is produced into the public directory at all — q2 renders on
       * the server — and if a prerendered page ever appears, this pattern will
       * not quietly start caching somebody's screen.
       */
      globPatterns: ['**/*.{js,css,woff,woff2,png,svg,ico}'],

      // Emitted as 'hidden' for the Sentry upload and deleted from the
      // artefact after it; never served to a browser.
      globIgnores: ['**/*.map'],

      /*
       * Two files are in the precache without appearing above, and both are
       * put there by the module rather than by these patterns:
       *
       *  - manifest.webmanifest, so that the install offer survives a bad
       *    connection. Listing it here as well would only add a duplicate;
       *  - _nuxt/builds/latest.json, Nuxt's own build manifest. Route
       *    resolution fetches it, so without a cached copy a navigation on a
       *    dead connection fails before the offline page can be reached.
       */
    },

    // Nothing in the UI asks to install; the browser's own affordance does.
    client: {
      installPrompt: false,
    },

    devOptions: {
      // A worker that caches assets while Vite is hot-reloading them is a
      // debugging session nobody asked for. `bun run build && bun run preview`
      // is where the PWA is tried out, which is also what E2E does.
      enabled: false,
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
