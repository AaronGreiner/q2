import { mountSuspended } from '@nuxt/test-utils/runtime'
import { describe, expect, it } from 'vitest'
import type { ApiFailure } from '~/api/errors'
import GoalList from '~/components/goals/GoalList.vue'
import type { Goal } from '~/api/types'

/**
 * The four states of a list, which is where most UI bugs actually live.
 * They are mutually exclusive: exactly one is rendered at a time.
 */
const today = new Date('2026-06-15T09:00:00Z')

function failure(overrides: Partial<ApiFailure> = {}): ApiFailure {
  return {
    kind: 'server',
    message: 'ignored',
    status: 500,
    fieldErrors: {},
    traceId: null,
    errorId: null,
    isExpected: false,
    ...overrides,
  }
}

function goal(id: string, title: string): Goal {
  return {
    id,
    title,
    description: null,
    status: 'Active',
    progressPercent: 30,
    createdAt: '2026-05-15T09:00:00+00:00',
    targetDate: null,
    participants: [],
    isOverdue: false,
  }
}

describe('GoalList', () => {
  it('shows skeletons while loading, and nothing else', async () => {
    const wrapper = await mountSuspended(GoalList, {
      props: { goals: [], loading: true, today },
    })

    const loading = wrapper.find('[data-testid="goal-list-loading"]')
    expect(loading.exists()).toBe(true)
    expect(loading.attributes('aria-busy')).toBe('true')
    expect(wrapper.find('[data-testid="goal-list"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="goal-list-empty"]').exists()).toBe(false)
  })

  it('announces loading to screen readers', async () => {
    const wrapper = await mountSuspended(GoalList, { props: { goals: [], loading: true, today } })

    expect(wrapper.text()).toContain('Loading goals')
  })

  it('shows the empty state when there are no goals', async () => {
    const wrapper = await mountSuspended(GoalList, { props: { goals: [], today } })

    const empty = wrapper.find('[data-testid="goal-list-empty"]')
    expect(empty.exists()).toBe(true)
    expect(empty.text()).toContain('No goals yet')
  })

  it('lets the page word the empty state for a filtered view', async () => {
    const wrapper = await mountSuspended(GoalList, {
      props: {
        goals: [],
        today,
        emptyTitle: 'No archived goals',
        emptyDescription: 'Nothing matches this filter right now.',
      },
    })

    expect(wrapper.text()).toContain('No archived goals')
  })

  it('shows an error state instead of an empty state when loading failed', async () => {
    const wrapper = await mountSuspended(GoalList, {
      props: {
        goals: [],
        error: failure(),
        today,
      },
    })

    expect(wrapper.find('[data-testid="error-state"]').exists()).toBe(true)

    // An error is not "you have no goals" — saying so would be a lie.
    expect(wrapper.find('[data-testid="goal-list-empty"]').exists()).toBe(false)
  })

  it('emits retry when the user asks to try again', async () => {
    const wrapper = await mountSuspended(GoalList, {
      props: {
        goals: [],
        error: failure({ kind: 'network', status: null, isExpected: true }),
        today,
      },
    })

    await wrapper.find('[data-testid="error-retry"]').trigger('click')

    expect(wrapper.emitted('retry')).toHaveLength(1)
  })

  it('renders one card per goal in a semantic list', async () => {
    const wrapper = await mountSuspended(GoalList, {
      props: {
        goals: [goal('1', 'Walk daily'), goal('2', 'Read more'), goal('3', 'Sleep earlier')],
        today,
      },
    })

    expect(wrapper.findAll('[data-testid="goal-card"]')).toHaveLength(3)
    expect(wrapper.findAll('ul > li')).toHaveLength(3)
    expect(wrapper.text()).toContain('Walk daily')
    expect(wrapper.text()).toContain('Sleep earlier')
  })

  it('prefers the error state over stale goals', async () => {
    const wrapper = await mountSuspended(GoalList, {
      props: {
        goals: [goal('1', 'Walk daily')],
        error: failure(),
        today,
      },
    })

    expect(wrapper.find('[data-testid="error-state"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="goal-list"]').exists()).toBe(false)
  })
})
