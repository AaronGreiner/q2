import { readdirSync, readFileSync } from 'node:fs'
import { join } from 'node:path'
import { expect, test } from '@playwright/test'
import { mailDirectory, recoveringAccount } from './support/e2eEnvironment'

/**
 * The way back into an account whose password is gone.
 *
 * Signed out, like the authentication spec, and on an account no other spec
 * signs in as: a reset ends every session of the account it is for. Nothing is
 * mailed anywhere — the API writes each mail into this run's directory, and the
 * spec reads the link out of it the way a person reads it out of their inbox.
 */
test.use({ storageState: { cookies: [], origins: [] } })

const newPassword = 'ein-ganz-neues-passwort'

/** The reset link in the newest mail to `address`, or null while there is none. */
function newestResetLink(address: string): string | null {
  let names: string[]

  try {
    names = readdirSync(mailDirectory).filter(name => name.endsWith('.eml')).sort().reverse()
  }
  catch {
    // Not there yet: the API creates the directory with the first mail.
    return null
  }

  for (const name of names) {
    const mail = readFileSync(join(mailDirectory, name), 'utf8')

    if (mail.includes(address)) {
      return /https?:\/\/\S+\/reset-password#token=[\w.-]+/.exec(mail)?.[0] ?? null
    }
  }

  return null
}

/** Waits for the API's background worker to have written the mail. */
async function resetLinkFor(address: string): Promise<string> {
  await expect.poll(() => newestResetLink(address), { timeout: 15_000 }).not.toBeNull()

  const link = newestResetLink(address)

  if (!link) {
    throw new Error(`No reset mail to ${address} in ${mailDirectory}`)
  }

  return link
}

test('a forgotten password is replaced through the link in the mail', async ({ page }) => {
  await page.goto('/login')
  await page.getByTestId('login-email').fill(recoveringAccount.email)
  await page.getByTestId('to-forgot-password').click()

  // What was typed on the sign-in screen came along.
  await expect(page).toHaveURL('/forgot-password')
  await expect(page.getByTestId('forgot-email')).toHaveValue(recoveringAccount.email)

  await page.getByTestId('forgot-submit').click()
  await expect(page.getByTestId('forgot-sent')).toBeVisible()

  const link = await resetLinkFor(recoveringAccount.email)
  await page.goto(link)

  // The token is out of the address bar before anything on the page could
  // have recorded it.
  await expect(page).toHaveURL('/reset-password')
  await expect(page.getByTestId('reset-form')).toBeVisible()

  await page.getByTestId('reset-password').fill(newPassword)
  await page.getByTestId('reset-submit').click()
  await expect(page.getByTestId('reset-to-login')).toBeVisible()

  // The sign-in screen already knows who is coming back.
  await page.getByTestId('reset-to-login').click()
  await expect(page.getByTestId('login-email')).toHaveValue(recoveringAccount.email)

  await page.getByTestId('login-password').fill(newPassword)
  await page.getByTestId('login-submit').click()
  await expect(page).toHaveURL('/')

  // The same link a second time is refused rather than replayed.
  await page.goto(link)
  await page.getByTestId('reset-password').fill('noch-ein-anderes-passwort')
  await page.getByTestId('reset-submit').click()
  await expect(page.getByTestId('reset-invalid')).toBeVisible()
})

test('an address nobody has gets exactly the same answer', async ({ page }) => {
  await page.goto('/forgot-password')

  await page.getByTestId('forgot-email').fill('nobody@kudos.example')
  await page.getByTestId('forgot-submit').click()

  // Nothing on this screen may tell the two apart.
  await expect(page.getByTestId('forgot-sent')).toBeVisible()
})

test('a link without a token says so instead of offering a form', async ({ page }) => {
  await page.goto('/reset-password')

  await expect(page.getByTestId('reset-invalid')).toBeVisible()
  await expect(page.getByTestId('reset-form')).toHaveCount(0)
})

test('a link opened while the page is already showing is taken up as well', async ({ page }) => {
  await page.goto('/reset-password')
  await expect(page.getByTestId('reset-invalid')).toBeVisible()

  // Only the fragment changes, which the browser treats as the same page: no
  // reload, so no inline script. The page has to notice by itself, and still
  // take the token out of the address bar.
  await page.goto('/reset-password#token=abc.def')

  await expect(page.getByTestId('reset-form')).toBeVisible()
  await expect(page).toHaveURL('/reset-password')
})
