import { languageKeys, messages, type Messages } from '~/i18n/messages'
import type { LanguagePreference } from '~/api/types'

/**
 * The language the interface is in.
 *
 * `useState` rather than a module-level ref: on the server one process answers
 * every request, and a shared ref would let one visitor's language leak into
 * the next one's page. It is also what carries the choice across hydration, so
 * the German the server rendered is still German a moment later.
 *
 * German is the default because that is the language q2 is designed in; the
 * stored preference (see useSettings) overrides it as soon as it is read.
 */
export function useLanguage() {
  return useState<LanguagePreference>('q2:language', () => 'German')
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
