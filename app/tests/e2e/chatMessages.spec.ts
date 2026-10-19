import { expect, test, type Browser, type BrowserContext, type TestInfo } from '@playwright/test'
import { apiBaseUrl } from './support/e2eEnvironment'
import { gotoHydrated } from './support/hydration'
import { deliverPhoto } from './support/proofPhoto'

/**
 * Looking after a conversation's messages: correcting and taking back your own
 * (#25), reporting somebody else's (#104), pinning the conversation and finding
 * its photographs again (#24).
 *
 * Between two people made for the test, in a direct chat of their own: the
 * seeded conversations are read by other specs, and a message taken back there
 * would change what those see.
 *
 * Who may do what is decided on the server (`ChatMessageManagementTests`,
 * `ModerationEndpointTests`). What only a browser shows is the gesture and the
 * sheets: a right click is what a held finger is on a desktop, and the menu it
 * opens is the one a phone gets.
 */
test.use({ storageState: { cookies: [], origins: [] } })

const password = 'ein-langes-passwort'

interface Account {
  id: string
  context: BrowserContext
}

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

/** Two friends and the direct chat between them, as the first one sees it. */
async function directChat(browser: Browser, testInfo: TestInfo) {
  const me = await register(browser, testInfo, 'Writer')
  const friend = await register(browser, testInfo, 'Reader')

  expect((await me.context.request.post(`${apiBaseUrl}/api/friends/${friend.id}/request`)).ok()).toBe(true)
  expect((await friend.context.request.post(`${apiBaseUrl}/api/friends/${me.id}/accept`)).ok()).toBe(true)

  const started = await me.context.request.post(`${apiBaseUrl}/api/chats/direct`, { data: { personId: friend.id } })
  expect(started.ok(), 'starting the direct chat').toBe(true)
  const { id } = await started.json() as { id: string }

  return { me, friend, chatId: id }
}

test('your own message is corrected and taken back from its menu, and the other end sees both', async ({ browser }, testInfo) => {
  const { me, friend, chatId } = await directChat(browser, testInfo)

  try {
    const page = await me.context.newPage()
    await gotoHydrated(page, `/chats/${chatId}`)

    await page.getByTestId('chat-input').fill('Bis morgn')
    await page.getByTestId('chat-send').click()

    const bubble = page.getByTestId('chat-bubble').filter({ hasText: 'Bis morgn' })
    await bubble.getByTestId('chat-message-text').click({ button: 'right' })
    await page.getByTestId('chat-action-edit').click()

    await page.locator('textarea[data-testid="chat-edit-text"], [data-testid="chat-edit-text"] textarea').fill('Bis morgen')
    await page.getByTestId('chat-edit-save').click()

    const corrected = page.getByTestId('chat-bubble').filter({ hasText: 'Bis morgen' })
    await expect(corrected.getByTestId('chat-edited')).toHaveText('bearbeitet')

    // The other end reads it corrected, and marked as such.
    const theirs = await friend.context.newPage()
    await gotoHydrated(theirs, `/chats/${chatId}`)
    await expect(theirs.getByTestId('chat-bubble').filter({ hasText: 'Bis morgen' }).getByTestId('chat-edited')).toBeVisible()

    // Taken back: the place stays, for both.
    await corrected.getByTestId('chat-message-text').click({ button: 'right' })
    await page.getByTestId('chat-action-delete').click()
    await page.getByTestId('confirm-accept').click()

    await expect(page.getByTestId('chat-deleted')).toHaveText('Nachricht gelöscht')
    await expect(page.getByTestId('chat-bubble').filter({ hasText: 'Bis morgen' })).toHaveCount(0)

    await theirs.reload()
    await expect(theirs.getByTestId('chat-deleted')).toHaveText('Nachricht gelöscht')
  }
  finally {
    await me.context.close()
    await friend.context.close()
  }
})

test('somebody else\'s message is reported from its menu', async ({ browser }, testInfo) => {
  const { me, friend, chatId } = await directChat(browser, testInfo)

  try {
    const sent = await friend.context.request.post(`${apiBaseUrl}/api/chats/${chatId}/messages`, { data: { text: 'Etwas Gemeines' } })
    expect(sent.ok(), 'the friend writing').toBe(true)

    const page = await me.context.newPage()
    await gotoHydrated(page, `/chats/${chatId}`)

    await page.getByTestId('chat-bubble').filter({ hasText: 'Etwas Gemeines' }).getByTestId('chat-message-text').click({ button: 'right' })
    await expect(page.getByTestId('chat-action-edit')).toHaveCount(0)
    await page.getByTestId('chat-action-report').click()

    await page.getByTestId('report-reason-Harassment').click()
    await page.getByTestId('report-submit').click()
    await expect(page.getByTestId('report-submit')).toHaveCount(0)
  }
  finally {
    await me.context.close()
    await friend.context.close()
  }
})

test('a conversation is pinned from its info sheet, which shows its photographs', async ({ browser }, testInfo) => {
  const { me, friend, chatId } = await directChat(browser, testInfo)

  try {
    const page = await me.context.newPage()
    await gotoHydrated(page, `/chats/${chatId}`)

    await page.getByTestId('chat-attach').click()
    await deliverPhoto(page)
    await expect(page.getByTestId('chat-photo')).toHaveCount(1)

    await page.getByTestId('chat-header-info').click()
    await expect(page.getByTestId('chat-info-photos').getByTestId('chat-photo-tile')).toHaveCount(1)

    await page.getByTestId('chat-info-pin').getByRole('switch').click()
    await expect(page.getByTestId('chat-info-pin').getByRole('switch')).toHaveAttribute('aria-checked', 'true')

    await gotoHydrated(page, '/chats')
    await expect(page.getByTestId('chat-row').first().getByTestId('chat-pinned')).toBeVisible()
  }
  finally {
    await me.context.close()
    await friend.context.close()
  }
})
