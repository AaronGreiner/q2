import { mountSuspended } from '@nuxt/test-utils/runtime'
import { describe, expect, it } from 'vitest'
import BadgeGrid from '~/components/profile/BadgeGrid.vue'
import type { Badge, BadgeKey } from '~/api/types'

/**
 * The badges on the profile (#22): every one, earned or not, and what each
 * takes — in words, not only in how faded it is.
 */
const keys: BadgeKey[] = ['StreakHero', 'EarlyBird', 'Bookworm', 'KudosGiver', 'Marathon', 'WeeklyWinner']

function badges(earned: BadgeKey[]): Badge[] {
  return keys.map(key => ({
    key,
    isEarned: earned.includes(key),
    earnedOn: earned.includes(key) ? '2026-10-19' : null,
  }))
}

describe('BadgeGrid', () => {
  it('shows every badge with its criterion, and counts the earned ones', async () => {
    const wrapper = await mountSuspended(BadgeGrid, { props: { badges: badges(['StreakHero', 'KudosGiver']) } })

    expect(wrapper.findAll('li')).toHaveLength(6)
    expect(wrapper.get('[data-testid="badge-count"]').text()).toBe('2 von 6')
    expect(wrapper.get('[data-testid="badge-StreakHero"]').text()).toContain('Streak-Held')
    expect(wrapper.get('[data-testid="badge-StreakHero"]').text()).toContain('14 Tage Streak')
    expect(wrapper.get('[data-testid="badge-Bookworm"]').text()).toContain('10 Fenster beim Lesen')
  })

  it('says in words which ones are not earned yet', async () => {
    const wrapper = await mountSuspended(BadgeGrid, { props: { badges: badges(['StreakHero']) } })

    expect(wrapper.get('[data-testid="badge-StreakHero"]').text()).not.toContain('Noch nicht verdient')
    expect(wrapper.get('[data-testid="badge-Marathon"]').text()).toContain('Noch nicht verdient')
    expect(wrapper.get('[data-testid="badge-Marathon"]').attributes('data-earned')).toBe('false')
  })

  it('keeps the collection out of a session replay, as personal state', async () => {
    const wrapper = await mountSuspended(BadgeGrid, { props: { badges: badges([]) } })

    expect(wrapper.get('[data-testid="badge-grid"]').attributes()).toHaveProperty('data-q2-block')
    expect(wrapper.get('h2').text()).toBe('Abzeichen')
  })
})
