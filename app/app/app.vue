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

// Both layouts size themselves off --q2-viewport-height, so the one place that
// wraps both is where the keyboard has to be watched.
useKeyboardViewport()

useHead({
  titleTemplate: title => (title ? `${title} · Kudos` : 'Kudos (q2)'),
  htmlAttrs: { lang: computed(() => languageKeys[language.value]) },
  meta: [
    { name: 'description', content: () => t.value.app.description },

    /*
     * The app is packaged with Capacitor and runs behind a notch. `cover` lets
     * the layout paint into the safe areas, which the shell then pads back out
     * with env(safe-area-inset-*).
     *
     * `maximum-scale=1, user-scalable=no` turns off pinch-zoom. Safari ignores
     * it in a browser tab and honours it once q2 is installed, which is the
     * case it is here for; `touch-action` in main.css covers the rest. It
     * costs a person the ability to magnify a screen they cannot read — see
     * docs/adr/0013-app-like-input.md.
     */
    { name: 'viewport', content: 'width=device-width, initial-scale=1, maximum-scale=1, user-scalable=no, viewport-fit=cover' },
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

    // iOS reads its own spelling of the above.
    { name: 'apple-mobile-web-app-capable', content: 'yes' },

    /*
     * Deliberately NOT 'black-translucent', which is the setting that lets a
     * page paint under the status bar.
     *
     * Installed, black-translucent makes WebKit lay out the first frame as if
     * the status bar were opaque and only correct itself a moment later, once
     * it applies the full-screen layout. Measured on an iPhone 17 Pro
     * (874pt tall) in a home-screen web app: at first paint innerHeight,
     * 100dvh, 100svh, 100lvh and visualViewport.height all read 812 — short by
     * exactly the 62pt status bar — and only then jump to 874. Nothing readable
     * from CSS or JS has the right number before that, so an `h-dvh` shell
     * opens 62pt too short and the tab bar visibly snaps down to the bottom of
     * the screen on the first scroll. -webkit-fill-available is not a way out:
     * it read 874 on one launch and 812 on the next, and went stale afterwards.
     *
     * With the status bar left opaque the viewport is 812 from the first frame
     * and stays there, so there is nothing to snap. The look survives: iOS
     * tints the status bar with the page's own background colour and picks a
     * contrasting time and battery on top of it, so the bar follows --ui-bg
     * into dark mode by itself — which is all black-translucent was buying.
     *
     * viewport-fit=cover above stays: env(safe-area-inset-bottom) is still what
     * keeps the tab bar off the home indicator, and Android still needs it.
     */
    { name: 'apple-mobile-web-app-status-bar-style', content: 'default' },
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
