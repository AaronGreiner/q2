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
  ],

  initialScope: {
    tags: {
      'service.name': 'q2-app',
      'q2.app_env': config.appEnv,
    },
  },
})
