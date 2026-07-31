import { mountSuspended } from '@nuxt/test-utils/runtime'
import { describe, expect, it } from 'vitest'
import GoalCard from '~/components/goals/GoalCard.vue'
import type { Goal, Person } from '~/api/types'

/**
 * The reference component test: real Nuxt environment, real Nuxt UI, plain
 * props in, rendered output asserted. Nothing here reads a clock or a store,
 * which is what makes the assertions the same on every run.
 */
function person(overrides: Partial<Person> = {}): Person {
  return {
    id: '019faece-5a81-7c67-8fa2-00a63d9e9200',
    displayName: 'Jonas Weber',
    handle: '@jonas.w',
    initials: 'JW',
    avatarColor: '#4f46e5',
    isOnline: true,
    ...overrides,
  }
}

function goal(overrides: Partial<Goal> = {}): Goal {
  return {
    id: '019faece-5a81-7c67-8fa2-00a63d9e9127',
    title: 'Halbmarathon im Mai',
    description: 'Drei Läufe pro Woche.',
    icon: 'medal',
    rhythm: 'Weekly',
    status: 'Active',
    isGroup: false,
    completedSteps: 14,
    totalSteps: 21,
    progressPercent: 67,
    streak: 12,
    reminderAt: '18:00:00',
    targetDate: '2026-08-25',
    createdAt: '2026-05-15T09:00:00+00:00',
    participants: [],
    isOverdue: false,
    ...overrides,
  }
}

describe('GoalCard', () => {
  it('shows the title, the rhythm and how far along it is', async () => {
    const wrapper = await mountSuspended(GoalCard, { props: { goal: goal() } })

    expect(wrapper.text()).toContain('Halbmarathon im Mai')
    expect(wrapper.text()).toContain('Wöchentlich')
    expect(wrapper.text()).toContain('14 von 21 Schritten')
    expect(wrapper.text()).toContain('67%')
    expect(wrapper.find('h3').attributes()).toHaveProperty('data-q2-private')
  })

  it('links to the goal detail page', async () => {
    const wrapper = await mountSuspended(GoalCard, { props: { goal: goal() } })

    expect(wrapper.find('a').attributes('href'))
      .toBe('/goals/019faece-5a81-7c67-8fa2-00a63d9e9127')
  })

  it('renders progress as an accessible progress bar', async () => {
    const wrapper = await mountSuspended(GoalCard, { props: { goal: goal({ progressPercent: 67 }) } })

    const progressbar = wrapper.find('[role="progressbar"]')

    expect(progressbar.exists()).toBe(true)
    expect(progressbar.attributes('aria-valuenow')).toBe('67')
    expect(progressbar.attributes('aria-valuemin')).toBe('0')
    expect(progressbar.attributes('aria-valuemax')).toBe('100')
    expect(progressbar.attributes()).toHaveProperty('data-q2-block')
  })

  it('shows the streak and the reminder', async () => {
    const wrapper = await mountSuspended(GoalCard, { props: { goal: goal() } })

    expect(wrapper.text()).toContain('12 Tage')
    expect(wrapper.text()).toContain('18:00')
  })

  it('leaves the reminder out when there is none', async () => {
    const wrapper = await mountSuspended(GoalCard, { props: { goal: goal({ reminderAt: null }) } })

    expect(wrapper.text()).not.toContain(':')
  })

  it('marks a group goal as one', async () => {
    const wrapper = await mountSuspended(GoalCard, { props: { goal: goal({ isGroup: true }) } })

    expect(wrapper.text()).toContain('GRUPPE')
  })

  it('says an overdue goal is overdue in words, not only in colour', async () => {
    const wrapper = await mountSuspended(GoalCard, { props: { goal: goal({ isOverdue: true }) } })

    expect(wrapper.find('[data-testid="goal-overdue"]').text()).toBe('Überfällig')
  })

  it('draws one avatar per participant', async () => {
    const wrapper = await mountSuspended(GoalCard, {
      props: {
        goal: goal({
          participants: [
            person(),
            person({ id: 'other', displayName: 'Lena Schulz', initials: 'LS', avatarColor: '#db2777' }),
          ],
        }),
      },
    })

    expect(wrapper.findAll('[data-testid="avatar"]')).toHaveLength(2)
  })

  it('is a heading at the level a list under an h2 needs', async () => {
    const wrapper = await mountSuspended(GoalCard, { props: { goal: goal() } })

    expect(wrapper.find('h3').text()).toBe('Halbmarathon im Mai')
  })
})
