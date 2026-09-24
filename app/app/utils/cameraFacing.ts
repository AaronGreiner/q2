/**
 * Which camera the photo screen opens with.
 *
 * The one somebody used last, on this device. Rear until they switch: a proof,
 * a challenge entry and a picture in a chat are all of something in front of
 * the person, and the front camera was only ever the default by accident.
 *
 * Local storage rather than a cookie or an account field: it is a property of
 * the phone, not of the person, and nothing needs it before the camera opens
 * in the browser — the server never has a reason to see it. Storage can be
 * missing or refused (a private window, a locked-down web view); that only ever
 * costs the preference, never the camera.
 */
export type CameraFacing = 'user' | 'environment'

const key = 'q2-camera-facing'

export function rememberedFacing(): CameraFacing {
  try {
    return globalThis.localStorage?.getItem(key) === 'user' ? 'user' : 'environment'
  }
  catch {
    return 'environment'
  }
}

export function rememberFacing(facing: CameraFacing): void {
  try {
    globalThis.localStorage?.setItem(key, facing)
  }
  catch {
    // Refused storage is the browser's answer, not a defect: the next opening
    // simply starts with the rear camera again.
  }
}
