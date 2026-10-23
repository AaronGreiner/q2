import type { MediaKind } from '~/api/types'

/**
 * What a proof shows: its photograph or clip, a clip's still, and the front
 * camera's picture when both cameras were used (#124, #125).
 *
 * Every response that carries a proof — the card, the gallery, a
 * conversation's grid — carries these four, and every place that draws one
 * goes through `ProofMediaFrame` with them.
 */
export interface ProofMediaParts {
  imageId: string
  media: MediaKind
  posterImageId: string | null
  insetImageId: string | null
}

/**
 * The four, from anything that carries them. A response from before #124, or
 * a chat photograph that is not a proof, is a single photograph.
 */
export function proofMediaOf(source: {
  imageId: string
  media?: MediaKind | null
  posterImageId?: string | null
  insetImageId?: string | null
}): ProofMediaParts {
  return {
    imageId: source.imageId,
    media: source.media ?? 'Photo',
    posterImageId: source.posterImageId ?? null,
    insetImageId: source.insetImageId ?? null,
  }
}
