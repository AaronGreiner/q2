import { mountSuspended } from '@nuxt/test-utils/runtime'
import { describe, expect, it } from 'vitest'
import GoalTaskRow from '~/components/goals/GoalTaskRow.vue'
import type { GoalTask } from '~/api/types'

/**
 * The row somebody actually taps, several times a day. Its accessibility is
 * not decoration: the tick box is the whole point of the screen.
 */
function task(overrides: Partial<GoalTask> = {}): GoalTask {
  return {
    id: '019faece-5a81-7c67-8fa2-00a63d9e9300',
    goalId: null,
    title: 'Joggen 5 km',
    rhythm: 'Daily',
    reminderAt: '07:00:00',
    isDone: false,
    measuredValue: null,
    targetValue: null,
    measureUnit: null,
    measurePercent: null,
    ...overrides,
  }
}

describe('GoalTaskRow', () => {
  it('shows the title, the rhythm and the time', async () => {
    const wrapper = await mountSuspended(GoalTaskRow, { props: { task: task() } })

    expect(wrapper.text()).toContain('Joggen 5 km')
    expect(wrapper.text()).toContain('Täglich')
    expect(wrapper.text()).toContain('07:00')
    expect(wrapper.find('[data-testid="task-row"]').attributes()).toHaveProperty('data-q2-block')
  })

  it('offers the tick as a checkbox a keyboard can reach', async () => {
    const wrapper = await mountSuspended(GoalTaskRow, { props: { task: task() } })
    const toggle = wrapper.find('[data-testid="task-toggle"]')

    expect(toggle.attributes('role')).toBe('checkbox')
    expect(toggle.attributes('aria-checked')).toBe('false')
    expect(toggle.attributes('aria-label')).toBe('Joggen 5 km')
  })

  it('announces a completed task as checked', async () => {
    const wrapper = await mountSuspended(GoalTaskRow, { props: { task: task({ isDone: true }) } })

    expect(wrapper.find('[data-testid="task-toggle"]').attributes('aria-checked')).toBe('true')
  })

  it('emits the id it was asked about rather than acting itself', async () => {
    const wrapper = await mountSuspended(GoalTaskRow, { props: { task: task() } })

    await wrapper.find('[data-testid="task-toggle"]').trigger('click')

    expect(wrapper.emitted('toggle')).toEqual([['019faece-5a81-7c67-8fa2-00a63d9e9300']])
  })

  it('does not emit while a toggle is already in flight', async () => {
    const wrapper = await mountSuspended(GoalTaskRow, { props: { task: task(), busy: true } })

    await wrapper.find('[data-testid="task-toggle"]').trigger('click')

    expect(wrapper.emitted('toggle')).toBeUndefined()
  })

  it('shows a measurable amount the way the language writes it', async () => {
    const wrapper = await mountSuspended(GoalTaskRow, {
      props: {
        task: task({ measuredValue: 1.2, targetValue: 2, measureUnit: 'L', measurePercent: 60 }),
      },
    })

    expect(wrapper.text()).toContain('1,2 / 2 L')
  })

  it('only draws the measure bar in the detailed layout', async () => {
    const measurable = task({ measuredValue: 1.2, targetValue: 2, measureUnit: 'L', measurePercent: 60 })

    const compact = await mountSuspended(GoalTaskRow, { props: { task: measurable } })
    const detailed = await mountSuspended(GoalTaskRow, { props: { task: measurable, detailed: true } })

    expect(compact.find('[data-testid="progress-bar"]').exists()).toBe(false)
    expect(detailed.find('[data-testid="progress-bar"]').attributes('aria-valuenow')).toBe('60')
  })
})
