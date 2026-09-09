import { mountSuspended } from '@nuxt/test-utils/runtime'
import { describe, expect, it } from 'vitest'
import ChallengeBanner from '~/components/home/ChallengeBanner.vue'
import ChallengeEntryCard from '~/components/challenge/ChallengeEntryCard.vue'
import type { ChallengeEntry, ChallengeRoom, Person } from '~/api/types'

/**
 * The daily challenge on screen.
 *
 * What is worth pinning down is not the layout but the reciprocity rule, which
 * is the whole feature: before contributing you are told who is in and shown
 * nothing of what they did — and there is no picture behind the cover to
 * reveal, because the server never sent one.
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

function entry(overrides: Partial<ChallengeEntry> = {}): ChallengeEntry {
  return {
    id: 'entry-1',
    author: person(),
    imageId: 'image-1',
    capturedInApp: true,
    createdAt: '2026-06-15T09:00:00+00:00',
    isMine: false,
    reactions: [],
    ...overrides,
  }
}

/** What the server sends while the room is still covered. */
function coveredEntry(): ChallengeEntry {
  return entry({ imageId: null, reactions: [] })
}

function room(overrides: Partial<ChallengeRoom> = {}): ChallengeRoom {
  return {
    challenge: {
      id: 'challenge-1',
      prompt: 'Zeig deinen Arbeitsplatz.',
      publishedAt: '2026-06-15T00:00:00+00:00',
      expiresAt: '2026-06-16T00:00:00+00:00',
    },
    ownEntry: null,
    entries: [],
    friendCount: 4,
    isRevealed: false,
    ...overrides,
  }
}

describe('ChallengeEntryCard', () => {
  it('keeps a contribution out of Session Replay', async () => {
    const wrapper = await mountSuspended(ChallengeEntryCard, {
      props: { entry: entry(), covered: false },
    })

    expect(wrapper.get('[data-testid="challenge-entry"]').attributes()).toHaveProperty('data-q2-block')
  })

  /**
   * The name is the incentive to join and costs nobody anything; the motif is
   * the part that is held back.
   */
  it('names who is in even while the picture is covered', async () => {
    const wrapper = await mountSuspended(ChallengeEntryCard, {
      props: { entry: coveredEntry(), covered: true },
    })

    expect(wrapper.text()).toContain('Jonas Weber')
    expect(wrapper.get('[data-testid="challenge-covered"]').text()).toContain('Mach mit, um zu sehen')
  })

  /**
   * Not a blur over a photograph that was sent anyway: there is no image
   * element at all, because there was no id to build one from.
   */
  it('has nothing behind the cover to reveal', async () => {
    const wrapper = await mountSuspended(ChallengeEntryCard, {
      props: { entry: coveredEntry(), covered: true },
    })

    expect(wrapper.find('[data-testid="challenge-image"]').exists()).toBe(false)
    expect(wrapper.html()).not.toContain('/api/images/')
  })

  it('offers no applause on something you cannot see', async () => {
    const wrapper = await mountSuspended(ChallengeEntryCard, {
      props: { entry: coveredEntry(), covered: true },
    })

    expect(wrapper.find('[data-testid="challenge-react-Applause"]').exists()).toBe(false)
  })

  it('shows the picture once the room is open, with the session attached', async () => {
    const wrapper = await mountSuspended(ChallengeEntryCard, {
      props: { entry: entry(), covered: false },
    })

    expect(wrapper.get('[data-testid="challenge-image"]').attributes('crossorigin')).toBe('use-credentials')
  })

  /**
   * The prompt asks for a moment, so a picture out of a gallery misses it by
   * definition. Only the exception is named.
   */
  it('names the origin only when the camera did not take it', async () => {
    const live = await mountSuspended(ChallengeEntryCard, {
      props: { entry: entry(), covered: false },
    })
    expect(live.find('[data-testid="challenge-origin"]').exists()).toBe(false)

    const picked = await mountSuspended(ChallengeEntryCard, {
      props: { entry: entry({ capturedInApp: false }), covered: false },
    })
    expect(picked.get('[data-testid="challenge-origin"]').text()).toBe('Nicht live aufgenommen')
  })

  /**
   * Even on your own: the section heading above the card already says whose it
   * is, so the card saying it again said it twice.
   */
  it('names the author on every card', async () => {
    const wrapper = await mountSuspended(ChallengeEntryCard, {
      props: { entry: entry({ isMine: true }), covered: false },
    })

    expect(wrapper.text()).toContain('Jonas Weber')
    expect(wrapper.text()).not.toContain('Dein Beitrag')
  })

  it('emits the reaction that was pressed', async () => {
    const wrapper = await mountSuspended(ChallengeEntryCard, {
      props: { entry: entry(), covered: false },
    })

    await wrapper.get('[data-testid="challenge-react-Fire"] button, [data-testid="challenge-react-Fire"]')
      .trigger('click')

    expect(wrapper.emitted('react')?.[0]).toEqual(['Fire'])
  })
})

describe('ChallengeBanner', () => {
  it('shows the prompt and how many friends are in', async () => {
    const wrapper = await mountSuspended(ChallengeBanner, {
      props: { room: room({ entries: [entry(), entry({ id: 'entry-2' })] }) },
    })

    expect(wrapper.text()).toContain('Zeig deinen Arbeitsplatz.')
    expect(wrapper.text()).toContain('2 von 4 Freunden dabei')
  })

  /**
   * The accent means "you can do this now" and is rationed to exactly that
   * (docs/adr/0015-qdos-design-language.md). Once you are in, being in is a
   * state, and a state is grey.
   */
  it('spends the accent on the invitation and gives it up once you are in', async () => {
    const inviting = await mountSuspended(ChallengeBanner, { props: { room: room() } })

    expect(inviting.text()).toContain('Mitmachen')
    expect(inviting.html()).toContain('--q2-accent-solid')

    const joined = await mountSuspended(ChallengeBanner, {
      props: { room: room({ ownEntry: entry({ isMine: true }), isRevealed: true }) },
    })

    expect(joined.get('[data-testid="challenge-banner-done"]').text()).toContain('Erledigt')
    expect(joined.html()).not.toContain('--q2-accent-solid')
  })

  it('leads to the room rather than opening a camera of its own', async () => {
    const wrapper = await mountSuspended(ChallengeBanner, { props: { room: room() } })

    expect(wrapper.get('[data-testid="challenge-banner"]').attributes('href')).toBe('/challenge')
  })
})
