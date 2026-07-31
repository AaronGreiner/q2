import * as Sentry from '@sentry/nuxt'
import { resolveSentryOptions } from './sentry.shared'

/**
 * Sentry for the Nuxt (Nitro) server.
 *
 * Separate from the client config because it runs in a different process, but
 * it applies the same filters — see sentry.shared.ts. Server-side rendering
 * failures are the ones a user never sees a console for, so they matter most.
 *
 * Note the source of configuration: this file runs before Nuxt's runtime config
 * is available, so it reads environment variables directly.
 */
Sentry.init({
  ...resolveSentryOptions({
    dsn: process.env.NUXT_PUBLIC_SENTRY_DSN ?? '',
    environment: process.env.NUXT_PUBLIC_SENTRY_ENVIRONMENT
      ?? process.env.NUXT_PUBLIC_APP_ENV
      ?? 'local-development',
    release: process.env.NUXT_PUBLIC_SENTRY_RELEASE ?? '',
    enabled: process.env.NUXT_PUBLIC_SENTRY_ENABLED === 'true',
    tracesSampleRate: Number(process.env.NUXT_PUBLIC_SENTRY_TRACES_SAMPLE_RATE ?? 1),
  }),

  integrations: [
    // Match the browser: warnings and errors are useful operational logs,
    // while debug/info commonly contain whole application objects.
    Sentry.consoleLoggingIntegration({ levels: ['warn', 'error'] }),
  ],

  initialScope: {
    tags: {
      'service.name': 'q2-app',
      'q2.runtime': 'nitro',
    },
  },
})
