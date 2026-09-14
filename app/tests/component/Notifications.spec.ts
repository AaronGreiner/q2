import { mountSuspended } from '@nuxt/test-utils/runtime'
import { describe, expect, it } from 'vitest'
import NotificationBell from '~/components/notifications/NotificationBell.vue'
import NotificationRow from '~/components/notifications/NotificationRow.vue'
import type { NotificationLine } from '~/api/types'

/**
 * The bell and its lines. The clock is injected, so "vor 15 Min" is the same
 * assertion on every run.
 */
const now = Date.parse('2026-06-17T09:00:00Z')

function line(overrides: Partial<NotificationLine> = {}): NotificationLine {
  return {
    id: '019faece-5a81-7c67-8fa2-00a63d9e9500',
    kind: 'ReactionReceived',
    actor: {
      id: '019faece-5a81-7c67-8fa2-00a63d9e9200',
      displayName: 'Lena Fischer',
      handle: '@lena.f',
      initials: 'LF',
      avatarColor: '#4f46e5',
      avatarImageId: null,
      isOnline: false,
    },
    subject: 'Jeden Tag laufen',
    excerpt: null,
    amount: null,
    target: 'Goal',
    targetId: '019faece-5a81-7c67-8fa2-00a63d9e9300',
    occurredAt: '2026-06-17T08:45:00+00:00',
    isNew: true,
    ...overrides,
  }
}

describe('NotificationRow', () => {
  it('reads as a sentence about a person, and leads to what it is about', async () => {
    const wrapper = await mountSuspended(NotificationRow, { props: { line: line(), now } })

    expect(wrapper.text()).toContain('Lena Fischer hat auf deinen Beweis für „Jeden Tag laufen“ reagiert.')
    expect(wrapper.text()).toContain('vor 15 Min')
    expect(wrapper.get('[data-testid="notification-row"]').attributes('href'))
      .toBe('/goals/019faece-5a81-7c67-8fa2-00a63d9e9300')
  })

  it('marks what is new with more than colour', async () => {
    const wrapper = await mountSuspended(NotificationRow, { props: { line: line(), now } })

    expect(wrapper.get('[data-testid="notification-new"]').text()).toBe('Neu')
  })

  it('draws nothing new on a line already seen', async () => {
    const wrapper = await mountSuspended(NotificationRow, { props: { line: line({ isNew: false }), now } })

    expect(wrapper.find('[data-testid="notification-new"]').exists()).toBe(false)
  })

  it('pictures nobody for a verdict', async () => {
    const wrapper = await mountSuspended(NotificationRow, {
      props: { line: line({ kind: 'ProofConfirmed', actor: null }), now },
    })

    expect(wrapper.text()).toContain('Dein Beweis für „Jeden Tag laufen“ wurde bestätigt.')
    expect(wrapper.find('b').exists()).toBe(false)
  })

  it('keeps the line out of screenshots and replays, since it quotes a goal', async () => {
    const wrapper = await mountSuspended(NotificationRow, { props: { line: line(), now } })

    expect(wrapper.find('[data-q2-private]').exists()).toBe(true)
  })
})

describe('NotificationBell', () => {
  it('carries no number when nothing is new', async () => {
    const wrapper = await mountSuspended(NotificationBell, { props: { count: 0 } })
    const bell = wrapper.get('[data-testid="open-notifications"]')

    expect(wrapper.find('[data-testid="bell-count"]').exists()).toBe(false)
    expect(bell.attributes('aria-label')).toBe('Mitteilungen öffnen')
    expect(bell.attributes('href')).toBe('/notifications')
  })

  it('says how many are new, to eyes and to screen readers', async () => {
    const wrapper = await mountSuspended(NotificationBell, { props: { count: 3 } })

    expect(wrapper.get('[data-testid="bell-count"]').text()).toBe('3')
    expect(wrapper.get('[data-testid="open-notifications"]').attributes('aria-label'))
      .toBe('Mitteilungen öffnen, 3 neue')
  })

  it('stops counting where the number stops meaning anything', async () => {
    const wrapper = await mountSuspended(NotificationBell, { props: { count: 120 } })

    expect(wrapper.get('[data-testid="bell-count"]').text()).toBe('99+')
  })
})
