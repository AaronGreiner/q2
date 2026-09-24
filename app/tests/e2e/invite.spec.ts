import { expect, test, type Browser, type BrowserContext, type TestInfo } from '@playwright/test'
import { apiBaseUrl } from './support/e2eEnvironment'

/**
 * Following somebody's invite link, all the way to the friendship.
 *
 * The first version of this never worked in a real browser: `/join/<code>` was
 * server-rendered, handed the code to the sign-up form through state, and
 * redirected — a 302 on the server, a fresh request, and the code was gone.
 * Nothing but a run through a real browser against a real server could have
 * noticed, which is why this spec exists.
 *
 * Both ends are made fresh for each test rather than taken from the seed.
 * Every spec shares one database, and a friendship added to a seeded person
 * would change what the other specs count.
 */
test.use({ storageState: { cookies: [], origins: [] } })

const password = 'ein-langes-passwort'

interface Account {
  name: string
  email: string
  context: BrowserContext
}

/** A new account, signed in inside a browser context of its own. */
async function register(browser: Browser, testInfo: TestInfo, label: string): Promise<Account> {
  const unique = `${Date.now()}${Math.floor(Math.random() * 1000)}`
  const name = `E2E ${label} ${unique}`
  const email = `e2e.${label.toLowerCase()}.${unique}@kudos.example`

  const context = await browser.newContext({ ...testInfo.project.use, storageState: { cookies: [], origins: [] } })
  const response = await context.request.post(`${apiBaseUrl}/api/auth/register`, { data: { name, email, password } })
  expect(response.ok(), `registering ${email}`).toBe(true)

  return { name, email, context }
}

async function inviteLinkOf(account: Account): Promise<string> {
  const { code } = await (await account.context.request.get(`${apiBaseUrl}/api/invite`)).json() as { code: string }
  return `/join/${code}`
}

async function friendNamesOf(account: Account): Promise<string[]> {
  const friends = await (await account.context.request.get(`${apiBaseUrl}/api/friends`)).json() as {
    friends: { person: { displayName: string } }[]
  }
  return friends.friends.map(friend => friend.person.displayName)
}

test('somebody without an account follows a link, signs up, and is friends', async ({ browser, page }, testInfo) => {
  const sender = await register(browser, testInfo, 'Sender')

  try {
    await page.goto(await inviteLinkOf(sender))

    // Server-rendered, and it says whose link it is before anything is asked.
    await expect(page.getByTestId('invite-welcome-name')).toHaveText(sender.name)

    await page.getByTestId('invite-welcome-register').click()
    await expect(page.getByTestId('register-invite')).toContainText(sender.name)

    // A reload is a fresh request, exactly like the one that used to lose the
    // code. It has to survive it.
    await page.reload()
    await expect(page.getByTestId('register-invite')).toContainText(sender.name)

    const unique = Date.now()
    const newcomer = `E2E Neu ${unique}`
    await page.getByTestId('register-name').fill(newcomer)
    await page.getByTestId('register-email').fill(`e2e.eingeladen.${unique}@kudos.example`)
    await page.getByTestId('register-password').fill(password)
    await page.getByTestId('register-submit').click()

    await expect(page).toHaveURL('/')
    await expect(page.getByText('Ihr seid jetzt befreundet', { exact: true })).toBeVisible()

    expect(await friendNamesOf(sender)).toContain(newcomer)
  }
  finally {
    await sender.context.close()
  }
})

test('somebody with an account signs in from the link and accepts it', async ({ browser, page }, testInfo) => {
  const sender = await register(browser, testInfo, 'Sender')
  const guest = await register(browser, testInfo, 'Gast')

  try {
    const link = await inviteLinkOf(sender)
    await page.goto(link)

    await page.getByTestId('invite-welcome-sign-in').click()
    await page.getByTestId('login-email').fill(guest.email)
    await page.getByTestId('login-password').fill(password)
    await page.getByTestId('login-submit').click()

    // Back where the link pointed, now with a session and one thing to do.
    await expect(page).toHaveURL(link)
    await page.getByTestId('invite-welcome-accept').click()

    await expect(page).toHaveURL(/\/people\//)
    await expect(page.getByText('Ihr seid jetzt befreundet', { exact: true })).toBeVisible()

    expect(await friendNamesOf(sender)).toContain(guest.name)

    // Opened again, the link has nothing left to offer.
    await page.goto(link)
    await expect(page.getByTestId('invite-welcome-standing')).toHaveText('Ihr seid schon befreundet.')
    await expect(page.getByTestId('invite-welcome-accept')).toHaveCount(0)
  }
  finally {
    await sender.context.close()
    await guest.context.close()
  }
})

test('a link that was replaced says so and still lets somebody in', async ({ page }) => {
  await page.goto('/join/gibt-es-nicht')

  await expect(page.getByRole('heading', { level: 1 })).toHaveText('Dieser Link gilt nicht mehr')
  await expect(page.getByTestId('invite-welcome-invalid')).toContainText('Konto erstellen')
})
