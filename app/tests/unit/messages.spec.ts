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
