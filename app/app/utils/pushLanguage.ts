import type { LanguagePreference } from '~/api/types'
// Relative rather than `~/`: the service worker imports this file, and its
// build resolves no alias for a value (service-worker/tsconfig.json).
import { de, en, languageKeys, messages, type Messages } from '../i18n/messages'

/**
 * Which catalogue a push is written from.
 *
 * The language the person chose in q2, which the server names in every push
 * (#39). A service worker cannot ask the account, and the browser's language
 * is not the choice: somebody with a German phone who reads q2 in English gets
 * English banners, and has to get English pushes too.
 *
 * The browser's language is the fallback only, for a payload that does not
 * say — one sent by a server from before this, or none at all.
 */
export function pushMessages(language: LanguagePreference | null | undefined, browserLanguage: string): Messages {
  return language ? messages[languageKeys[language]] : browserMessages(browserLanguage)
}

/**
 * The browser's guess, for the one page that really cannot reach the account:
 * the offline page, which is shown precisely when there is no connection.
 * German is the product default.
 */
export function browserMessages(browserLanguage: string): Messages {
  return browserLanguage.toLowerCase().startsWith('en') ? en : de
}
