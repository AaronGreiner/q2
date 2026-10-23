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
    expect(corner.element.tagName).toBe('SPAN')
    expect(corner.attributes('aria-hidden')).toBe('true')
  })

  it('swaps the front camera\'s picture with the main one, and back', async () => {
    const wrapper = await mountSuspended(ProofMediaFrame, {
      props: { kind: 'Photo', src: 'blob:rear', insetSrc: 'blob:front' },
    })

    const corner = () => wrapper.get('[data-testid="proof-inset"]')
    expect(corner().get('img').attributes('src')).toBe('blob:front')
    expect(corner().attributes('aria-label')).toBe('Frontkamera groß zeigen')
    expect(corner().element.closest('[data-q2-block]')).not.toBeNull()

    await corner().trigger('click')
    expect(wrapper.get('[data-testid="proof-image"]').attributes('src')).toBe('blob:front')
    expect(corner().get('img').attributes('src')).toBe('blob:rear')
    expect(corner().attributes('aria-label')).toBe('Hauptbild groß zeigen')

    await corner().trigger('click')
    expect(wrapper.get('[data-testid="proof-image"]').attributes('src')).toBe('blob:rear')
  })

  /** A second playing clip in a corner would be noise: swapped, the clip is its still. */
  it('puts a clip in the corner by its still when the front picture is large', async () => {
    const wrapper = await mountSuspended(ProofMediaFrame, {
      props: { kind: 'Video', src: 'blob:clip', posterSrc: 'blob:still', insetSrc: 'blob:front' },
    })

    await wrapper.get('[data-testid="proof-inset"]').trigger('click')

    expect(wrapper.find('video').exists()).toBe(false)
    expect(wrapper.get('[data-testid="proof-image"]').attributes('src')).toBe('blob:front')
    expect(wrapper.get('[data-testid="proof-inset"] img').attributes('src')).toBe('blob:still')
    expect(wrapper.find('[data-testid="proof-video-mark"]').exists()).toBe(true)
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
      props: { parts: { imageId: 'clip-1', media: 'Video', posterImageId: 'still-1', insetImageId: 'front-1' } },
    })

    expect(wrapper.get('[data-testid="proof-video"]').attributes('src')).toMatch(/\/api\/images\/clip-1$/)
    expect(wrapper.get('[data-testid="proof-video"]').attributes('poster')).toMatch(/\/api\/images\/still-1$/)
    expect(wrapper.get('[data-testid="proof-inset"] img').attributes('src')).toMatch(/\/api\/images\/front-1$/)
  })

  /** A tile does not play, so it never asks for the clip's bytes. */
  it('asks a tile for the still only', async () => {
    const wrapper = await mountSuspended(ProofMedia, {
      props: { parts: { imageId: 'clip-1', media: 'Video', posterImageId: 'still-1', insetImageId: null }, mode: 'still' },
    })

    expect(wrapper.html()).not.toContain('clip-1')
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

    const clip = wrapper.get('[data-testid="photo-viewer-video"]')
    expect(clip.attributes('src')).toMatch(/\/api\/images\/clip-1$/)
    expect(clip.attributes('poster')).toMatch(/\/api\/images\/still-1$/)
    expect(clip.attributes()).toHaveProperty('controls')
    expect((clip.element as HTMLVideoElement).muted).toBe(true)
    expect(clip.element.closest('[data-q2-block]')).not.toBeNull()
    expect(wrapper.find('[data-testid="photo-viewer-image"]').exists()).toBe(false)
  })

  it('swaps the front camera\'s picture in, as large and as zoomable as any photograph', async () => {
    const wrapper = await mountSuspended(AppPhotoViewer)

    usePhotoViewer().open({ imageId: 'clip-1', media: 'Video', posterImageId: 'still-1', insetImageId: 'front-1' })
    await nextTick()

    const corner = wrapper.get('[data-testid="photo-viewer-inset"]')
    expect(corner.attributes()).toHaveProperty('data-q2-block')
    expect(corner.get('img').attributes('src')).toMatch(/\/api\/images\/front-1$/)

    await corner.trigger('click')

    expect(wrapper.find('[data-testid="photo-viewer-video"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="photo-viewer-image"]').attributes('src')).toMatch(/\/api\/images\/front-1$/)
    expect(wrapper.get('[data-testid="photo-viewer-inset"] img').attributes('src')).toMatch(/\/api\/images\/still-1$/)
  })
})
