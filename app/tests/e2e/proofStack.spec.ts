import { expect, test } from '@playwright/test'
import { apiBaseUrl, e2eAccount } from './support/e2eEnvironment'
import { deliverPhoto } from './support/proofPhoto'
import { createGoal } from './support/createGoal'

/**
 * A friend's photograph, decided on the start screen with a swipe.
 *
 * The goal is created by the test and named after it, because the suite
 * shares one database and a spent window cannot be given back. The friend's
 * side is a second browser context signed in as them.
 */

const friend = { name: 'E2E Jonas', email: 'e2e.jonas@kudos.example' }

test('a friend\'s photograph waits on the start screen, opens full screen and is believed with a swipe', async ({ page, browser }, testInfo) => {
  const title = `E2E swipe ${Date.now()}`

  await createGoal(page, { title, friend: friend.name })

  await page.getByTestId('goal-card').filter({ hasText: title }).click()
  await page.getByTestId('goal-open-chat').click()
  await page.getByTestId('chat-deliver').click()
  await deliverPhoto(page)
  await expect(page.getByTestId('chat-proof').last().getByTestId('proof-status')).toContainText('Dein eigener Beweis')

  const theirs = await browser.newContext({ ...testInfo.project.use, storageState: { cookies: [], origins: [] } })

  try {
    const signIn = await theirs.request.post(`${apiBaseUrl}/api/auth/login`, {
      data: { email: friend.email, password: e2eAccount.password },
    })
    expect(signIn.ok(), `signing in as ${friend.email}`).toBe(true)

    const friendPage = await theirs.newPage()
    await friendPage.goto('/')

    // Newest first, so the photograph just delivered is on top.
    const stack = friendPage.getByTestId('home-vote')
    const card = stack.getByTestId('proof-swipe-card')
    await expect(card).toContainText(title)

    // A tap on the picture opens it full screen, with the goal under it.
    await card.getByTestId('proof-enlarge').click()
    const viewer = friendPage.getByTestId('photo-viewer')
    await expect(viewer).toBeVisible()
    await expect(viewer.getByTestId('photo-viewer-info')).toContainText(title)
    await friendPage.getByTestId('photo-viewer-close').click()
    await expect(viewer).toHaveCount(0)

    // Right is "I believe it".
    const box = await card.boundingBox()
    if (!box) throw new Error('the card has no box to swipe')

    const y = box.y + box.height / 3
    await friendPage.mouse.move(box.x + box.width / 2, y)
    await friendPage.mouse.down()
    await friendPage.mouse.move(box.x + box.width / 2 + 60, y, { steps: 4 })
    await friendPage.mouse.move(box.x + box.width / 2 + 220, y, { steps: 6 })
    await friendPage.mouse.up()

    // The only friend on it believed it, so it has left the stack and the
    // photograph counts.
    await expect(friendPage.getByTestId('home-vote').filter({ hasText: title })).toHaveCount(0)

    await friendPage.goto('/chats')
    await friendPage.getByTestId('chat-section-friends').getByTestId('chat-row').filter({ hasText: title }).click()
    await expect(friendPage.getByTestId('chat-proof').last().getByTestId('proof-status')).toContainText('Bestätigt')
  }
  finally {
    await theirs.close()
  }
})
