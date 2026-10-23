import { stopStream } from '~/utils/clipRecording'

/**
 * Whether this device can keep the front and the rear camera open at once
 * (#125).
 *
 * Most phones cannot, in a browser: WebKit allows one camera capture at a time
 * — opening a second one mutes the first — and plenty of Android phones refuse
 * the second outright. A desktop with two cameras can. The only way to know is
 * to try, so the first opening of the camera in both-cameras mode tries, and a
 * device that said no is remembered and not asked again: trying costs a moment
 * of a stalled viewfinder each time. Where both cannot run at once, the camera
 * takes the two pictures one after the other instead, the way BeReal does.
 *
 * Local storage, like the remembered facing (`utils/cameraFacing.ts`): a
 * property of the device, which the server never needs to know.
 */
const key = 'q2-camera-both-at-once'

export function mayTryBothAtOnce(): boolean {
  try {
    return globalThis.localStorage?.getItem(key) !== 'no'
  }
  catch {
    return true
  }
}

export function rememberNotBothAtOnce(): void {
  try {
    globalThis.localStorage?.setItem(key, 'no')
  }
  catch {
    // Refused storage only means trying again next time.
  }
}

/**
 * Opens the front camera beside `main`, which is already open on the rear one,
 * and returns it — or null, with nothing left open, when both cannot run at
 * once.
 *
 * The front camera is asked for by facing first. A camera that reports no
 * facing at all — a desktop's, or a test's fake one — is then asked for by
 * being a different device from `main`. Both have to be live a moment later:
 * a phone that "opens" the second camera by muting the first has not opened
 * both.
 */
export async function openFrontBeside(
  main: MediaStream,
  devices: Pick<MediaDevices, 'getUserMedia' | 'enumerateDevices'>,
  settleMs = 400,
): Promise<MediaStream | null> {
  if (!mayTryBothAtOnce()) return null

  const mainTrack = main.getVideoTracks()[0]
  const mainDevice = mainTrack?.getSettings?.().deviceId

  let front: MediaStream | null = null

  try {
    front = await devices.getUserMedia({ video: { facingMode: { exact: 'user' } }, audio: false })
  }
  catch {
    const other = (await devices.enumerateDevices().catch(() => []))
      .find(device => device.kind === 'videoinput' && device.deviceId && device.deviceId !== mainDevice)

    if (other) {
      front = await devices.getUserMedia({ video: { deviceId: { exact: other.deviceId } }, audio: false }).catch(() => null)
    }
  }

  if (!front) {
    rememberNotBothAtOnce()
    return null
  }

  await new Promise(resolve => setTimeout(resolve, settleMs))

  const frontTrack = front.getVideoTracks()[0]
  const bothLive = isLive(mainTrack) && isLive(frontTrack)
    && frontTrack?.getSettings?.().deviceId !== mainDevice

  if (!bothLive) {
    stopStream(front)
    rememberNotBothAtOnce()
    return null
  }

  return front
}

function isLive(track: MediaStreamTrack | undefined): boolean {
  return !!track && track.readyState === 'live' && !track.muted
}

/**
 * Whether `stream` can still be shown: a phone that refused to run both may
 * have muted or ended the first camera on the way.
 */
export function streamIsLive(stream: MediaStream | null | undefined): boolean {
  return isLive(stream?.getVideoTracks()[0])
}
