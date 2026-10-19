import { expect, test } from '@playwright/test'
import { apiBaseUrl, e2eAccount } from './support/e2eEnvironment'
import { deliverPhoto } from './support/proofPhoto'
import { createGoal } from './support/createGoal'
import { goalLink } from './support/goalList'

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

  await goalLink(page, title).click()
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

    // Above the challenge, which is above their own day (#135).
    await expect(friendPage.getByTestId('home-sections')
      .locator('[data-testid="home-vote"] ~ [data-testid="challenge-card"]')).toHaveCount(1)

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

/**
 * What counts, said when the goal is made, is read where it is judged (#95):
 * on the goal, on the friend's card and above the thread the photographs are
 * voted in — not only as a title.
 */
test('what counts goes from the create sheet to the goal, the friend\'s card and the thread', async ({ page, browser }, testInfo) => {
  const title = `E2E described ${Date.now()}`
  const description = 'Mindestens 5 km, ohne Pause.'

  await createGoal(page, { title, friend: friend.name, description })

  await goalLink(page, title).click()
  await expect(page.getByTestId('goal-description')).toHaveText(description)

  await page.getByTestId('goal-open-chat').click()
  await expect(page.getByTestId('chat-goal-description')).toHaveText(description)
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

    const card = friendPage.getByTestId('home-vote').getByTestId('proof-swipe-card').filter({ hasText: title })
    await expect(card.getByTestId('proof-goal-description')).toHaveText(description)

    // Decided, so no photograph from this test is left waiting for the next.
    await card.getByTestId('proof-confirm').click()
    await expect(friendPage.getByTestId('home-vote').filter({ hasText: title })).toHaveCount(0)
  }
  finally {
    await theirs.close()
  }
})

/**
 * A reaction to a photograph leads to the photograph (#112): from the bell,
 * through the goal, into the goal's conversation, with the card brought into
 * view and outlined. The address is cleaned up afterwards, so coming back to
 * the conversation later does not point at it again.
 */
test('a reaction in the bell opens the conversation at the photograph it is about', async ({ page, browser }, testInfo) => {
  const title = `E2E focus ${Date.now()}`

  await createGoal(page, { title, friend: friend.name })

  await goalLink(page, title).click()
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

    const pending = await (await theirs.request.get(`${apiBaseUrl}/api/proofs/pending`)).json() as Array<{
      proof: { id: string }
      goalTitle: string
    }>
    const proof = pending.find(card => card.goalTitle === title)?.proof
    expect(proof, `${friend.name} has the photograph to vote on`).toBeDefined()

    const reacted = await theirs.request.post(`${apiBaseUrl}/api/proofs/${proof!.id}/reactions`, { data: { kind: 'Fire' } })
    expect(reacted.ok(), 'reacting to the photograph').toBe(true)

    await page.goto('/notifications')
    await page.getByTestId('notification-row').filter({ hasText: title }).click()

    await expect(page).toHaveURL(/\/chats\/[^/?]+$/)
    const card = page.locator(`[data-proof-id="${proof!.id}"]`)
    await expect(card).toHaveAttribute('data-focused', '')
    await expect(card).toBeInViewport()

    // Decided, so no photograph from this test is left waiting for the next.
    const voted = await theirs.request.post(`${apiBaseUrl}/api/proofs/${proof!.id}/vote`, { data: { value: 'Confirm' } })
    expect(voted.ok(), 'confirming the photograph').toBe(true)
  }
  finally {
    await theirs.close()
  }
})
