import { mountSuspended } from '@nuxt/test-utils/runtime'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { hapticTap } from '~/utils/haptics'
import ProofCard from '~/components/proofs/ProofCard.vue'
import { usePhotoViewer } from '~/composables/usePhotoViewer'
import type { Person, Proof } from '~/api/types'

vi.mock('~/utils/haptics', () => ({ hapticTap: vi.fn() }))
afterEach(() => vi.mocked(hapticTap).mockClear())

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
    media: 'Photo',
    posterImageId: null,
    insetImageId: null,
    insetMedia: null,
    insetPosterImageId: null,
    status: 'Voting',
    attempt: 1,
    attemptsLeft: 1,
    source: 'Live',
    createdAt: '2026-06-15T09:00:00+00:00',
    expiresAt: '2026-06-15T21:00:00+00:00',
    votes: {
      confirmCount: 0,
      doubtCount: 0,
      confirmedBy: [],
      myVote: null,
      canIVote: true,
      doubtCanRefuse: true,
      myDoubtReason: null,
      doubtReasons: [],
    },
    reactions: [],
    ...overrides,
  }
}

describe('ProofCard', () => {
  /**
   * The only checker on a goal cannot refuse a photograph alone, and is told
   * so where they would try (#97). Whether doubt can refuse is the server's
   * answer, not a count this card does.
   */
  it('tells a lone checker that their doubt alone does not refuse the photograph', async () => {
    const alone = await mountSuspended(ProofCard, {
      props: { proof: proof({ votes: { ...proof().votes, doubtCanRefuse: false, myDoubtReason: null, doubtReasons: [] } }) },
    })

    expect(alone.get('[data-testid="proof-doubt-alone"]').text())
      .toBe('Du prüfst allein: Dein Zweifel lehnt das Foto nicht ab. Dafür braucht es zwei, die zweifeln.')
    expect(alone.find('[data-testid="proof-doubt"]').exists()).toBe(true)

    const together = await mountSuspended(ProofCard, { props: { proof: proof() } })

    expect(together.find('[data-testid="proof-doubt-alone"]').exists()).toBe(false)
  })

  it('shows whose photograph it is and what they promised', async () => {
    const wrapper = await mountSuspended(ProofCard, {
      props: { proof: proof(), goalTitle: 'Dreimal die Woche laufen' },
    })

    expect(wrapper.text()).toContain('Jonas Weber')
    expect(wrapper.text()).toContain('Dreimal die Woche laufen')
  })

  it('says what counts under what was promised, and keeps it out of Session Replay (#95)', async () => {
    const wrapper = await mountSuspended(ProofCard, {
      props: { proof: proof(), goalTitle: 'Laufen', goalDescription: 'Mindestens 5 km, ohne Pause.' },
    })

    const description = wrapper.get('[data-testid="proof-goal-description"]')
    expect(description.text()).toBe('Mindestens 5 km, ohne Pause.')
    expect(description.attributes()).toHaveProperty('data-q2-private')
  })

  it('says nothing more where nothing was said, or where the thread names the goal', async () => {
    const undescribed = await mountSuspended(ProofCard, { props: { proof: proof(), goalTitle: 'Laufen' } })
    expect(undescribed.find('[data-testid="proof-goal-description"]').exists()).toBe(false)

    // In a goal's conversation the banner above carries it.
    const inThread = await mountSuspended(ProofCard, { props: { proof: proof(), goalDescription: 'Mindestens 5 km' } })
    expect(inThread.find('[data-testid="proof-goal-description"]').exists()).toBe(false)
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
   * refusal (#61).
   */
  it('says whether the camera took it or it is a screenshot', async () => {
    const live = await mountSuspended(ProofCard, { props: { proof: proof() } })
    expect(live.get('[data-testid="proof-source"]').text()).toBe('Live')
    expect(live.get('[data-testid="proof-image"]').classes()).toContain('object-cover')

    const screenshot = await mountSuspended(ProofCard, {
      props: { proof: proof({ source: 'Screenshot' }) },
    })
    expect(screenshot.get('[data-testid="proof-source"]').text()).toBe('Screenshot')

    // Tall and full of text: fitted into the square rather than cropped.
    expect(screenshot.get('[data-testid="proof-image"]').classes()).toContain('object-contain')

    const unknown = await mountSuspended(ProofCard, {
      props: { proof: proof({ source: 'Unknown' }) },
    })
    expect(unknown.get('[data-testid="proof-source"]').text()).toBe('Ohne Angabe')
  })

  it('takes its source into the full-screen view', async () => {
    const wrapper = await mountSuspended(ProofCard, {
      props: { proof: proof({ source: 'Screenshot' }), goalTitle: 'Bildschirmzeit' },
    })

    await wrapper.get('[data-testid="proof-enlarge"]').trigger('click')

    expect(usePhotoViewer().current.value?.source).toBe('Screenshot')
    usePhotoViewer().close()
  })

  it('offers confirm before doubt, and says doubting is anonymous', async () => {
    const wrapper = await mountSuspended(ProofCard, { props: { proof: proof() } })
    const html = wrapper.html()

    // Believing a friend is the ordinary answer; doubting has to be the
    // deliberate second choice rather than a symmetrical one.
    expect(html.indexOf('proof-confirm')).toBeLessThan(html.indexOf('proof-doubt'))
    expect(wrapper.text()).toContain('Wer zweifelt, bleibt anonym.')
  })

  it('acknowledges and emits the Confirm verdict', async () => {
    const wrapper = await mountSuspended(ProofCard, { props: { proof: proof() } })

    expect(hapticTap).not.toHaveBeenCalled()
    await wrapper.get('[data-testid="proof-confirm"]').trigger('click')

    expect(wrapper.emitted('vote')?.[0]).toEqual(['Confirm', null])
    expect(hapticTap).toHaveBeenCalledOnce()
  })

  /**
   * "Anzweifeln" asks what was missing first (#111): one of three fixed
   * answers, or none. Anonymous, and the card says so.
   */
  it('asks what was missing before it doubts, and doubts with or without a reason', async () => {
    const wrapper = await mountSuspended(ProofCard, { props: { proof: proof() } })

    await wrapper.get('[data-testid="proof-doubt"]').trigger('click')
    expect(wrapper.emitted('vote')).toBeUndefined()
    expect(wrapper.get('[data-testid="proof-doubt-reasons"]').text()).toContain('Was fehlt? Anonym und freiwillig.')

    await wrapper.get('[data-testid="proof-doubt-OldPicture"]').trigger('click')
    expect(wrapper.emitted('vote')?.[0]).toEqual(['Doubt', 'OldPicture'])
    expect(hapticTap).toHaveBeenCalledOnce()

    const plain = await mountSuspended(ProofCard, { props: { proof: proof() } })
    await plain.get('[data-testid="proof-doubt"]').trigger('click')
    await plain.get('[data-testid="proof-doubt-none"]').trigger('click')
    expect(plain.emitted('vote')?.[0]).toEqual(['Doubt', null])
  })

  it('lets a doubt already cast say, or change, what was missing', async () => {
    const wrapper = await mountSuspended(ProofCard, {
      props: {
        proof: proof({
          votes: { confirmCount: 0, doubtCount: 1, confirmedBy: [], myVote: 'Doubt', canIVote: true, doubtCanRefuse: true, myDoubtReason: 'Unrecognisable', doubtReasons: [] },
        }),
      },
    })

    expect(wrapper.get('[data-testid="proof-reason-Unrecognisable"]').attributes('aria-pressed')).toBe('true')
    await wrapper.get('[data-testid="proof-reason-NotWhatWasPromised"]').trigger('click')
    expect(wrapper.emitted('vote')?.[0]).toEqual(['Doubt', 'NotWhatWasPromised'])
  })

  it('shows the owner the reasons counted, and names nobody', async () => {
    const wrapper = await mountSuspended(ProofCard, {
      props: {
        proof: proof({
          status: 'Rejected',
          votes: {
            confirmCount: 0,
            doubtCount: 3,
            confirmedBy: [],
            myVote: null,
            canIVote: false,
            doubtCanRefuse: true,
            myDoubtReason: null,
            doubtReasons: [{ reason: 'OldPicture', count: 2 }, { reason: 'Unrecognisable', count: 1 }],
          },
        }),
      },
    })

    expect(wrapper.get('[data-testid="proof-reasons"]').text())
      .toBe('Was gefehlt hat: Altes Bild oder Screenshot (2) · Nicht erkennbar (1)')
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
            doubtCanRefuse: true,
            myDoubtReason: null,
            doubtReasons: [],
          },
        }),
      },
    })

    const tally = wrapper.get('[data-testid="proof-tally"]').text()

    expect(tally).toContain('Lena Schulz')
    expect(tally).toContain('2 Zweifel')
    expect(wrapper.get('[data-testid="proof-my-vote"]').text()).toBe('Du hast bestätigt.')
  })

  /**
   * A vote may be changed while the photograph is undecided. The card says
   * what you chose and offers the other answer — one quiet button rather than
   * the two big ones again.
   */
  it('lets a vote already cast be changed while the vote runs', async () => {
    const wrapper = await mountSuspended(ProofCard, {
      props: {
        proof: proof({
          votes: { confirmCount: 1, doubtCount: 0, confirmedBy: [], myVote: 'Confirm', canIVote: true, doubtCanRefuse: true, myDoubtReason: null, doubtReasons: [] },
        }),
      },
    })

    expect(wrapper.find('[data-testid="proof-confirm"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="proof-my-vote"]').text()).toBe('Du hast bestätigt.')

    const change = wrapper.get('[data-testid="proof-change"]')
    expect(change.text()).toContain('Doch anzweifeln')

    await change.trigger('click')

    expect(wrapper.emitted('vote')?.[0]).toEqual(['Doubt', null])
  })

  it('offers no change once the vote has closed', async () => {
    const wrapper = await mountSuspended(ProofCard, {
      props: {
        proof: proof({
          status: 'Confirmed',
          votes: { confirmCount: 1, doubtCount: 0, confirmedBy: [], myVote: 'Confirm', canIVote: false, doubtCanRefuse: true, myDoubtReason: null, doubtReasons: [] },
        }),
      },
    })

    expect(wrapper.find('[data-testid="proof-change"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="proof-status"]').text()).toBe('Bestätigt')
  })

  it('opens the photograph full screen with what it proves', async () => {
    const wrapper = await mountSuspended(ProofCard, {
      props: { proof: proof(), context: 'Jeden Tag lesen' },
    })

    await wrapper.get('[data-testid="proof-enlarge"]').trigger('click')

    const opened = usePhotoViewer().current.value
    expect(opened?.imageId).toBe('image-1')
    expect(opened?.title).toBe('Jeden Tag lesen')
    expect(opened?.subtitle).toBe('Jonas Weber')

    usePhotoViewer().close()
  })

  it('offers no verdict on your own photograph', async () => {
    const wrapper = await mountSuspended(ProofCard, {
      props: {
        proof: proof({ votes: { confirmCount: 0, doubtCount: 0, confirmedBy: [], myVote: null, canIVote: false, doubtCanRefuse: true, myDoubtReason: null, doubtReasons: [] } }),
      },
    })

    expect(wrapper.find('[data-testid="proof-confirm"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="proof-status"]').text()).toContain('Dein eigener Beweis')
  })
})
