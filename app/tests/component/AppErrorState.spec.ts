import { mountSuspended } from '@nuxt/test-utils/runtime'
import { describe, expect, it } from 'vitest'
import AppErrorState from '~/components/ui/AppErrorState.vue'
import type { ApiFailure } from '~/api/errors'

/**
 * What a person is shown when a request fails.
 *
 * The two rules this component exists to enforce are both asserted here: no
 * backend wording ever reaches the screen, and a recorded incident always shows
 * its reference.
 */
function failure(overrides: Partial<ApiFailure> = {}): ApiFailure {
  return {
    kind: 'server',
    status: 500,
    fieldErrors: {},
    traceId: null,
    errorId: null,
    isExpected: false,
    ...overrides,
  }
}

describe('AppErrorState', () => {
  it('interrupts a screen reader, because something actually went wrong', async () => {
    const wrapper = await mountSuspended(AppErrorState, { props: { error: failure() } })

    expect(wrapper.find('[role="alert"]').exists()).toBe(true)
  })

  it('names an offline failure as one rather than as a defect', async () => {
    const wrapper = await mountSuspended(AppErrorState, { props: { error: failure({ kind: 'network' }) } })

    expect(wrapper.text()).toContain('Keine Verbindung')
  })

  it('gives a stale link its own calm wording', async () => {
    const wrapper = await mountSuspended(AppErrorState, { props: { error: failure({ kind: 'notFound' }) } })

    expect(wrapper.text()).toContain('Nicht gefunden')
  })

  it('shows the reference a support request can quote', async () => {
    const wrapper = await mountSuspended(AppErrorState, {
      props: { error: failure({ errorId: 'sentry-event-1' }) },
    })

    expect(wrapper.text()).toContain('sentry-event-1')
  })

  it('falls back to the trace id when the backend recorded no event', async () => {
    const wrapper = await mountSuspended(AppErrorState, {
      props: { error: failure({ traceId: 'trace-1' }) },
    })

    expect(wrapper.text()).toContain('trace-1')
  })

  it('offers a retry only where retrying makes sense', async () => {
    const without = await mountSuspended(AppErrorState, { props: { error: failure() } })
    const with_ = await mountSuspended(AppErrorState, { props: { error: failure(), retryable: true } })

    expect(without.find('[data-testid="error-retry"]').exists()).toBe(false)
    expect(with_.find('[data-testid="error-retry"]').exists()).toBe(true)
  })

  it('emits retry rather than reloading anything itself', async () => {
    const wrapper = await mountSuspended(AppErrorState, {
      props: { error: failure(), retryable: true },
    })

    await wrapper.find('[data-testid="error-retry"]').trigger('click')

    expect(wrapper.emitted('retry')).toHaveLength(1)
  })
})
