import { expect, test } from '@playwright/test'
import type { Page } from '@playwright/test'
import { goalLink } from './support/goalList'

/**
 * Back goes to the screen something was opened from, and finds it the way it
 * was left (#118, docs/adr/0040-back-goes-where-you-came-from.md).
 *
 * These read the history the way the router writes it — `history.state.back`
 * and `forward` — because "went back" and "pushed the same address again" look
 * identical on screen, and only the second leaves the system's back gesture
 * pointing into the screen that was just left.
 */

/** From the E2E seed — see api/.../Seeding/E2ESeed.cs. */
const seeded = {
  friend: 'E2E Jonas',
  sharedGoal: 'E2E shared goal with participants',
  sharedGoalId: 'e2e00000-0000-4000-8000-000000000001',
  groupChat: 'E2E group chat',
}

const goalPath = `/goals/${seeded.sharedGoalId}`

function historyState(page: Page) {
  return page.evaluate(() => window.history.state as { back: string | null, forward: string | null })
}

function panelScroll(page: Page) {
  return page.getByTestId('content-panel').locator('[data-slot="body"]').first()
}

test('a Do opened from the start screen goes back to the start screen', async ({ page }) => {
  await page.goto('/')

  await page.getByTestId('content-panel').locator(`a[href="${goalPath}"]`).first().click()
  await expect(page).toHaveURL(goalPath)

  await page.getByTestId('back-link').click()

  await expect(page).toHaveURL('/')
  // Stepped back, rather than pushing the start screen on top of the Do.
  expect((await historyState(page)).forward).toBe(goalPath)
})

test('a Do\'s conversation goes back to the Do, and the Do to the list it came from', async ({ page }) => {
  await page.goto('/goals')
  await goalLink(page, seeded.sharedGoal).click()
  await expect(page).toHaveURL(goalPath)

  await page.getByTestId('goal-open-chat').click()
  await expect(page).toHaveURL(/\/chats\//)

  // The thread's own fallback is the chat list; it was opened from the Do.
  await page.getByTestId('back-link').click()
  await expect(page).toHaveURL(goalPath)

  await page.getByTestId('back-link').click()
  await expect(page).toHaveURL('/goals')
})

test('a screen opened by its address goes to its parent and leaves nothing behind', async ({ page }) => {
  // What a push notification or a shared link does: q2 starts on the Do.
  await page.goto(goalPath)
  await expect(page.getByTestId('goal-detail')).toBeVisible()
  const entries = await page.evaluate(() => window.history.length)

  await page.getByTestId('back-link').click()

  await expect(page).toHaveURL('/goals')
  // Replaced, not pushed: the system's back does not return into the Do.
  expect(await page.evaluate(() => window.history.length)).toBe(entries)
  expect(await historyState(page)).toMatchObject({ back: null, forward: null })
})

test('a list scrolled down is where it was left when it is gone back to', async ({ page }) => {
  await page.goto('/')
  const area = panelScroll(page)
  await expect(page.getByTestId('home-feed')).toBeVisible()

  const top = await area.evaluate((element) => {
    element.scrollTop = Math.min(240, element.scrollHeight - element.clientHeight)
    return element.scrollTop
  })
  expect(top).toBeGreaterThan(0)

  // The bell sits in the header, above the scroll area, so reaching it does
  // not scroll anything.
  await page.getByTestId('open-notifications').click()
  await expect(page).toHaveURL('/notifications')
  await page.getByTestId('back-link').click()

  await expect(page).toHaveURL('/')
  await expect.poll(() => panelScroll(page).evaluate(element => element.scrollTop)).toBe(top)
})

test('the Friends search is still there when a result is gone back from, and only then', async ({ page }) => {
  await page.goto('/search')
  await page.getByTestId('friend-search').fill('e2e.jonas')
  const result = page.getByTestId('person-result').filter({ hasText: seeded.friend })
  await expect(result).toBeVisible()

  await result.getByTestId('person-link').click()
  await expect(page).toHaveURL(/\/people\//)
  await page.getByTestId('back-link').click()

  await expect(page).toHaveURL('/search')
  await expect(page.getByTestId('friend-search')).toHaveValue('e2e.jonas')
  await expect(page.getByTestId('person-result').filter({ hasText: seeded.friend })).toBeVisible()

  // Opened afresh from the tab bar, the screen starts clean.
  await page.getByTestId('nav-home').click()
  await expect(page).toHaveURL('/')
  await page.getByTestId('nav-search').click()
  await expect(page.getByTestId('friend-search')).toHaveValue('')
})

test('a conversation\'s info sheet is open again when a member is gone back from', async ({ page }) => {
  await page.goto('/chats')
  await page.getByTestId('chat-row').filter({ hasText: seeded.groupChat }).click()
  await page.getByTestId('chat-header-info').click()

  await page.getByTestId('chat-info-member').filter({ hasText: seeded.friend }).getByRole('link').click()
  await expect(page).toHaveURL(/\/people\//)
  await page.getByTestId('back-link').click()

  await expect(page).toHaveURL(/\/chats\//)
  await expect(page.getByTestId('chat-info')).toBeVisible()

  // Closed, and the thread goes back to the list it was opened from.
  await page.keyboard.press('Escape')
  await expect(page.getByTestId('chat-info')).toHaveCount(0)
  await page.getByTestId('back-link').click()
  await expect(page).toHaveURL('/chats')
})
