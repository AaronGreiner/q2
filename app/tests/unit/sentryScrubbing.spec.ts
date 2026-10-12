import type { Breadcrumb, ErrorEvent, Event, Log, Metric, ReplayFrameEvent } from '@sentry/nuxt'
import { describe, expect, it } from 'vitest'
import {
  isSensitiveKey,
  replayBlockSelectors,
  replayMaskSelectors,
  redactText,
  resolveSentryOptions,
  scrubAddresses,
  scrubBreadcrumb,
  scrubEvent,
  scrubLog,
  scrubMetric,
  scrubRecordingEvent,
  scrubTransaction,
  sentryMetricNames,
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

  it('strips a fragment too, which is where a reset link carries its token', () => {
    const result = redactText('opened https://q2.example.com/reset-password#token=aaaa.bbbbsecret')

    expect(result).not.toContain('bbbbsecret')
    expect(result).toContain('/reset-password')
  })

  it.each([
    ['token=abc123secret', 'abc123secret'],
    ['"password": "hunter2"', 'hunter2'],
    ['Authorization: Bearer aaa.bbb.ccc', 'aaa.bbb.ccc'],
    ['jwt eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxIn0.sig', 'eyJhbGciOiJIUzI1NiJ9'],
    ['mail robin.sample@example.com', 'robin.sample@example.com'],
    ['lat=48.1372 lon=11.5756', '48.1372'],
    ['GoalTitle: Therapy appointment every Tuesday', 'Therapy appointment'],
    ['{"messageText":"I feel overwhelmed today"}', 'I feel overwhelmed'],
  ])('redacts %s', (input, secret) => {
    expect(redactText(input)).not.toContain(secret)
  })

  it('leaves harmless text alone', () => {
    const message = 'Loaded 3 goals in 42ms'
    expect(redactText(message)).toBe(message)
  })
})

describe('isSensitiveKey', () => {
  it.each(['password', 'apiKey', 'Authorization', 'cookie', 'latitude', 'goal.description', 'GoalTitle'])(
    'recognises %s',
    key => expect(isSensitiveKey(key)).toBe(true),
  )

  it.each(['requestId', 'status', 'durationMs'])(
    'keeps %s',
    key => expect(isSensitiveKey(key)).toBe(false),
  )
})

describe('scrubEvent', () => {
  it('keeps only the IP address from the Sentry user context', () => {
    const event = scrubEvent(errorEvent({
      user: { id: '42', email: 'robin.sample@example.com', ip_address: '203.0.113.9' },
      server_name: 'build-agent-7',
    }))

    expect(event?.user).toEqual({ ip_address: '203.0.113.9' })

    // The host the code ran on is still never reported.
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

  it('removes the fragment from the request URL', () => {
    const event = scrubEvent(errorEvent({
      request: { url: 'https://q2.example.com/reset-password#token=aaaa.bbbbsecret' },
    }))

    expect(event?.request?.url).toBe('https://q2.example.com/reset-password')
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

describe('scrubLog', () => {
  it('redacts the message and drops personal or identifying attributes', () => {
    const log = scrubLog({
      level: 'warn',
      message: 'request failed for robin.sample@example.com',
      attributes: {
        'q2.feature': 'goals',
        'goal_title': 'Therapy appointment',
        'user.id': '42',
      },
    } as Log)

    expect(log?.message).not.toContain('robin.sample@example.com')
    expect(log?.attributes).toEqual({ 'q2.feature': 'goals' })
  })
})

describe('scrubMetric', () => {
  it('keeps the declared counter but removes SDK-added identity', () => {
    const metric = scrubMetric({
      type: 'counter',
      name: sentryMetricNames.apiFailure,
      value: 1,
      attributes: { 'kind': 'Offline', 'user.id': '42', 'user.email': 'robin@example.com' },
    } as Metric)

    expect(metric?.attributes).toEqual({ kind: 'Offline' })
  })

  it('drops an undeclared metric', () => {
    expect(scrubMetric({ type: 'counter', name: 'anything.dynamic', value: 1 } as Metric)).toBeNull()
  })
})

describe('Session Replay privacy selectors', () => {
  it('masks personal text and blocks stateful personal visuals', () => {
    expect(replayMaskSelectors).toEqual(['[data-q2-private]', 'head > title'])
    expect(replayBlockSelectors).toEqual(['[data-q2-block]'])
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

  it('strips the fragment from a breadcrumb URL', () => {
    const crumb = scrubBreadcrumb({
      category: 'navigation',
      data: { url: 'https://q2.example.com/reset-password#token=aaaa.bbbbsecret' },
    }) as Breadcrumb

    expect(crumb.data?.url).toBe('https://q2.example.com/reset-password')
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

  it('records every session with replay', () => {
    const options = resolveSentryOptions(base)

    expect(options.replaysSessionSampleRate).toBe(1)
    expect(options.replaysOnErrorSampleRate).toBe(1)
  })

  it('sends default PII', () => {
    expect(resolveSentryOptions(base).sendDefaultPii).toBe(true)
  })

  it('wires the shared filters', () => {
    const options = resolveSentryOptions(base)

    expect(options.beforeSend).toBe(scrubEvent)
    expect(options.beforeSendTransaction).toBe(scrubTransaction)
    expect(options.beforeBreadcrumb).toBe(scrubBreadcrumb)
    expect(options.beforeSendLog).toBe(scrubLog)
    expect(options.beforeSendMetric).toBe(scrubMetric)
    expect(options.enableLogs).toBe(true)
    expect(options.enableMetrics).toBe(true)
  })
})

/**
 * An invite code is a credential in everything but name, and it is part of the
 * address: `/join/<code>`, then `/register?invite=<code>` and
 * `/login?next=%2Fjoin%2F<code>` (#47). Every route Sentry has to an address
 * is followed here — events, breadcrumbs, transactions, a replay's metadata and
 * its recording, and a feedback.
 */
describe('invite codes', () => {
  const code = 'q2Inv1te_Code-x7Yz'

  type TransactionEvent = Parameters<typeof scrubTransaction>[0]

  it.each([
    ['/join/' + code, '/join/[redacted]'],
    ['https://q2.example.com/join/' + code, 'https://q2.example.com/join/[redacted]'],
    ['/register?invite=' + code, '/register?invite=[redacted]'],
    ['/login?next=%2Fjoin%2F' + code, '/login?next=%2Fjoin%2F[redacted]'],
    ['{"inviteCode":"' + code + '"}', '{"inviteCode":"[redacted]"}'],
  ])('redacts the code in %s', (input, expected) => {
    expect(redactText(input)).toBe(expected)
  })

  it('keeps the route template, which says nothing', () => {
    expect(redactText('/join/:code()')).toBe('/join/:code()')
  })

  it('leaves the word alone where it is not a code', () => {
    expect(redactText('invited: 3 friends')).toBe('invited: 3 friends')
  })

  it('drops the route parameter and the query the router puts on a span', () => {
    expect(isSensitiveKey('params.code')).toBe(true)
    expect(isSensitiveKey('url.path.parameter.code')).toBe(true)
    expect(isSensitiveKey('query.invite')).toBe(true)
    expect(isSensitiveKey('query.next')).toBe(true)

    // A goal's id is not a credential, and the template says which route it was.
    expect(isSensitiveKey('params.id')).toBe(false)
    expect(isSensitiveKey('url.template')).toBe(false)
  })

  it('leaves no code in an error raised on the page', () => {
    const event = scrubEvent(errorEvent({ request: { url: `https://q2.example.com/join/${code}` } }))

    expect(event?.request?.url).toBe('https://q2.example.com/join/[redacted]')
  })

  it('leaves no code in a navigation breadcrumb, either way', () => {
    const into = scrubBreadcrumb({ category: 'navigation', data: { from: '/', to: `/join/${code}` } })
    const onward = scrubBreadcrumb({
      category: 'navigation',
      data: { from: `/join/${code}`, to: `/register?invite=${code}` },
    })

    expect(into?.data?.to).toBe('/join/[redacted]')
    expect(onward?.data).toEqual({ from: '/join/[redacted]', to: '/register' })
  })

  // Named after the address, not the route: the browser's tracing has no router.
  it('leaves no code in a page-load transaction', () => {
    const event = scrubTransaction({
      type: 'transaction',
      transaction: `/join/${code}`,
      request: { url: `https://q2.example.com/join/${code}`, headers: { Referer: `https://q2.example.com/join/${code}` } },
      contexts: {
        trace: {
          trace_id: 'a'.repeat(32),
          span_id: 'b'.repeat(16),
          data: {
            'params.code': code,
            'url.path.parameter.code': code,
            'url.template': '/join/:code()',
            'url.full': `https://q2.example.com/join/${code}`,
          },
        },
      },
      spans: [{
        span_id: 'c'.repeat(16),
        trace_id: 'a'.repeat(32),
        start_timestamp: 1,
        description: `GET /register?invite=${code}`,
        data: { 'http.target': `/login?next=%2Fjoin%2F${code}`, 'query.invite': code },
      }],
    } as TransactionEvent)

    expect(JSON.stringify(event)).not.toContain(code)
    expect(event?.transaction).toBe('/join/[redacted]')
    expect(event?.contexts?.trace?.data?.['url.template']).toBe('/join/:code()')
  })

  it('leaves no code in a server transaction named after the request', () => {
    const event = scrubTransaction({ type: 'transaction', transaction: `GET /join/${code}` } as TransactionEvent)

    expect(event?.transaction).toBe('GET /join/[redacted]')
  })

  it('leaves no code in the pages a replay lists, nor in a feedback', () => {
    const replay = scrubAddresses({
      type: 'replay_event',
      urls: [`https://q2.example.com/join/${code}`, `/register?invite=${code}`],
      request: { url: `https://q2.example.com/join/${code}`, headers: { 'Referer': `https://q2.example.com/join/${code}`, 'User-Agent': 'Mobile' } },
    } as Event & { urls: string[] })
    const feedback = scrubAddresses({
      type: 'feedback',
      contexts: { feedback: { message: 'The button does nothing', url: `https://q2.example.com/join/${code}` } },
    } as Event)

    expect(JSON.stringify(replay)).not.toContain(code)
    // The browser is still worth knowing.
    expect(replay.request?.headers?.['User-Agent']).toBe('Mobile')
    expect(JSON.stringify(feedback)).not.toContain(code)
    // What somebody wrote is sent as written, on purpose (sentry.feedback.ts).
    expect(feedback.contexts?.feedback?.message).toBe('The button does nothing')
  })

  it('leaves no code in what a replay recorded on the way', () => {
    const navigation = scrubRecordingEvent({
      type: 5,
      timestamp: 1,
      data: {
        tag: 'performanceSpan',
        payload: {
          op: 'navigation.push',
          description: `/register?invite=${code}`,
          startTimestamp: 1,
          endTimestamp: 1,
          data: { previous: `/join/${code}` },
        },
      },
    } as ReplayFrameEvent)
    const crumb = scrubRecordingEvent({
      type: 5,
      timestamp: 1,
      data: {
        tag: 'breadcrumb',
        payload: { category: 'replay.hydrate-error', timestamp: 1, data: { url: `https://q2.example.com/join/${code}` } },
      },
    } as ReplayFrameEvent)

    expect(JSON.stringify(navigation)).not.toContain(code)
    expect(JSON.stringify(crumb)).not.toContain(code)
  })

  it('drops from a recording what it drops everywhere else', () => {
    const typed = scrubRecordingEvent({
      type: 5,
      timestamp: 1,
      data: { tag: 'breadcrumb', payload: { category: 'ui.input', timestamp: 1, message: 'input[name=title]' } },
    } as ReplayFrameEvent)

    expect(typed).toBeNull()
  })
})
