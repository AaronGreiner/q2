import { describe, expect, it } from 'vitest'
import { ApiError, isApiError, normalizeApiError, toApiFailure, type ApiErrorKind } from '~/api/errors'
import { de, en } from '~/i18n/messages'

/**
 * Normalisation is the single place a failure gets its meaning, so this is
 * where the mapping from "what came back" to "what the UI does about it" is
 * pinned down.
 */

/** The shape ofetch throws. */
function fetchError(status: number, data?: unknown) {
  return { status, statusCode: status, data, message: `HTTP ${status}` }
}

describe('normalizeApiError', () => {
  it.each<[number, ApiErrorKind]>([
    [400, 'validation'],
    [401, 'unauthorized'],
    [403, 'unauthorized'],
    [404, 'notFound'],
    [409, 'conflict'],
    [500, 'server'],
    [503, 'server'],
    [418, 'unknown'],
  ])('maps %i onto "%s"', (status, kind) => {
    expect(normalizeApiError(fetchError(status)).kind).toBe(kind)
  })

  it('treats a request that never got an answer as a network failure', () => {
    // Offline, DNS, CORS, abort — all of them arrive without a status.
    expect(normalizeApiError(new TypeError('Failed to fetch')).kind).toBe('network')
    expect(normalizeApiError({ status: 0 }).kind).toBe('network')
    expect(normalizeApiError(undefined).kind).toBe('network')
  })

  it('keeps the field errors a validation problem carried', () => {
    const error = normalizeApiError(fetchError(400, {
      errors: { Title: ['A title is required.'], Icon: ['That icon is not one of the available goal icons.'] },
    }))

    expect(error.fieldErrors.Title).toEqual(['A title is required.'])
    expect(Object.keys(error.fieldErrors)).toHaveLength(2)
  })

  it('ignores field errors that are not lists of strings', () => {
    const error = normalizeApiError(fetchError(400, { errors: { Title: 'nope', Icon: [1, 2] } }))

    expect(error.fieldErrors.Title).toBeUndefined()
    expect(error.fieldErrors.Icon).toEqual([])
  })

  it('keeps the correlation ids a user can quote', () => {
    const error = normalizeApiError(fetchError(500, { traceId: 'trace-1', errorId: 'sentry-1' }))

    expect(error.traceId).toBe('trace-1')
    expect(error.errorId).toBe('sentry-1')
  })

  it('leaves an already-normalised error alone', () => {
    const original = new ApiError({ kind: 'conflict', status: 409 })

    expect(normalizeApiError(original)).toBe(original)
  })

  it('never puts the backend\'s wording in front of a person', () => {
    // `detail` is written for developers and may contain internals.
    const error = normalizeApiError(fetchError(500, { detail: 'NullReferenceException at Q2.Api.Foo' }))

    expect(error.message).not.toContain('NullReference')
    expect(error.message).not.toContain('Q2.Api')
  })
})

describe('ApiError.isExpected', () => {
  it.each<[ApiErrorKind, boolean]>([
    ['validation', true],
    ['notFound', true],
    ['conflict', true],
    ['unauthorized', true],
    ['network', true],
    ['server', false],
    ['unknown', false],
  ])('treats "%s" as expected: %s', (kind, expected) => {
    expect(new ApiError({ kind }).isExpected).toBe(expected)
  })
})

describe('toApiFailure', () => {
  it('produces plain data that survives the SSR payload', () => {
    const failure = toApiFailure(new ApiError({
      kind: 'validation',
      status: 400,
      fieldErrors: { Title: ['A title is required.'] },
      traceId: 'trace-1',
    }))

    // A class instance would arrive in the browser without its prototype, and
    // `isExpected` would be gone.
    expect(JSON.parse(JSON.stringify(failure))).toEqual(failure)
    expect(failure.isExpected).toBe(true)
  })
})

describe('isApiError', () => {
  it('recognises its own type and nothing else', () => {
    expect(isApiError(new ApiError({ kind: 'network' }))).toBe(true)
    expect(isApiError(new Error('nope'))).toBe(false)
    expect(isApiError(null)).toBe(false)
  })
})

describe('the copy behind a failure', () => {
  const kinds: ApiErrorKind[] = [
    'validation', 'notFound', 'conflict', 'unauthorized', 'network', 'server', 'unknown',
  ]

  it('exists in both languages for every kind', () => {
    for (const kind of kinds) {
      expect(de.errors[kind]).toBeTruthy()
      expect(en.errors[kind]).toBeTruthy()
    }
  })

  it('never repeats the heading it is shown under', () => {
    // "Something went wrong" followed by "Something went wrong on our side" is
    // what this assertion exists to prevent.
    for (const [messages, titles] of [[de, de.errors.title], [en, en.errors.title]] as const) {
      for (const kind of kinds) {
        for (const title of Object.values(titles)) {
          expect(messages.errors[kind]).not.toContain(title)
        }
      }
    }
  })
})
