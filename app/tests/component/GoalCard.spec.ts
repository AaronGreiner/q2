import { mountSuspended } from '@nuxt/test-utils/runtime'
import { describe, expect, it } from 'vitest'
import GoalCard from '~/components/goals/GoalCard.vue'
import type { Goal, GoalWindow, Person } from '~/api/types'

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
    avatarImageId: null,
    isOnline: true,
    ...overrides,
  }
}

function window(overrides: Partial<GoalWindow> = {}): GoalWindow {
  return {
    id: '019faece-5a81-7c67-8fa2-00a63d9e9400',
    startsOn: '2026-05-25',
    dueOn: '2026-05-31',
    dueAt: '2026-05-31T21:59:59+00:00',
    requiredProofs: 3,
    confirmedProofs: 1,
    remainingProofs: 2,
    status: 'Open',
    ...overrides,
  }
}

function goal(overrides: Partial<Goal> = {}): Goal {
  return {
    id: '019faece-5a81-7c67-8fa2-00a63d9e9127',
    title: 'Halbmarathon im Mai',
    description: 'Drei Läufe pro Woche.',
    icon: 'medal',
    schedule: { kind: 'Times', everyDays: null, weekdays: [], times: 3, period: 'Week' },
    status: 'Active',
    isGroup: false,
    current: window(),
    streak: 12,
    windowsDone: 14,
    windowsMissed: 2,
    reminderAt: '18:00:00',
    targetDate: null,
    createdAt: '2026-05-15T09:00:00+00:00',
    participants: [],
    isOverdue: false,
    isMine: true,
    closedAt: null,
    pause: null,
    remainingPauses: 2,
    ...overrides,
  }
}

describe('GoalCard', () => {
  it('shows the title, the schedule and what the window still wants', async () => {
    const wrapper = await mountSuspended(GoalCard, { props: { goal: goal() } })

    expect(wrapper.text()).toContain('Halbmarathon im Mai')
    expect(wrapper.text()).toContain('3× pro Woche')
    expect(wrapper.text()).toContain('Noch 2 von 3')
    expect(wrapper.find('h3').attributes()).toHaveProperty('data-q2-private')
  })

  it('says what a finished goal is rather than what it still wants', async () => {
    const wrapper = await mountSuspended(GoalCard, {
      props: { goal: goal({ current: null, status: 'Completed' }) },
    })

    expect(wrapper.get('[data-testid="goal-window"]').text()).toBe('Abgeschlossen')
  })

  it('links to the goal detail page', async () => {
    const wrapper = await mountSuspended(GoalCard, { props: { goal: goal() } })

    expect(wrapper.find('a').attributes('href'))
      .toBe('/goals/019faece-5a81-7c67-8fa2-00a63d9e9127')
  })

  it('renders the window as an accessible progress bar', async () => {
    const wrapper = await mountSuspended(GoalCard, { props: { goal: goal() } })

    const progressbar = wrapper.find('[role="progressbar"]')

    expect(progressbar.exists()).toBe(true)

    // One of three delivered.
    expect(progressbar.attributes('aria-valuenow')).toBe('33')
    expect(progressbar.attributes('aria-valuemin')).toBe('0')
    expect(progressbar.attributes('aria-valuemax')).toBe('100')
    expect(progressbar.attributes()).toHaveProperty('data-q2-block')
  })

  it('draws no bar when the window wants exactly one proof', async () => {
    // "1 von 1" is a bar that is either empty or full, which says nothing the
    // line beside it has not already said.
    const wrapper = await mountSuspended(GoalCard, {
      props: {
        goal: goal({
          schedule: { kind: 'Interval', everyDays: 1, weekdays: [], times: null, period: null },
          current: window({ requiredProofs: 1, confirmedProofs: 0, remainingProofs: 1, startsOn: '2026-05-31' }),
        }),
      },
    })

    expect(wrapper.find('[role="progressbar"]').exists()).toBe(false)
    expect(wrapper.text()).toContain('Heute fällig')
  })

  it('shows the streak and the reminder', async () => {
    const wrapper = await mountSuspended(GoalCard, { props: { goal: goal() } })

    expect(wrapper.text()).toContain('12 Fenster')
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
