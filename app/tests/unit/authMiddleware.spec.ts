import { computed, ref } from 'vue'
import { beforeEach, describe, expect, it, vi } from 'vitest'

/**
 * The route guard: who may see which screen.
 *
 * What is pinned down here is the introduction (docs/adr/0039-first-run-onboarding.md):
 * somebody signed in who has not been through it is taken there from every
 * screen, somebody who has is never shown it again, and the open routes and
 * invite links stay reachable for both — a reset link has to work, and a link
 * somebody was sent is the reason they are here.
 */
interface Route {
  path: string
  fullPath: string
}

function route(path: string): Route {
  return { path, fullPath: path }
}

let session: { isOnboarded: boolean } | null

const navigateTo = vi.fn((target: unknown) => ({ redirectedTo: target }))

beforeEach(() => {
  vi.resetModules()
  vi.unstubAllGlobals()
  navigateTo.mockClear()
  session = null

  vi.stubGlobal('defineNuxtRouteMiddleware', (middleware: (to: Route) => unknown) => middleware)
  vi.stubGlobal('navigateTo', navigateTo)
  vi.stubGlobal('useSession', () => {
    const state = ref(session)
    return {
      isSignedIn: computed(() => state.value !== null),
      isOnboarded: computed(() => state.value?.isOnboarded ?? true),
      resolve: vi.fn().mockResolvedValue(state.value),
    }
  })
})

async function guard() {
  return (await import('~/middleware/auth.global')).default as unknown as (to: Route) => Promise<unknown>
}

describe('the route guard', () => {
  it('takes somebody who has not been through the introduction there, from anywhere', async () => {
    session = { isOnboarded: false }
    const middleware = await guard()

    for (const path of ['/', '/goals', '/chats/abc', '/settings']) {
      await middleware(route(path))
    }

    expect(navigateTo.mock.calls).toEqual([['/onboarding'], ['/onboarding'], ['/onboarding'], ['/onboarding']])
  })

  it('lets them stay on it', async () => {
    session = { isOnboarded: false }

    await expect((await guard())(route('/onboarding'))).resolves.toBeUndefined()
    expect(navigateTo).not.toHaveBeenCalled()
  })

  it('never shows it again once it is over', async () => {
    session = { isOnboarded: true }
    const middleware = await guard()

    await middleware(route('/onboarding'))
    await expect(middleware(route('/goals'))).resolves.toBeUndefined()

    expect(navigateTo.mock.calls).toEqual([['/']])
  })

  it('keeps reset and invite links reachable before it is done', async () => {
    session = { isOnboarded: false }
    const middleware = await guard()

    await middleware(route('/reset-password'))
    await middleware(route('/diagnostics'))
    await middleware(route('/join/abc123'))

    expect(navigateTo).not.toHaveBeenCalled()
  })

  it('sends somebody signed out to the sign-in screen, remembering where they were going', async () => {
    const middleware = await guard()

    await middleware(route('/onboarding'))

    expect(navigateTo).toHaveBeenCalledWith({ path: '/login', query: { next: '/onboarding' } })
  })
})
