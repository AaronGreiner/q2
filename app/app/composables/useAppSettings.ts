import type { LanguagePreference, Settings, ThemePreference, UpdateSettingsRequest } from '~/api/types'

/**
 * Light or dark, and how to change it.
 *
 * Nuxt UI ships `@nuxtjs/color-mode`, which already owns the `.dark` class, the
 * no-flash inline script and the local persistence. This only translates
 * between its lowercase strings and the API's enum, so that exactly one of the
 * two vocabularies appears in any given file.
 */
export function useTheme() {
  const colorMode = useColorMode()

  const preference = computed<ThemePreference>({
    get: () => colorMode.preference === 'dark'
      ? 'Dark'
      : colorMode.preference === 'light' ? 'Light' : 'System',
    set: (value) => {
      colorMode.preference = value === 'Dark' ? 'dark' : value === 'Light' ? 'light' : 'system'
    },
  })

  const isDark = computed(() => colorMode.value === 'dark')

  /** The header's one-tap switch: whatever it looks like now, make it the other. */
  function toggle() {
    preference.value = isDark.value ? 'Light' : 'Dark'
  }

  return { preference, isDark, toggle }
}

/**
 * The signed-in person's preferences, loaded once and applied to the app.
 *
 * Theme and language are applied locally the moment they change and written to
 * the server afterwards. Waiting for the round trip would mean tapping "Dunkel"
 * and watching a white screen think about it; and the server is where a
 * preference is *remembered*, not where it is decided.
 *
 * Loaded under a shared `useAsyncData` key, so the settings screen and the
 * layout share one request rather than making two.
 *
 * The language is applied inside the read as well as by the watcher: during
 * server rendering a watcher never fires again, and the read is awaited before
 * the page renders (it is prefetched from app.vue). That is what makes the
 * server paint English for an English account on a device that has no
 * remembered language yet (#85). Every language applied is remembered on the
 * device for the next first paint (`rememberLanguage`).
 */
export function useAppSettings() {
  const api = useQ2Api()
  const { report } = useErrorReporter()
  const theme = useTheme()
  const language = useLanguage()
  const remembered = useLanguageCookie()

  function applyLanguage(value: LanguagePreference) {
    language.value = value
    rememberLanguage(remembered, value)
  }

  const { data, refresh } = useAsyncData<Settings | null>(
    'settings',
    async () => {
      try {
        const settings = await api.settings.get()
        if (settings.language) applyLanguage(settings.language)
        return settings
      }
      catch (caught) {
        // Preferences are not worth an error screen: without them the app runs
        // on its defaults, which is exactly what a new person would see.
        report(caught, { feature: 'settings', action: 'load' })
        return null
      }
    },
    { default: () => null },
  )

  // Applied as soon as the answer arrives, and again if it is refreshed.
  watch(data, (settings) => {
    if (!settings) return
    // Never null from here; the generated union carries the null of a push's
    // language, which is optional (see `LanguagePreference` in api/types.ts).
    if (settings.language) applyLanguage(settings.language)
    theme.preference.value = settings.theme
  }, { immediate: true })

  const isSaving = ref(false)

  async function update(change: UpdateSettingsRequest) {
    if (change.theme) theme.preference.value = change.theme
    if (change.language) applyLanguage(change.language)

    isSaving.value = true
    try {
      data.value = await api.settings.update(change)
    }
    catch (caught) {
      report(caught, { feature: 'settings', action: 'update' })
    }
    finally {
      isSaving.value = false
    }
  }

  return { settings: data, refresh, update, isSaving }
}
