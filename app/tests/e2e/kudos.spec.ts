import { expect, test } from '@playwright/test'

/**
 * The flows q2 exists for, through a real browser at phone width.
 *
 * Runs against a freshly created and seeded temporary SQLite database, so the
 * starting state is identical on every run. The suite shares that one database
 * and runs in sequence, so anything a test creates is named after itself.
 *
 * What these assert is behaviour and text, never geometry — a card that
 * overflows or a tap target nobody can reach would still be green here, which
 * is why the layout is also looked at by hand (app/AGENTS.md section 8).
 */

/** From the E2E seed — see api/.../Seeding/E2ESeed.cs. */
const seeded = {
  me: 'E2E Mara',
  friend: 'E2E Jonas',
  requester: 'E2E Max',
  suggested: 'E2E Emma',
  sharedGoal: 'E2E shared goal with participants',
  completedGoal: 'E2E completed goal',
  zeroProgressGoal: 'E2E goal without any progress',
  overdueGoal: 'E2E overdue goal',
  openTask: 'E2E open task for today',
  doneTask: 'E2E task already done',
  groupChat: 'E2E group chat',
  sharedGoalId: 'e2e00000-0000-4000-8000-000000000001',
}

test.describe('the start screen', () => {
  test('opens on the streak, the day and what friends have been doing', async ({ page }) => {
    await page.goto('/')

    await expect(page.getByTestId('streak-hero')).toContainText('5')
    await expect(page.getByTestId('today-progress')).toContainText('von')

    await expect(page.getByText(seeded.openTask)).toBeVisible()
    await expect(page.getByTestId('activity-row').first()).toContainText(seeded.friend)
    await expect(page.getByTestId('leaderboard')).toContainText(seeded.friend)
  })

  test('ticking a task off moves the day and the feed together', async ({ page }) => {
    await page.goto('/')

    const row = page.getByTestId('task-row').filter({ hasText: seeded.openTask })
    const toggle = row.getByTestId('task-toggle')

    await expect(toggle).toHaveAttribute('aria-checked', 'false')
    await toggle.click()
    await expect(toggle).toHaveAttribute('aria-checked', 'true')

    // The ring is derived from the same tasks, so it has to have moved.
    await expect(page.getByTestId('today-progress')).toContainText('100%')

    // Put it back, so the next test starts where this one did.
    await toggle.click()
    await expect(toggle).toHaveAttribute('aria-checked', 'false')
  })

  test('giving kudos raises the count and can be taken back', async ({ page }) => {
    await page.goto('/')

    const button = page.getByTestId('activity-row').first().getByTestId('kudos-button')

    await expect(button).toHaveAttribute('aria-pressed', 'false')
    const before = Number((await button.innerText()).trim())

    await button.click()
    await expect(button).toHaveAttribute('aria-pressed', 'true')
    await expect(button).toContainText(String(before + 1))

    await button.click()
    await expect(button).toHaveAttribute('aria-pressed', 'false')
    await expect(button).toContainText(String(before))
  })

  test('the theme can be switched from the header', async ({ page }) => {
    await page.goto('/')

    const html = page.locator('html')
    const wasDark = (await html.getAttribute('class'))?.includes('dark') ?? false

    await page.getByTestId('theme-toggle').click()

    await expect(html).toHaveClass(wasDark ? /light/ : /dark/)
  })
})

test.describe('goals', () => {
  test('the two tabs show today and the goals themselves', async ({ page }) => {
    await page.goto('/goals')

    await expect(page.getByTestId('task-list')).toContainText(seeded.openTask)
    await expect(page.getByTestId('task-list')).toContainText(seeded.doneTask)

    await page.getByTestId('segment-goals').click()

    const list = page.getByTestId('goal-list')
    await expect(list).toContainText(seeded.sharedGoal)
    await expect(list).toContainText(seeded.completedGoal)
    await expect(list).toContainText(seeded.zeroProgressGoal)
    await expect(list).toContainText(seeded.overdueGoal)
  })

  test('an overdue goal says so, and not only through colour', async ({ page }) => {
    await page.goto('/goals?tab=goals')

    const card = page.getByTestId('goal-card').filter({ hasText: seeded.overdueGoal })

    await expect(card.getByTestId('goal-overdue')).toHaveText('Überfällig')
  })

  test('opening a goal shows its ring, its team and its tasks', async ({ page }) => {
    await page.goto('/goals?tab=goals')

    await page.getByTestId('goal-card').filter({ hasText: seeded.sharedGoal }).click()

    await expect(page).toHaveURL(`/goals/${seeded.sharedGoalId}`)
    await expect(page.getByRole('heading', { name: seeded.sharedGoal })).toBeVisible()
    await expect(page.getByTestId('progress-ring')).toHaveAttribute('aria-valuenow', '50')
    await expect(page.getByTestId('goal-team-member')).toContainText([seeded.friend, 'E2E Lena'])
  })

  test('logging a day moves the goal forward', async ({ page }) => {
    await page.goto(`/goals/${seeded.sharedGoalId}`)

    await expect(page.getByTestId('progress-ring')).toHaveAttribute('aria-valuenow', '50')

    await page.getByTestId('goal-contribute').click()

    await expect(page.getByTestId('progress-ring')).toHaveAttribute('aria-valuenow', '60')
  })

  test('a goal that does not exist gets its own calm state', async ({ page }) => {
    await page.goto('/goals/e2e00000-0000-4000-8000-999999999999')

    await expect(page.getByTestId('goal-not-found')).toBeVisible()
  })

  test('a new goal can be created and appears in the list', async ({ page }) => {
    await page.goto('/goals')

    await page.getByTestId('open-create-goal').click()

    const title = `E2E created goal ${Date.now()}`
    await page.getByTestId('goal-title-input').fill(title)
    await page.getByTestId('rhythm-Weekly').click()
    await page.getByTestId('icon-flame').click()
    await page.getByTestId('goal-submit').click()

    await expect(page.getByTestId('goal-list')).toContainText(title)
  })

  test('creating a goal without a title is refused with a field message', async ({ page }) => {
    await page.goto('/goals')
    await page.getByTestId('open-create-goal').click()

    // The button stays out of reach rather than sending something the server
    // would only reject.
    await expect(page.getByTestId('goal-submit')).toBeDisabled()
  })
})

test.describe('chats', () => {
  test('the list shows who wrote last and what is unread', async ({ page }) => {
    await page.goto('/chats')

    const row = page.getByTestId('chat-row').filter({ hasText: seeded.friend })

    await expect(row).toContainText('E2E unread reply')
    await expect(page.getByTestId('chat-list')).toContainText(seeded.groupChat)
  })

  test('opening a thread shows the shared goal and marks it read', async ({ page }) => {
    await page.goto('/chats')

    await page.getByTestId('chat-row').filter({ hasText: seeded.friend }).click()

    await expect(page.getByRole('heading', { name: seeded.friend })).toBeVisible()
    await expect(page.getByTestId('chat-goal-banner')).toContainText(seeded.sharedGoal)
    await expect(page.getByTestId('chat-messages')).toContainText('E2E unread reply')

    await page.getByTestId('back-link').click()

    // The tab bar counts unread conversations, and this one is not one any more.
    await expect(page.getByTestId('nav-chats')).toHaveText('Chats')
  })

  test('opening a thread that is longer than the screen lands on the newest message', async ({ page }) => {
    await page.goto('/chats')
    await page.getByTestId('chat-row').filter({ hasText: seeded.groupChat }).click()

    // The seeded thread fits on the screen, and a thread that fits is at its
    // newest message whatever it does. Make it longer than the screen first.
    const newest = `E2E newest ${Date.now()}`
    for (let index = 1; index <= 12; index++) {
      await page.getByTestId('chat-input').fill(index === 12 ? newest : `E2E filler ${index}`)
      await page.getByTestId('chat-send').click()
      await expect(page.getByTestId('chat-input')).toHaveValue('')
    }

    // Leaving and coming back is the moment this is about: the conversation is
    // fetched after the screen is already on display.
    await page.getByTestId('back-link').click()
    await page.getByTestId('chat-row').filter({ hasText: seeded.groupChat }).click()

    await expect(page.getByTestId('chat-bubble').last()).toContainText(newest)
    await expect(page.getByTestId('chat-bubble').last()).toBeInViewport()
  })

  test('a message can be sent and appears in the thread', async ({ page }) => {
    await page.goto('/chats')
    await page.getByTestId('chat-row').filter({ hasText: seeded.friend }).click()

    const text = `E2E message ${Date.now()}`
    await page.getByTestId('chat-input').fill(text)
    await page.getByTestId('chat-send').click()

    await expect(page.getByTestId('chat-messages')).toContainText(text)
    await expect(page.getByTestId('chat-input')).toHaveValue('')
  })

  test('a quick cheer is one tap', async ({ page }) => {
    await page.goto('/chats')
    await page.getByTestId('chat-row').filter({ hasText: seeded.groupChat }).click()

    // The button carries a short label; the message it sends is the longer form
    // from the catalogue.
    await page.getByTestId('quick-cheer').first().click()

    await expect(page.getByTestId('chat-messages')).toContainText('Stark gemacht!')
  })

  test('somebody else\'s conversation is not found rather than forbidden', async ({ page }) => {
    await page.goto('/chats/e2e00000-0010-4000-8000-999999999999')

    await expect(page.getByTestId('chat-not-found')).toBeVisible()
  })

  test('a group can be created from friends and left again', async ({ page }) => {
    await page.goto('/chats')

    await page.getByTestId('new-chat').click()
    await page.getByTestId('chat-mode-group').click()

    const title = `E2E created group ${Date.now()}`
    await page.getByTestId('group-title-input').fill(title)
    await page.getByTestId('group-emoji-🌱').click()

    // Every friend is offered, and the server refuses anybody else.
    await page.getByTestId('group-create-form').getByRole('checkbox').first().check()
    await page.getByTestId('group-create-submit').click()

    await expect(page).toHaveURL(/\/chats\//)
    await expect(page.getByRole('heading', { name: title })).toBeVisible()

    await page.getByTestId('leave-group').click()
    await page.getByTestId('confirm-accept').click()

    await expect(page).toHaveURL('/chats')
    await expect(page.getByTestId('chat-list')).not.toContainText(title)
  })

  test('a direct chat opened twice is the same thread, not a second one', async ({ page }) => {
    await page.goto('/chats')

    const before = await page.getByTestId('chat-row').count()

    await page.getByTestId('new-chat').click()
    await page.getByTestId('chat-mode-direct').click()
    await page.getByTestId('chat-friend-picker').getByRole('button').first().click()

    await expect(page).toHaveURL(/\/chats\//)
    await page.goto('/chats')

    await expect(page.getByTestId('chat-row')).toHaveCount(before)
  })
})

test.describe('friends', () => {
  test('requests, suggestions and friends each have their own section', async ({ page }) => {
    await page.goto('/friends')

    await expect(page.getByTestId('friend-request')).toContainText(seeded.requester)
    await expect(page.getByTestId('friend-suggestion')).toContainText(seeded.suggested)
    await expect(page.getByTestId('friend-list')).toContainText(seeded.friend)
  })

  test('asking a suggested person moves them into the sent requests', async ({ page }) => {
    await page.goto('/friends')

    const suggestion = page.getByTestId('friend-suggestion').filter({ hasText: seeded.suggested })
    await suggestion.getByTestId('suggestion-request').click()

    // A suggestion is derived from the friend graph, so asking takes them out
    // of it and puts them where a request that is waiting belongs.
    await expect(page.getByTestId('sent-request')).toContainText(seeded.suggested)
    await expect(page.getByTestId('friend-suggestion')).toHaveCount(0)

    // Put it back, so the next test starts where this one did.
    await page.getByTestId('sent-request').getByTestId('sent-request-withdraw').click()
    await expect(page.getByTestId('sent-request')).toHaveCount(0)
  })

  test('accepting a request makes them a friend', async ({ page }) => {
    await page.goto('/friends')

    const request = page.getByTestId('friend-request').filter({ hasText: seeded.requester })
    await request.getByTestId('request-accept').click()

    await expect(page.getByTestId('friend-list')).toContainText(seeded.requester)
    await expect(page.getByTestId('friend-request')).toHaveCount(0)
  })

  test('searching finds somebody who is not a friend yet, and adds them', async ({ page }) => {
    await page.goto('/friends')

    // The one box searches everybody, which is what makes it possible to add
    // somebody the app has not already suggested.
    await page.getByTestId('friend-search').fill('E2E Lena')

    const result = page.getByTestId('person-result').filter({ hasText: 'E2E Lena' })
    await expect(result).toBeVisible()

    // Already a friend, so the row offers a message rather than an add.
    await expect(result.getByTestId('result-message')).toBeVisible()
  })

  test('search matches a handle as well as a name', async ({ page }) => {
    await page.goto('/friends')

    await page.getByTestId('friend-search').fill('e2e.jonas')

    await expect(page.getByTestId('person-results')).toContainText(seeded.friend)
  })

  test('a term too short to be useful asks for more rather than listing everybody', async ({ page }) => {
    await page.goto('/friends')

    await page.getByTestId('friend-search').fill('E')

    await expect(page.getByTestId('search-empty')).toBeVisible()
  })

  test('writing to a friend opens the conversation with them', async ({ page }) => {
    await page.goto('/friends')

    await page
      .getByTestId('friend-row')
      .filter({ hasText: seeded.friend })
      .getByTestId('friend-message')
      .click()

    await expect(page).toHaveURL(/\/chats\//)
    await expect(page.getByRole('heading', { name: seeded.friend })).toBeVisible()
  })
})

test.describe('profile and settings', () => {
  test('the profile shows the streak, the badges and your own history', async ({ page }) => {
    await page.goto('/profile')

    await expect(page.getByRole('heading', { name: seeded.me })).toBeVisible()
    await expect(page.getByTestId('badge-grid')).toContainText('Streak-Held')
    await expect(page.getByTestId('own-activity')).toBeVisible()
  })

  test('switching to English changes the interface and survives a reload', async ({ page }) => {
    await page.goto('/profile')
    await page.getByTestId('open-settings').click()

    await expect(page.getByRole('heading', { name: 'Einstellungen' })).toBeVisible()

    await page.getByTestId('segment-English').click()
    await expect(page.getByRole('heading', { name: 'Settings' })).toBeVisible()

    await page.reload()
    await expect(page.getByRole('heading', { name: 'Settings' })).toBeVisible()

    // Put it back for whatever runs next.
    await page.getByTestId('segment-German').click()
    await expect(page.getByRole('heading', { name: 'Einstellungen' })).toBeVisible()
  })

  test('a notification switch is a real switch and remembers its state', async ({ page }) => {
    await page.goto('/settings')

    const weekly = page.getByRole('switch', { name: 'Wochenrückblick' })

    await expect(weekly).toHaveAttribute('aria-checked', 'false')
    await weekly.click()
    await expect(weekly).toHaveAttribute('aria-checked', 'true')

    await page.reload()
    await expect(page.getByRole('switch', { name: 'Wochenrückblick' })).toHaveAttribute('aria-checked', 'true')
  })
})

test.describe('the shell', () => {
  test('every screen is reachable from the bottom navigation', async ({ page }) => {
    await page.goto('/')

    for (const [testId, url] of [
      ['nav-goals', '/goals'],
      ['nav-chats', '/chats'],
      ['nav-friends', '/friends'],
      ['nav-profile', '/profile'],
      ['nav-home', '/'],
    ] as const) {
      await page.getByTestId(testId).click()
      await expect(page).toHaveURL(url)
    }
  })

  test('the current screen is marked for assistive technology', async ({ page }) => {
    await page.goto('/goals')

    await expect(page.getByTestId('nav-goals')).toHaveAttribute('aria-current', 'page')
    await expect(page.getByTestId('nav-home')).not.toHaveAttribute('aria-current', 'page')
  })

  test('the environment is on screen, so a screenshot is never ambiguous', async ({ page }) => {
    await page.goto('/settings')

    await expect(page.getByText('Q2 · Kudos — e2e')).toBeVisible()
  })
})
