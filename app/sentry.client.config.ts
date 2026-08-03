import * as Sentry from '@sentry/nuxt'
import { feedbackFormOptions } from './sentry.feedback'
import {
  replayBlockSelectors,
  replayMaskSelectors,
  resolveSentryOptions,
} from './sentry.shared'

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

  /*
   * The current UI Profiling API samples browser sessions, then follows traced
   * root spans inside a sampled session. Browser profiling additionally needs
   * the `Document-Policy: js-profiling` response header from nuxt.config.ts.
   */
  profileSessionSampleRate: Number(config.sentry.profileSessionSampleRate ?? 1),
  profileLifecycle: 'trace',

  integrations: [
    Sentry.browserTracingIntegration(),

    /*
     * Where the time went, per sampled transaction. Samples the JavaScript
     * stack of our own code — it carries no user data, and the redaction that
     * applies to events does not apply to a profile because there is nothing
     * in one to redact.
     */
    Sentry.browserProfilingIntegration(),

    /*
     * console.warn and console.error become Sentry logs. Debug and info are
     * deliberately not captured: they are the levels a component logs a whole
     * object at, and an object here usually contains a goal.
     */
    Sentry.consoleLoggingIntegration({ levels: ['warn', 'error'] }),

    /*
     * Session Replay. The sample rates live in resolveSentryOptions; without
     * this integration they would do nothing at all, which is the trap here —
     * a replay rate of 1 and no integration looks configured and records
     * nothing.
     *
     * What is masked, and why it is no longer everything:
     *
     *  - `maskAllText: false` — a replay in which every label, heading, empty
     *    state and error message is a row of asterisks shows that something
     *    went wrong somewhere, which is what a stack trace already said. The
     *    interface itself is ours, not the user's, so it is recorded.
     *  - `mask: replayMaskSelectors` — everything the user wrote is not.
     *    `data-q2-private` sits on the elements rendering goal and task titles,
     *    messages, names and handles; AGENTS.md section 9 has not changed, this
     *    is how it is kept.
     *  - `maskAllInputs` stays on. Every free-text field in q2 takes personal
     *    content — a goal title, a message, an address, a password — so there
     *    is nothing to gain by making them readable, and `ui.input` breadcrumbs
     *    are dropped for the same reason.
     *  - `blockAllMedia: false` — q2 has no user media at all. An avatar is
     *    initials on a colour and every icon is inline SVG, which the media
     *    selector also covers, so blocking it removed the interface and
     *    protected nothing.
     *  - `block: replayBlockSelectors` — personal state whose geometry leaks
     *    information (messages, progress, activity and avatars) is replaced as
     *    a whole rather than merely having its text masked.
     */
    Sentry.replayIntegration({
      maskAllText: false,
      mask: replayMaskSelectors,
      block: replayBlockSelectors,
      maskAllInputs: true,
      blockAllMedia: false,
    }),

    /*
     * User Feedback. `feedbackIntegration` is the synchronous build, so the
     * dialog is part of our bundle and nothing is fetched from a CDN when
     * somebody opens it — the app has no runtime third-party script, and an
     * installed q2 that is slow to reach the network still opens the form.
     *
     * What the form collects and why is in sentry.feedback.ts; the text is not
     * here because it is translated and applied when the dialog is opened, in
     * `useFeedback`. The integration only exists when a DSN is configured — a
     * disabled client sets up no integrations at all — which is what the
     * `isAvailable` check in that composable is about.
     */
    Sentry.feedbackIntegration({ ...feedbackFormOptions }),
  ],

  initialScope: {
    tags: {
      'service.name': 'q2-app',
      'q2.app_env': config.appEnv,
    },
  },
})
