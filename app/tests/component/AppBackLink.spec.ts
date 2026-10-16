import { mockNuxtImport, mountSuspended } from '@nuxt/test-utils/runtime'
import { describe, expect, it, vi } from 'vitest'
import AppBackLink from '~/components/layout/AppBackLink.vue'

const { goBack } = vi.hoisted(() => ({ goBack: vi.fn() }))

mockNuxtImport('useGoBack', () => () => goBack)

/**
 * The arrow is a link to the screen's parent, and a tap on it goes back
 * instead (#118). Where "back" leads is useGoBack's business and is covered
 * with it; what is checked here is that a tap reaches it and a modified click
 * does not.
 */
describe('AppBackLink', () => {
  it('is a link to the parent, named "Zurück" unless told otherwise', async () => {
    const plain = await mountSuspended(AppBackLink, { props: { to: '/goals' } })
    expect(plain.get('a').attributes('href')).toBe('/goals')
    expect(plain.get('a').attributes('aria-label')).toBe('Zurück')

    const named = await mountSuspended(AppBackLink, { props: { to: '/settings', label: 'Zu den Einstellungen' } })
    expect(named.get('a').attributes('aria-label')).toBe('Zu den Einstellungen')
  })

  it('goes back on a tap, with the parent as the fallback', async () => {
    goBack.mockClear()
    const wrapper = await mountSuspended(AppBackLink, { props: { to: '/goals' } })

    await wrapper.get('a').trigger('click')

    expect(goBack).toHaveBeenCalledWith('/goals')
  })

  it('leaves a modified click to the browser, which opens the address', async () => {
    goBack.mockClear()
    const wrapper = await mountSuspended(AppBackLink, { props: { to: '/goals' } })

    await wrapper.get('a').trigger('click', { metaKey: true })

    expect(goBack).not.toHaveBeenCalled()
  })
})
