import type { MediaKind } from '~/api/types'

/**
 * What a proof shows: its photograph or clip, a clip's still, and the front
 * camera's picture — or, where both cameras filmed at once, its clip with a
 * still of its own (#124, #125, #151).
 *
 * Every response that carries a proof — the card, the gallery, a
 * conversation's grid — carries these, and every place that draws one goes
 * through `ProofMediaFrame` with them.
 */
export interface ProofMediaParts {
  imageId: string
  media: MediaKind
  posterImageId: string | null
  insetImageId: string | null
  /** Whether the front camera's part is a photograph or a clip. */
  insetMedia: MediaKind
  /** A front clip's still. */
  insetPosterImageId: string | null
}

/**
 * The parts, from anything that carries them. A response from before #124, or
 * a chat photograph that is not a proof, is a single photograph; a front part
 * from before #151 is a photograph.
 */
export function proofMediaOf(source: {
  imageId: string
  media?: MediaKind | null
  posterImageId?: string | null
  insetImageId?: string | null
  insetMedia?: MediaKind | null
  insetPosterImageId?: string | null
}): ProofMediaParts {
  return {
    imageId: source.imageId,
    media: source.media ?? 'Photo',
    posterImageId: source.posterImageId ?? null,
    insetImageId: source.insetImageId ?? null,
    insetMedia: source.insetMedia ?? 'Photo',
    insetPosterImageId: source.insetPosterImageId ?? null,
  }
}
