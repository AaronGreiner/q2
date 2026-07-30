import type { Breadcrumb, ErrorEvent, EventHint } from '@sentry/nuxt'

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
  = /(password|passwd|pwd|secret|token|api[_-]?key|apikey|authorization|cookie|session|credential|dsn|latitude|longitude|coordinates?|geolocation)/i

/** User-authored goal content: personal, and never useful in a stack trace. */
const userContentKeys = ['goal.title', 'goal.description', 'title', 'description']

/**
 * Order matters. The specific token shapes have to run before the generic
 * `key=value` rule: that rule stops at the first whitespace, so for
 * `Authorization: Bearer aaa.bbb.ccc` it would replace only the word "Bearer"
 * and leave the token itself in the payload.
 */
const redactionPatterns: RegExp[] = [
  // Query strings on any URL — where tokens and ids end up.
  /(https?:\/\/[^\s?"']+)\?[^\s"']*/gi,
  /\bbearer\s+[A-Za-z0-9\-._~+/]+=*/gi,
  /\beyJ[A-Za-z0-9_-]{5,}\.[A-Za-z0-9_-]{5,}\.[A-Za-z0-9_-]*/g,
  // key=value / "key": "value" in free text.
  /\b(password|passwd|pwd|secret|token|access[_-]?token|api[_-]?key|authorization|cookie|session)\b("?\s*[=:]\s*"?)[^&\s"';,}]+/gi,
  /[A-Za-z0-9._%+-]+@[A-Za-z0-9-]+\.[A-Za-z]{2,}/g,
  /\b(lat|lon|lng|latitude|longitude)\b\s*[:=]\s*-?\d{1,3}\.\d+/gi,
]

export const redactionPlaceholder = '[redacted]'

export function isSensitiveKey(key: string): boolean {
  return sensitiveKeyPattern.test(key) || userContentKeys.includes(key.toLowerCase())
}

/** Removes credential-, token-, email- and coordinate-shaped substrings. */
export function redactText(value: string): string {
  let result = value

  for (const pattern of redactionPatterns) {
    result = result.replace(pattern, (match, ...groups) => {
      // The URL pattern keeps the path and only replaces the query.
      if (typeof groups[0] === 'string' && match.startsWith(groups[0])) {
        return groups.length >= 2 && typeof groups[1] === 'string'
          ? `${groups[0]}${groups[1]}${redactionPlaceholder}`
          : `${groups[0]}?${redactionPlaceholder}`
      }
      return redactionPlaceholder
    })
  }

  return result
}

function sanitiseRecord(source: Record<string, unknown>): Record<string, unknown> {
  const result: Record<string, unknown> = {}

  for (const [key, value] of Object.entries(source)) {
    if (isSensitiveKey(key)) continue
    result[key] = typeof value === 'string' ? redactText(value) : value
  }

  return result
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

  // No accounts yet, so there is no legitimate identity to report — and the
  // SDK would otherwise attach an IP address.
  delete event.user

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
      event.request.url = event.request.url.split('?')[0]
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
    if (typeof data.url === 'string') {
      data.url = data.url.split('?')[0]
    }
    scrubbed.data = data
  }

  return scrubbed
}

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

    // Never attach IP addresses or other automatic personal data.
    sendDefaultPii: false,

    // Session Replay records the screen. It is not enabled here, and enabling
    // it later needs its own privacy review — see docs/privacy.md.
    replaysSessionSampleRate: 0,
    replaysOnErrorSampleRate: 0,

    beforeSend: scrubEvent,
    beforeBreadcrumb: scrubBreadcrumb,
  }
}
