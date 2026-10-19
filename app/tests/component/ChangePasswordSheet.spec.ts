import { mockNuxtImport, mountSuspended } from '@nuxt/test-utils/runtime'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { ApiError } from '~/api/errors'
import ChangePasswordSheet from '~/components/settings/ChangePasswordSheet.vue'

const { changePassword } = vi.hoisted(() => ({ changePassword: vi.fn() }))

mockNuxtImport('useSession', () => () => ({ changePassword }))

afterEach(() => {
  changePassword.mockReset()
  document.body.replaceChildren()
})

function element<T extends HTMLElement = HTMLElement>(selector: string): T {
  const found = document.querySelector<T>(`${selector} input, ${selector} button, ${selector}`)
  if (!found) throw new Error(`nothing matches ${selector}`)
  return found
}

function type(input: HTMLInputElement, value: string) {
  input.value = value
  input.dispatchEvent(new Event('input'))
}

/**
 * Changing the password while signed in (#40). The rules — the current
 * password, the policy, the lockout — are the server's and are asserted there
 * (`PasswordChangeEndpointTests`); this is about what the sheet says and when.
 */
describe('ChangePasswordSheet', () => {
  it('says what happens to the other devices before anything is typed', async () => {
    const wrapper = await mountSuspended(ChangePasswordSheet, { props: { open: true }, attachTo: document.body })

    expect(document.body.textContent).toContain('Du bleibst hier angemeldet, alle anderen Geräte werden abgemeldet.')
    expect(element<HTMLButtonElement>('[data-testid="change-password-submit"]').disabled).toBe(true)

    wrapper.unmount()
  })

  it('sends the current and the new password', async () => {
    changePassword.mockResolvedValue(undefined)
    const wrapper = await mountSuspended(ChangePasswordSheet, { props: { open: true }, attachTo: document.body })

    type(element<HTMLInputElement>('[data-testid="change-password-current"]'), 'das-alte-passwort')
    type(element<HTMLInputElement>('[data-testid="change-password-new"]'), 'ein-ganz-neues-passwort')
    await wrapper.vm.$nextTick()

    element<HTMLButtonElement>('[data-testid="change-password-submit"]').click()
    await vi.waitFor(() => expect(changePassword).toHaveBeenCalledWith('das-alte-passwort', 'ein-ganz-neues-passwort'))
    await vi.waitFor(() => expect(wrapper.emitted('update:open')?.at(-1)).toEqual([false]))

    wrapper.unmount()
  })

  /** A wrong password is the person's own answer: said under its field, from the code. */
  it('words a wrong current password under its field', async () => {
    changePassword.mockRejectedValue(new ApiError({
      kind: 'validation',
      status: 400,
      fieldErrors: { CurrentPassword: ['That password is not correct.'] },
      fieldCodes: { CurrentPassword: [{ code: 'wrongPassword' }] },
    }))
    const wrapper = await mountSuspended(ChangePasswordSheet, { props: { open: true }, attachTo: document.body })

    type(element<HTMLInputElement>('[data-testid="change-password-current"]'), 'falsch')
    type(element<HTMLInputElement>('[data-testid="change-password-new"]'), 'ein-ganz-neues-passwort')
    await wrapper.vm.$nextTick()

    element<HTMLButtonElement>('[data-testid="change-password-submit"]').click()

    await vi.waitFor(() => expect(document.body.textContent).toContain('Das Passwort stimmt nicht.'))
    expect(wrapper.emitted('update:open')).toBeUndefined()

    wrapper.unmount()
  })
})
