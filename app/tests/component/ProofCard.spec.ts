import { mountSuspended } from '@nuxt/test-utils/runtime'
import { describe, expect, it } from 'vitest'
import ProofCard from '~/components/proofs/ProofCard.vue'
import type { Person, Proof } from '~/api/types'

/**
 * The card the product turns on.
 *
 * What is worth pinning down here is not the layout but the three things that
 * would quietly break the check if they drifted: that a photograph is blocked
 * from Session Replay, that confirm is offered before doubt, and that the card
 * says where the picture came from.
 */
function person(overrides: Partial<Person> = {}): Person {
  return {
    id: '019faece-5a81-7c67-8fa2-00a63d9e9200',
    displayName: 'Jonas Weber',
    handle: '@jonas.w',
    initials: 'JW',
    avatarColor: '#4f46e5',
    isOnline: true,
    avatarImageId: null,
    ...overrides,
  }
}

function proof(overrides: Partial<Proof> = {}): Proof {
  return {
    id: 'proof-1',
    goalId: 'goal-1',
    goalInstanceId: 'window-1',
    uploader: person(),
    imageId: 'image-1',
    status: 'Voting',
    attempt: 1,
    attemptsLeft: 1,
    capturedInApp: true,
    createdAt: '2026-06-15T09:00:00+00:00',
    expiresAt: '2026-06-15T21:00:00+00:00',
    votes: {
      confirmCount: 0,
      doubtCount: 0,
      confirmedBy: [],
      myVote: null,
      canIVote: true,
    },
    reactions: [],
    ...overrides,
  }
}

describe('ProofCard', () => {
  it('shows whose photograph it is and what they promised', async () => {
    const wrapper = await mountSuspended(ProofCard, {
      props: { proof: proof(), goalTitle: 'Dreimal die Woche laufen' },
    })

    expect(wrapper.text()).toContain('Jonas Weber')
    expect(wrapper.text()).toContain('Dreimal die Woche laufen')
  })

  it('keeps somebody else\'s photograph out of Session Replay', async () => {
    const wrapper = await mountSuspended(ProofCard, { props: { proof: proof() } })

    expect(wrapper.get('[data-testid="proof-card"]').attributes()).toHaveProperty('data-q2-block')
  })

  it('sends the session with the picture, or the feed is a wall of broken images', async () => {
    const wrapper = await mountSuspended(ProofCard, { props: { proof: proof() } })

    expect(wrapper.get('[data-testid="proof-image"]').attributes('crossorigin')).toBe('use-credentials')
  })

  /**
   * A picture chosen from a gallery can be any age. The person voting is
   * entitled to know which they are looking at — it is a label, never a
   * refusal.
   */
  it('says whether the camera took it', async () => {
    const live = await mountSuspended(ProofCard, { props: { proof: proof() } })
    expect(live.get('[data-testid="proof-provenance"]').text()).toBe('Live aufgenommen')

    const picked = await mountSuspended(ProofCard, {
      props: { proof: proof({ capturedInApp: false }) },
    })
    expect(picked.get('[data-testid="proof-provenance"]').text()).toBe('Aus der Galerie')
  })

  it('offers confirm before doubt, and says doubting is anonymous', async () => {
    const wrapper = await mountSuspended(ProofCard, { props: { proof: proof() } })
    const html = wrapper.html()

    // Believing a friend is the ordinary answer; doubting has to be the
    // deliberate second choice rather than a symmetrical one.
    expect(html.indexOf('proof-confirm')).toBeLessThan(html.indexOf('proof-doubt'))
    expect(wrapper.text()).toContain('Wer zweifelt, bleibt anonym.')
  })

  it('emits the verdict that was pressed', async () => {
    const wrapper = await mountSuspended(ProofCard, { props: { proof: proof() } })

    await wrapper.get('[data-testid="proof-doubt"] button, [data-testid="proof-doubt"]').trigger('click')

    expect(wrapper.emitted('vote')?.[0]).toEqual(['Doubt'])
  })

  /**
   * The rule the whole check depends on. The API never sends a doubter's name,
   * and the card must not invent a way to imply one either.
   */
  it('counts doubts without naming anybody', async () => {
    const wrapper = await mountSuspended(ProofCard, {
      props: {
        proof: proof({
          votes: {
            confirmCount: 1,
            doubtCount: 2,
            confirmedBy: [person({ id: 'p2', displayName: 'Lena Schulz' })],
            myVote: 'Confirm',
            canIVote: false,
          },
        }),
      },
    })

    const tally = wrapper.get('[data-testid="proof-tally"]').text()

    expect(tally).toContain('Lena Schulz')
    expect(tally).toContain('2 Zweifel')
    expect(wrapper.get('[data-testid="proof-my-vote"]').text()).toBe('Du hast bestätigt.')
  })

  it('offers no verdict on your own photograph', async () => {
    const wrapper = await mountSuspended(ProofCard, {
      props: {
        proof: proof({ votes: { confirmCount: 0, doubtCount: 0, confirmedBy: [], myVote: null, canIVote: false } }),
      },
    })

    expect(wrapper.find('[data-testid="proof-confirm"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="proof-status"]').text()).toContain('Dein eigener Beweis')
  })
})
