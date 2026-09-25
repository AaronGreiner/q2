import { imageUrl } from '~/api/images'

/**
 * What an `<img>` about to be painted in place of a picture that is still on
 * its way: a transparent pixel, so the frame stays empty rather than showing
 * the "no picture" state for a picture that is merely loading.
 */
const loadingImage = 'data:image/gif;base64,R0lGODlhAQABAAAAACH5BAEKAAEALAAAAAABAAEAAAICTAEAOw=='

/**
 * What an `<img>` gets for a picture that could not be fetched: an address
 * that is not an image, so its `error` event fires and the component falls
 * back exactly as it does in the browser.
 */
const brokenImage = 'data:,'

/**
 * The `src` for a stored picture, or null when there is none.
 *
 * In the browser this is `imageUrl` and nothing more: the `<img>` carries the
 * session cookie itself. In the iOS app an `<img>` cannot carry the bearer
 * token, so the picture is fetched with it and shown from memory
 * (`plugins/native.client.ts`, docs/adr/0034-bearer-tokens-for-the-native-app.md).
 * Components do not need to know which: they bind the result and keep their
 * own `@error` handling.
 */
export function useImageSource(id: () => string | null | undefined): ComputedRef<string | null> {
  const { public: config } = useRuntimeConfig()
  const cache = useNuxtApp().$imageCache ?? null

  if (!cache) {
    return computed(() => {
      const value = id()
      return value ? imageUrl(config.apiBaseUrl, value) : null
    })
  }

  const api = useQ2Api()
  const loaded = shallowRef<{ id: string, src: string } | null>(null)

  watch(id, async (value) => {
    if (!value) return

    let src: string

    try {
      src = await cache.get(value, () => api.images.load(value))
    }
    catch {
      // Not reported here: the component's `@error` is what decides whether
      // a missing picture is worth a word, as it does in the browser.
      src = brokenImage
    }

    if (id() === value) loaded.value = { id: value, src }
  }, { immediate: true })

  return computed(() => {
    const value = id()
    if (!value) return null

    return loaded.value?.id === value ? loaded.value.src : loadingImage
  })
}
