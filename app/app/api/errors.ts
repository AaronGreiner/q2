import type { ProblemDetails, ValidationProblemDetails } from './types'

/**
 * The kinds of failure the UI treats differently.
 *
 * The distinction that matters is `isExpected`: an expected failure is part of
 * normal use (a blank title, a stale link) and gets a friendly message but no
 * Sentry issue. Only genuinely unexpected failures are worth waking someone up
 * for — see docs/observability.md.
 */
export type ApiErrorKind
  /** 400 with field-level messages. */
  = | 'validation'
  /** 404 — something that does not exist, or a stale link. */
    | 'notFound'
  /** 409 — the request conflicts with the current state. */
    | 'conflict'
  /** 401/403 — no session, wrong credentials, or not yours to touch. */
    | 'unauthorized'
  /** The request never got an answer: offline, DNS, CORS, timeout. */
    | 'network'
  /** 5xx — the backend failed. */
    | 'server'
  /** Anything we could not classify. */
    | 'unknown'

const expectedKinds: ReadonlySet<ApiErrorKind> = new Set<ApiErrorKind>([
  'validation',
  'notFound',
  'conflict',
  'unauthorized',
  // A network failure is nearly always the user's connectivity, not a defect.
  // Reporting it would drown the real issues in noise.
  'network',
])

/**
 * A failed API call, normalised into something the UI can act on.
 *
 * Note what is *not* here: a sentence to show somebody. The app speaks two
 * languages, so the words live in the message catalogue and are chosen from
 * `kind` at render time (app/i18n/messages.ts). `message` is the technical
 * summary that ends up in a breadcrumb.
 */
export class ApiError extends Error {
  readonly kind: ApiErrorKind
  readonly status: number | null
  /** Field name -> messages. Empty unless `kind === 'validation'`. */
  readonly fieldErrors: Readonly<Record<string, string[]>>
  /**
   * Why a 401 came back — `invalidCredentials`, `lockedOut`, `noSession`.
   *
   * Structure rather than a sentence, because the API never sends prose: the
   * sign-in screen chooses the words from its own catalogue. Null for every
   * other kind of failure.
   */
  readonly reason: string | null
  /** Correlation id from the backend, safe to show to a user. */
  readonly traceId: string | null
  /** Sentry event id the backend recorded, when it recorded one. */
  readonly errorId: string | null

  constructor(init: {
    kind: ApiErrorKind
    status?: number | null
    fieldErrors?: Record<string, string[]>
    reason?: string | null
    traceId?: string | null
    errorId?: string | null
    cause?: unknown
  }) {
    super(`API request failed (${init.kind}${init.status ? `, ${init.status}` : ''})`, { cause: init.cause })
    this.name = 'ApiError'
    this.kind = init.kind
    this.status = init.status ?? null
    this.fieldErrors = Object.freeze({ ...(init.fieldErrors ?? {}) })
    this.reason = init.reason ?? null
    this.traceId = init.traceId ?? null
    this.errorId = init.errorId ?? null
  }

  /** True when this is normal use, not a defect. Such errors skip Sentry. */
  get isExpected(): boolean {
    return expectedKinds.has(this.kind)
  }
}

export function isApiError(error: unknown): error is ApiError {
  return error instanceof ApiError
}

/**
 * A failure as plain data.
 *
 * Components take this rather than an {@link ApiError} because it survives the
 * server-to-client boundary: anything a page puts in `useAsyncData` is
 * serialised into the payload and revived in the browser, and a class instance
 * would arrive as a bare object with no prototype — `isExpected` gone, the
 * error state silently lost during hydration.
 */
export interface ApiFailure {
  kind: ApiErrorKind
  status: number | null
  fieldErrors: Record<string, string[]>
  reason: string | null
  traceId: string | null
  errorId: string | null
  isExpected: boolean
}

export function toApiFailure(error: ApiError): ApiFailure {
  return {
    kind: error.kind,
    status: error.status,
    fieldErrors: { ...error.fieldErrors },
    reason: error.reason,
    traceId: error.traceId,
    errorId: error.errorId,
    isExpected: error.isExpected,
  }
}

function kindForStatus(status: number): ApiErrorKind {
  // A 400 is always the caller's input, whether or not the backend broke it
  // down per field.
  if (status === 400) return 'validation'
  if (status === 401 || status === 403) return 'unauthorized'
  if (status === 404) return 'notFound'
  if (status === 409) return 'conflict'
  if (status >= 500) return 'server'
  return 'unknown'
}

interface FetchLikeError {
  status?: number
  statusCode?: number
  data?: unknown
  message?: string
  cause?: unknown
}

function readProblem(data: unknown): ProblemDetails & Partial<ValidationProblemDetails> & {
  reason?: unknown
  traceId?: unknown
  errorId?: unknown
} {
  return (typeof data === 'object' && data !== null ? data : {}) as never
}

function readFieldErrors(problem: { errors?: unknown }): Record<string, string[]> {
  if (typeof problem.errors !== 'object' || problem.errors === null) return {}

  const result: Record<string, string[]> = {}
  for (const [field, messages] of Object.entries(problem.errors as Record<string, unknown>)) {
    if (Array.isArray(messages)) {
      result[field] = messages.filter((m): m is string => typeof m === 'string')
    }
  }
  return result
}

/**
 * Turns anything a fetch can throw into an {@link ApiError}.
 *
 * Called in exactly one place — the API client — so every component downstream
 * sees the same shape and nobody has to inspect `error.response.data` again.
 */
export function normalizeApiError(error: unknown): ApiError {
  if (isApiError(error)) return error

  const candidate = (error ?? {}) as FetchLikeError
  const status = candidate.status ?? candidate.statusCode ?? null

  // No status means the request never completed: offline, CORS, DNS, abort.
  if (status === null || status === 0) {
    return new ApiError({ kind: 'network', status: null, cause: error })
  }

  const problem = readProblem(candidate.data)

  return new ApiError({
    kind: kindForStatus(status),
    status,
    fieldErrors: readFieldErrors(problem),
    reason: typeof problem.reason === 'string' ? problem.reason : null,
    traceId: typeof problem.traceId === 'string' ? problem.traceId : null,
    errorId: typeof problem.errorId === 'string' ? problem.errorId : null,
    cause: error,
  })
}
