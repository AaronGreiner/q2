import { describe, expect, it } from 'vitest'
import { de, en } from '~/i18n/messages'
import { browserMessages, pushMessages } from '~/utils/pushLanguage'

/**
 * Which language the service worker writes a push in (#39). The worker itself
 * is not unit-testable — it is a script for `self` — so its one decision lives
 * here and is tested on its own.
 */
describe('pushMessages', () => {
  it('writes in the language chosen in q2, whatever the browser says', () => {
    expect(pushMessages('English', 'de-DE')).toBe(en)
    expect(pushMessages('German', 'en-US')).toBe(de)
  })

  it('falls back to the browser only when the payload does not say', () => {
    expect(pushMessages(null, 'en-GB')).toBe(en)
    expect(pushMessages(undefined, 'de-AT')).toBe(de)
  })
})

describe('browserMessages', () => {
  it('reads English from any English locale and German from everything else', () => {
    expect(browserMessages('EN-us')).toBe(en)
    expect(browserMessages('fr-FR')).toBe(de)
  })
})
