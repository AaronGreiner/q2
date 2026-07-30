import * as Sentry from '@sentry/nuxt'
import { resolveSentryOptions } from './sentry.shared'

/**
 * Browser-side Sentry.
 *
 * Loaded by the @sentry/nuxt module before the app boots, which is what lets
 * it catch errors thrown during hydration. Configuration comes from runtime
 * config, so one build runs in every environment.
 */
const { public: config } = useRuntimeConfig()

Sentry.init({
  ...resolveSentryOptions({
    dsn: config.sentry.dsn,
    environment: config.sentry.environment || config.appEnv,
    release: config.sentry.release,
    enabled: Boolean(config.sentry.enabled),
    tracesSampleRate: Number(config.sentry.tracesSampleRate ?? 1),
  }),

  integrations: [
    Sentry.browserTracingIntegration(),

    /*
     * Session Replay. The sample rates live in resolveSentryOptions; without
     * this integration they would do nothing at all, which is the trap here —
     * a replay rate of 1 and no integration looks configured and records
     * nothing.
     *
     * Masking stays on. AGENTS.md section 9 is unambiguous that a goal title is
     * personal and must never be reported, and a replay of the screen would
     * otherwise carry exactly that — in a form far harder to audit than an
     * event payload. So the replay shows layout, navigation and interaction,
     * with text and inputs masked.
     */
    Sentry.replayIntegration({
      maskAllText: true,
      maskAllInputs: true,
      blockAllMedia: true,
    }),
  ],

  initialScope: {
    tags: {
      'service.name': 'q2-app',
      'q2.app_env': config.appEnv,
    },
  },
})
