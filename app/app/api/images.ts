import type { ApiCaller } from './client'
import type { Image, ImagePurpose, ImageQuota } from './types'

/**
 * The media type every upload leaves the browser as.
 *
 * One type rather than "whatever was picked", because everything goes through a
 * canvas on its way out (`app/utils/images.ts`): a HEIC photograph, a PNG
 * screenshot and a WebP download all arrive at the server as JPEG. That is why
 * the backend needs only two header parsers, and why nothing here has to reason
 * about what a phone's camera roll happens to hold.
 */
export const uploadContentType = 'image/jpeg'

/**
 * Pictures: uploading one, addressing one, deleting one.
 *
 * There is no "list" and no "get metadata". An image is always reached through
 * whatever points at it — a person's `avatarImageId`, later a proof — so a
 * second way to enumerate them would be a second thing to keep scoped to the
 * right viewer.
 */
export interface ImagesApi {
  /**
   * Sends the bytes as the request body. Downscale first with
   * `downscaleForUpload`; this does not resize anything.
   */
  upload: (file: Blob, purpose: ImagePurpose) => Promise<Image>

  /** How much of their storage allowance the signed-in person has used. */
  quota: () => Promise<ImageQuota>

  remove: (id: string) => Promise<void>
}

export function createImagesApi(call: ApiCaller): ImagesApi {
  return {
    upload: (file, purpose) => call<Image>('/api/images', {
      method: 'POST',
      query: { purpose },
      body: file,

      // The blob's own type would usually do, but a Blob built from a canvas
      // on a browser that refused the format has an empty one — and an upload
      // with no media type is a 415 rather than a picture.
      headers: { 'Content-Type': file.type || uploadContentType },
    }),

    quota: () => call<ImageQuota>('/api/images/quota', { method: 'GET' }),

    remove: async (id) => {
      await call<unknown>(`/api/images/${encodeURIComponent(id)}`, { method: 'DELETE' })
    },
  }
}

/**
 * The address an `<img>` loads a picture from.
 *
 * Absolute, because the API is a different origin from the app in development
 * and the browser resolves an `src` against the page rather than against the
 * fetch client. The endpoint checks the session on every request — this is a
 * URL, not a capability, and pasting it into a signed-out browser gets a 401.
 *
 * The element needs `crossorigin="use-credentials"` for the session cookie to
 * travel with it; `AppAvatar` and `AppPhoto` set it, which is the reason to
 * reach for one of those rather than writing an `<img>` by hand.
 */
export function imageUrl(baseUrl: string, id: string): string {
  return `${baseUrl.replace(/\/$/, '')}/api/images/${encodeURIComponent(id)}`
}
