import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError } from '~/api/errors'
import { useErrorReporter } from '~/composables/useErrorReporter'

const sentry = vi.hoisted(() => ({
  addBreadcrumb: vi.fn(),
  captureException: vi.fn(),
  logger: { info: vi.fn(), error: vi.fn() },
  metrics: { count: vi.fn() },
}))

vi.mock('@sentry/nuxt', () => sentry)

describe('useErrorReporter', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('records expected failures as context but never as issues', () => {
    const { report } = useErrorReporter()
    const result = report(new ApiError({ kind: 'network' }), { feature: 'goals', action: 'list' })

    expect(result).toMatchObject({ kind: 'network', isExpected: true })
    expect(sentry.addBreadcrumb).toHaveBeenCalledWith({
      category: 'q2.api',
      level: 'info',
      message: 'goals.list failed (network)',
      data: { kind: 'network', status: 'none' },
    })
    expect(sentry.logger.info).toHaveBeenCalledWith('goals.list failed (network)', {
      'q2.feature': 'goals',
      'q2.action': 'list',
      'q2.error_kind': 'network',
      'status': 'none',
    })
    expect(sentry.metrics.count).toHaveBeenCalledWith('q2.api.failure', 1, {
      attributes: { kind: 'network', expected: true, status: 0 },
    })
    expect(sentry.captureException).not.toHaveBeenCalled()
  })

  it('does not duplicate a server issue the backend already recorded', () => {
    const { report } = useErrorReporter()
    const result = report(new ApiError({
      kind: 'server',
      status: 500,
      errorId: 'event-1',
    }), { feature: 'chats', action: 'send' })

    expect(result).toMatchObject({ kind: 'server', errorId: 'event-1' })
    expect(sentry.logger.error).toHaveBeenCalledOnce()
    expect(sentry.addBreadcrumb).toHaveBeenLastCalledWith({
      category: 'q2.api',
      level: 'error',
      message: 'Backend reported this failure',
      data: { errorId: 'event-1' },
    })
    expect(sentry.captureException).not.toHaveBeenCalled()
  })

  it('captures an unexpected client-side cause once with a closed context', () => {
    const cause = new Error('render failed')
    const { report } = useErrorReporter()
    const result = report(new ApiError({
      kind: 'unknown',
      status: 418,
      traceId: 'trace-1',
      cause,
    }), { feature: 'profile', action: 'load' })

    expect(result).toMatchObject({ kind: 'unknown', traceId: 'trace-1' })
    expect(sentry.captureException).toHaveBeenCalledWith(cause, {
      tags: {
        'q2.feature': 'profile',
        'q2.action': 'load',
        'q2.error_kind': 'unknown',
      },
      contexts: { q2: { status: 418, traceId: 'trace-1' } },
    })
  })

  it('normalises unknown thrown values before deciding what to report', () => {
    const { report } = useErrorReporter()

    expect(report(new TypeError('offline'), { feature: 'home', action: 'load' })).toMatchObject({
      kind: 'network',
      isExpected: true,
    })
    expect(sentry.captureException).not.toHaveBeenCalled()
  })
})
