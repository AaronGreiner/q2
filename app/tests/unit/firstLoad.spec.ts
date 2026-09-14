import { reactive } from 'vue'
import { describe, expect, it } from 'vitest'
import { isFirstLoad, placeholder } from '~/utils/firstLoad'

/**
 * A skeleton is for a screen's first read only. Every read after it — a live
 * refresh above all — keeps what is on screen until the answer replaces it.
 */
describe('isFirstLoad', () => {
  it('is loading while a first read has nothing to show', () => {
    expect(isFirstLoad('pending', placeholder({ items: [] }))).toBe(true)
  })

  it('keeps what a screen has while it reads again', () => {
    expect(isFirstLoad('pending', { items: ['an earlier answer'] })).toBe(false)
  })

  it('counts an empty answer as an answer', () => {
    expect(isFirstLoad('pending', { items: [] })).toBe(false)
  })

  it('is done once the read is', () => {
    expect(isFirstLoad('success', placeholder({ items: [] }))).toBe(false)
    expect(isFirstLoad('error', placeholder({ items: [] }))).toBe(false)
  })

  it('sees through a reactive wrapper', () => {
    expect(isFirstLoad('pending', reactive(placeholder({ items: [] })))).toBe(true)
  })

  it('does not copy the initial marker into a replacement payload', () => {
    const initial = placeholder({ items: [] as string[] })
    expect(isFirstLoad('pending', { ...initial, items: ['new'] })).toBe(false)
    expect(JSON.stringify(initial)).toBe('{"items":[]}')
  })
})
