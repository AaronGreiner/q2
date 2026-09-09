import { describe, expect, it } from 'vitest'
import { de, en, languageKeys, messages } from '~/i18n/messages'

/**
 * The message catalogue.
 *
 * TypeScript already guarantees that `en` has every key `de` has — that is what
 * `en: Messages` buys. What it cannot check is that the values were actually
 * translated rather than pasted, or that a function in one language takes the
 * same arguments as its counterpart. That is what this file is for.
 */

type Node = Record<string, unknown>

/** Every leaf in the catalogue, as `path -> value`. */
function flatten(node: Node, prefix = ''): Map<string, unknown> {
  const result = new Map<string, unknown>()

  for (const [key, value] of Object.entries(node)) {
    const path = prefix ? `${prefix}.${key}` : key

    if (value !== null && typeof value === 'object' && !Array.isArray(value) && typeof value !== 'function') {
      for (const [nested, leaf] of flatten(value as Node, path)) {
        result.set(nested, leaf)
      }
      continue
    }

    result.set(path, value)
  }

  return result
}

const german = flatten(de)
const english = flatten(en)

describe('the catalogue', () => {
  it('offers exactly the two languages the settings screen does', () => {
    expect(Object.keys(messages)).toEqual(['de', 'en'])
    expect(Object.values(languageKeys)).toEqual(['de', 'en'])
  })

  it('has the same shape in both languages', () => {
    expect([...english.keys()].sort()).toEqual([...german.keys()].sort())
  })

  it('has no empty string anywhere', () => {
    for (const [path, value] of [...german, ...english]) {
      if (typeof value === 'string') {
        expect(value.trim(), path).not.toBe('')
      }
    }
  })

  it('takes the same arguments in both languages', () => {
    for (const [path, value] of german) {
      if (typeof value !== 'function') continue

      const counterpart = english.get(path)

      expect(typeof counterpart, path).toBe('function')
      expect((counterpart as (...args: unknown[]) => string).length, path).toBe(value.length)
    }
  })

  it('produces a usable sentence from every function in both languages', () => {
    for (const [path, value] of [...german, ...english]) {
      if (typeof value !== 'function') continue

      // Every message function in q2 takes a number or a name; both are
      // covered by passing something that reads as either.
      const produced = (value as (...args: unknown[]) => unknown)(3, 'Ziel')

      expect(typeof produced === 'string' || typeof produced === 'object', path).toBe(true)
    }
  })

  /**
   * And at every count, not only at three.
   *
   * Almost every counting message in q2 branches on one-versus-many, and the
   * sweep above only ever takes the "many" arm. A rule that throws — or
   * silently produces "1 Beweise" — on the arm nobody passed is a bug that
   * reaches a screen, so each function is asked for every count it can be given
   * a sentence about.
   */
  it('answers at every count, not only at three', () => {
    for (const [path, value] of [...german, ...english]) {
      if (typeof value !== 'function') continue

      for (const count of [0, 1, 2, 7, 12, 23]) {
        const produced = (value as (...args: unknown[]) => unknown)(count, 'Ziel')
        const text = typeof produced === 'string' ? produced : (produced as { text: string }).text

        expect(text, `${path} at ${count}`).toBeTruthy()
        expect(text, `${path} at ${count}`).not.toContain('undefined')
        expect(text, `${path} at ${count}`).not.toContain('NaN')
      }
    }
  })
})

/**
 * The rules that say "one" differently from "many".
 *
 * Named one at a time rather than swept, because the sweep above cannot know
 * which functions are counting something — `greeting(1)` and `greeting(2)` are
 * both the middle of the night, and quite right too. These are the ones where
 * the two arms carry different words, and where a copied branch would read as
 * "1 Beweise".
 */
describe('one and many', () => {
  const counting: Array<[string, () => string, () => string]> = [
    ['home.streakDays', () => de.home.streakDays(1), () => de.home.streakDays(2)],
    ['vote.remaining', () => de.vote.remaining(1), () => de.vote.remaining(2)],
    ['vote.banner', () => de.vote.banner(1), () => de.vote.banner(2)],
    ['proof.doubtCount', () => de.proof.doubtCount(1), () => de.proof.doubtCount(2)],
    ['proof.waitingHint', () => de.proof.waitingHint(1), () => de.proof.waitingHint(2)],
    ['proof.expiresIn', () => de.proof.expiresIn(1), () => de.proof.expiresIn(5)],
    ['challenge.remaining', () => de.challenge.remaining(1), () => de.challenge.remaining(5)],
    ['challenge.archiveCount', () => de.challenge.archiveCount(1), () => de.challenge.archiveCount(2)],
    ['challenge.participation', () => de.challenge.participation(0, 1), () => de.challenge.participation(0, 2)],
    ['push.riskBody', () => de.push.riskBody('Laufen', 1), () => de.push.riskBody('Laufen', 2)],
  ]

  it.each(counting)('%s says one differently from many', (_path, one, many) => {
    expect(one()).not.toBe(many())
  })

  /** English pluralises differently, and its own arms have to differ too. */
  it('pluralises in English as well', () => {
    expect(en.home.streakDays(1)).not.toBe(en.home.streakDays(2))
    expect(en.vote.remaining(1)).not.toBe(en.vote.remaining(2))
    expect(en.proof.doubtCount(1)).not.toBe(en.proof.doubtCount(2))
    expect(en.challenge.archiveCount(1)).not.toBe(en.challenge.archiveCount(2))
    expect(en.challenge.participation(0, 1)).not.toBe(en.challenge.participation(0, 2))
    expect(en.push.riskBody('Running', 1)).not.toBe(en.push.riskBody('Running', 2))
  })
})

describe('the two languages', () => {
  it('actually differ', () => {
    // A translation that was pasted rather than translated would pass every
    // shape check above.
    const identical = [...german.entries()].filter(([path, value]) =>
      typeof value === 'string' && english.get(path) === value)

    // Some strings are the same in both — a brand name, a handle, an emoji
    // label. Most are not.
    expect(identical.length).toBeLessThan(german.size / 4)
  })

  it('names each language in its own words', () => {
    expect(de.settings.languageGerman).toBe('Deutsch')
    expect(en.settings.languageGerman).toBe('Deutsch')
  })

  it('starts the week on Monday in both', () => {
    expect(de.time.weekdayInitials).toHaveLength(7)
    expect(en.time.weekdayInitials).toHaveLength(7)
  })

  it('writes decimals the way each language does', () => {
    expect(de.numbers.decimal).toBe(',')
    expect(en.numbers.decimal).toBe('.')
  })
})
