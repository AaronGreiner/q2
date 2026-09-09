import { mountSuspended } from '@nuxt/test-utils/runtime'
import { describe, expect, it } from 'vitest'
import ActivityRow from '~/components/social/ActivityRow.vue'
import type { Activity } from '~/api/types'

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
    subject: 'Joggen 5 km',
    amount: null,
    kudosCount: 8,
    hasMyKudos: false,
    occurredAt: '2026-06-17T08:48:00+00:00',
    ...overrides,
  }
}

describe('ActivityRow', () => {
  it('reads as a sentence about a person', async () => {
    const wrapper = await mountSuspended(ActivityRow, { props: { activity: activity(), now } })

    expect(wrapper.text()).toContain('Jonas Weber')
    expect(wrapper.text()).toContain('hat „Joggen 5 km“ abgeschlossen')
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
    expect(button.attributes('aria-label')).toBe('Kudos geben')
    expect(button.text()).toContain('8')
  })

  it('says so when you have already given kudos', async () => {
    const wrapper = await mountSuspended(ActivityRow, {
      props: { activity: activity({ hasMyKudos: true }), now },
    })
    const button = wrapper.find('[data-testid="kudos-button"]')

    expect(button.attributes('aria-pressed')).toBe('true')
    expect(button.attributes('aria-label')).toBe('Kudos zurücknehmen')
  })

  it('emits the id rather than acting itself', async () => {
    const wrapper = await mountSuspended(ActivityRow, { props: { activity: activity(), now } })

    await wrapper.find('[data-testid="kudos-button"]').trigger('click')

    expect(wrapper.emitted('kudos')).toEqual([['019faece-5a81-7c67-8fa2-00a63d9e9400']])
  })

  it('shows your own history without a button to cheer yourself on', async () => {
    const wrapper = await mountSuspended(ActivityRow, {
      props: { activity: activity(), now, readonly: true },
    })

    expect(wrapper.find('[data-testid="kudos-button"]').exists()).toBe(false)
    expect(wrapper.text()).toContain('8')
  })
})
