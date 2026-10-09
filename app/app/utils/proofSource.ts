import type { ProofSource } from '~/api/types'

/**
 * Which way a picture came into the camera screen: its shutter, or the phone's
 * pictures.
 *
 * `PhotoCapture` reports this for every picture and stays out of what it means.
 * For a proof it becomes the `ProofSource` friends are shown (#61): a picture
 * from the library is called a screenshot, because that is what one is when a
 * commitment only shows on a screen — and it is the honest word for anything
 * not taken just now.
 */
export type PhotoOrigin = 'camera' | 'library'

export function proofSourceOf(origin: PhotoOrigin): ProofSource {
  return origin === 'camera' ? 'Live' : 'Screenshot'
}

/**
 * One icon per source, so the label is never colour or position alone. Literal
 * names, because the icon bundle is collected by scanning for them.
 */
export const proofSourceIcons: Readonly<Record<ProofSource, string>> = {
  Live: 'i-lucide-camera',
  Screenshot: 'i-lucide-smartphone',
  Unknown: 'i-lucide-image',
}
