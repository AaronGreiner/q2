import { languageKeys, messages, type Messages } from '~/i18n/messages'
import type { LanguagePreference } from '~/api/types'

/**
 * The language the interface is in.
 *
 * `useState` rather than a module-level ref: on the server one process answers
 * every request, and a shared ref would let one visitor's language leak into
 * the next one's page. It is also what carries the choice across hydration, so
 * the language the server rendered is still the language a moment later.
 *
 * It starts as the language this device last showed (`q2-language`, below), so
 * the server renders English for somebody who chose English rather than German
 * that turns English after hydration (#85). German is the default for a device
 * that has never been told anything — the language q2 is designed in. The
 * stored preference (useAppSettings) is the answer either way, and replaces
 * this as soon as it is read.
 */
export function useLanguage() {
  return useState<LanguagePreference>('q2:language', () => rememberedLanguage(useLanguageCookie().value))
}

const rememberedKey = 'q2-language'

/**
 * Where the last language is kept on this device: a cookie, because the server
 * has to read it before it renders anything — the same reason as `q2-accent`.
 * A year, `SameSite=Lax`, the name of a language and nothing else
 * (docs/privacy.md).
 */
export function useLanguageCookie() {
  return useCookie<string | null>(rememberedKey, {
    default: () => null,
    maxAge: 60 * 60 * 24 * 365,
    sameSite: 'lax',
    path: '/',
  })
}

/**
 * The remembered language, from the cookie or — in the iOS app, where nothing
 * is rendered on a server and a WebView keeps no cookie for the app's own
 * scheme — from local storage. Anything unrecognised is German.
 */
export function rememberedLanguage(cookie: string | null | undefined): LanguagePreference {
  let value = cookie
  if (!value && import.meta.client) {
    try {
      value = globalThis.localStorage?.getItem(rememberedKey)
    }
    catch {
      // Refused storage only costs the head start; the settings still arrive.
    }
  }
  return value === 'English' ? 'English' : 'German'
}

/** Keeps the language on this device for the next first paint, in both places. */
export function rememberLanguage(cookie: Ref<string | null>, language: LanguagePreference): void {
  cookie.value = language
  if (!import.meta.client) return
  try {
    globalThis.localStorage?.setItem(rememberedKey, language)
  }
  catch {
    // As above: the cookie, or the settings, carry it instead.
  }
}

/**
 * Every user-facing string, in the current language.
 *
 * Used as `const t = useMessages()` and then `t.home.todayHeading`. Components
 * never contain literal user-facing text — see app/i18n/messages.ts for why.
 */
export function useMessages(): ComputedRef<Messages> {
  const language = useLanguage()
  return computed(() => messages[languageKeys[language.value]])
}
