import { mountSuspended } from '@nuxt/test-utils/runtime'
import { describe, expect, it } from 'vitest'
import GoalCard from '~/components/goals/GoalCard.vue'
import type { Goal } from '~/api/types'

/**
 * The reference component test: real Nuxt environment, real Nuxt UI, plain
 * props in, rendered output asserted. `today` is injected, so nothing here
 * depends on the day the suite happens to run.
 */
const today = new Date('2026-06-15T09:00:00Z')

function goal(overrides: Partial<Goal> = {}): Goal {
  return {
    id: '019faece-5a81-7c67-8fa2-00a63d9e9127',
    title: 'Run a 10k together',
    description: 'Twice a week, building up slowly.',
    status: 'Active',
    progressPercent: 62,
    createdAt: '2026-05-15T09:00:00+00:00',
    targetDate: '2026-06-25',
    participants: ['Robin Sample', 'Kim Example'],
    isOverdue: false,
    ...overrides,
  }
}

describe('GoalCard', () => {
  it('shows the title, description and status', async () => {
    const wrapper = await mountSuspended(GoalCard, { props: { goal: goal(), today } })

    expect(wrapper.text()).toContain('Run a 10k together')
    expect(wrapper.text()).toContain('Twice a week, building up slowly.')
    expect(wrapper.text()).toContain('Active')
  })

  it('links to the goal detail page', async () => {
    const wrapper = await mountSuspended(GoalCard, { props: { goal: goal(), today } })

    expect(wrapper.find('a').attributes('href'))
      .toBe('/goals/019faece-5a81-7c67-8fa2-00a63d9e9127')
  })

  it('renders progress as an accessible progress bar', async () => {
    const wrapper = await mountSuspended(GoalCard, { props: { goal: goal({ progressPercent: 62 }), today } })

    expect(wrapper.text()).toContain('62%')

    const progressbar = wrapper.find('[role="progressbar"]')
    expect(progressbar.exists()).toBe(true)
    expect(progressbar.attributes('aria-valuenow')).toBe('62')
    expect(progressbar.attributes('aria-valuemin')).toBe('0')
    expect(progressbar.attributes('aria-valuemax')).toBe('100')
  })

  it('summarises participants', async () => {
    const wrapper = await mountSuspended(GoalCard, {
      props: {
        goal: goal({ participants: ['Robin Sample', 'Kim Example', 'Alex Placeholder'] }),
        today,
      },
    })

    expect(wrapper.text()).toContain('Robin Sample, Kim Example and 1 other')
  })

  it('counts down to the target date', async () => {
    const wrapper = await mountSuspended(GoalCard, {
      props: { goal: goal({ targetDate: '2026-06-25', isOverdue: false }), today },
    })

    expect(wrapper.text()).toContain('Due in 10 days')
  })

  it('marks an overdue goal without relying on colour alone', async () => {
    const wrapper = await mountSuspended(GoalCard, {
      props: { goal: goal({ targetDate: '2026-06-08', isOverdue: true }), today },
    })

    expect(wrapper.text()).toContain('Overdue')
    expect(wrapper.text()).toContain('Overdue by 7 days')
  })

  it('omits the date row entirely when there is no target date', async () => {
    const wrapper = await mountSuspended(GoalCard, {
      props: { goal: goal({ targetDate: null }), today },
    })

    expect(wrapper.text()).not.toContain('Due')
    expect(wrapper.text()).not.toContain('Overdue')
  })

  it('omits the participants row for a solo goal', async () => {
    const wrapper = await mountSuspended(GoalCard, {
      props: { goal: goal({ participants: [] }), today },
    })

    expect(wrapper.find('dl').text()).not.toContain('Robin')
  })

  it('renders a completed goal at full progress', async () => {
    const wrapper = await mountSuspended(GoalCard, {
      props: { goal: goal({ status: 'Completed', progressPercent: 100 }), today },
    })

    expect(wrapper.text()).toContain('Completed')
    expect(wrapper.text()).toContain('100%')
  })

  it('renders a zero-progress goal without breaking the bar', async () => {
    const wrapper = await mountSuspended(GoalCard, {
      props: { goal: goal({ progressPercent: 0 }), today },
    })

    expect(wrapper.text()).toContain('0%')
    expect(wrapper.find('[role="progressbar"]').attributes('aria-valuenow')).toBe('0')
  })

  it('keeps the heading level below the list heading', async () => {
    const wrapper = await mountSuspended(GoalCard, { props: { goal: goal(), today } })

    // The page uses h1/h2; a card must not jump back to h2 or the heading
    // outline breaks for screen-reader navigation.
    expect(wrapper.find('h3').exists()).toBe(true)
    expect(wrapper.find('h2').exists()).toBe(false)
  })
})
