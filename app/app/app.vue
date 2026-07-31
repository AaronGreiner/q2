<script setup lang="ts">
import { languageKeys } from '~/i18n/messages'

/**
 * UApp provides the overlay and toast context Nuxt UI components expect and
 * must wrap the whole application.
 *
 * Preferences are loaded here rather than in a layout, because both layouts
 * need the language and the theme and neither should have to remember to ask.
 */
const { settings } = useAppSettings()
const language = useLanguage()
const t = useMessages()

useHead({
  titleTemplate: title => (title ? `${title} · Kudos` : 'Kudos (q2)'),
  htmlAttrs: { lang: computed(() => languageKeys[language.value]) },
  meta: [
    { name: 'description', content: () => t.value.app.description },

    // The app is packaged with Capacitor and runs behind a notch. `cover` lets
    // the layout paint into the safe areas, which the shell then pads back out
    // with env(safe-area-inset-*).
    { name: 'viewport', content: 'width=device-width, initial-scale=1, viewport-fit=cover' },
    { name: 'color-scheme', content: 'light dark' },
  ],
})

// Read so the settings request is not tree-shaken away in a build where
// nothing else references it.
void settings
</script>

<template>
  <UApp :toaster="{ position: 'bottom-center', expand: false }">
    <NuxtLayout>
      <NuxtPage />
    </NuxtLayout>
  </UApp>
</template>
