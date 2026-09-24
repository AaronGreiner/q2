import { expect, test } from '@playwright/test'
import { apiBaseUrl, e2eAccount } from './support/e2eEnvironment'
import { deliverPhoto } from './support/proofPhoto'
import { createGoal } from './support/createGoal'

/**
 * A goal and its conversation, as one thing: made in front of a friend, the
 * photograph delivered where that friend is already looking, and the verdict
 * given there (docs/adr/0027-goal-conversations.md).
 *
 * The friend's side is a second browser context signed in as them — the only
 * way to see their screen rather than trust that the API would have drawn it.
 * Every goal here is created by the test itself and named after it, because
 * the suite shares one database and a spent window cannot be given back.
 */

const friend = { name: 'E2E Jonas', email: 'e2e.jonas@kudos.example' }

// The suite runs the production build, whose service worker would carry the
// staged refusal below past `page.route` — Playwright cannot see a request a
// worker makes. Nothing here is about the worker, and pwa.spec.ts covers it.
test.use({ serviceWorkers: 'block' })

test('a goal is made in front of a friend, delivered in its chat and checked there', async ({ page, browser }, testInfo) => {
  const title = `E2E goal chat ${Date.now()}`

  // Made with a friend, which is the only way a goal is made now.
  await createGoal(page, { title, friend: friend.name })

  // Its conversation exists the moment it does, one tap from the goal.
  await page.getByTestId('goal-card').filter({ hasText: title }).click()
  await page.getByTestId('goal-open-chat').click()

  await expect(page.getByRole('heading', { name: title })).toBeVisible()
  await expect(page.getByTestId('chat-event').first()).toContainText('Du hast das Ziel erstellt.')

  // The camera is in the thread, for the owner, while the window takes one.
  await page.getByTestId('chat-deliver').click()
  await deliverPhoto(page)

  const mine = page.getByTestId('chat-proof').last()
  await expect(mine.getByTestId('proof-status')).toContainText('Dein eigener Beweis')
  await expect(mine.getByTestId('proof-confirm')).toHaveCount(0)
  await expect(page.getByTestId('chat-deliver')).toHaveCount(0)

  const chatUrl = page.url()

  // The friend's side, in a browser of their own.
  const theirs = await browser.newContext({ ...testInfo.project.use, storageState: { cookies: [], origins: [] } })

  try {
    const signIn = await theirs.request.post(`${apiBaseUrl}/api/auth/login`, {
      data: { email: friend.email, password: e2eAccount.password },
    })
    expect(signIn.ok(), `signing in as ${friend.email}`).toBe(true)

    const friendPage = await theirs.newPage()
    await friendPage.goto('/chats')

    const row = friendPage.getByTestId('chat-section-friends').getByTestId('chat-row').filter({ hasText: title })
    await expect(row.getByTestId('chat-awaiting-vote')).toBeVisible()

    await row.click()
    await expect(friendPage).toHaveURL(chatUrl)

    const proof = friendPage.getByTestId('chat-proof').last()
    await proof.getByTestId('proof-confirm').click()

    // The only friend on it believed it: the photograph counts and the window
    // is kept, in the thread as much as on the goal.
    await expect(proof.getByTestId('proof-status')).toContainText('Bestätigt')
    await expect(friendPage.getByTestId('chat-event').last()).toContainText('Geschafft · Streak 1')
  }
  finally {
    await theirs.close()
  }
})

/**
 * Delivering from anywhere but the conversation ends in the conversation: the
 * row that offered the camera stops offering it, so staying put would leave
 * the photograph looking as though it had vanished.
 *
 * And a delivery the server refuses stays on the camera's sheet, photograph and
 * all. The refusal is staged by answering the one request with what the API
 * sends for a window that closed while the screen was open — the real thing
 * needs a deadline to pass between two clicks.
 */
test('a refused photograph stays on the sheet, and a delivered one is followed to its conversation', async ({ page }) => {
  const title = `E2E follow ${Date.now()}`

  await createGoal(page, { title, friend: friend.name })

  const uploads: string[] = []
  page.on('request', (request) => {
    if (request.method() === 'POST' && request.url().includes('/api/images')) uploads.push(request.url())
  })

  let refuse = true
  await page.route('**/api/goals/*/proof', async (route) => {
    // Only the delivery itself: the API is another origin, so a preflight
    // goes first and has to pass untouched.
    if (!refuse || route.request().method() !== 'POST') return route.fallback()

    refuse = false
    await route.fulfill({
      status: 400,
      contentType: 'application/problem+json',
      body: JSON.stringify({
        type: 'https://tools.ietf.org/html/rfc9110#section-15.5.1',
        title: 'One or more validation errors occurred.',
        status: 400,
        errors: { Proof: ['This window is not taking a photograph right now.'] },
      }),
    })
  })

  // Creating lands on the list of every goal; the camera is on today's.
  await page.getByTestId('segment-today').click()
  await page.getByTestId('window-row').filter({ hasText: title }).getByTestId('window-deliver').click()
  await deliverPhoto(page)

  // Refused: the sheet is still up, with the photograph and the reason.
  await expect(page.getByTestId('photo-message')).toContainText('Dieses Fenster nimmt gerade keinen Beweis an.')
  await expect(page.getByTestId('photo-preview')).toBeVisible()

  // Trying again hands in the same upload rather than sending the bytes twice.
  await page.getByTestId('photo-confirm').click()

  await expect(page).toHaveURL(/\/chats\//)
  await expect(page.getByRole('heading', { name: title })).toBeVisible()
  await expect(page.getByTestId('chat-proof').last().getByTestId('proof-status')).toContainText('Dein eigener Beweis')
  await expect(page.getByRole('dialog')).toHaveCount(0)
  expect(uploads).toHaveLength(1)
})
