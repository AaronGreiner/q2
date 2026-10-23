import { mountSuspended } from '@nuxt/test-utils/runtime'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { hapticTap } from '~/utils/haptics'
import ChallengeCard from '~/components/home/ChallengeCard.vue'
import ChallengeEntryCard from '~/components/challenge/ChallengeEntryCard.vue'
import ChallengeStripTile from '~/components/challenge/ChallengeStripTile.vue'
import { usePhotoViewer } from '~/composables/usePhotoViewer'
import type { ChallengeEntry, ChallengeRoom, Person } from '~/api/types'

vi.mock('~/utils/haptics', () => ({ hapticTap: vi.fn() }))
afterEach(() => vi.mocked(hapticTap).mockClear())

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
    source: 'Live',
    createdAt: '2026-06-15T09:00:00+00:00',
    isMine: false,
    reactions: [],
    isLate: false,
    media: 'Photo',
    posterImageId: null,
    insetImageId: null,
    insetMedia: null,
    insetPosterImageId: null,
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

    expect(wrapper.find('[data-testid="proof-media"]').exists()).toBe(false)
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

    expect(wrapper.get('[data-testid="proof-image"]').attributes('crossorigin')).toBe('use-credentials')
    expect(wrapper.get('[data-testid="proof-image"]').element.closest('[data-q2-block]')).not.toBeNull()
  })

  /**
   * A contribution is drawn like a proof (#152): a clip plays without sound,
   * and the front camera's clip plays in the corner beside it.
   */
  it('plays a clip with the front camera\'s clip in the corner', async () => {
    const wrapper = await mountSuspended(ChallengeEntryCard, {
      props: {
        entry: entry({ media: 'Video', posterImageId: 'still-1', insetImageId: 'front-1', insetMedia: 'Video', insetPosterImageId: 'front-still-1' }),
        covered: false,
      },
    })

    expect(wrapper.get('[data-testid="proof-video"]').attributes('src')).toMatch(/\/api\/images\/image-1$/)
    expect(wrapper.get('[data-testid="proof-inset-video"]').attributes('src')).toMatch(/\/api\/images\/front-1$/)
    expect(wrapper.get('[data-testid="proof-sound"]').attributes('aria-pressed')).toBe('false')
  })

  it('opens full screen with every part of it', async () => {
    const wrapper = await mountSuspended(ChallengeEntryCard, {
      props: { entry: entry({ insetImageId: 'front-1', insetMedia: 'Photo' }), covered: false, prompt: 'Zeig deinen Arbeitsplatz.' },
    })

    await wrapper.get('[data-testid="proof-enlarge"]').trigger('click')

    expect(usePhotoViewer().current.value).toMatchObject({ imageId: 'image-1', insetImageId: 'front-1', title: 'Zeig deinen Arbeitsplatz.' })
    usePhotoViewer().close()
  })

  /**
   * The prompt asks for a moment, so a picture out of a gallery misses it by
   * definition. Only the exception is named — as a screenshot, the way the
   * camera said it would be (#152).
   */
  it('names the origin only when the camera did not take it', async () => {
    const live = await mountSuspended(ChallengeEntryCard, {
      props: { entry: entry(), covered: false },
    })
    expect(live.find('[data-testid="challenge-origin"]').exists()).toBe(false)

    const picked = await mountSuspended(ChallengeEntryCard, {
      props: { entry: entry({ source: 'Screenshot' }), covered: false },
    })
    expect(picked.get('[data-testid="challenge-origin"]').text()).toBe('Screenshot')
  })

  /**
   * Every contribution from before #64 was sent as taken live, so its route is
   * not known — and calling it a gallery pick would be as wrong as calling it
   * live.
   */
  it('claims nothing about a contribution whose route is unknown', async () => {
    const unknown = await mountSuspended(ChallengeEntryCard, {
      props: { entry: entry({ source: 'Unknown' }), covered: false },
    })
    expect(unknown.find('[data-testid="challenge-origin"]').exists()).toBe(false)
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
    expect(hapticTap).toHaveBeenCalledOnce()
  })
})

describe('ChallengeCard', () => {
  it('shows the prompt and how many friends are in', async () => {
    const wrapper = await mountSuspended(ChallengeCard, {
      props: { room: room({ entries: [coveredEntry(), { ...coveredEntry(), id: 'entry-2' }] }) },
    })

    expect(wrapper.get('[data-testid="challenge-card-prompt"]').text()).toBe('Zeig deinen Arbeitsplatz.')
    expect(wrapper.text()).toContain('2 von 4 Freunden dabei')
  })

  /**
   * The reciprocity rule, on the start screen: every friend who is in gets a
   * tile with their name, and none of them a picture, because there is none
   * to show (docs/adr/0021-daily-challenge.md).
   */
  it('shows who is in before you are, and nothing of what they did', async () => {
    const wrapper = await mountSuspended(ChallengeCard, {
      props: {
        room: room({
          entries: [
            { ...coveredEntry(), id: 'entry-1', author: person({ displayName: 'Lea Hoffmann' }) },
            { ...coveredEntry(), id: 'entry-2', author: person({ displayName: 'Jonas Weber' }) },
          ],
        }),
      },
    })

    const tiles = wrapper.findAll('[data-testid="challenge-tile"]')
    expect(tiles).toHaveLength(2)
    expect(tiles.every(tile => tile.attributes('data-covered') !== undefined)).toBe(true)
    expect(wrapper.find('img[data-testid="challenge-tile-image"]').exists()).toBe(false)
    expect(wrapper.find('button[data-testid="challenge-tile"]').exists()).toBe(false)
    expect(wrapper.text()).toContain('Lea Hoffmann ist dabei, verdeckt')
  })

  /**
   * Yours first, because it is what uncovered the rest; then the friends'
   * newest first, so what is cut off at the edge is what you saw earlier.
   */
  it('puts your own first and your friends\' newest first once you are in', async () => {
    const wrapper = await mountSuspended(ChallengeCard, {
      props: {
        room: room({
          ownEntry: entry({ id: 'own', isMine: true, imageId: 'image-own' }),
          entries: [
            entry({ id: 'early', author: person({ displayName: 'Lea Hoffmann' }), imageId: 'image-early' }),
            entry({ id: 'late', author: person({ displayName: 'Jonas Weber' }), imageId: 'image-late' }),
          ],
          isRevealed: true,
        }),
      },
    })

    const names = wrapper.findAll('[data-testid="challenge-tile-name"]').map(name => name.text())
    expect(names).toEqual(['Du', 'Jonas Weber', 'Lea Hoffmann'])
    expect(wrapper.findAll('[data-testid="challenge-tile"]').every(tile => tile.attributes('data-covered') === undefined)).toBe(true)
  })

  it('leaves the strip out while nobody is in', async () => {
    const wrapper = await mountSuspended(ChallengeCard, { props: { room: room() } })

    expect(wrapper.find('[data-testid="challenge-strip"]').exists()).toBe(false)
  })

  /**
   * The accent means "you can do this now" and is rationed to exactly that
   * (docs/adr/0015-qdos-design-language.md). Once you are in, being in is a
   * state, and a state is grey.
   */
  it('offers the camera until you are in, and then says so in grey', async () => {
    const inviting = await mountSuspended(ChallengeCard, { props: { room: room() } })

    const join = inviting.get('[data-testid="challenge-card-join"]')
    expect(join.text()).toContain('Mitmachen')
    await join.trigger('click')
    expect(inviting.emitted('join')).toHaveLength(1)
    expect(inviting.find('[data-testid="challenge-card-done"]').exists()).toBe(false)

    const joined = await mountSuspended(ChallengeCard, {
      props: { room: room({ ownEntry: entry({ isMine: true }), isRevealed: true }) },
    })

    expect(joined.get('[data-testid="challenge-card-done"]').text()).toContain('Erledigt')
    expect(joined.find('[data-testid="challenge-card-join"]').exists()).toBe(false)
  })

  it('keeps the room one tap away, for reacting, replacing and removing', async () => {
    const wrapper = await mountSuspended(ChallengeCard, { props: { room: room() } })

    expect(wrapper.get('[data-testid="challenge-card-room"]').attributes('href')).toBe('/challenge')
  })
})

describe('ChallengeStripTile', () => {
  it('says a contribution after the moment is late, and nothing about one in time (#136)', async () => {
    const late = await mountSuspended(ChallengeStripTile, {
      props: { entry: entry({ isLate: true }), covered: false, prompt: 'Zeig deinen Arbeitsplatz.' },
    })
    expect(late.get('[data-testid="challenge-tile-late"]').text()).toBe('zu spät')

    const inTime = await mountSuspended(ChallengeStripTile, {
      props: { entry: entry(), covered: false, prompt: 'Zeig deinen Arbeitsplatz.' },
    })
    expect(inTime.find('[data-testid="challenge-tile-late"]').exists()).toBe(false)
  })

  it('opens an uncovered picture full screen, with the prompt and whose it is', async () => {
    const wrapper = await mountSuspended(ChallengeStripTile, {
      props: { entry: entry(), covered: false, prompt: 'Zeig deinen Arbeitsplatz.' },
    })

    await wrapper.get('[data-testid="challenge-tile"]').trigger('click')

    const opened = usePhotoViewer().current.value
    expect(opened?.imageId).toBe('image-1')
    expect(opened?.title).toBe('Zeig deinen Arbeitsplatz.')
    expect(opened?.subtitle).toBe('Jonas Weber')
    usePhotoViewer().close()
  })

  it('keeps the picture and the name out of Session Replay', async () => {
    const wrapper = await mountSuspended(ChallengeStripTile, {
      props: { entry: entry(), covered: false, prompt: 'Zeig deinen Arbeitsplatz.' },
    })

    expect(wrapper.get('[data-testid="challenge-tile"]').attributes()).toHaveProperty('data-q2-block')
    expect(wrapper.get('[data-testid="challenge-tile-name"]').attributes()).toHaveProperty('data-q2-private')
  })
})
