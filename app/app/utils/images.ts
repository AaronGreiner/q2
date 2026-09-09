import { uploadContentType } from '~/api/images'

/**
 * How large a picture may be when it leaves the browser, by purpose.
 *
 * An avatar is drawn at 88 pixels at its largest and a proof photograph at the
 * width of a phone, so these are already generous — the extra headroom is for
 * a device with a three-times pixel ratio. Anything beyond that is bytes spent
 * on detail no screen in this product will ever show.
 *
 * The server has its own ceiling (`StoredImage.MaxDimension`, 2048) and does
 * not trust these. This is about what is polite to upload over a phone
 * connection; that is about what is safe to store.
 */
export const uploadSizes = {
  avatar: 512,
  proof: 1440,
} as const

/** How hard JPEG compresses. 0.82 is where a photograph stops looking cheap. */
const quality = 0.82

/**
 * Shrinks a picked file to fit inside `maxEdge` and re-encodes it as JPEG.
 *
 * Two things are happening here and both are the point:
 *
 * **The size.** A photograph straight off a modern phone is four thousand
 * pixels across and several megabytes. Sending that so the server can show it
 * at 390 would cost somebody a noticeable part of a mobile data allowance, and
 * the upload limit would reject a good half of the camera roll.
 *
 * **The format.** A canvas re-encode is the only reliable way to turn whatever
 * a phone produced — HEIC, WebP, a PNG screenshot — into something the server
 * accepts. It is also what strips the Exif block, and that is not a side
 * effect: a photograph taken outdoors carries the GPS coordinates it was taken
 * at, and q2 does not process location data at all
 * (AGENTS.md section 8). The one thing worth knowing is that the orientation
 * tag goes with it, which is why the bitmap is decoded with
 * `imageOrientation: 'from-image'` — without it, a portrait photograph would
 * be stored on its side.
 *
 * A file that is already small enough is still re-encoded, for the same two
 * reasons.
 */
export async function downscaleForUpload(file: Blob, maxEdge: number): Promise<Blob> {
  const bitmap = await createImageBitmap(file, { imageOrientation: 'from-image' })

  try {
    const { width, height } = fit(bitmap.width, bitmap.height, maxEdge)
    const canvas = document.createElement('canvas')
    canvas.width = width
    canvas.height = height

    const context = canvas.getContext('2d')

    if (!context) {
      throw new Error('This browser did not provide a 2D canvas context.')
    }

    context.drawImage(bitmap, 0, 0, width, height)

    return await new Promise<Blob>((resolve, reject) => canvas.toBlob(
      // A browser that cannot encode the requested type hands back null rather
      // than throwing, which would otherwise surface much later as an empty
      // upload.
      blob => (blob ? resolve(blob) : reject(new Error('This browser could not encode the picture.'))),
      uploadContentType,
      quality,
    ))
  }
  finally {
    // Bitmaps hold decoded pixels outside the JavaScript heap; on a phone,
    // a few full-resolution ones left around is a tab the system kills.
    bitmap.close()
  }
}

/**
 * The largest whole-pixel size inside a `maxEdge` square that keeps the
 * original proportions. Never enlarges: upscaling adds bytes and no detail.
 */
export function fit(width: number, height: number, maxEdge: number): { width: number, height: number } {
  const scale = Math.min(1, maxEdge / Math.max(width, height))

  return {
    width: Math.max(1, Math.round(width * scale)),
    height: Math.max(1, Math.round(height * scale)),
  }
}
