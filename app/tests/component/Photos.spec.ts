import { mountSuspended } from '@nuxt/test-utils/runtime'
import { describe, expect, it, vi } from 'vitest'
import AppAvatar from '~/components/ui/AppAvatar.vue'
import PhotoCapture from '~/components/ui/PhotoCapture.vue'
import ProfileEditSheet from '~/components/profile/ProfileEditSheet.vue'
import type { Person } from '~/api/types'

/**
 * Everything stage 3 put on screen.
 *
 * Two things are being pinned down here and neither is cosmetic. The first is
 * that a picture is **personal**: it must carry `data-q2-block`, or Session
 * Replay records somebody's face on every session (AGENTS.md section 9, and
 * app/AGENTS.md section 7). The second is that a missing picture is a normal
 * state rather than a broken one — the initials have to be there before the
 * image arrives, when there is none, and when the request for it fails.
 *
 * `getUserMedia` does not exist in happy-dom, which is not a gap in the test:
 * it is the case a desktop browser without a camera actually presents, and the
 * component has to reach the file picker without anybody being stuck.
 */
function person(overrides: Partial<Person> = {}): Person {
  return {
    id: '019faece-5a81-7c67-8fa2-00a63d9e9200',
    displayName: 'Mara Beispiel',
    handle: '@mara',
    initials: 'MB',
    avatarColor: '#4f46e5',
    isOnline: true,
    avatarImageId: null,
    ...overrides,
  }
}

describe('AppAvatar', () => {
  it('draws the initials when there is no picture', async () => {
    const wrapper = await mountSuspended(AppAvatar, {
      props: { initials: 'MB', color: '#4f46e5' },
    })

    expect(wrapper.get('[data-testid="avatar"]').text()).toBe('MB')
    expect(wrapper.find('[data-testid="avatar-image"]').exists()).toBe(false)
  })

  it('keeps the initials underneath the picture rather than instead of it', async () => {
    const wrapper = await mountSuspended(AppAvatar, {
      props: { initials: 'MB', color: '#4f46e5', imageId: 'image-1' },
    })

    // What is on screen while the photograph is still on its way.
    expect(wrapper.get('[data-testid="avatar"]').text()).toBe('MB')
    expect(wrapper.get('[data-testid="avatar-image"]').attributes('src')).toContain('/api/images/image-1')
  })

  it('sends the session with the picture, or every avatar is a 401', async () => {
    const wrapper = await mountSuspended(AppAvatar, {
      props: { initials: 'MB', color: '#4f46e5', imageId: 'image-1' },
    })

    expect(wrapper.get('[data-testid="avatar-image"]').attributes('crossorigin')).toBe('use-credentials')
  })

  it('falls back to the initials when the picture cannot be loaded', async () => {
    const wrapper = await mountSuspended(AppAvatar, {
      props: { initials: 'MB', color: '#4f46e5', imageId: 'image-1' },
    })

    await wrapper.get('[data-testid="avatar-image"]').trigger('error')

    expect(wrapper.find('[data-testid="avatar-image"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="avatar"]').text()).toBe('MB')
  })

  it('is blocked from Session Replay, picture or not', async () => {
    const withPicture = await mountSuspended(AppAvatar, {
      props: { initials: 'MB', color: '#4f46e5', imageId: 'image-1' },
    })
    const without = await mountSuspended(AppAvatar, {
      props: { initials: 'MB', color: '#4f46e5' },
    })

    expect(withPicture.attributes()).toHaveProperty('data-q2-block')
    expect(without.attributes()).toHaveProperty('data-q2-block')
  })

  it('never lends a group somebody\'s photograph', async () => {
    // A group's avatar is its icon. Lending it a member's face would say
    // something untrue about whose it is.
    const wrapper = await mountSuspended(AppAvatar, {
      props: { initials: 'LG', color: '#4f46e5', icon: 'sprout', imageId: 'image-1' },
    })

    expect(wrapper.find('[data-testid="avatar-image"]').exists()).toBe(false)
  })
})

describe('PhotoCapture', () => {
  it('offers the file picker when there is no camera to open', async () => {
    const wrapper = await mountSuspended(PhotoCapture, {
      props: { purpose: 'Avatar', open: true },
    })

    expect(document.querySelector('[data-testid="photo-choose"]')).not.toBeNull()

    // The shutter is not offered when there is nothing behind it.
    expect(document.querySelector('[data-testid="photo-shutter"]')).toBeNull()

    wrapper.unmount()
  })

  it('keeps the live camera and the preview out of Session Replay', async () => {
    const wrapper = await mountSuspended(PhotoCapture, {
      props: { purpose: 'Proof', open: true },
    })

    const frame = document.querySelector('[data-q2-block]')

    expect(frame).not.toBeNull()
    expect(frame?.querySelector('[data-testid="photo-camera"]')).not.toBeNull()

    wrapper.unmount()
  })
})

describe('ProfileEditSheet', () => {
  it('offers to add a picture when there is none, and says what stands in for one', async () => {
    const wrapper = await mountSuspended(ProfileEditSheet, {
      props: { person: person(), submitting: false, open: true },
    })

    expect(document.body.textContent).toContain('Noch kein Bild')
    expect(document.querySelector('[data-testid="profile-photo-remove"]')).toBeNull()

    wrapper.unmount()
  })

  it('offers to remove the picture once there is one', async () => {
    const wrapper = await mountSuspended(ProfileEditSheet, {
      props: { person: person({ avatarImageId: 'image-1' }), submitting: false, open: true },
    })

    expect(document.querySelector('[data-testid="profile-photo-remove"]')).not.toBeNull()

    wrapper.unmount()
  })

  it('says the handle does not move, because friends wrote it down', async () => {
    const wrapper = await mountSuspended(ProfileEditSheet, {
      props: { person: person(), submitting: false, open: true },
    })

    expect(document.body.textContent).toContain('Dein Kürzel bleibt')

    wrapper.unmount()
  })
})

/**
 * Opening the sheet, with and without a camera behind it.
 *
 * Only what can be reached by changing state: a `UDrawer` portals its body out
 * of the component's tree, and an event dispatched at an element in there does
 * not reach the handler Vue attached to it under happy-dom — so the shutter,
 * the picker and the confirm button cannot be pressed from here. What they lead
 * to is asserted end to end instead, where a real browser presses them.
 *
 * The watcher is why these mount closed and then open: it is a plain `watch` on
 * `open` rather than an immediate one, so a sheet that was already open when it
 * mounted has never started a camera.
 */
describe('PhotoCapture, when the sheet opens', () => {
  function installCamera(stream: unknown | null) {
    const getUserMedia = stream === null
      ? vi.fn().mockRejectedValue(new Error('denied'))
      : vi.fn().mockResolvedValue(stream)

    Object.defineProperty(globalThis.navigator, 'mediaDevices', {
      configurable: true,
      value: { getUserMedia },
    })

    /*
     * happy-dom has no media element implementation. The component only ever
     * asks the element to hold a stream and to play — and it already tolerates
     * a refused play, which is what a real browser does when autoplay is
     * blocked.
     */
    Object.defineProperty(globalThis.HTMLMediaElement.prototype, 'srcObject', {
      configurable: true,
      writable: true,
      value: null,
    })

    Object.defineProperty(globalThis.HTMLMediaElement.prototype, 'play', {
      configurable: true,
      value: vi.fn().mockRejectedValue(new Error('autoplay refused')),
    })

    return getUserMedia
  }

  it('starts the camera and offers the shutter when there is one', async () => {
    const stop = vi.fn()
    const getUserMedia = installCamera({ getTracks: () => [{ stop }] })

    const wrapper = await mountSuspended(PhotoCapture, { props: { purpose: 'Proof', open: false } })
    await wrapper.setProps({ open: true })

    await vi.waitFor(() => expect(document.querySelector('[data-testid="photo-shutter"]')).not.toBeNull())
    expect(getUserMedia).toHaveBeenCalledWith(expect.objectContaining({ audio: false }))

    // A stream left running is a recording light that stays on.
    wrapper.unmount()
    expect(stop).toHaveBeenCalled()
  })

  /**
   * A refused permission is the person's answer, not a defect: no issue, just
   * the other route, and a sentence saying why.
   */
  it('says so and leaves only the picker when permission is refused', async () => {
    installCamera(null)

    const wrapper = await mountSuspended(PhotoCapture, { props: { purpose: 'Avatar', open: false } })
    await wrapper.setProps({ open: true })

    await vi.waitFor(() =>
      expect(document.querySelector('[data-testid="photo-message"]')?.textContent)
        .toContain('Kein Zugriff auf die Kamera'))

    expect(document.querySelector('[data-testid="photo-shutter"]')).toBeNull()
    expect(document.querySelector('[data-testid="photo-choose"]')).not.toBeNull()

    wrapper.unmount()
  })
})
