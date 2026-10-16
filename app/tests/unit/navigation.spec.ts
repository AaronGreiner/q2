import { afterEach, describe, expect, it, vi } from 'vitest'
import { useGoBack } from '~/composables/useGoBack'
import { useReturnState } from '~/composables/useReturnState'
import { restoreScroll } from '~/utils/restoreScroll'

/**
 * Going back to where somebody came from, and finding it the way it was left
 * (#118).
 *
 * The browser history is the whole of the state here, so each test builds the
 * history entry it needs — the router's own fields (`back`, `position`) plus
 * whatever a screen put there — and checks what is read off it and what is
 * written back into it.
 */
afterEach(() => {
  vi.unstubAllGlobals()
})

describe('useGoBack', () => {
  function installRouter(back: string | null) {
    const router = {
      options: { history: { state: { back } } },
      back: vi.fn(),
      replace: vi.fn().mockResolvedValue(undefined),
    }
    vi.stubGlobal('useRouter', () => router)
    return router
  }

  it('steps back through the history when q2 has something to go back to', () => {
    const router = installRouter('/')

    useGoBack()('/goals')

    expect(router.back).toHaveBeenCalledOnce()
    expect(router.replace).not.toHaveBeenCalled()
  })

  it('replaces the screen with its parent when it was the first thing q2 showed', () => {
    // Opened from a notification, a link or a reload: there is no screen of
    // q2's before it, and stepping back would leave the app.
    const router = installRouter(null)

    useGoBack()('/goals')

    expect(router.back).not.toHaveBeenCalled()
    expect(router.replace).toHaveBeenCalledWith('/goals')
  })
})

describe('useReturnState', () => {
  type Guard = () => void

  function installHistory(state: Record<string, unknown>, { isHydrating = false } = {}) {
    const history = {
      state,
      replaceState: vi.fn((next: Record<string, unknown>) => {
        history.state = next
      }),
    }
    const guards: { leave?: Guard, update?: Guard } = {}

    vi.stubGlobal('window', { history })
    vi.stubGlobal('useNuxtApp', () => ({ isHydrating }))
    vi.stubGlobal('onBeforeRouteLeave', (guard: Guard) => (guards.leave = guard))
    vi.stubGlobal('onBeforeRouteUpdate', (guard: Guard) => (guards.update = guard))

    return { history, guards }
  }

  it('has nothing to hand back on a fresh visit', () => {
    installHistory({ position: 3, back: '/' })

    expect(useReturnState('scroll', () => 120)).toBeUndefined()
  })

  it('hands back what the entry remembers when the screen is gone back to', () => {
    installHistory({ 'position': 3, 'back': '/', 'q2:friendSearch': 'Anna' })

    expect(useReturnState('friendSearch', () => undefined)).toBe('Anna')
  })

  it('hands back nothing while hydrating, so the first render matches the server', () => {
    installHistory({ 'position': 3, 'q2:scroll': 480 }, { isHydrating: true })

    expect(useReturnState('scroll', () => 0)).toBeUndefined()
  })

  it('writes into its own entry as the screen is left forwards, keeping the router\'s fields', () => {
    const { history, guards } = installHistory({ position: 3, back: '/', current: '/goals' })
    let scrolled = 0
    useReturnState('scroll', () => scrolled)

    scrolled = 480
    guards.leave!()

    expect(history.replaceState).toHaveBeenCalledOnce()
    expect(history.state).toEqual({ 'position': 3, 'back': '/', 'current': '/goals', 'q2:scroll': 480 })

    // A query change on the same screen is remembered the same way.
    scrolled = 90
    guards.update!()
    expect(history.state).toMatchObject({ 'q2:scroll': 90 })
  })

  it('writes nothing once the browser has already gone back to an earlier entry', () => {
    // By the time the router asks, the current entry is the one being gone
    // back to; writing now would hand this screen's scroll to that screen.
    const { history, guards } = installHistory({ position: 3 })
    useReturnState('scroll', () => 480)

    history.state = { position: 2 }
    guards.leave!()

    expect(history.replaceState).not.toHaveBeenCalled()
    expect(history.state).toEqual({ position: 2 })
  })
})

describe('restoreScroll', () => {
  function installFrames() {
    let now = 0
    const frames: Array<() => void> = []

    vi.stubGlobal('performance', { now: () => now })
    vi.stubGlobal('requestAnimationFrame', (callback: () => void) => frames.push(callback))
    vi.stubGlobal('cancelAnimationFrame', vi.fn())

    return {
      /** Runs the next frame, `elapsed` milliseconds later. */
      tick(elapsed = 16) {
        now += elapsed
        frames.shift()?.()
      },
      get pending() {
        return frames.length
      },
    }
  }

  function scroller(scrollHeight: number) {
    const listeners = new Map<string, () => void>()

    return {
      scrollHeight,
      clientHeight: 700,
      scrollTop: 0,
      listeners,
      addEventListener: vi.fn((type: string, listener: () => void) => listeners.set(type, listener)),
      removeEventListener: vi.fn((type: string) => listeners.delete(type)),
    }
  }

  it('scrolls straight there when the content is already tall enough', () => {
    installFrames()
    const element = scroller(2000)

    restoreScroll(element as unknown as HTMLElement, 480)

    expect(element.scrollTop).toBe(480)
    expect(element.listeners.size).toBe(0)
  })

  it('waits for the content to arrive before scrolling', () => {
    const frames = installFrames()
    // A skeleton, too short to scroll anywhere.
    const element = scroller(700)

    restoreScroll(element as unknown as HTMLElement, 480)
    frames.tick()
    expect(element.scrollTop).toBe(0)

    element.scrollHeight = 2000
    frames.tick()

    expect(element.scrollTop).toBe(480)
    expect(frames.pending).toBe(0)
  })

  it('goes as far as it can once it has waited long enough', () => {
    // The list came back shorter than it was left; the browser clamps the
    // assignment to the end of it.
    const frames = installFrames()
    const element = scroller(900)

    restoreScroll(element as unknown as HTMLElement, 480, 100)
    frames.tick(50)
    expect(element.scrollTop).toBe(0)

    frames.tick(60)

    expect(element.scrollTop).toBe(480)
    expect(frames.pending).toBe(0)
  })

  it('gives up the moment the person starts using the screen', () => {
    installFrames()
    const element = scroller(700)

    restoreScroll(element as unknown as HTMLElement, 480)
    element.listeners.get('touchstart')!()

    expect(cancelAnimationFrame).toHaveBeenCalled()
    expect(element.listeners.size).toBe(0)
    expect(element.scrollTop).toBe(0)
  })

  it('can be abandoned by a screen that is left before it finished', () => {
    installFrames()
    const element = scroller(700)

    const abandon = restoreScroll(element as unknown as HTMLElement, 480)
    abandon()

    expect(cancelAnimationFrame).toHaveBeenCalled()
    expect(element.listeners.size).toBe(0)
  })
})
