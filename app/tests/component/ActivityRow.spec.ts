import { mountSuspended } from '@nuxt/test-utils/runtime'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { hapticTap } from '~/utils/haptics'
import ActivityRow from '~/components/social/ActivityRow.vue'
import type { Activity } from '~/api/types'

vi.mock('~/utils/haptics', () => ({ hapticTap: vi.fn() }))
afterEach(() => vi.mocked(hapticTap).mockClear())

/**
 * One line of the feed. The clock is injected, so "vor 12 Min" is the same
 * assertion on every run.
 */
const now = Date.parse('2026-06-17T09:00:00Z')

function activity(overrides: Partial<Activity> = {}): Activity {
  return {
    id: '019faece-5a81-7c67-8fa2-00a63d9e9400',
    actor: {
      id: '019faece-5a81-7c67-8fa2-00a63d9e9200',
      displayName: 'Jonas Weber',
      handle: '@jonas.w',
      initials: 'JW',
      avatarColor: '#4f46e5',
      avatarImageId: null,
      isOnline: true,
    },
    kind: 'TaskCompleted',
    subject: 'Jeden Morgen joggen',
    amount: null,
    kudosCount: 8,
    hasMyKudos: false,
    occurredAt: '2026-06-17T08:48:00+00:00',
    goalId: null,
    ...overrides,
  }
}

describe('ActivityRow', () => {
  it('reads as a sentence about a person', async () => {
    const wrapper = await mountSuspended(ActivityRow, { props: { activity: activity(), now } })

    expect(wrapper.text()).toContain('Jonas Weber')
    expect(wrapper.text()).toContain('hat bei „Jeden Morgen joggen“ geliefert')
    expect(wrapper.text()).toContain('vor 12 Min')
  })

  it('composes a streak entry from the amount', async () => {
    const wrapper = await mountSuspended(ActivityRow, {
      props: { activity: activity({ kind: 'StreakReached', subject: null, amount: 7 }), now },
    })

    expect(wrapper.text()).toContain('7-Tage-Streak')
  })

  it('offers kudos as a labelled toggle rather than an unnamed icon', async () => {
    const wrapper = await mountSuspended(ActivityRow, { props: { activity: activity(), now } })
    const button = wrapper.find('[data-testid="kudos-button"]')

    expect(button.attributes('aria-pressed')).toBe('false')
    expect(button.attributes('aria-label')).toBe('Kudos geben: Jonas Weber hat bei „Jeden Morgen joggen“ geliefert')
    expect(button.text()).toContain('8')
  })

  /**
   * Five buttons on /activity used to be five times "Kudos geben" (#103):
   * each one is named after its own line.
   */
  it('names every kudos button after its own line', async () => {
    const streak = await mountSuspended(ActivityRow, {
      props: { activity: activity({ kind: 'StreakReached', subject: null, amount: 7, actor: { ...activity().actor, displayName: 'Lena Schulz' } }), now },
    })

    expect(streak.get('[data-testid="kudos-button"]').attributes('aria-label'))
      .toBe('Kudos geben: Lena Schulz hat einen 7-Tage-Streak erreicht')
  })

  it('opens the goal from the line and the person from the avatar, when the reader is on the goal', async () => {
    const wrapper = await mountSuspended(ActivityRow, {
      props: { activity: activity({ goalId: 'goal-1' }), now },
    })

    expect(wrapper.get('[data-testid="activity-link"]').attributes('href')).toBe('/goals/goal-1')

    // Two different places, so the avatar is a target of its own, with a name.
    const person = wrapper.get('[data-testid="activity-person"]')
    expect(person.attributes('href')).toBe('/people/019faece-5a81-7c67-8fa2-00a63d9e9200')
    expect(person.attributes('aria-label')).toBe('Profil von Jonas Weber öffnen')
    expect(person.attributes('tabindex')).toBeUndefined()

    // The kudos button is not inside either link.
    expect(wrapper.find('[data-testid="activity-link"] [data-testid="kudos-button"]').exists()).toBe(false)
  })

  it('opens the person from the line when the goal is not one the reader may open', async () => {
    const wrapper = await mountSuspended(ActivityRow, { props: { activity: activity(), now } })

    expect(wrapper.get('[data-testid="activity-link"]').attributes('href')).toBe('/people/019faece-5a81-7c67-8fa2-00a63d9e9200')

    // The avatar goes to the same place: one target for a screen reader, not two.
    const person = wrapper.get('[data-testid="activity-person"]')
    expect(person.attributes('aria-hidden')).toBe('true')
    expect(person.attributes('tabindex')).toBe('-1')
  })

  it('says so when you have already given kudos', async () => {
    const wrapper = await mountSuspended(ActivityRow, {
      props: { activity: activity({ hasMyKudos: true }), now },
    })
    const button = wrapper.find('[data-testid="kudos-button"]')

    expect(button.attributes('aria-pressed')).toBe('true')
    expect(button.attributes('aria-label')).toBe('Kudos zurücknehmen: Jonas Weber hat bei „Jeden Morgen joggen“ geliefert')
  })

  it('emits the id rather than acting itself', async () => {
    const wrapper = await mountSuspended(ActivityRow, { props: { activity: activity(), now } })

    await wrapper.find('[data-testid="kudos-button"]').trigger('click')

    expect(wrapper.emitted('kudos')).toEqual([['019faece-5a81-7c67-8fa2-00a63d9e9400']])
    expect(hapticTap).toHaveBeenCalledOnce()
  })

  it('shows your own history without a button to cheer yourself on', async () => {
    const wrapper = await mountSuspended(ActivityRow, {
      props: { activity: activity(), now, readonly: true },
    })

    expect(wrapper.find('[data-testid="kudos-button"]').exists()).toBe(false)
    expect(wrapper.text()).toContain('8')

    // Never a link to yourself; a line about one of your goals still opens it.
    expect(wrapper.find('[data-testid="activity-person"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="activity-link"]').exists()).toBe(false)

    const goal = await mountSuspended(ActivityRow, {
      props: { activity: activity({ goalId: 'goal-1' }), now, readonly: true },
    })
    expect(goal.get('[data-testid="activity-link"]').attributes('href')).toBe('/goals/goal-1')
  })
})
