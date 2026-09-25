import { computed, nextTick, ref, shallowRef, watch } from 'vue'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { useImageSource } from '~/composables/useImageSource'
import { createObjectUrlCache } from '~/utils/objectUrlCache'

beforeEach(() => {
  vi.restoreAllMocks()
  vi.unstubAllGlobals()
  vi.stubGlobal('computed', computed)
  vi.stubGlobal('watch', watch)
  vi.stubGlobal('shallowRef', shallowRef)
  vi.stubGlobal('useRuntimeConfig', () => ({ public: { apiBaseUrl: 'https://api.example.test' } }))
})

describe('the source of a stored picture', () => {
  it('is the image address in the browser, where the img carries the cookie', () => {
    vi.stubGlobal('useNuxtApp', () => ({}))
    const id = ref<string | null>('abc')

    const source = useImageSource(() => id.value)
    expect(source.value).toBe('https://api.example.test/api/images/abc')

    id.value = null
    expect(source.value).toBeNull()
  })

  it('is fetched with the token in the iOS app, and empty until it arrives', async () => {
    const cache = createObjectUrlCache({ create: () => 'blob:abc', revoke: vi.fn() })
    let deliver: (blob: Blob) => void = () => undefined
    const load = vi.fn(() => new Promise<Blob>((resolve) => {
      deliver = resolve
    }))
    vi.stubGlobal('useNuxtApp', () => ({ $imageCache: cache }))
    vi.stubGlobal('useQ2Api', () => ({ images: { load } }))

    const source = useImageSource(() => 'abc')

    // A transparent pixel while loading, not the "no picture" state.
    expect(source.value).toMatch(/^data:image\/gif/)
    expect(load).toHaveBeenCalledWith('abc')

    deliver(new Blob(['bytes']))
    await vi.waitFor(() => expect(source.value).toBe('blob:abc'))
  })

  it('hands the img something that fails when the picture cannot be fetched', async () => {
    const cache = createObjectUrlCache({ revoke: vi.fn() })
    vi.stubGlobal('useNuxtApp', () => ({ $imageCache: cache }))
    vi.stubGlobal('useQ2Api', () => ({ images: { load: vi.fn().mockRejectedValue(new Error('404')) } }))

    const source = useImageSource(() => 'gone')

    await vi.waitFor(() => expect(source.value).toBe('data:,'))
  })

  it('never shows the previous picture for a new one', async () => {
    const cache = createObjectUrlCache({ create: blob => `blob:${blob.size}`, revoke: vi.fn() })
    const pending = new Map<string, (blob: Blob) => void>()
    const load = vi.fn((id: string) => new Promise<Blob>((resolve) => {
      pending.set(id, resolve)
    }))
    vi.stubGlobal('useNuxtApp', () => ({ $imageCache: cache }))
    vi.stubGlobal('useQ2Api', () => ({ images: { load } }))
    const id = ref<string | null>('first')

    const source = useImageSource(() => id.value)
    id.value = 'second'
    await nextTick()

    // The first answer arrives after the id moved on: it must not be shown.
    pending.get('first')!(new Blob(['1']))
    await Promise.resolve()
    expect(source.value).toMatch(/^data:image\/gif/)

    pending.get('second')!(new Blob(['22']))
    await vi.waitFor(() => expect(source.value).toBe('blob:2'))

    id.value = null
    expect(source.value).toBeNull()
  })
})
