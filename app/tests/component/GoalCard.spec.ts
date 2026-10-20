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
    pendingProofId: null,
    acceptsProof: true,
    limit: null,
    uses: 0,
    ...overrides,
  }
}

function goal(overrides: Partial<Goal> = {}): Goal {
  return {
    id: '019faece-5a81-7c67-8fa2-00a63d9e9127',
    title: 'Halbmarathon im Mai',
    description: 'Drei Läufe pro Woche.',
    category: 'Sport',
    icon: 'dumbbell',
    kind: 'Do',
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
    owner: person({ id: 'me', displayName: 'Mara Klein', handle: '@mara.k', initials: 'MK' }),
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

  it('says how much of the window is left in words rather than in a bar', async () => {
    // A row has no room for a bar, and "Noch 2 von 3" is all the bar said —
    // in the words the row under "Heute" uses for it.
    const wrapper = await mountSuspended(GoalCard, { props: { goal: goal() } })

    expect(wrapper.find('[role="progressbar"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="goal-window"]').text()).toBe('Noch 2 von 3')
  })

  it('says a photograph is being looked at, and when the window is kept', async () => {
    const waiting = await mountSuspended(GoalCard, {
      props: { goal: goal({ current: window({ pendingProofId: 'proof-1', acceptsProof: false }) }) },
    })
    expect(waiting.get('[data-testid="goal-window"]').text()).toBe('Wird geprüft')

    const kept = await mountSuspended(GoalCard, {
      props: { goal: goal({ current: window({ confirmedProofs: 3, remainingProofs: 0, acceptsProof: false }) }) },
    })
    expect(kept.get('[data-testid="goal-window"]').text()).toBe('Geschafft')
  })

  it('says a daily Do delivered today is done, not "Aktiv" (#140)', async () => {
    // The server keeps today's delivered window as the shown one until it ends.
    const delivered = window({
      startsOn: '2026-05-25',
      dueOn: '2026-05-25',
      dueAt: '2026-05-25T21:59:59+00:00',
      requiredProofs: 1,
      confirmedProofs: 1,
      remainingProofs: 0,
      status: 'Done',
      acceptsProof: false,
    })
    const wrapper = await mountSuspended(GoalCard, {
      props: {
        goal: goal({ current: delivered, schedule: { kind: 'Interval', everyDays: 1, weekdays: [], times: null, period: null } }),
        today: '2026-05-25',
      },
    })

    expect(wrapper.get('[data-testid="goal-window"]').text()).toBe('Geschafft')
  })

  it('shows the streak and the reminder', async () => {
    const wrapper = await mountSuspended(GoalCard, { props: { goal: goal() } })

    expect(wrapper.get('[data-testid="goal-streak"]').text()).toBe('12 Fenster')
    expect(wrapper.get('[data-testid="goal-reminder"]').text()).toContain('18:00')
  })

  it('spends no flame on a streak that has not started', async () => {
    // The flame gradient means a streak (docs/adr/0015); "0 Fenster" in it was
    // the colour spent on nothing.
    const wrapper = await mountSuspended(GoalCard, { props: { goal: goal({ streak: 0 }) } })

    expect(wrapper.find('[data-testid="goal-streak"]').exists()).toBe(false)
    expect(wrapper.text()).not.toContain('Fenster')
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

  it('leads your own goal with its icon, not with the people checking it', async () => {
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

    expect(wrapper.find('[data-testid="avatar"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="goal-owner"]').exists()).toBe(false)
  })

  /**
   * A friend's goal sits on the same screen as your own (#77), so the row has
   * to say whose it is — and offer nothing, because only its owner delivers.
   */
  it('leads a friend\'s goal with the friend, and offers nothing to press', async () => {
    const wrapper = await mountSuspended(GoalCard, {
      props: {
        goal: goal({
          isMine: false,
          owner: person(),
          participants: [person({ id: 'me', displayName: 'Mara Klein', initials: 'MK' })],
        }),
      },
    })

    const avatars = wrapper.findAll('[data-testid="avatar"]')
    expect(avatars).toHaveLength(1)
    expect(avatars[0]!.text()).toBe('JW')

    expect(wrapper.get('[data-testid="goal-owner"]').text()).toBe('Jonas Weber')
    expect(wrapper.get('[data-testid="goal-owner"]').attributes()).toHaveProperty('data-q2-private')
    expect(wrapper.find('button').exists()).toBe(false)

    // Their reminder is a note to themselves, not a deadline for you.
    expect(wrapper.find('[data-testid="goal-reminder"]').exists()).toBe(false)
  })

  it('is a heading at the level a list under an h2 needs', async () => {
    const wrapper = await mountSuspended(GoalCard, { props: { goal: goal() } })

    expect(wrapper.find('h3').text()).toBe('Halbmarathon im Mai')
  })

  it('says from when a goal made to start tomorrow counts, rather than a bare status (#96)', async () => {
    const wrapper = await mountSuspended(GoalCard, {
      props: { goal: goal({ current: null, startsOn: '2026-10-20' }), today: '2026-10-19' },
    })

    expect(wrapper.text()).toContain('Ab 20.10.')
    expect(wrapper.text()).not.toContain('Aktiv')
  })
})
