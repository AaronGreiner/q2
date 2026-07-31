import { expect, test } from '@playwright/test'
import { e2eAccount } from './support/e2eEnvironment'

/**
 * Getting in, and being kept out.
 *
 * The only spec in the suite that starts signed *out*: everything else reuses
 * the session globalSetup establishes, because the sign-in is not what those
 * tests are about. Here it is.
 */
test.use({ storageState: { cookies: [], origins: [] } })

/** From the E2E seed — see api/.../Seeding/E2ESeed.cs. */
const seeded = {
  me: 'E2E Mara',
  sharedGoal: 'E2E shared goal with participants',
}

test.describe('signed out', () => {
  test('every screen sends you to the sign-in rather than showing an error', async ({ page }) => {
    for (const path of ['/', '/goals', '/chats', '/friends', '/profile']) {
      await page.goto(path)
      await expect(page.getByTestId('login-form')).toBeVisible()
    }
  })

  test('where you were heading survives the detour', async ({ page }) => {
    await page.goto('/friends')

    // The router does not percent-encode a slash inside a query value.
    await expect(page).toHaveURL('/login?next=/friends')

    await page.getByTestId('demo-fill').click()
    await page.getByTestId('login-submit').click()

    // Not the start screen: the link somebody actually followed.
    await expect(page).toHaveURL('/friends')
  })

  test('signing in lands on the start screen with that person\'s data', async ({ page }) => {
    await page.goto('/login')

    await page.getByTestId('login-email').fill(e2eAccount.email)
    await page.getByTestId('login-password').fill(e2eAccount.password)
    await page.getByTestId('login-submit').click()

    await expect(page).toHaveURL('/')
    await expect(page.getByTestId('streak-hero')).toContainText('5')
  })

  test('a wrong password says so without saying whether the account exists', async ({ page }) => {
    await page.goto('/login')

    await page.getByTestId('login-email').fill(e2eAccount.email)
    await page.getByTestId('login-password').fill('definitely-not-the-password')
    await page.getByTestId('login-submit').click()

    const error = page.getByTestId('login-error')
    await expect(error).toBeVisible()
    await expect(error).toHaveText('E-Mail-Adresse oder Passwort stimmt nicht.')

    // Still on the sign-in screen, with what was typed still there.
    await expect(page.getByTestId('login-email')).toHaveValue(e2eAccount.email)
  })

  test('an address nobody has gets exactly the same answer', async ({ page }) => {
    await page.goto('/login')

    await page.getByTestId('login-email').fill('nobody@kudos.example')
    await page.getByTestId('login-password').fill(e2eAccount.password)
    await page.getByTestId('login-submit').click()

    await expect(page.getByTestId('login-error')).toHaveText('E-Mail-Adresse oder Passwort stimmt nicht.')
  })

  test('registering creates an account, signs it in, and starts it empty', async ({ page }) => {
    await page.goto('/login')
    await page.getByTestId('to-register').click()

    await expect(page).toHaveURL('/register')

    const unique = Date.now()
    await page.getByTestId('register-name').fill(`E2E Neu ${unique}`)
    await page.getByTestId('register-email').fill(`e2e.neu.${unique}@kudos.example`)
    await page.getByTestId('register-password').fill('ein-langes-passwort')
    await page.getByTestId('register-submit').click()

    await expect(page).toHaveURL('/')

    // A new account sees none of the seeded person's world — which is the whole
    // reason ownership arrived together with accounts.
    await page.goto('/goals?tab=goals')
    await expect(page.getByTestId('goals-empty')).toBeVisible()
    await expect(page.getByTestId('goal-list')).toHaveCount(0)
  })

  test('a password that is too short is refused at the field', async ({ page }) => {
    await page.goto('/register')

    await page.getByTestId('register-name').fill('E2E Zu Kurz')
    await page.getByTestId('register-email').fill(`e2e.kurz.${Date.now()}@kudos.example`)
    await page.getByTestId('register-password').fill('kurz')
    await page.getByTestId('register-submit').click()

    await expect(page).toHaveURL('/register')
    await expect(page.getByTestId('register-form')).toContainText('at least 10 characters')
  })

  test('an address that is already taken is refused', async ({ page }) => {
    await page.goto('/register')

    await page.getByTestId('register-name').fill('E2E Doppelt')
    await page.getByTestId('register-email').fill(e2eAccount.email)
    await page.getByTestId('register-password').fill('ein-langes-passwort')
    await page.getByTestId('register-submit').click()

    await expect(page).toHaveURL('/register')
  })
})

test.describe('signed in', () => {
  test('signing out ends the session and locks the app again', async ({ page }) => {
    await page.goto('/login')
    await page.getByTestId('demo-fill').click()
    await page.getByTestId('login-submit').click()
    await expect(page).toHaveURL('/')

    await page.goto('/settings')
    await expect(page.getByTestId('signed-in-as')).toContainText(seeded.me)

    await page.getByTestId('sign-out').click()
    await expect(page).toHaveURL('/login')

    // And the data that was on screen a moment ago is not reachable any more.
    await page.goto('/goals')
    await expect(page.getByTestId('login-form')).toBeVisible()
    await expect(page.getByText(seeded.sharedGoal)).toHaveCount(0)
  })

  test('the sign-in screen is not somewhere to go back to', async ({ page }) => {
    await page.goto('/login')
    await page.getByTestId('demo-fill').click()
    await page.getByTestId('login-submit').click()
    await expect(page).toHaveURL('/')

    await page.goto('/login')

    // Nothing to do there while signed in.
    await expect(page).toHaveURL('/')
  })
})
