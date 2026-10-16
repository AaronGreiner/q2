import { mountSuspended } from '@nuxt/test-utils/runtime'
import { describe, expect, it } from 'vitest'
import AppPasswordInput from '~/components/ui/AppPasswordInput.vue'

/**
 * Every password field in q2 (#114).
 *
 * What matters is that what was typed can be seen and hidden again, by a
 * button with one name that says whether it is pressed, and that the field's
 * own attributes still land on the field — that is where the tests and
 * password managers look for them.
 */
describe('AppPasswordInput', () => {
  it('hides the password until the button is pressed, and hides it again', async () => {
    const wrapper = await mountSuspended(AppPasswordInput, {
      props: { 'modelValue': 'geheim-genug', 'onUpdate:modelValue': () => undefined },
      attrs: { 'autocomplete': 'current-password', 'data-testid': 'login-password' },
    })
    const field = wrapper.get('input[data-testid="login-password"]')
    const reveal = wrapper.get('[data-testid="password-reveal"]')

    expect(field.attributes('type')).toBe('password')
    expect(field.attributes('autocomplete')).toBe('current-password')
    expect(reveal.attributes('type')).toBe('button')
    expect(reveal.attributes('aria-label')).toBe('Passwort anzeigen')
    expect(reveal.attributes('aria-pressed')).toBe('false')

    await reveal.trigger('click')

    expect(field.attributes('type')).toBe('text')
    expect(reveal.attributes('aria-pressed')).toBe('true')
    expect(reveal.attributes('aria-label')).toBe('Passwort anzeigen')

    await reveal.trigger('click')

    expect(field.attributes('type')).toBe('password')
    expect(reveal.attributes('aria-pressed')).toBe('false')
  })

  it('keeps the keyboard from correcting a revealed password', async () => {
    const wrapper = await mountSuspended(AppPasswordInput, {
      props: { 'modelValue': '', 'onUpdate:modelValue': () => undefined },
    })
    const field = wrapper.get('input')

    expect(field.attributes('autocapitalize')).toBe('none')
    expect(field.attributes('autocorrect')).toBe('off')
    expect(field.attributes('spellcheck')).toBe('false')
  })
})
