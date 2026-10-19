import { expect, test, type Browser, type BrowserContext, type TestInfo } from '@playwright/test'
import { apiBaseUrl } from './support/e2eEnvironment'
import { gotoHydrated } from './support/hydration'

/**
 * A goal that has ended keeps its conversation, folded away at the end of the
 * chat list under "Beendet" rather than among the running ones (#101).
 *
 * Both people are made fresh for the test: ending one of the seeded goals
 * would take it away from every spec that comes after.
 */
test.use({ storageState: { cookies: [], origins: [] } })

const password = 'ein-langes-passwort'

interface Account {
  id: string
  context: BrowserContext
}

/** A new account, signed in in a context of its own and past the introduction. */
async function register(browser: Browser, testInfo: TestInfo, label: string): Promise<Account> {
  const unique = `${Date.now()}${Math.floor(Math.random() * 1000)}`
  const email = `e2e.${label.toLowerCase()}.${unique}@kudos.example`

  const context = await browser.newContext({ ...testInfo.project.use, storageState: { cookies: [], origins: [] } })
  const response = await context.request.post(`${apiBaseUrl}/api/auth/register`, {
    data: { name: `E2E ${label} ${unique}`, email, password },
  })
  expect(response.ok(), `registering ${email}`).toBe(true)
  expect((await context.request.post(`${apiBaseUrl}/api/onboarding/complete`)).ok()).toBe(true)

  const { person } = await response.json() as { person: { id: string } }
  return { id: person.id, context }
}

async function goalWith(owner: Account, checker: Account, title: string): Promise<string> {
  const created = await owner.context.request.post(`${apiBaseUrl}/api/goals`, {
    data: { title, participantIds: [checker.id] },
  })
  expect(created.ok(), `creating ${title}`).toBe(true)
  return (await created.json() as { id: string }).id
}

test('an ended goal\'s conversation is folded away under "Beendet", one tap from the list', async ({ browser }, testInfo) => {
  const owner = await register(browser, testInfo, 'Ender')
  const checker = await register(browser, testInfo, 'Watcher')

  try {
    expect((await owner.context.request.post(`${apiBaseUrl}/api/friends/${checker.id}/request`)).ok()).toBe(true)
    expect((await checker.context.request.post(`${apiBaseUrl}/api/friends/${owner.id}/accept`)).ok()).toBe(true)

    const unique = Date.now()
    const running = `E2E still running ${unique}`
    const ended = `E2E carried through ${unique}`

    await goalWith(owner, checker, running)
    const endedId = await goalWith(owner, checker, ended)
    expect((await owner.context.request.post(`${apiBaseUrl}/api/goals/${endedId}/close`, { data: { completed: true } })).ok()).toBe(true)

    const page = await owner.context.newPage()
    await gotoHydrated(page, '/chats')

    // The running goal where it always was; the ended one in none of the
    // three sections.
    await expect(page.getByTestId('chat-section-mine')).toContainText(running)
    await expect(page.getByTestId('chat-section-mine')).not.toContainText(ended)

    // Folded: there, counted, and not shown until asked for.
    const toggle = page.getByTestId('chat-ended-toggle')
    await expect(toggle).toHaveAttribute('aria-expanded', 'false')
    await expect(toggle).toContainText('Beendet · 1')
    const row = page.getByTestId('chat-section-ended').getByTestId('chat-row').filter({ hasText: ended })
    await expect(row).toBeHidden()

    await toggle.click()
    await expect(toggle).toHaveAttribute('aria-expanded', 'true')
    await row.click()
    await expect(page).toHaveURL(/\/chats\/[0-9a-f-]+$/)

    // Still a conversation that opens — and coming back, it is still unfolded.
    await page.getByTestId('back-link').click()
    await expect(page).toHaveURL('/chats')
    await expect(page.getByTestId('chat-ended-toggle')).toHaveAttribute('aria-expanded', 'true')
  }
  finally {
    await owner.context.close()
    await checker.context.close()
  }
})
