import { mountSuspended } from '@nuxt/test-utils/runtime'
import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import AppPhotoViewer from '~/components/ui/AppPhotoViewer.vue'
import { usePhotoViewer } from '~/composables/usePhotoViewer'

/** The scale the picture is drawn at, read off its transform. */
function scaleOf(style: string | undefined): number {
  return Number(/scale\(([\d.]+)\)/.exec(style ?? '')?.[1] ?? Number.NaN)
}

/**
 * The one full-screen photograph.
 *
 * What it must never lose is what the picture is *of* — full screen is where
 * the card that said so has gone — and a way out that works without a gesture.
 */
describe('AppPhotoViewer', () => {
  beforeEach(() => {
    // Not implemented by the DOM the tests run in; the browser has it.
    HTMLElement.prototype.setPointerCapture ??= () => {}
  })

  afterEach(() => {
    usePhotoViewer().close()
  })

  it('shows nothing until a photograph is opened', async () => {
    const wrapper = await mountSuspended(AppPhotoViewer)

    expect(wrapper.find('[data-testid="photo-viewer"]').exists()).toBe(false)
  })

  it('shows the picture with what it answers, whose it is and when', async () => {
    const wrapper = await mountSuspended(AppPhotoViewer)

    usePhotoViewer().open({
      imageId: 'image-1',
      title: 'Zeig deinen Arbeitsplatz.',
      subtitle: 'Mara',
      meta: '8.9.2026',
    })
    await nextTick()

    const dialog = wrapper.get('[data-testid="photo-viewer"]')
    expect(dialog.attributes('role')).toBe('dialog')
    expect(wrapper.get('[data-testid="photo-viewer-image"]').attributes('crossorigin')).toBe('use-credentials')

    const info = wrapper.get('[data-testid="photo-viewer-info"]').text()
    expect(info).toContain('Zeig deinen Arbeitsplatz.')
    expect(info).toContain('Mara · 8.9.2026')

    // Somebody's photograph and name, so neither reaches Session Replay.
    expect(wrapper.get('[data-testid="photo-viewer-info"] p').attributes()).toHaveProperty('data-q2-private')
  })

  it('closes with the button and with Escape', async () => {
    const wrapper = await mountSuspended(AppPhotoViewer)
    const viewer = usePhotoViewer()

    viewer.open({ imageId: 'image-1' })
    await nextTick()
    await wrapper.get('[data-testid="photo-viewer-close"]').trigger('click')
    expect(viewer.current.value).toBeNull()

    viewer.open({ imageId: 'image-1' })
    await nextTick()
    await wrapper.get('[data-testid="photo-viewer"]').trigger('keydown', { key: 'Escape' })
    expect(viewer.current.value).toBeNull()
  })

  it('closes when dragged far enough down, and springs back when not', async () => {
    const wrapper = await mountSuspended(AppPhotoViewer)
    const viewer = usePhotoViewer()

    viewer.open({ imageId: 'image-1' })
    await nextTick()
    const surface = wrapper.get('[data-testid="photo-viewer-image"]').element.parentElement!
    const at = (type: string, y: number) =>
      surface.dispatchEvent(new PointerEvent(type, { pointerId: 1, clientX: 100, clientY: y, bubbles: true }))

    at('pointerdown', 100)
    at('pointermove', 160)
    at('pointerup', 160)
    await nextTick()
    expect(viewer.current.value).not.toBeNull()

    at('pointerdown', 100)
    at('pointermove', 300)
    at('pointerup', 300)
    await nextTick()
    expect(viewer.current.value).toBeNull()
  })

  it('zooms in on a double tap and with two fingers, and out again', async () => {
    const wrapper = await mountSuspended(AppPhotoViewer)

    usePhotoViewer().open({ imageId: 'image-1', title: 'Jeden Tag lesen' })
    await nextTick()
    const image = () => wrapper.get('[data-testid="photo-viewer-image"]')
    const surface = image().element.parentElement!
    const pointer = (type: string, id: number, x: number) =>
      surface.dispatchEvent(new PointerEvent(type, { pointerId: id, clientX: x, clientY: 200, bubbles: true }))

    pointer('pointerdown', 1, 100)
    pointer('pointerup', 1, 100)
    pointer('pointerdown', 1, 100)
    pointer('pointerup', 1, 100)
    await nextTick()
    expect(scaleOf(image().attributes('style'))).toBe(2.5)

    // The caption steps aside while somebody is looking at detail.
    expect(wrapper.get('[data-testid="photo-viewer-info"]').classes()).toContain('opacity-0')

    pointer('pointerdown', 1, 100)
    pointer('pointerup', 1, 100)
    pointer('pointerdown', 1, 100)
    pointer('pointerup', 1, 100)
    await nextTick()
    expect(scaleOf(image().attributes('style'))).toBe(1)

    // Two fingers twice as far apart is twice the size.
    pointer('pointerdown', 1, 100)
    pointer('pointerdown', 2, 200)
    pointer('pointermove', 2, 300)
    await nextTick()
    expect(scaleOf(image().attributes('style'))).toBe(2)

    // Lifting one finger carries on as a pan, and the zoom stays.
    pointer('pointerup', 2, 300)
    pointer('pointermove', 1, 140)
    pointer('pointerup', 1, 140)
    await nextTick()
    expect(scaleOf(image().attributes('style'))).toBe(2)
  })
})
