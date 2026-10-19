import { expect, test, type Browser, type BrowserContext, type TestInfo } from '@playwright/test'
import { apiBaseUrl } from './support/e2eEnvironment'
import { gotoHydrated } from './support/hydration'

/**
 * Deciding about somebody on their profile (#86): answering their request,
 * asking them, and — once friends — writing to them and opening what the two
 * of you share.
 *
 * Every person here is made fresh for the test, the way invite.spec.ts does
 * it. A friendship changed between seeded people would change what every
 * other spec sees on the Freunde tab.
 */
test.use({ storageState: { cookies: [], origins: [] } })

const password = 'ein-langes-passwort'

interface Account {
  id: string
  name: string
  context: BrowserContext
}

/** A new account, signed in in a context of its own and past the introduction. */
async function register(browser: Browser, testInfo: TestInfo, label: string): Promise<Account> {
  const unique = `${Date.now()}${Math.floor(Math.random() * 1000)}`
  const name = `E2E ${label} ${unique}`
  const email = `e2e.${label.toLowerCase()}.${unique}@kudos.example`

  const context = await browser.newContext({ ...testInfo.project.use, storageState: { cookies: [], origins: [] } })
  const response = await context.request.post(`${apiBaseUrl}/api/auth/register`, { data: { name, email, password } })
  expect(response.ok(), `registering ${email}`).toBe(true)
  expect((await context.request.post(`${apiBaseUrl}/api/onboarding/complete`)).ok()).toBe(true)

  const { person } = await response.json() as { person: { id: string } }
  return { id: person.id, name, context }
}

async function ask(from: Account, to: Account) {
  expect((await from.context.request.post(`${apiBaseUrl}/api/friends/${to.id}/request`)).ok()).toBe(true)
}

test('a request is answered on the profile of whoever sent it', async ({ browser }, testInfo) => {
  const asker = await register(browser, testInfo, 'Asker')
  const reader = await register(browser, testInfo, 'Reader')

  try {
    await ask(asker, reader)

    const page = await reader.context.newPage()
    await gotoHydrated(page, `/people/${asker.id}`)

    const actions = page.getByTestId('person-friendship')
    await expect(actions).toHaveAttribute('data-state', 'RequestReceived')
    await expect(actions).toContainText('Möchte dich hinzufügen')

    await page.getByTestId('person-accept').click()

    // Friends now: the answer is replaced by the way to write to them.
    await expect(actions).toHaveAttribute('data-state', 'Friends')
    await page.getByTestId('person-message').click()
    await expect(page).toHaveURL(/\/chats\/[0-9a-f-]+$/)
    await expect(page.getByTestId('chat-header-info')).toContainText(asker.name)
  }
  finally {
    await asker.context.close()
    await reader.context.close()
  }
})

test('a stranger is asked from their profile, and the request can be taken back', async ({ browser }, testInfo) => {
  const stranger = await register(browser, testInfo, 'Stranger')
  const reader = await register(browser, testInfo, 'Reader')

  try {
    const page = await reader.context.newPage()
    await gotoHydrated(page, `/people/${stranger.id}`)

    const actions = page.getByTestId('person-friendship')
    await expect(actions).toHaveAttribute('data-state', 'None')

    await page.getByTestId('person-request').click()
    await expect(actions).toHaveAttribute('data-state', 'RequestSent')
    await expect(page.getByTestId('person-requested')).toHaveText('Angefragt')

    await page.getByTestId('person-withdraw').click()
    await expect(actions).toHaveAttribute('data-state', 'None')
  }
  finally {
    await stranger.context.close()
    await reader.context.close()
  }
})

test('a friend\'s profile names the Dos you check for them, and opens them', async ({ browser }, testInfo) => {
  const owner = await register(browser, testInfo, 'Owner')
  const checker = await register(browser, testInfo, 'Checker')

  try {
    await ask(owner, checker)
    expect((await checker.context.request.post(`${apiBaseUrl}/api/friends/${owner.id}/accept`)).ok()).toBe(true)

    const title = `E2E shared on a profile ${Date.now()}`
    const created = await owner.context.request.post(`${apiBaseUrl}/api/goals`, {
      data: { title, participantIds: [checker.id] },
    })
    expect(created.ok(), 'creating the shared goal').toBe(true)
    const { id: goalId } = await created.json() as { id: string }

    const page = await checker.context.newPage()
    await gotoHydrated(page, `/people/${owner.id}`)

    await expect(page.getByTestId('balance-scope')).toHaveText('1 gemeinsames Do oder Don’t')
    await page.getByTestId('balance-goal').filter({ hasText: title }).click()

    await expect(page).toHaveURL(new RegExp(`/goals/${goalId}$`))
    await expect(page.getByTestId('goal-detail')).toContainText(title)
  }
  finally {
    await owner.context.close()
    await checker.context.close()
  }
})
