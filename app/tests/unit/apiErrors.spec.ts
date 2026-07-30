import { describe, expect, it } from 'vitest'
import { ApiError, isApiError, messageForKind, normalizeApiError } from '~/api/errors'

/**
 * The classification that decides both what a user reads and whether an
 * incident reaches Sentry.
 */

/** Shaped like what ofetch throws for a failed response. */
function fetchError(status: number, data?: unknown) {
  return Object.assign(new Error(`[GET] failed with ${status}`), { status, data })
}

describe('normalizeApiError', () => {
  it('treats a request that never completed as a network error', () => {
    const error = normalizeApiError(new TypeError('Failed to fetch'))

    expect(error.kind).toBe('network')
    expect(error.status).toBeNull()
    expect(error.isExpected).toBe(true)
  })

  it('reads field errors out of a validation problem', () => {
    const error = normalizeApiError(fetchError(400, {
      title: 'One or more validation errors occurred.',
      status: 400,
      errors: {
        Title: ['A title is required.'],
        ProgressPercent: ['Progress must be between 0 and 100.'],
      },
      traceId: '0HN123:00000001',
    }))

    expect(error.kind).toBe('validation')
    expect(error.fieldErrors.Title).toEqual(['A title is required.'])
    expect(error.fieldErrors.ProgressPercent).toHaveLength(1)
    expect(error.traceId).toBe('0HN123:00000001')
    expect(error.isExpected).toBe(true)
  })

  it('classifies a 400 without field details as validation too', () => {
    expect(normalizeApiError(fetchError(400, { title: 'Malformed request' })).kind).toBe('validation')
  })

  it.each([
    [401, 'unauthorized'],
    [403, 'unauthorized'],
    [404, 'notFound'],
    [409, 'conflict'],
    [500, 'server'],
    [503, 'server'],
    [418, 'unknown'],
  ])('maps status %i to %s', (status, expected) => {
    expect(normalizeApiError(fetchError(status)).kind).toBe(expected)
  })

  it('keeps the backend error id so support can find the Sentry issue', () => {
    const error = normalizeApiError(fetchError(500, {
      title: 'Unexpected error',
      traceId: '0HN123:00000002',
      errorId: '8df1d696c6ec446ca43d20f8c5a38a67',
    }))

    expect(error.errorId).toBe('8df1d696c6ec446ca43d20f8c5a38a67')
  })

  it('never surfaces the backend detail text to the user', () => {
    const error = normalizeApiError(fetchError(500, {
      title: 'Unexpected error',
      detail: 'SqliteException: unable to open database file Data Source=/srv/live.db',
    }))

    expect(error.message).toBe(messageForKind('server'))
    expect(error.message).not.toContain('Sqlite')
    expect(error.message).not.toContain('Data Source')
  })

  it('passes an ApiError through unchanged', () => {
    const original = new ApiError({ kind: 'notFound', message: 'gone' })

    expect(normalizeApiError(original)).toBe(original)
  })

  it('produces field errors that cannot be mutated by a caller', () => {
    const error = normalizeApiError(fetchError(400, { errors: { Title: ['required'] } }))

    expect(() => {
      // @ts-expect-error deliberately violating the readonly contract
      error.fieldErrors.Title = ['changed']
    }).toThrow()
  })

  it('ignores malformed error payloads instead of throwing', () => {
    expect(normalizeApiError(fetchError(400, 'just a string')).fieldErrors).toEqual({})
    expect(normalizeApiError(fetchError(400, { errors: 'nope' })).fieldErrors).toEqual({})
    expect(normalizeApiError(undefined).kind).toBe('network')
  })
})

describe('isExpected', () => {
  it.each([
    ['validation', true],
    ['notFound', true],
    ['conflict', true],
    ['unauthorized', true],
    ['network', true],
    ['server', false],
    ['unknown', false],
  ] as const)('%s -> %s', (kind, expected) => {
    // Only the last two deserve a Sentry issue; the rest are normal use.
    expect(new ApiError({ kind, message: 'x' }).isExpected).toBe(expected)
  })
})

describe('messageForKind', () => {
  it('never repeats the heading it is shown under', () => {
    // AppErrorState renders "Something went wrong" as the heading for these
    // two kinds. The page used to read "Something went wrongSomething went
    // wrong on our side." — found by reading the rendered page, not by a test.
    expect(messageForKind('server')).not.toContain('Something went wrong')
    expect(messageForKind('unknown')).not.toContain('Something went wrong')
    expect(messageForKind('network')).not.toContain('No connection')
    expect(messageForKind('notFound')).not.toContain('Not found')
  })

  it('is written for a person, not a developer', () => {
    for (const kind of ['validation', 'notFound', 'conflict', 'unauthorized', 'network', 'server', 'unknown'] as const) {
      const message = messageForKind(kind)

      expect(message.length).toBeGreaterThan(10)
      expect(message).not.toMatch(/\d{3}|exception|null|undefined/i)
    }
  })
})

describe('isApiError', () => {
  it('distinguishes our errors from ordinary ones', () => {
    expect(isApiError(new ApiError({ kind: 'server', message: 'x' }))).toBe(true)
    expect(isApiError(new Error('x'))).toBe(false)
    expect(isApiError(null)).toBe(false)
  })
})
