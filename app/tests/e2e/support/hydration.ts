import type { Page } from '@playwright/test'

/**
 * Wait until Vue has taken over the server-rendered page (#123).
 *
 * Until then a field is in the DOM, visible and enabled, so Playwright fills it
 * at once — but no `v-model` listens yet, the input event goes nowhere, and the
 * screen keeps an empty search or an empty form. On a quiet machine hydration
 * wins that race; on a busy one, or in CI, it does not.
 *
 * The signal is Nuxt's own: `window.useNuxtApp` is exposed in the production
 * build too, and `isHydrating` turns false once the last suspended component
 * has hydrated. After that every client navigation renders on the client, so a
 * spec only needs this after a full page load that is followed by typing.
 */
export async function waitForHydration(page: Page) {
  await page.waitForFunction(() => {
    const nuxt = window as unknown as { useNuxtApp?: () => { isHydrating: boolean } }

    try {
      return nuxt.useNuxtApp?.().isHydrating === false
    }
    catch {
      // The app instance is not set up yet.
      return false
    }
  })
}

/** `page.goto`, then {@link waitForHydration}: a full load you can type into. */
export async function gotoHydrated(page: Page, url: string) {
  await page.goto(url)
  await waitForHydration(page)
}
