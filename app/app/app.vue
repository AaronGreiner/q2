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

    /*
     * Installed, q2 has no browser chrome of its own, so these describe the
     * window the operating system draws around it.
     *
     * These two carry a media query the web app manifest cannot: it holds one
     * colour, used before anything has rendered, while these follow the scheme
     * the app is actually showing. Without them a dark screen sits under a
     * white status bar.
     */
    { name: 'theme-color', content: themeColors.light, media: '(prefers-color-scheme: light)' },
    { name: 'theme-color', content: themeColors.dark, media: '(prefers-color-scheme: dark)' },
    { name: 'mobile-web-app-capable', content: 'yes' },

    // iOS reads its own spelling of the above. 'black-translucent' is what lets
    // the layout paint under the status bar, which viewport-fit=cover asked for.
    { name: 'apple-mobile-web-app-capable', content: 'yes' },
    { name: 'apple-mobile-web-app-status-bar-style', content: 'black-translucent' },
    { name: 'apple-mobile-web-app-title', content: 'Kudos' },
  ],

  /*
   * The icon, everywhere it is asked for.
   *
   * `@vite-pwa/nuxt` writes manifest.webmanifest but cannot inject a link into
   * a document Nuxt renders, so the link belongs here — and without it nothing
   * offers to install the app.
   */
  link: [
    { rel: 'manifest', href: '/manifest.webmanifest' },
    { rel: 'icon', type: 'image/svg+xml', href: '/favicon.svg' },

    // For the browsers that ignore an SVG favicon. Requested by every browser
    // at /favicon.ico whether or not it is declared, so declaring it is free.
    { rel: 'icon', type: 'image/x-icon', sizes: '32x32', href: '/favicon.ico' },

    // iOS uses this one for the home screen and applies its own rounded mask.
    { rel: 'apple-touch-icon', sizes: '180x180', href: '/apple-touch-icon.png' },
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
