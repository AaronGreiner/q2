import type { Breadcrumb, ErrorEvent, Event, EventHint, init, Log, Metric, ReplayFrameEvent } from '@sentry/nuxt'

/** Not exported by `@sentry/nuxt` under its own name, so read off the option it is handed to. */
type TransactionEvent = Parameters<NonNullable<Parameters<typeof init>[0]['beforeSendTransaction']>>[0]

/**
 * Sentry rules shared by the browser and the Nuxt server.
 *
 * Kept in one module so both runtimes filter identically: a value that is
 * unsafe to send from the browser is unsafe to send from SSR too. Imported by
 * sentry.client.config.ts and sentry.server.config.ts, and unit-tested
 * directly — the tests exercise these exact functions, not a copy.
 */

export interface SentryRuntimeConfig {
  dsn: string
  environment: string
  release: string
  enabled: boolean
  tracesSampleRate: number
}

/** Keys whose value is dropped wherever it appears. */
const sensitiveKeyPattern
  = /(password|passwd|pwd|secret|token|api[_-]?key|apikey|authorization|cookie|session|credential|invite|dsn|latitude|longitude|coordinates?|geolocation)/i

/**
 * Span attributes the Vue router integration adds to every navigation: the
 * query, value by value, and the route's parameters. A query is dropped
 * everywhere else, and `code` is the invite code of `/join/:code` (#47).
 */
const routeAttributePattern = /^(query\.|(params|url\.path\.parameter)\.code$)/i

/**
 * An invite code in an address: `/join/<code>`, or the same percent-encoded in
 * another address's `?next=`. The code is a credential in everything but name
 * (ADR 0022, ADR 0033), and paths are kept everywhere else, so the segment
 * itself is replaced (#47). Not the route template `/join/:code()`, which
 * says nothing.
 */
const invitePathPattern = /(\/join\/|%2Fjoin%2F)(?!:)[^\s/?#&"'%]+/gi

/** User-authored content: personal, and never useful in telemetry. */
const userContentKeys = [
  'title',
  'description',
  'goal.title',
  'goal.description',
  'goaltitle',
  'goaldescription',
  'goal_title',
  'goal_description',
  'task.title',
  'tasktitle',
  'task_title',
  'message.text',
  'messagetext',
  'message_text',
  'chat.name',
  'chatname',
  'chat_name',
  'conversation.name',
  'conversationname',
  'conversation_name',
  'displayname',
  'display_name',
  'handle',
  'email',
]

/**
 * Attributes the SDK attaches by itself and that identify a person or a
 * machine. The backend removes the same set from its logs — see
 * `SentryEventScrubber.ScrubLog`.
 */
const identifyingKeys = [
  'user.id',
  'user.name',
  'user.username',
  'user.email',
  'server.address',
  'server.name',
  'host.name',
]

/**
 * Order matters. The specific token shapes have to run before the generic
 * `key=value` rule: that rule stops at the first whitespace, so for
 * `Authorization: Bearer aaa.bbb.ccc` it would replace only the word "Bearer"
 * and leave the token itself in the payload.
 */
const redactionPatterns: RegExp[] = [
  // Query strings and fragments on any URL — where tokens and ids end up. A
  // reset link carries its token in the fragment.
  /(https?:\/\/[^\s?#"']+)[?#][^\s"']*/gi,
  /\bbearer\s+[A-Za-z0-9\-._~+/]+=*/gi,
  /\beyJ[A-Za-z0-9_-]{5,}\.[A-Za-z0-9_-]{5,}\.[A-Za-z0-9_-]*/g,
  // key=value / "key": "value" in free text. `invite=` is the code an invite
  // link hands to the sign-up form.
  /\b(password|passwd|pwd|secret|token|access[_-]?token|api[_-]?key|authorization|cookie|session|invite(?:[_-]?code)?)\b("?\s*[=:]\s*"?)[^&\s"';,}]+/gi,
  /\b(title|description|goal[._-]?title|goal[._-]?description|task[._-]?title|message[._-]?text|chat[._-]?name|conversation[._-]?name|display[_-]?name|handle|email)\b("?\s*[=:]\s*)(?:"[^"]*"|'[^']*'|[^,;}\r\n]+)/gi,
  /[A-Za-z0-9._%+-]+@[A-Za-z0-9-]+\.[A-Za-z]{2,}/g,
  /\b(lat|lon|lng|latitude|longitude)\b\s*[:=]\s*-?\d{1,3}\.\d+/gi,
]

export const redactionPlaceholder = '[redacted]'

export function isSensitiveKey(key: string): boolean {
  const lowered = key.toLowerCase()

  return sensitiveKeyPattern.test(key)
    || routeAttributePattern.test(key)
    || userContentKeys.includes(lowered)
    || identifyingKeys.includes(lowered)
}

/** Removes credential-, token-, email-, invite- and coordinate-shaped substrings. */
export function redactText(value: string): string {
  let result = value

  for (const pattern of redactionPatterns) {
    result = result.replace(pattern, (match, ...groups) => {
      // The URL pattern keeps the path and only replaces the query or fragment.
      if (typeof groups[0] === 'string' && match.startsWith(groups[0])) {
        return groups.length >= 2 && typeof groups[1] === 'string'
          ? `${groups[0]}${groups[1]}${redactionPlaceholder}`
          : `${groups[0]}?${redactionPlaceholder}`
      }
      return redactionPlaceholder
    })
  }

  return result.replace(invitePathPattern, `$1${redactionPlaceholder}`)
}

/** An address without its query and fragment, and without an invite code in its path. */
export function scrubUrl(value: string): string {
  return redactText(value.split(/[?#]/)[0] ?? '')
}

/**
 * The same kind of record with sensitive keys dropped and strings redacted —
 * the same kind because a string only ever becomes another string.
 */
function sanitiseRecord<T extends Record<string, unknown>>(source: T): T {
  const result: Record<string, unknown> = {}

  for (const [key, value] of Object.entries(source)) {
    if (isSensitiveKey(key)) continue
    result[key] = typeof value === 'string' ? redactText(value) : value
  }

  return result as T
}

/**
 * Errors that are part of normal use and must not become issues:
 * offline requests, navigations the user cancelled, and the browser's
 * own noisy non-errors.
 */
const ignoredMessages = [
  'Failed to fetch',
  'NetworkError when attempting to fetch resource',
  'Load failed',
  'AbortError',
  'ResizeObserver loop completed with undelivered notifications',
  'Navigation cancelled',
]

function isIgnoredError(event: ErrorEvent): boolean {
  const values = event.exception?.values ?? []

  return values.some(value =>
    typeof value.value === 'string'
    && ignoredMessages.some(ignored => value.value!.includes(ignored)),
  )
}

/**
 * The single `beforeSend` for both runtimes.
 * Returning `null` drops the event.
 */
export function scrubEvent(event: ErrorEvent, _hint?: EventHint): ErrorEvent | null {
  if (isIgnoredError(event)) {
    return null
  }

  scrubEnvelope(event)

  for (const value of event.exception?.values ?? []) {
    if (typeof value.value === 'string') {
      value.value = redactText(value.value)
    }
  }

  if (typeof event.message === 'string') {
    event.message = redactText(event.message)
  }

  return event
}

/**
 * The single `beforeSendTransaction` for both runtimes.
 *
 * A transaction is a page load or a request, and it records where it went:
 * the address, the route's parameters, the query value by value and every
 * span's address. Without this none of it passed through a filter — and on
 * `/join/<code>` all of it carries the invite code (#47).
 */
export function scrubTransaction(event: TransactionEvent, _hint?: EventHint): TransactionEvent | null {
  scrubEnvelope(event)

  if (typeof event.transaction === 'string') {
    event.transaction = redactText(event.transaction)
  }

  const trace = event.contexts?.trace
  if (trace?.data) {
    trace.data = sanitiseRecord(trace.data)
  }

  for (const span of event.spans ?? []) {
    if (typeof span.description === 'string') {
      span.description = redactText(span.description)
    }
    if (span.data) {
      span.data = sanitiseRecord(span.data)
    }
  }

  return event
}

/** What errors and transactions have in common: who, where from, and the extras. */
function scrubEnvelope(event: Event) {
  // sendDefaultPii is on so Sentry may attach the IP address. Keep exactly that
  // field, while removing account identity which is not needed to diagnose an
  // incident. This mirrors the backend scrubber.
  if (event.user) {
    event.user = event.user.ip_address
      ? { ip_address: event.user.ip_address }
      : {}
  }

  // Never the machine or container the code ran on.
  delete event.server_name

  if (event.request) {
    delete event.request.cookies
    delete event.request.headers
    delete event.request.data

    if (event.request.query_string) {
      event.request.query_string = redactionPlaceholder
    }
    if (typeof event.request.url === 'string') {
      event.request.url = scrubUrl(event.request.url)
    }
  }

  if (event.extra) {
    event.extra = sanitiseRecord(event.extra)
  }

  if (event.tags) {
    event.tags = sanitiseRecord(event.tags) as typeof event.tags
  }

  if (event.contexts) {
    event.contexts = Object.fromEntries(
      Object.entries(event.contexts).filter(([key]) => !isSensitiveKey(key)),
    )
  }
}

/**
 * An event processor for the two kinds of event no `beforeSend*` hook reaches:
 * a replay, which lists every page it was recorded on, and a feedback, which
 * says which page it was sent from. Only their addresses are touched — the
 * feedback's message is sent as written, on purpose (`sentry.feedback.ts`).
 *
 * It runs on every event, before any `beforeSend*` hook. For errors and
 * transactions those scrub the same addresses again, which changes nothing.
 */
export function scrubAddresses(event: Event & { urls?: unknown }): Event {
  if (event.request) {
    if (typeof event.request.url === 'string') {
      event.request.url = scrubUrl(event.request.url)
    }
    const referer = event.request.headers?.Referer
    if (event.request.headers && typeof referer === 'string') {
      event.request.headers.Referer = scrubUrl(referer)
    }
  }

  if (Array.isArray(event.urls)) {
    event.urls = event.urls.map(url => (typeof url === 'string' ? scrubUrl(url) : url))
  }

  const feedback = event.contexts?.feedback
  if (feedback && typeof feedback.url === 'string') {
    feedback.url = scrubUrl(feedback.url)
  }

  return event
}

/**
 * The integration that runs `scrubAddresses`. An integration rather than a
 * global event processor so it sits in the browser's integration list, beside
 * Session Replay and the feedback form whose events it is for.
 */
export const addressScrubbingIntegration = {
  name: 'Q2AddressScrubbing',
  processEvent: scrubAddresses,
}

/**
 * Breadcrumbs are the biggest accidental leak in a browser SDK: they record
 * every fetch URL, every console line and, for `ui.input`, what was typed.
 */
export function scrubBreadcrumb(breadcrumb: Breadcrumb): Breadcrumb | null {
  // Keystrokes in a goal title are exactly the content we promised not to send.
  if (breadcrumb.category === 'ui.input') {
    return null
  }

  const scrubbed: Breadcrumb = { ...breadcrumb }

  if (typeof scrubbed.message === 'string') {
    scrubbed.message = redactText(scrubbed.message)
  }

  if (scrubbed.data) {
    const data = sanitiseRecord(scrubbed.data)
    // `url` on a request, `from` and `to` on a navigation.
    for (const key of ['url', 'from', 'to']) {
      const value = data[key]
      if (typeof value === 'string') {
        data[key] = scrubUrl(value)
      }
    }
    scrubbed.data = data
  }

  return scrubbed
}

/**
 * Session Replay's `beforeAddRecordingEvent`.
 *
 * A recording carries its own copy of the timeline: every page it went to, as
 * a span whose description is the address, and its breadcrumbs, some of which
 * it makes itself and never offers to `beforeBreadcrumb`. Both go through the
 * same filters here (#47).
 *
 * What it cannot reach is the page itself. A link's address is recorded as it
 * is, before `maskAttributes` is consulted, so an element whose `href` carries
 * something private has to be blocked with `data-q2-block` instead.
 */
export function scrubRecordingEvent(event: ReplayFrameEvent): ReplayFrameEvent | null {
  if (event.data.tag === 'breadcrumb') {
    const breadcrumb = scrubBreadcrumb(event.data.payload)
    if (!breadcrumb) return null
    event.data.payload = breadcrumb as typeof event.data.payload
  }

  if (event.data.tag === 'performanceSpan') {
    const span = event.data.payload
    span.description = redactText(span.description)
    if (span.data) {
      span.data = sanitiseRecord(span.data as Record<string, unknown>) as typeof span.data
    }
  }

  return event
}

/**
 * The single `beforeSendLog` for both runtimes.
 *
 * Structured logs say what the app was doing; the event says what broke. A log
 * is written by hand, so the first rule is the same as for a backend log line:
 * no user content goes into one. This is the net underneath that — it redacts
 * the message and drops identifying or credential-shaped attributes, including
 * the ones the SDK adds by itself.
 */
export function scrubLog(log: Log): Log | null {
  const scrubbed: Log = { ...log }

  if (typeof scrubbed.message === 'string') {
    scrubbed.message = redactText(scrubbed.message)
  }

  if (scrubbed.attributes) {
    scrubbed.attributes = sanitiseRecord(scrubbed.attributes)
  }

  return scrubbed
}

export const sentryMetricNames = {
  apiFailure: 'q2.api.failure',
} as const

const allowedSentryMetricNames = new Set<string>(Object.values(sentryMetricNames))

/**
 * Metrics are aggregated operational signals, never an alternate event body.
 * An allow-list makes a new name a deliberate privacy decision, and the same
 * attribute scrubber removes identity the SDK may add from its current scope.
 */
export function scrubMetric(metric: Metric): Metric | null {
  if (!allowedSentryMetricNames.has(metric.name)) {
    return null
  }

  return {
    ...metric,
    attributes: metric.attributes
      ? sanitiseRecord(metric.attributes)
      : metric.attributes,
  }
}

/**
 * Elements whose text is somebody's own — goal and task titles, messages,
 * names, handles — and stays masked in a Session Replay.
 *
 * The replay shows the app; it does not show what is written in it. Marking the
 * content rather than masking everything is what makes a recording worth
 * watching: layout, navigation, which button was pressed and what state a
 * screen was in are all visible, while the personal part is not.
 *
 * `data-q2-private` is an attribute rather than a class so it cannot be lost to
 * a styling change, and it is added at the element that renders the content —
 * `app/AGENTS.md` section 7 lists what counts. `.sentry-mask` and
 * `[data-sentry-mask]` keep working alongside it; they are the SDK's own
 * defaults and are always in the selector list.
 */
export const replayMaskSelectors = [
  '[data-q2-private]',

  // Goal and chat pages use personal titles in the browser tab. The <title>
  // element lives in <head>, outside every component that can carry the data
  // attribute, so it needs the one structural selector in this policy.
  'head > title',
]

/**
 * Stateful visuals whose shape itself is personal: messages, progress,
 * activity history and avatars. Masking only their text would still reveal a
 * message's length, a checked task or a progress percentage.
 */
export const replayBlockSelectors = ['[data-q2-block]']

/**
 * Resolves what to pass to `Sentry.init`.
 *
 * Sentry is never switched off just because the environment is local: if a DSN
 * is configured, the SDK initialises and reports, tagged with the local
 * environment. What differs between environments is the environment name and
 * the sampling — not whether the integration exists.
 */
export function resolveSentryOptions(config: SentryRuntimeConfig) {
  const enabled = config.enabled || Boolean(config.dsn)

  if (config.enabled && !config.dsn) {
    throw new Error(
      'NUXT_PUBLIC_SENTRY_ENABLED is true but NUXT_PUBLIC_SENTRY_DSN is empty. '
      + 'Set a DSN or remove the explicit enable.',
    )
  }

  return {
    dsn: config.dsn || undefined,
    enabled: enabled && Boolean(config.dsn),
    environment: config.environment,
    release: config.release || undefined,
    tracesSampleRate: config.tracesSampleRate,

    /*
     * Both of these send personal data on purpose. They are switched on for
     * the Staging host, where the point is to be able to reconstruct exactly
     * what a user did before something broke.
     *
     * sendDefaultPii attaches the IP address and request headers.
     * Session Replay records the screen — every session, not only the ones
     * that fail — which is the most privacy-invasive thing q2 does.
     *
     * What still protects the user: beforeSend, beforeBreadcrumb and
     * beforeSendLog run on everything that leaves, ui.input breadcrumbs are
     * dropped, inputs stay masked in the replay, and every element rendering
     * somebody's own words carries `data-q2-private`. See docs/privacy.md.
     */
    sendDefaultPii: true,
    replaysSessionSampleRate: 1,
    replaysOnErrorSampleRate: 1,

    /*
     * Structured logs, in addition to events — the frontend counterpart of
     * `EnableLogs` in the API. What produces them is `useErrorReporter` and
     * the console integration wired up in sentry.client.config.ts.
     */
    enableLogs: true,
    enableMetrics: true,

    beforeSend: scrubEvent,
    beforeSendTransaction: scrubTransaction,
    beforeBreadcrumb: scrubBreadcrumb,
    beforeSendLog: scrubLog,
    beforeSendMetric: scrubMetric,
  }
}
