import { mountSuspended } from '@nuxt/test-utils/runtime'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import ProofSwipeStack from '~/components/proofs/ProofSwipeStack.vue'
import type { FeedProof } from '~/api/types'

/**
 * The stack on the start screen and the vote screen.
 *
 * What matters is which verdict a gesture turns into, and that a gesture that
 * was not meant as one — a scroll, a short wobble — turns into nothing.
 */
function card(id: string): FeedProof {
  return {
    goalTitle: 'Jeden Tag lesen',
    goalIcon: 'book-open',
    proof: {
      id,
      goalId: 'goal-1',
      goalInstanceId: 'window-1',
      uploader: {
        id: 'person-1',
        displayName: 'Jonas Weber',
        handle: '@jonas.w',
        initials: 'JW',
        avatarColor: '#4f46e5',
        isOnline: true,
        avatarImageId: null,
      },
      imageId: 'image-1',
      status: 'Voting',
      attempt: 1,
      attemptsLeft: 1,
      capturedInApp: true,
      createdAt: '2026-06-15T09:00:00+00:00',
      expiresAt: '2026-06-15T21:00:00+00:00',
      votes: { confirmCount: 0, doubtCount: 0, confirmedBy: [], myVote: null, canIVote: true },
      reactions: [],
    },
  }
}

async function drag(target: ReturnType<Awaited<ReturnType<typeof mountSuspended>>['get']>, dx: number, dy = 0) {
  await target.trigger('pointerdown', { pointerId: 1, clientX: 200, clientY: 300, button: 0, pointerType: 'touch' })
  await target.trigger('pointermove', { pointerId: 1, clientX: 200 + dx / 2, clientY: 300 + dy / 2, pointerType: 'touch' })
  await target.trigger('pointermove', { pointerId: 1, clientX: 200 + dx, clientY: 300 + dy, pointerType: 'touch' })
  await target.trigger('pointerup', { pointerId: 1, clientX: 200 + dx, clientY: 300 + dy, pointerType: 'touch' })
}

describe('ProofSwipeStack', () => {
  beforeEach(() => {
    vi.useFakeTimers()
    // Not implemented by the DOM the tests run in; the browser has it.
    HTMLElement.prototype.setPointerCapture ??= () => {}
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('shows the top card and what it is for', async () => {
    const wrapper = await mountSuspended(ProofSwipeStack, { props: { cards: [card('a'), card('b')] } })

    expect(wrapper.findAll('[data-testid="proof-card"]')).toHaveLength(1)
    expect(wrapper.text()).toContain('Jeden Tag lesen')
    expect(wrapper.text()).toContain('Rechts wischen bestätigt, links zweifelt an.')
  })

  it('believes a photograph swiped to the right', async () => {
    const wrapper = await mountSuspended(ProofSwipeStack, { props: { cards: [card('a')] } })

    await drag(wrapper.get('[data-testid="proof-swipe-card"]'), 160)
    vi.runAllTimers()

    expect(wrapper.emitted('vote')?.[0]).toEqual(['a', 'Confirm'])
  })

  it('doubts a photograph swiped to the left', async () => {
    const wrapper = await mountSuspended(ProofSwipeStack, { props: { cards: [card('a')] } })

    await drag(wrapper.get('[data-testid="proof-swipe-card"]'), -160)
    vi.runAllTimers()

    expect(wrapper.emitted('vote')?.[0]).toEqual(['a', 'Doubt'])
  })

  it('decides nothing on a short or a vertical drag', async () => {
    const wrapper = await mountSuspended(ProofSwipeStack, { props: { cards: [card('a')] } })
    const target = wrapper.get('[data-testid="proof-swipe-card"]')

    await drag(target, 30)
    await drag(target, 20, 200)
    vi.runAllTimers()

    expect(wrapper.emitted('vote')).toBeUndefined()
  })

  it('sends a pressed button off the same way a swipe would', async () => {
    const wrapper = await mountSuspended(ProofSwipeStack, { props: { cards: [card('a')] } })

    await wrapper.get('[data-testid="proof-confirm"]').trigger('click')
    expect(wrapper.emitted('vote')).toBeUndefined()

    vi.runAllTimers()
    expect(wrapper.emitted('vote')?.[0]).toEqual(['a', 'Confirm'])
  })
})
