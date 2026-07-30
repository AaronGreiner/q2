import type { Breadcrumb, ErrorEvent } from '@sentry/nuxt'
import { describe, expect, it } from 'vitest'
import {
  isSensitiveKey,
  redactText,
  resolveSentryOptions,
  scrubBreadcrumb,
  scrubEvent,
} from '../../sentry.shared'

/**
 * These run against the exact functions wired into `Sentry.init` as
 * `beforeSend` / `beforeBreadcrumb` — not a copy of them — so a passing test
 * means the shipped configuration filters this way.
 */

function errorEvent(overrides: Partial<ErrorEvent> = {}): ErrorEvent {
  return {
    event_id: 'abc',
    exception: { values: [{ type: 'Error', value: 'Something failed' }] },
    ...overrides,
  } as ErrorEvent
}

describe('redactText', () => {
  it('strips query strings from URLs but keeps the path', () => {
    const result = redactText('GET http://localhost:5080/api/goals?token=supersecret123')

    expect(result).not.toContain('supersecret123')
    expect(result).toContain('/api/goals')
  })

  it.each([
    ['token=abc123secret', 'abc123secret'],
    ['"password": "hunter2"', 'hunter2'],
    ['Authorization: Bearer aaa.bbb.ccc', 'aaa.bbb.ccc'],
    ['jwt eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxIn0.sig', 'eyJhbGciOiJIUzI1NiJ9'],
    ['mail robin.sample@example.com', 'robin.sample@example.com'],
    ['lat=48.1372 lon=11.5756', '48.1372'],
  ])('redacts %s', (input, secret) => {
    expect(redactText(input)).not.toContain(secret)
  })

  it('leaves harmless text alone', () => {
    const message = 'Loaded 3 goals in 42ms'
    expect(redactText(message)).toBe(message)
  })
})

describe('isSensitiveKey', () => {
  it.each(['password', 'apiKey', 'Authorization', 'cookie', 'latitude', 'goal.description'])(
    'recognises %s',
    key => expect(isSensitiveKey(key)).toBe(true),
  )

  it.each(['requestId', 'status', 'durationMs'])(
    'keeps %s',
    key => expect(isSensitiveKey(key)).toBe(false),
  )
})

describe('scrubEvent', () => {
  it('removes user identity and machine name', () => {
    const event = scrubEvent(errorEvent({
      user: { id: '42', email: 'robin.sample@example.com', ip_address: '203.0.113.9' },
      server_name: 'build-agent-7',
    }))

    expect(event?.user).toBeUndefined()
    expect(event?.server_name).toBeUndefined()
  })

  it('removes cookies, headers, body and query string from the request', () => {
    const event = scrubEvent(errorEvent({
      request: {
        url: 'https://q2.example.com/goals?token=leak',
        query_string: 'token=leak',
        cookies: { session: 'abc' },
        headers: { Authorization: 'Bearer secret' },
        data: { title: 'Therapy appointment' },
      },
    }))

    expect(event?.request?.cookies).toBeUndefined()
    expect(event?.request?.headers).toBeUndefined()
    expect(event?.request?.data).toBeUndefined()
    expect(event?.request?.url).toBe('https://q2.example.com/goals')
    expect(JSON.stringify(event)).not.toContain('leak')
    expect(JSON.stringify(event)).not.toContain('Therapy appointment')
  })

  it('drops sensitive keys from extras and tags', () => {
    const event = scrubEvent(errorEvent({
      extra: { 'requestId': '0HN1', 'apiKey': 'secret-key', 'goal.title': 'Therapy appointment' },
      tags: { 'q2.feature': 'goals', 'authorization': 'Bearer x' },
    }))

    expect(event?.extra).toEqual({ requestId: '0HN1' })
    expect(event?.tags).toEqual({ 'q2.feature': 'goals' })
  })

  it('redacts secrets inside exception messages', () => {
    const event = scrubEvent(errorEvent({
      exception: { values: [{ type: 'Error', value: 'failed for token=abc123secret' }] },
    }))

    expect(event?.exception?.values?.[0]?.value).not.toContain('abc123secret')
  })

  it.each([
    'Failed to fetch',
    'AbortError: The user aborted a request',
    'ResizeObserver loop completed with undelivered notifications',
  ])('drops the expected browser noise: %s', (message) => {
    const event = scrubEvent(errorEvent({
      exception: { values: [{ type: 'Error', value: message }] },
    }))

    expect(event).toBeNull()
  })

  it('keeps a genuine application error', () => {
    expect(scrubEvent(errorEvent())).not.toBeNull()
  })
})

describe('scrubBreadcrumb', () => {
  it('drops UI input breadcrumbs entirely', () => {
    // These record what the user typed into a goal title.
    expect(scrubBreadcrumb({ category: 'ui.input', message: 'input[name=title]' })).toBeNull()
  })

  it('strips the query string from fetch breadcrumbs', () => {
    const crumb = scrubBreadcrumb({
      category: 'fetch',
      data: { url: 'https://q2.example.com/api/goals?token=leak', status_code: 500 },
    }) as Breadcrumb

    expect(crumb.data?.url).toBe('https://q2.example.com/api/goals')
    expect(crumb.data?.status_code).toBe(500)
  })

  it('redacts breadcrumb messages', () => {
    const crumb = scrubBreadcrumb({ category: 'console', message: 'using token=abc123secret' })

    expect(crumb?.message).not.toContain('abc123secret')
  })

  it('keeps an ordinary navigation breadcrumb', () => {
    const crumb = scrubBreadcrumb({ category: 'navigation', data: { from: '/', to: '/goals/1' } })

    expect(crumb?.data?.to).toBe('/goals/1')
  })
})

describe('resolveSentryOptions', () => {
  const base = {
    dsn: 'https://publickey@sentry.example.com/42',
    environment: 'local-development',
    release: 'q2@abc123',
    enabled: false,
    tracesSampleRate: 1,
  }

  it('initialises in local development when a DSN is present', () => {
    const options = resolveSentryOptions(base)

    // Sentry is not switched off just because this is a laptop.
    expect(options.enabled).toBe(true)
    expect(options.environment).toBe('local-development')
    expect(options.release).toBe('q2@abc123')
  })

  it('stays quiet without a DSN instead of failing to start', () => {
    const options = resolveSentryOptions({ ...base, dsn: '' })

    expect(options.enabled).toBe(false)
    expect(options.dsn).toBeUndefined()
  })

  it('fails loudly when explicitly enabled without a DSN', () => {
    expect(() => resolveSentryOptions({ ...base, dsn: '', enabled: true }))
      .toThrow(/DSN/i)
  })

  it('never enables session replay by default', () => {
    const options = resolveSentryOptions(base)

    expect(options.replaysSessionSampleRate).toBe(0)
    expect(options.replaysOnErrorSampleRate).toBe(0)
  })

  it('never sends default PII', () => {
    expect(resolveSentryOptions(base).sendDefaultPii).toBe(false)
  })

  it('wires the shared filters', () => {
    const options = resolveSentryOptions(base)

    expect(options.beforeSend).toBe(scrubEvent)
    expect(options.beforeBreadcrumb).toBe(scrubBreadcrumb)
  })
})
