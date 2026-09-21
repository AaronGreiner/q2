import { expect, test } from '@playwright/test'
import { apiBaseUrl, e2eAccount } from './support/e2eEnvironment'
import { deliverPhoto } from './support/proofPhoto'

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

test('a goal is made in front of a friend, delivered in its chat and checked there', async ({ page, browser }, testInfo) => {
  const title = `E2E goal chat ${Date.now()}`

  // Made with a friend, which is the only way a goal is made now.
  await page.goto('/goals?create=1')
  await page.getByTestId('goal-title-input').fill(title)
  await page.getByTestId('goal-friend-picker').getByText(friend.name).click()
  await page.getByTestId('goal-submit').click()

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
