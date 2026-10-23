import type { Image } from '~/api/types'
import type { PhotoOrigin } from '~/utils/proofSource'

/**
 * What the camera screen hands over once everything is uploaded: the picture
 * or clip, the route it took, and what came with it.
 *
 * A clip comes with a still taken while it was recorded (#124) — the server
 * never decodes video, so the phone is the only place one can come from — and
 * a proof taken with both cameras comes with the front camera's picture
 * (#125). Anything that is not a proof only ever reads `image` and `origin`.
 */
export interface Captured {
  /** The photograph, or a proof's clip (`image.kind`). */
  image: Image
  origin: PhotoOrigin
  /** A clip's still. Null for a photograph. */
  poster: Image | null
  /** The front camera's picture, when both cameras were used. */
  inset: Image | null
}

/** The ids a delivery names, as the proof endpoints take them. */
export interface ProofPartIds {
  imageId: string
  posterImageId: string | null
  insetImageId: string | null
}

export function proofPartsOf(captured: Captured): ProofPartIds {
  return {
    imageId: captured.image.id,
    posterImageId: captured.poster?.id ?? null,
    insetImageId: captured.inset?.id ?? null,
  }
}
