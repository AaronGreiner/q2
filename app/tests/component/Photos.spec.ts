import { mountSuspended } from '@nuxt/test-utils/runtime'
import { afterEach, describe, expect, it, vi } from 'vitest'
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
      props: { person: person(), bio: null, submitting: false, open: true },
    })

    expect(document.body.textContent).toContain('Noch kein Bild')
    expect(document.querySelector('[data-testid="profile-photo-remove"]')).toBeNull()

    wrapper.unmount()
  })

  it('offers to remove the picture once there is one', async () => {
    const wrapper = await mountSuspended(ProfileEditSheet, {
      props: { person: person({ avatarImageId: 'image-1' }), bio: null, submitting: false, open: true },
    })

    expect(document.querySelector('[data-testid="profile-photo-remove"]')).not.toBeNull()

    wrapper.unmount()
  })

  it('says the handle does not move, because friends wrote it down', async () => {
    const wrapper = await mountSuspended(ProfileEditSheet, {
      props: { person: person(), bio: null, submitting: false, open: true },
    })

    expect(document.body.textContent).toContain('Dein Kürzel bleibt')

    wrapper.unmount()
  })

  it('edits a bio beside the name and saves both, a cleared one as blank (#20)', async () => {
    const wrapper = await mountSuspended(ProfileEditSheet, {
      props: { person: person(), bio: 'Läuft lieber morgens.', submitting: false, open: false },
      attachTo: document.body,
    })
    await wrapper.setProps({ open: true })

    const field = document.querySelector<HTMLTextAreaElement>('[data-testid="profile-bio"] textarea, textarea[data-testid="profile-bio"]')!
    expect(field.value).toBe('Läuft lieber morgens.')
    expect(document.body.textContent).toContain('Noch 139 Zeichen')

    field.value = '   '
    field.dispatchEvent(new Event('input'))
    await wrapper.vm.$nextTick()
    document.querySelector<HTMLButtonElement>('[data-testid="profile-save"]')!.click()

    expect(wrapper.emitted('save')?.[0]?.[0]).toMatchObject({ bio: '' })
    wrapper.unmount()
    document.body.replaceChildren()
  })
})

/**
 * Opening the sheet, with and without a camera behind it.
 *
 * Only what can be reached by changing state: a `UModal` portals its content
 * out of the component's tree, and an event dispatched at an element in there does
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

  it('opens a screen of its own rather than a sheet', async () => {
    installCamera({ getTracks: () => [] })

    const wrapper = await mountSuspended(PhotoCapture, { props: { purpose: 'Proof', open: false } })
    await wrapper.setProps({ open: true })

    await vi.waitFor(() => expect(document.querySelector('[data-testid="photo-screen"]')).not.toBeNull())
    expect(document.querySelector('[data-vaul-drawer]')).toBeNull()
    expect(document.querySelector('[data-testid="photo-close"]')).not.toBeNull()

    wrapper.unmount()
  })

  /**
   * The front camera used to be the default everywhere, which is backwards for
   * a proof: the evidence is in front of the person, not the person.
   */
  it('opens with the rear camera until somebody has switched', async () => {
    localStorage.removeItem('q2-camera-facing')
    const getUserMedia = installCamera({ getTracks: () => [] })

    const wrapper = await mountSuspended(PhotoCapture, { props: { purpose: 'Proof', open: false } })
    await wrapper.setProps({ open: true })

    await vi.waitFor(() => expect(getUserMedia).toHaveBeenCalled())
    expect(getUserMedia).toHaveBeenCalledWith(expect.objectContaining({ video: { facingMode: 'environment' } }))

    wrapper.unmount()
  })

  it('opens with the camera used last on this device', async () => {
    localStorage.setItem('q2-camera-facing', 'user')
    const getUserMedia = installCamera({ getTracks: () => [] })

    const wrapper = await mountSuspended(PhotoCapture, { props: { purpose: 'Avatar', open: false } })
    await wrapper.setProps({ open: true })

    await vi.waitFor(() => expect(getUserMedia).toHaveBeenCalled())
    expect(getUserMedia).toHaveBeenCalledWith(expect.objectContaining({ video: { facingMode: 'user' } }))

    localStorage.removeItem('q2-camera-facing')
    wrapper.unmount()
  })

  it('offers the other camera only when there is one', async () => {
    const getUserMedia = installCamera({ getTracks: () => [] })
    Object.defineProperty(globalThis.navigator, 'mediaDevices', {
      configurable: true,
      value: {
        getUserMedia,
        enumerateDevices: vi.fn().mockResolvedValue([{ kind: 'videoinput' }, { kind: 'audioinput' }]),
      },
    })

    const wrapper = await mountSuspended(PhotoCapture, { props: { purpose: 'Proof', open: false } })
    await wrapper.setProps({ open: true })

    await vi.waitFor(() => expect(document.querySelector('[data-testid="photo-shutter"]')).not.toBeNull())
    expect(document.querySelector('[data-testid="photo-switch"]')).toBeNull()

    wrapper.unmount()
  })

  it('stops a camera that arrives after the screen was closed again', async () => {
    const stop = vi.fn()
    let grant: (stream: unknown) => void = () => {}
    Object.defineProperty(globalThis.navigator, 'mediaDevices', {
      configurable: true,
      value: { getUserMedia: vi.fn(() => new Promise((resolve) => { grant = resolve })) },
    })

    const wrapper = await mountSuspended(PhotoCapture, { props: { purpose: 'Proof', open: false } })
    await wrapper.setProps({ open: true })
    await wrapper.setProps({ open: false })

    grant({ getTracks: () => [{ stop }] })

    await vi.waitFor(() => expect(stop).toHaveBeenCalled())
    expect(document.querySelector('[data-testid="photo-shutter"]')).toBeNull()

    wrapper.unmount()
  })

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
   * For a proof both routes are real choices (#61): the camera, and a
   * screenshot for what only shows on a screen. Elsewhere the picker is just a
   * picture.
   */
  it('offers a proof\'s second route as a screenshot, and any other picture as a picture', async () => {
    installCamera({ getTracks: () => [] })

    const proof = await mountSuspended(PhotoCapture, { props: { purpose: 'Proof', labelsSource: true, open: false } })
    await proof.setProps({ open: true })

    await vi.waitFor(() => expect(document.querySelector('[data-testid="photo-shutter"]')).not.toBeNull())
    expect(document.querySelector('[data-testid="photo-choose"]')?.getAttribute('aria-label')).toBe('Screenshot auswählen')
    expect(document.querySelector('[data-testid="photo-screen"]')?.textContent).toContain('Screenshot')
    proof.unmount()

    const avatar = await mountSuspended(PhotoCapture, { props: { purpose: 'Avatar', open: false } })
    await avatar.setProps({ open: true })

    await vi.waitFor(() => expect(document.querySelector('[data-testid="photo-shutter"]')).not.toBeNull())
    expect(document.querySelector('[data-testid="photo-choose"]')?.getAttribute('aria-label')).toBe('Bild auswählen')
    avatar.unmount()
  })

  /**
   * A spot check asks what somebody is doing right now, so its camera offers
   * nothing else — and says why when there is no camera to offer.
   */
  it('offers the camera and nothing else for a spot check', async () => {
    installCamera({ getTracks: () => [] })

    const live = await mountSuspended(PhotoCapture, { props: { purpose: 'Proof', liveOnly: true, open: false } })
    await live.setProps({ open: true })

    await vi.waitFor(() => expect(document.querySelector('[data-testid="photo-shutter"]')).not.toBeNull())
    expect(document.querySelector('[data-testid="photo-choose"]')).toBeNull()
    expect(document.querySelector('[data-testid="photo-file"]')).toBeNull()
    live.unmount()

    installCamera(null)
    const refused = await mountSuspended(PhotoCapture, { props: { purpose: 'Proof', liveOnly: true, open: false } })
    await refused.setProps({ open: true })

    await vi.waitFor(() =>
      expect(document.querySelector('[data-testid="photo-message"]')?.textContent).toContain('braucht Qdos die Kamera'))
    expect(document.querySelector('[data-testid="photo-choose"]')).toBeNull()
    refused.unmount()
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

/**
 * A proof's own camera: a clip as well as a photograph, and both cameras at
 * once (#124, #125).
 *
 * As above, only what changing state can reach: which modes are offered on
 * which device, and what is on screen once the cameras are open. Recording,
 * the front picture taken in turn and delivering are pressed in a real
 * browser, in `tests/e2e/videoAndBothCameras.spec.ts`.
 */
describe('PhotoCapture, for a proof', () => {
  let mounted: Array<{ unmount: () => void }> = []

  afterEach(() => {
    mounted.forEach(wrapper => wrapper.unmount())
    mounted = []
    document.body.replaceChildren()
    localStorage.removeItem('q2-camera-both-at-once')
    localStorage.removeItem('q2-camera-facing')
    vi.unstubAllGlobals()
  })

  function videoTrack(deviceId: string) {
    return { stop: vi.fn(), readyState: 'live', muted: false, getSettings: () => ({ deviceId }) }
  }

  function cameraStream(deviceId: string) {
    const track = videoTrack(deviceId)
    return { getTracks: () => [track], getVideoTracks: () => [track], track }
  }

  /**
   * A phone with `cameras` cameras whose `getUserMedia` answers with the
   * rear camera first and the front one after it. `recordsMp4` is what the
   * browser's recorder can write.
   */
  function installDevice({ cameras = 2, recordsMp4 = true, frontBeside = true } = {}) {
    const rear = cameraStream('rear')
    const front = cameraStream('front')
    // The rear camera by facing; the front one by exact facing or by device,
    // which is how a second camera beside the first is asked for.
    const getUserMedia = vi.fn(async (constraints: MediaStreamConstraints) => {
      const video = constraints.video as { facingMode?: unknown, deviceId?: unknown }
      if (typeof video?.facingMode === 'object' || video?.deviceId) {
        if (!frontBeside) throw new Error('NotReadableError')
        return front
      }
      return rear
    })

    Object.defineProperty(globalThis.navigator, 'mediaDevices', {
      configurable: true,
      value: {
        getUserMedia,
        enumerateDevices: vi.fn().mockResolvedValue([
          { kind: 'videoinput', deviceId: 'rear' },
          ...(cameras > 1 ? [{ kind: 'videoinput', deviceId: 'front' }] : []),
        ]),
      },
    })
    Object.defineProperty(globalThis.HTMLMediaElement.prototype, 'srcObject', { configurable: true, writable: true, value: null })
    Object.defineProperty(globalThis.HTMLMediaElement.prototype, 'play', { configurable: true, value: vi.fn().mockResolvedValue(undefined) })

    vi.stubGlobal('MediaRecorder', { isTypeSupported: (type: string) => recordsMp4 && type.startsWith('video/mp4') })
    localStorage.removeItem('q2-camera-both-at-once')

    return { getUserMedia, rear, front }
  }

  async function openProofCamera(props: Record<string, unknown> = {}) {
    const wrapper = await mountSuspended(PhotoCapture, { props: { purpose: 'Proof', labelsSource: true, open: false, ...props } })
    mounted.push(wrapper)
    await wrapper.setProps({ open: true })
    await vi.waitFor(() => expect(document.querySelector('[data-testid="photo-shutter"]')).not.toBeNull())
    return wrapper
  }

  function query(id: string) {
    return document.querySelector(`[data-testid="${id}"]`)
  }

  it('offers a clip beside a photograph where the browser records MP4', async () => {
    installDevice()

    await openProofCamera()

    expect(query('photo-modes')?.textContent).toContain('Foto')
    expect(query('photo-modes')?.textContent).toContain('Video')
  })

  /** A WebM some iPhones cannot play is worse than no video mode at all. */
  it('offers no clip where the browser cannot record MP4, and none for anything but a proof', async () => {
    installDevice({ recordsMp4: false })
    const proof = await openProofCamera()
    expect(query('photo-modes')).toBeNull()
    proof.unmount()
    mounted = []
    document.body.replaceChildren()

    installDevice()
    const avatar = await mountSuspended(PhotoCapture, { props: { purpose: 'Avatar', open: false } })
    mounted.push(avatar)
    await avatar.setProps({ open: true })
    await vi.waitFor(() => expect(query('photo-shutter')).not.toBeNull())
    expect(query('photo-modes')).toBeNull()
    expect(query('photo-both')).toBeNull()
  })

  /**
   * The full camera everywhere but a profile picture (#152): a challenge
   * contribution and a chat photograph get a clip and both cameras exactly as
   * a proof does.
   */
  it.each(['ChallengeEntry', 'ChatPhoto'] as const)('gives %s the full camera', async (purpose) => {
    installDevice()

    const wrapper = await mountSuspended(PhotoCapture, { props: { purpose, open: false } })
    mounted.push(wrapper)
    await wrapper.setProps({ open: true })
    await vi.waitFor(() => expect(query('photo-shutter')).not.toBeNull())

    expect(query('photo-modes')?.textContent).toContain('Video')
    expect(query('photo-both')?.getAttribute('aria-pressed')).toBe('true')
  })

  /**
   * Both cameras is on at every opening, wherever there are two — the rear
   * one large, the front one in the corner, mirrored while live — and is a
   * switch somebody can turn off.
   */
  it('opens with both cameras on, the front one beside the rear one where both can run', async () => {
    const { getUserMedia } = installDevice()
    localStorage.setItem('q2-camera-facing', 'user')

    await openProofCamera()

    // The rear camera large, whatever was used last for a single picture.
    expect(getUserMedia).toHaveBeenNthCalledWith(1, expect.objectContaining({ video: { facingMode: 'environment' } }))

    await vi.waitFor(() => expect(getUserMedia).toHaveBeenCalledWith(expect.objectContaining({ video: { facingMode: { exact: 'user' } } })))

    const both = query('photo-both')
    expect(both?.getAttribute('aria-pressed')).toBe('true')
    expect(both?.getAttribute('aria-label')).toBe('Beide Kameras')

    const front = query('photo-front-camera') as HTMLVideoElement
    await vi.waitFor(() => expect(front.parentElement!.style.display).not.toBe('none'))
    expect(front.className).toContain('-scale-x-100')
    expect(front.closest('[data-q2-block]')).toBe(query('photo-frame'))

    // A single picture has the other-camera switch; both cameras do not need it.
    expect(query('photo-switch')).toBeNull()
  })

  /**
   * Most phones cannot run both: the corner then says the second picture
   * follows, and the device is remembered so it is not asked again.
   */
  it('says the front picture follows where both cannot run at once', async () => {
    installDevice({ frontBeside: false })

    await openProofCamera()

    await vi.waitFor(() => expect(localStorage.getItem('q2-camera-both-at-once')).toBe('no'))
    expect(query('photo-front-after')?.textContent).toContain('Danach du')
    expect((query('photo-front-camera') as HTMLVideoElement).parentElement!.style.display).toBe('none')
  })

  /**
   * Where both run at once, a tap on the corner trades the two places before
   * anything is taken (#151) — and only that: no camera is opened again, so
   * neither viewfinder stalls.
   */
  it('swaps which camera is large with a tap on the corner, where both run at once', async () => {
    const { getUserMedia } = installDevice()

    await openProofCamera()

    const rear = (query('photo-camera') as HTMLVideoElement).parentElement!
    const front = (query('photo-front-camera') as HTMLVideoElement).parentElement!
    await vi.waitFor(() => expect(front.style.display).not.toBe('none'))
    expect(rear.className).toContain('inset-0')
    expect(front.className).toContain('aspect-[3/4]')

    const corner = query('photo-corner') as HTMLButtonElement
    expect(corner.getAttribute('aria-label')).toBe('Frontkamera groß zeigen')
    const opened = getUserMedia.mock.calls.length

    corner.click()
    await nextTick()

    expect(front.className).toContain('inset-0')
    expect(rear.className).toContain('aspect-[3/4]')
    expect(corner.getAttribute('aria-label')).toBe('Rückkamera groß zeigen')
    expect(getUserMedia.mock.calls.length).toBe(opened)
  })

  /**
   * Where they run in turn, the same tap puts the front camera first: it is
   * opened now, mirrored, and the corner says the rear one follows.
   */
  it('opens the front camera first with a tap on the corner, where both run in turn', async () => {
    const { getUserMedia } = installDevice({ frontBeside: false })

    await openProofCamera()
    await vi.waitFor(() => expect(query('photo-front-after')?.textContent).toContain('Danach du'))

    const corner = query('photo-front-after') as HTMLButtonElement
    expect(corner.getAttribute('aria-label')).toBe('Frontkamera zuerst')

    corner.click()

    await vi.waitFor(() => expect(query('photo-front-after')?.textContent).toContain('Danach hinten'))
    expect(getUserMedia).toHaveBeenLastCalledWith(expect.objectContaining({ video: { facingMode: 'user' } }))
    expect(query('photo-camera')?.className).toContain('-scale-x-100')
    expect(query('photo-front-after')?.getAttribute('aria-label')).toBe('Rückkamera zuerst')
  })

  /** Every opening starts with the rear camera first again, and the corner top-left. */
  it('forgets the order and the corner when it closes', async () => {
    installDevice({ frontBeside: false })

    const wrapper = await openProofCamera()
    await vi.waitFor(() => expect(query('photo-front-after')).not.toBeNull())
    ;(query('photo-front-after') as HTMLButtonElement).click()
    await vi.waitFor(() => expect(query('photo-front-after')?.textContent).toContain('Danach hinten'))
    query('photo-front-after')!.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowRight', bubbles: true }))
    await nextTick()
    expect(query('photo-front-after')?.getAttribute('data-side')).toBe('end')

    await wrapper.setProps({ open: false })
    await wrapper.setProps({ open: true })

    await vi.waitFor(() => expect(query('photo-front-after')?.textContent).toContain('Danach du'))
    expect(query('photo-front-after')?.getAttribute('data-side')).toBe('start')
  })

  it('offers no second camera where there is only one', async () => {
    installDevice({ cameras: 1 })

    await openProofCamera()

    expect(query('photo-both')).toBeNull()
    expect(query('photo-front-after')).toBeNull()
  })

  it('stops every camera when the screen closes', async () => {
    const { rear, front } = installDevice()

    const wrapper = await openProofCamera()
    await vi.waitFor(() => expect((query('photo-front-camera') as HTMLVideoElement).parentElement!.style.display).not.toBe('none'))

    await wrapper.setProps({ open: false })

    expect(rear.track.stop).toHaveBeenCalled()
    expect(front.track.stop).toHaveBeenCalled()
  })
})
