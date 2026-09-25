import { mountSuspended } from '@nuxt/test-utils/runtime'
import { describe, expect, it } from 'vitest'
import AuthScreen from '~/components/layout/AuthScreen.vue'
import { q2MarkPath } from '~/utils/q2Mark'

/**
 * The frame of the sign-in and sign-up screens.
 *
 * What is asserted is that the mark on it is the app icon's outline, not a
 * second drawing of its own, and that it stays out of the accessibility tree
 * next to the written name.
 */
describe('AuthScreen', () => {
  it('shows the app icon\'s mark, hidden from screen readers', async () => {
    const wrapper = await mountSuspended(AuthScreen, { props: { heading: 'Willkommen zurück', intro: 'Melde dich an.' } })
    const mark = wrapper.get('[data-testid="q2-mark"]')

    expect(mark.attributes('aria-hidden')).toBe('true')
    expect(mark.get('path').attributes('d')).toBe(q2MarkPath)
    expect(wrapper.text()).toContain('Qdos')
  })
})
