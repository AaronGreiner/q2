/** What the full-screen view shows: the picture, and what it is a picture of. */
export interface PhotoViewerItem {
  imageId: string
  /** What the picture answers — a goal's title, or the day's challenge prompt. */
  title?: string | null
  /** Whose it is. */
  subtitle?: string | null
  /** When — a date, or a relative time. */
  meta?: string | null
}

/**
 * The one full-screen photograph.
 *
 * Shared state rather than a viewer per card: every photograph in q2 opens
 * into the same view, and one mounted in the layout (`AppPhotoViewer`) means a
 * feed of thirty cards is not thirty dialogs. A card only says *what* to show;
 * it never fetches anything, so this stays within "components do not load
 * data".
 */
export function usePhotoViewer() {
  const current = useState<PhotoViewerItem | null>('photo-viewer', () => null)

  return {
    current: computed(() => current.value),
    open: (item: PhotoViewerItem) => {
      current.value = item
    },
    close: () => {
      current.value = null
    },
  }
}
