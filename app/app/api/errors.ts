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
  /** 404 — a goal that does not exist, or a stale link. */
    | 'notFound'
  /** 409 — the request conflicts with the current state. */
    | 'conflict'
  /** 401/403 — reserved for when authentication arrives. */
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

/** A failed API call, normalised into something the UI can act on. */
export class ApiError extends Error {
  readonly kind: ApiErrorKind
  readonly status: number | null
  /** Field name -> messages. Empty unless `kind === 'validation'`. */
  readonly fieldErrors: Readonly<Record<string, string[]>>
  /** Correlation id from the backend, safe to show to a user. */
  readonly traceId: string | null
  /** Sentry event id the backend recorded, when it recorded one. */
  readonly errorId: string | null

  constructor(init: {
    kind: ApiErrorKind
    message: string
    status?: number | null
    fieldErrors?: Record<string, string[]>
    traceId?: string | null
    errorId?: string | null
    cause?: unknown
  }) {
    super(init.message, { cause: init.cause })
    this.name = 'ApiError'
    this.kind = init.kind
    this.status = init.status ?? null
    this.fieldErrors = Object.freeze({ ...(init.fieldErrors ?? {}) })
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
  message: string
  status: number | null
  fieldErrors: Record<string, string[]>
  traceId: string | null
  errorId: string | null
  isExpected: boolean
}

export function toApiFailure(error: ApiError): ApiFailure {
  return {
    kind: error.kind,
    message: error.message,
    status: error.status,
    fieldErrors: { ...error.fieldErrors },
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

/**
 * Messages a person can act on. Deliberately free of status codes, exception
 * names and backend wording — the API's `detail` is written for developers.
 *
 * These are read as the *description* under a short heading (see
 * AppErrorState), so none of them may repeat that heading. "Something went
 * wrong" followed by "Something went wrong on our side" is what this comment
 * exists to prevent.
 */
export function messageForKind(kind: ApiErrorKind): string {
  switch (kind) {
    case 'validation':
      return 'Please check the highlighted fields and try again.'
    case 'notFound':
      return 'We could not find that goal. It may have been removed.'
    case 'conflict':
      return 'That change conflicts with the current state. Reload and try again.'
    case 'unauthorized':
      return 'You do not have access to this.'
    case 'network':
      return 'We could not reach the server. Check your connection and try again.'
    case 'server':
      return 'The server could not complete your request. Please try again in a moment.'
    case 'unknown':
      return 'Please try again. If it keeps happening, let us know and quote the reference below.'
  }
}

interface FetchLikeError {
  status?: number
  statusCode?: number
  data?: unknown
  message?: string
  cause?: unknown
}

function readProblem(data: unknown): ProblemDetails & Partial<ValidationProblemDetails> & {
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
    return new ApiError({
      kind: 'network',
      message: messageForKind('network'),
      status: null,
      cause: error,
    })
  }

  const problem = readProblem(candidate.data)
  const fieldErrors = readFieldErrors(problem)
  const kind = kindForStatus(status)

  return new ApiError({
    kind,
    message: messageForKind(kind),
    status,
    fieldErrors,
    traceId: typeof problem.traceId === 'string' ? problem.traceId : null,
    errorId: typeof problem.errorId === 'string' ? problem.errorId : null,
    cause: error,
  })
}
