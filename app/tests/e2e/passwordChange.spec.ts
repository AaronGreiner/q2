import { expect, test } from '@playwright/test'
import { apiBaseUrl } from './support/e2eEnvironment'
import { gotoHydrated } from './support/hydration'

/**
 * Changing the password from the settings (#40).
 *
 * On an account made for the test, never a seeded one: a changed password
 * ends every other session of the account within a minute, and the seeded
 * person's session is the one every other spec runs in.
 *
 * Who is signed out and what counts towards the lockout is decided on the
 * server and asserted there (`PasswordChangeEndpointTests`). What only a
 * browser shows is the sheet: a wrong password said under its field, and the
 * right one taking effect while this page stays signed in.
 */
test.use({ storageState: { cookies: [], origins: [] } })

const password = 'ein-langes-passwort'
const newPassword = 'ein-ganz-neues-passwort'

test('a password is changed in the settings, and the new one is the one that signs in', async ({ browser }, testInfo) => {
  const unique = `${Date.now()}${Math.floor(Math.random() * 1000)}`
  const email = `e2e.password.${unique}@kudos.example`

  const context = await browser.newContext({ ...testInfo.project.use, storageState: { cookies: [], origins: [] } })

  try {
    const registered = await context.request.post(`${apiBaseUrl}/api/auth/register`, {
      data: { name: `E2E Password ${unique}`, email, password },
    })
    expect(registered.ok(), `registering ${email}`).toBe(true)
    expect((await context.request.post(`${apiBaseUrl}/api/onboarding/complete`)).ok()).toBe(true)

    const page = await context.newPage()
    await gotoHydrated(page, '/settings')

    await page.getByTestId('settings-change-password').click()
    const form = page.getByTestId('change-password-form')
    await expect(page.getByRole('dialog')).toContainText('alle anderen Geräte werden abgemeldet')

    // A wrong current password is said under its field, and nothing changes.
    await page.getByTestId('change-password-current').fill('nicht-das-passwort')
    await page.getByTestId('change-password-new').fill(newPassword)
    await page.getByTestId('change-password-submit').click()
    await expect(form).toContainText('Das Passwort stimmt nicht.')

    await page.getByTestId('change-password-current').fill(password)
    await page.getByTestId('change-password-submit').click()
    await expect(form).toHaveCount(0)

    // This page is still signed in…
    await page.reload()
    await expect(page.getByTestId('settings-change-password')).toBeVisible()

    // …and from now on only the new password opens the account.
    const signingIn = await browser.newContext({ ...testInfo.project.use, storageState: { cookies: [], origins: [] } })

    try {
      const withTheOld = await signingIn.request.post(`${apiBaseUrl}/api/auth/login`, { data: { email, password } })
      expect(withTheOld.status()).toBe(401)

      const withTheNew = await signingIn.request.post(`${apiBaseUrl}/api/auth/login`, { data: { email, password: newPassword } })
      expect(withTheNew.ok(), 'signing in with the new password').toBe(true)
    }
    finally {
      await signingIn.close()
    }
  }
  finally {
    await context.close()
  }
})
