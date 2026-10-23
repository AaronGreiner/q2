import { mountSuspended } from '@nuxt/test-utils/runtime'
import { afterEach, describe, expect, it } from 'vitest'
import AppPhotoViewer from '~/components/ui/AppPhotoViewer.vue'
import ProofMedia from '~/components/proofs/ProofMedia.vue'
import ProofMediaFrame from '~/components/proofs/ProofMediaFrame.vue'
import { usePhotoViewer } from '~/composables/usePhotoViewer'

/**
 * A proof that is a clip, or that has the front camera's picture beside it
 * (#124, #125), wherever one is drawn.
 *
 * What has to hold everywhere: every element that can show a face or a room
 * is inside `data-q2-block`, a clip never plays with sound unasked, and the
 * two pictures of a proof from both cameras can be swapped.
 */
describe('ProofMediaFrame', () => {
  it('draws a photograph as a picture, blocked from Session Replay', async () => {
    const wrapper = await mountSuspended(ProofMediaFrame, { props: { kind: 'Photo', src: 'blob:main' } })

    const image = wrapper.get('[data-testid="proof-image"]')
    expect(image.attributes('src')).toBe('blob:main')
    expect(image.attributes('crossorigin')).toBe('use-credentials')
    expect(image.element.closest('[data-q2-block]')).not.toBeNull()
    expect(wrapper.find('video').exists()).toBe(false)
    expect(wrapper.find('[data-testid="proof-inset"]').exists()).toBe(false)
  })

  it('plays a clip inline, without sound until asked, behind its still', async () => {
    const wrapper = await mountSuspended(ProofMediaFrame, {
      props: { kind: 'Video', src: 'blob:clip', posterSrc: 'blob:still' },
    })

    const clip = wrapper.get('[data-testid="proof-video"]')
    const element = clip.element as HTMLVideoElement
    expect(clip.attributes('src')).toBe('blob:clip')
    expect(clip.attributes('poster')).toBe('blob:still')
    expect(clip.attributes('crossorigin')).toBe('use-credentials')
    expect(clip.attributes()).toHaveProperty('playsinline')
    expect(element.muted).toBe(true)
    expect(clip.attributes()).not.toHaveProperty('controls')
    expect(clip.element.closest('[data-q2-block]')).not.toBeNull()

    const sound = wrapper.get('[data-testid="proof-sound"]')
    expect(sound.attributes('aria-pressed')).toBe('false')
    expect(sound.attributes('aria-label')).toBe('Ton einschalten')

    await sound.trigger('click')
    expect(sound.attributes('aria-pressed')).toBe('true')
    expect(element.muted).toBe(false)
  })

  it('gives a clip the player\'s own controls full screen', async () => {
    const wrapper = await mountSuspended(ProofMediaFrame, {
      props: { kind: 'Video', src: 'blob:clip', posterSrc: 'blob:still', mode: 'controls' },
    })

    expect(wrapper.get('[data-testid="proof-video"]').attributes()).toHaveProperty('controls')
    expect(wrapper.find('[data-testid="proof-sound"]').exists()).toBe(false)
  })

  /** A tile never plays: it shows the still and says it is a clip. */
  it('shows a clip on a tile by its still, marked as a video', async () => {
    const wrapper = await mountSuspended(ProofMediaFrame, {
      props: { kind: 'Video', src: null, posterSrc: 'blob:still', insetSrc: 'blob:front', mode: 'still' },
    })

    expect(wrapper.find('video').exists()).toBe(false)
    expect(wrapper.get('[data-testid="proof-image"]').attributes('src')).toBe('blob:still')
    expect(wrapper.get('[data-testid="proof-video-mark"]').attributes('aria-label')).toBe('Video')

    // Shown, never pressed: the tile itself is the button.
    const corner = wrapper.get('[data-testid="proof-inset"]')
    expect(corner.element.tagName).not.toBe('BUTTON')
    expect(corner.attributes('aria-hidden')).toBe('true')
    expect(corner.get('img').attributes('src')).toBe('blob:front')
    expect(wrapper.find('button').exists()).toBe(false)
  })

  /** A front clip on a tile is its still too: nothing on a tile plays. */
  it('shows two clips on a tile by their stills', async () => {
    const wrapper = await mountSuspended(ProofMediaFrame, {
      props: { kind: 'Video', src: null, posterSrc: 'blob:still', insetKind: 'Video', insetSrc: null, insetPosterSrc: 'blob:front-still', mode: 'still' },
    })

    expect(wrapper.find('video').exists()).toBe(false)
    expect(wrapper.get('[data-testid="proof-inset"] img').attributes('src')).toBe('blob:front-still')
  })

  it('swaps the front camera\'s picture with the main one, and back', async () => {
    const wrapper = await mountSuspended(ProofMediaFrame, {
      props: { kind: 'Photo', src: 'blob:rear', insetSrc: 'blob:front' },
    })

    const corner = () => wrapper.get('[data-testid="proof-inset"]')
    expect(wrapper.get('[data-testid="proof-inset-image"]').attributes('src')).toBe('blob:front')
    expect(corner().attributes('aria-label')).toBe('Frontkamera groß zeigen')
    expect(corner().element.closest('[data-q2-block]')).not.toBeNull()

    await corner().trigger('click')
    expect(wrapper.get('[data-testid="proof-image"]').attributes('src')).toBe('blob:front')
    expect(wrapper.get('[data-testid="proof-inset-image"]').attributes('src')).toBe('blob:rear')
    expect(corner().attributes('aria-label')).toBe('Hauptbild groß zeigen')

    await corner().trigger('click')
    expect(wrapper.get('[data-testid="proof-image"]').attributes('src')).toBe('blob:rear')
  })

  /**
   * Swapped, a clip keeps playing in the corner — the same element, moved,
   * so it carries on from where it was — and is silent there.
   */
  it('keeps a clip playing in the corner when the front picture is large', async () => {
    const wrapper = await mountSuspended(ProofMediaFrame, {
      props: { kind: 'Video', src: 'blob:clip', posterSrc: 'blob:still', insetSrc: 'blob:front' },
    })

    const clip = wrapper.get('[data-testid="proof-video"]').element as HTMLVideoElement
    await wrapper.get('[data-testid="proof-sound"]').trigger('click')
    expect(clip.muted).toBe(false)

    await wrapper.get('[data-testid="proof-inset"]').trigger('click')

    expect(wrapper.get('[data-testid="proof-image"]').attributes('src')).toBe('blob:front')
    expect(wrapper.get('[data-testid="proof-inset-video"]').element).toBe(clip)
    expect(clip.muted).toBe(true)
    // The large one is a photograph: there is nothing to hear.
    expect(wrapper.find('[data-testid="proof-sound"]').exists()).toBe(false)
  })

  /**
   * Both cameras filmed at once (#151): both clips play, the corner always
   * silent, and a swap only trades their places — the sound goes with the
   * large one.
   */
  it('plays two clips together, the corner silent, and swaps them in place', async () => {
    const wrapper = await mountSuspended(ProofMediaFrame, {
      props: {
        kind: 'Video',
        src: 'blob:rear-clip',
        posterSrc: 'blob:rear-still',
        insetKind: 'Video',
        insetSrc: 'blob:front-clip',
        insetPosterSrc: 'blob:front-still',
      },
    })

    const rear = wrapper.get('[data-testid="proof-video"]').element as HTMLVideoElement
    const front = wrapper.get('[data-testid="proof-inset-video"]').element as HTMLVideoElement
    expect(front.getAttribute('src')).toBe('blob:front-clip')
    expect(front.getAttribute('poster')).toBe('blob:front-still')
    expect(front.muted).toBe(true)
    expect(front.closest('[data-q2-block]')).not.toBeNull()

    // The large one leads, and only the leader loops: the corner follows it round.
    expect(rear.loop).toBe(true)
    expect(front.loop).toBe(false)

    await wrapper.get('[data-testid="proof-sound"]').trigger('click')
    expect(rear.muted).toBe(false)

    await wrapper.get('[data-testid="proof-inset"]').trigger('click')

    expect(wrapper.get('[data-testid="proof-video"]').element).toBe(front)
    expect(wrapper.get('[data-testid="proof-inset-video"]').element).toBe(rear)
    expect(front.muted).toBe(false)
    expect(rear.muted).toBe(true)
    expect(front.loop).toBe(true)
    expect(rear.loop).toBe(false)
  })

  it('gives the large clip of two the player\'s controls full screen, and the corner none', async () => {
    const wrapper = await mountSuspended(ProofMediaFrame, {
      props: { kind: 'Video', src: 'blob:rear-clip', posterSrc: 'blob:rear-still', insetKind: 'Video', insetSrc: 'blob:front-clip', insetPosterSrc: 'blob:front-still', mode: 'controls' },
    })

    expect(wrapper.get('[data-testid="proof-video"]').attributes()).toHaveProperty('controls')
    expect(wrapper.get('[data-testid="proof-inset-video"]').attributes()).not.toHaveProperty('controls')

    await wrapper.get('[data-testid="proof-inset"]').trigger('click')

    expect(wrapper.get('[data-testid="proof-video"]').attributes('src')).toBe('blob:front-clip')
    expect(wrapper.get('[data-testid="proof-video"]').attributes()).toHaveProperty('controls')
    expect(wrapper.get('[data-testid="proof-inset-video"]').attributes()).not.toHaveProperty('controls')
  })

  /** Moving the corner never depends on a drag alone (#151). */
  it('moves the corner between the top corners with the arrow keys, without swapping', async () => {
    const wrapper = await mountSuspended(ProofMediaFrame, {
      props: { kind: 'Photo', src: 'blob:rear', insetSrc: 'blob:front' },
    })

    const corner = wrapper.get('[data-testid="proof-inset"]')
    expect(corner.attributes('data-side')).toBe('start')
    expect(corner.attributes('aria-keyshortcuts')).toBe('ArrowLeft ArrowRight')

    await corner.trigger('keydown', { key: 'ArrowRight' })
    expect(corner.attributes('data-side')).toBe('end')
    expect(corner.classes()).toContain('end-2.5')
    expect(wrapper.get('[data-testid="proof-inset-image"]').element.parentElement!.className).toContain('end-2.5')
    expect(wrapper.get('[data-testid="proof-image"]').attributes('src')).toBe('blob:rear')

    await corner.trigger('keydown', { key: 'ArrowLeft' })
    expect(corner.attributes('data-side')).toBe('start')
  })

  /**
   * Where the frame runs under something — the full screen's status bar, and
   * its close button in the end corner — the corner keeps below it.
   */
  it('keeps the corner below what the frame runs under, in either corner', async () => {
    const wrapper = await mountSuspended(ProofMediaFrame, {
      props: { kind: 'Photo', src: 'blob:rear', insetSrc: 'blob:front', insetTop: '12px', insetTopEnd: '68px' },
    })

    const corner = wrapper.get('[data-testid="proof-inset"]')
    expect(corner.attributes('style')).toContain('top: 12px')

    await corner.trigger('keydown', { key: 'ArrowRight' })
    expect(corner.attributes('style')).toContain('top: 68px')
    expect(wrapper.get('[data-testid="proof-inset-image"]').element.parentElement!.getAttribute('style')).toContain('top: 68px')
  })

  /** A new proof starts with the rear camera large and the corner top-left. */
  it('starts over for another proof', async () => {
    const wrapper = await mountSuspended(ProofMediaFrame, {
      props: { kind: 'Photo', src: 'blob:rear', insetSrc: 'blob:front' },
    })

    await wrapper.get('[data-testid="proof-inset"]').trigger('keydown', { key: 'ArrowRight' })
    await wrapper.get('[data-testid="proof-inset"]').trigger('click')

    await wrapper.setProps({ src: 'blob:other-rear', insetSrc: 'blob:other-front' })

    expect(wrapper.get('[data-testid="proof-image"]').attributes('src')).toBe('blob:other-rear')
    expect(wrapper.get('[data-testid="proof-inset"]').attributes('data-side')).toBe('start')
  })

  it('opens what it shows when it is a button, without the corner doing the same', async () => {
    const wrapper = await mountSuspended(ProofMediaFrame, {
      props: { kind: 'Photo', src: 'blob:rear', insetSrc: 'blob:front', openable: true, openLabel: 'Vergrößern' },
    })

    await wrapper.get('[data-testid="proof-inset"]').trigger('click')
    expect(wrapper.emitted('open')).toBeUndefined()

    const open = wrapper.get('[data-testid="proof-enlarge"]')
    expect(open.attributes('aria-label')).toBe('Vergrößern')
    await open.trigger('click')
    expect(wrapper.emitted('open')).toHaveLength(1)
  })
})

describe('ProofMedia', () => {
  it('fetches each part of a stored proof through the image endpoint', async () => {
    const wrapper = await mountSuspended(ProofMedia, {
      props: { parts: { imageId: 'clip-1', media: 'Video', posterImageId: 'still-1', insetImageId: 'front-1', insetMedia: 'Photo', insetPosterImageId: null } },
    })

    expect(wrapper.get('[data-testid="proof-video"]').attributes('src')).toMatch(/\/api\/images\/clip-1$/)
    expect(wrapper.get('[data-testid="proof-video"]').attributes('poster')).toMatch(/\/api\/images\/still-1$/)
    expect(wrapper.get('[data-testid="proof-inset-image"]').attributes('src')).toMatch(/\/api\/images\/front-1$/)
  })

  it('fetches a front clip and its still', async () => {
    const wrapper = await mountSuspended(ProofMedia, {
      props: { parts: { imageId: 'clip-1', media: 'Video', posterImageId: 'still-1', insetImageId: 'front-1', insetMedia: 'Video', insetPosterImageId: 'front-still-1' } },
    })

    const front = wrapper.get('[data-testid="proof-inset-video"]')
    expect(front.attributes('src')).toMatch(/\/api\/images\/front-1$/)
    expect(front.attributes('poster')).toMatch(/\/api\/images\/front-still-1$/)
  })

  /** A tile does not play, so it never asks for the clip's bytes. */
  it('asks a tile for the still only', async () => {
    const wrapper = await mountSuspended(ProofMedia, {
      props: {
        parts: { imageId: 'clip-1', media: 'Video', posterImageId: 'still-1', insetImageId: 'front-1', insetMedia: 'Video', insetPosterImageId: 'front-still-1' },
        mode: 'still',
      },
    })

    expect(wrapper.html()).not.toContain('clip-1')
    expect(wrapper.html()).not.toContain('/front-1')
    expect(wrapper.get('[data-testid="proof-inset"] img').attributes('src')).toMatch(/\/api\/images\/front-still-1$/)
    expect(wrapper.get('[data-testid="proof-image"]').attributes('src')).toMatch(/\/api\/images\/still-1$/)
  })
})

describe('AppPhotoViewer, with a clip or both cameras', () => {
  afterEach(() => {
    usePhotoViewer().close()
  })

  it('plays a clip with the player\'s controls, starting without sound', async () => {
    const wrapper = await mountSuspended(AppPhotoViewer)

    usePhotoViewer().open({ imageId: 'clip-1', media: 'Video', posterImageId: 'still-1', title: 'Laufen' })
    await nextTick()

    const viewer = wrapper.get('[data-testid="photo-viewer"]')
    const clip = viewer.get('[data-testid="proof-video"]')
    expect(clip.attributes('src')).toMatch(/\/api\/images\/clip-1$/)
    expect(clip.attributes('poster')).toMatch(/\/api\/images\/still-1$/)
    expect(clip.attributes()).toHaveProperty('controls')
    expect((clip.element as HTMLVideoElement).muted).toBe(true)
    expect(clip.element.closest('[data-q2-block]')).not.toBeNull()
    expect(viewer.find('[data-testid="proof-image"]').exists()).toBe(false)
  })

  it('swaps the front camera\'s picture in, as large and as zoomable as any photograph', async () => {
    const wrapper = await mountSuspended(AppPhotoViewer)

    usePhotoViewer().open({ imageId: 'clip-1', media: 'Video', posterImageId: 'still-1', insetImageId: 'front-1' })
    await nextTick()

    const corner = wrapper.get('[data-testid="proof-inset"]')
    expect(corner.element.closest('[data-q2-block]')).not.toBeNull()
    expect(wrapper.get('[data-testid="proof-inset-image"]').attributes('src')).toMatch(/\/api\/images\/front-1$/)

    await corner.trigger('click')

    const large = wrapper.get('[data-testid="proof-image"]')
    expect(large.attributes('src')).toMatch(/\/api\/images\/front-1$/)
    expect(large.attributes('style')).toContain('scale(1)')
    // The clip carries on in the corner, silent.
    expect(wrapper.get('[data-testid="proof-inset-video"]').attributes('src')).toMatch(/\/api\/images\/clip-1$/)
  })

  /** Two clips filmed at once play as one full screen too. */
  it('plays two clips together', async () => {
    const wrapper = await mountSuspended(AppPhotoViewer)

    usePhotoViewer().open({
      imageId: 'clip-1',
      media: 'Video',
      posterImageId: 'still-1',
      insetImageId: 'front-1',
      insetMedia: 'Video',
      insetPosterImageId: 'front-still-1',
    })
    await nextTick()

    expect(wrapper.get('[data-testid="proof-video"]').attributes()).toHaveProperty('controls')
    expect(wrapper.get('[data-testid="proof-inset-video"]').attributes('src')).toMatch(/\/api\/images\/front-1$/)
  })
})
