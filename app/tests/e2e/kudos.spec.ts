import { expect, test } from '@playwright/test'
import { apiBaseUrl, e2eAccount } from './support/e2eEnvironment'
import { deliverPhoto } from './support/proofPhoto'
import { createGoal } from './support/createGoal'
import { goalLink } from './support/goalList'
import { gotoHydrated } from './support/hydration'

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
  quotaGoal: 'E2E goal three times a week',
  dont: 'E2E don\'t with a weekly limit',
  groupChat: 'E2E group chat',
  sharedGoalId: 'e2e00000-0000-4000-8000-000000000001',
}

test.describe('the start screen', () => {
  test('opens on the streak beside the name and what friends have been doing', async ({ page }) => {
    await page.goto('/')

    // The streak is a lit flame in the header, said once (#121): five days
    // behind the seed, and no card of its own any more.
    const flame = page.getByTestId('streak-flame')
    await expect(flame).toHaveAttribute('data-lit', '')
    await expect(flame).toContainText('5 Tage in Folge')
    await expect(page.getByTestId('streak-count')).toHaveText('5')

    await expect(page.getByText(seeded.sharedGoal).first()).toBeVisible()
    await expect(page.getByTestId('activity-row').first()).toContainText(seeded.friend)
  })

  /**
   * Delivering into a window is the one write on this screen: the window
   * closes and leaves the list of what is still owed.
   *
   * It is deliberately one-way, so this uses the one goal nothing downstream
   * asserts on. A test that spent the shared goal's window would leave every
   * later test looking at a goal with nothing to deliver into — which is
   * exactly what "a window cannot be un-delivered" means.
   */
  test('delivering a proof closes the window', async ({ page }) => {
    await page.goto('/')

    // Wherever it is on the screen: the card at the top if it closes first,
    // otherwise a row under "Heute".
    const due = page.locator('[data-testid="window-row"], [data-testid="next-up"]')
    const row = due.filter({ hasText: seeded.zeroProgressGoal })

    // The camera opens from the row, and the file fallback stands in for it —
    // the same way somebody without a working camera delivers.
    await row.locator('[data-testid="window-deliver"], [data-testid="next-up-deliver"]').click()
    await deliverPhoto(page)

    // This goal has nobody on it, so there is nobody to convince: the
    // photograph is believed at once and the window closes.
    await expect(due.filter({ hasText: seeded.zeroProgressGoal })).toHaveCount(0)
  })

  /**
   * The friend's side of the same goals (#78, #79). `/api/today` carries the
   * goals somebody was asked to check as well — the Dos tab needs them — and
   * the start screen used to take them for the reader's own: a row under
   * "Heute", sometimes "Als Nächstes" with a camera, and a goal screen that
   * offered "Beweis liefern" to somebody who may not hand one in.
   */
  test('a friend\'s goals are not on the start screen, and their screen offers no camera', async ({ browser }, testInfo) => {
    const theirs = await browser.newContext({ ...testInfo.project.use, storageState: { cookies: [], origins: [] } })

    try {
      const signIn = await theirs.request.post(`${apiBaseUrl}/api/auth/login`, {
        data: { email: 'e2e.jonas@kudos.example', password: e2eAccount.password },
      })
      expect(signIn.ok(), 'signing in as the friend').toBe(true)

      const friendPage = await theirs.newPage()
      await friendPage.goto('/')
      await expect(friendPage.getByTestId('streak-flame')).toBeVisible()

      // Not by text on the whole screen: the feed and the vote stack may name
      // the same goal, and both are rightly there.
      const owed = friendPage.locator('[data-testid="window-row"], [data-testid="next-up"], [data-testid="dont-row"]')
      await expect(owed.filter({ hasText: seeded.sharedGoal })).toHaveCount(0)
      await expect(owed.filter({ hasText: seeded.dont })).toHaveCount(0)

      await friendPage.goto(`/goals/${seeded.sharedGoalId}`)
      await expect(friendPage.getByTestId('goal-owner')).toContainText(`Von ${seeded.me}`)
      await expect(friendPage.getByTestId('goal-deliver-proof')).toHaveCount(0)

      // Owed by its owner, or already being looked at — which one depends on
      // whether an earlier spec delivered into today's window.
      await expect(friendPage.locator('[data-testid="goal-proof-owed"], [data-testid="goal-proof-waiting"]')).toBeVisible()
    }
    finally {
      await theirs.close()
    }
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

    // Named after its line, not "Kudos geben" five times over (#103).
    await expect(button).toHaveAttribute('aria-label', new RegExp(`^Kudos geben: ${seeded.friend} `))
  })

  test('a feed line opens what it is about', async ({ page }) => {
    await page.goto('/activity')

    // A friend's run on no goal the reader is on opens the friend (#103); the
    // kudos button beside it stays a target of its own.
    const row = page.getByTestId('activity-row').filter({ hasText: seeded.friend }).first()
    await row.getByTestId('activity-link').click()

    await expect(page).toHaveURL(/\/people\//)
    await expect(page.getByTestId('content-panel')).toContainText(seeded.friend)
  })

  test('the header leads to your own profile', async ({ page }) => {
    await page.goto('/')

    // Light and dark used to sit here; that is a setting, and it lives there.
    await expect(page.getByTestId('theme-toggle')).toHaveCount(0)

    await page.getByTestId('home-profile').click()
    await expect(page).toHaveURL('/profile')
  })
})

test.describe('goals', () => {
  test('the Dos list what is due today first, with its camera, and then the rest', async ({ page }) => {
    await page.goto('/goals')

    // Your own goals that are due today carry the camera (#77).
    const mine = page.getByTestId('goal-section-mine')
    await expect(mine.getByTestId('window-row').filter({ hasText: seeded.sharedGoal })).toBeVisible()
    await expect(mine.getByTestId('window-row').filter({ hasText: seeded.quotaGoal })).toBeVisible()

    const list = page.getByTestId('goal-list')
    await expect(list).toContainText(seeded.sharedGoal)
    await expect(list).toContainText(seeded.zeroProgressGoal)
    await expect(list).toContainText(seeded.overdueGoal)

    // A Don't is under its own segment, never among the Dos.
    await expect(list).not.toContainText(seeded.dont)

    // What has stopped is not here any more. A finished goal has no deadline
    // and nothing to deliver into, so it lives in the archive
    // (docs/adr/0020-pause-and-archive.md).
    await expect(list).not.toContainText(seeded.completedGoal)

    await page.getByTestId('goals-archive-link').click()
    await expect(page.getByTestId('archive-row')).toContainText([seeded.completedGoal])
  })

  test('the Don\'ts are a segment of their own, kept in the address', async ({ page }) => {
    await page.goto('/goals')
    await page.getByTestId('segment-donts').click()

    await expect(page).toHaveURL('/goals?tab=donts')
    await expect(page.getByTestId('goal-list')).toContainText(seeded.dont)
    await expect(page.getByTestId('goal-list')).not.toContainText(seeded.sharedGoal)

    await page.reload()
    await expect(page.getByRole('radio', { name: 'Don’ts', exact: true })).toBeChecked()
  })

  /**
   * The same goal from both ends (#77): its owner finds it under "Deine" with
   * the camera, the friend checking it under "Von Freunden", with the owner's
   * name and nothing to press — only the owner may hand in a photograph.
   */
  test('your own goals and your friends\' are listed apart', async ({ page, browser }, testInfo) => {
    await page.goto('/goals')

    const mine = page.getByTestId('goal-section-mine')
    await expect(mine.getByRole('heading', { level: 2 })).toContainText('Deine')
    await expect(mine.getByTestId('window-row').filter({ hasText: seeded.sharedGoal })).toBeVisible()

    const theirs = await browser.newContext({ ...testInfo.project.use, storageState: { cookies: [], origins: [] } })

    try {
      const signIn = await theirs.request.post(`${apiBaseUrl}/api/auth/login`, {
        data: { email: 'e2e.jonas@kudos.example', password: e2eAccount.password },
      })
      expect(signIn.ok(), 'signing in as the friend').toBe(true)

      const friendPage = await theirs.newPage()
      await friendPage.goto('/goals')

      const friends = friendPage.getByTestId('goal-section-friends')
      await expect(friends.getByRole('heading', { level: 2 })).toContainText('Von Freunden')

      const row = friends.getByTestId('goal-card').filter({ hasText: seeded.sharedGoal })
      await expect(row.getByTestId('goal-owner')).toHaveText(seeded.me)
      await expect(friends.getByTestId('window-deliver')).toHaveCount(0)
      await expect(friendPage.getByTestId('goal-section-mine').getByText(seeded.sharedGoal)).toHaveCount(0)
    }
    finally {
      await theirs.close()
    }
  })

  /**
   * "Dreimal die Woche" is the shape the old rhythm enum could not express at
   * all, so it gets its own assertion on the card people actually read.
   */
  test('a quota goal says how much of its window is left', async ({ page }) => {
    await page.goto('/goals')

    const row = goalLink(page, seeded.quotaGoal)

    await expect(row).toContainText('3× pro Woche')
    await expect(row.getByTestId('window-remaining')).toContainText('Noch 2 von 3')
  })

  test('a goal whose chain has broken shows the miss in its record', async ({ page }) => {
    await page.goto('/goals')

    await goalLink(page, seeded.overdueGoal).click()

    await expect(page.getByTestId('goal-balance')).toContainText('verpasst')
    await expect(page.getByTestId('history-grid')).toContainText('Verpasst')
  })

  test('opening a goal shows its window, its team and its history', async ({ page }) => {
    await page.goto('/goals')

    await goalLink(page, seeded.sharedGoal).click()

    await expect(page).toHaveURL(`/goals/${seeded.sharedGoalId}`)
    await expect(page.getByRole('heading', { name: seeded.sharedGoal })).toBeVisible()
    await expect(page.getByTestId('progress-ring')).toHaveAttribute('aria-valuenow', '0')
    await expect(page.getByTestId('goal-team-member')).toContainText([seeded.friend, 'E2E Lena'])
    await expect(page.getByRole('heading', { name: 'Wer dich prüft' })).toBeVisible()
    await expect(page.getByTestId('history-grid')).toBeVisible()
  })

  /**
   * A shared goal is the case the whole product turns on: the owner delivers
   * and somebody else decides. So the window does *not* close here — it waits,
   * which is what "wird geprüft" on the screen means — and the owner is taken
   * to the conversation where the friends are voting on it.
   */
  test('delivering a proof on a shared goal hands it to the friends who vote', async ({ page }) => {
    await page.goto(`/goals/${seeded.sharedGoalId}`)

    await expect(page.getByTestId('progress-ring')).toHaveAttribute('aria-valuenow', '0')

    // A Do keeps its streak and its reminder; only a Don't goes without (#89).
    await expect(page.getByTestId('goal-streak')).toBeVisible()
    await expect(page.getByTestId('goal-reminder')).toBeVisible()

    await page.getByTestId('goal-deliver-proof').click()
    await deliverPhoto(page)

    await expect(page).toHaveURL(/\/chats\//)
    await expect(page.getByTestId('chat-proof').last().getByTestId('proof-status')).toContainText('Dein eigener Beweis')

    // Nothing to deliver again while friends are looking at the first one.
    await page.goto(`/goals/${seeded.sharedGoalId}`)
    await expect(page.getByTestId('goal-proof-waiting')).toBeVisible()
    await expect(page.getByTestId('goal-deliver-proof')).toHaveCount(0)
  })

  test('a goal that does not exist gets its own calm state', async ({ page }) => {
    await page.goto('/goals/e2e00000-0000-4000-8000-999999999999')

    await expect(page.getByTestId('goal-not-found')).toBeVisible()
  })

  test('a new goal can be created and appears in the list', async ({ page }) => {
    // The way in is the tab bar's centre button, which is a link to this URL.
    await gotoHydrated(page, '/goals?create=1')

    const title = `E2E created goal ${Date.now()}`
    await page.getByTestId('goal-title-input').fill(title)
    await page.getByTestId('category-Sport').click()
    await page.getByTestId('goal-next').click()

    await page.getByTestId('schedule-kind-Times').click()
    await page.getByTestId('schedule-times-3').click()

    // The preview says, in the words the card will use, what is about to be
    // created.
    await expect(page.getByTestId('schedule-preview')).toContainText('3× pro Woche')
    await page.getByTestId('goal-next').click()

    // Nobody to check it, no way on.
    await expect(page.getByTestId('goal-next')).toBeDisabled()
    await page.getByTestId('goal-friend-picker').getByText(seeded.friend).click()
    await page.getByTestId('goal-next').click()

    // The summary says when the first window ends — a quota's is the end of
    // the week — before anything is committed.
    await expect(page.getByTestId('goal-create-review')).toContainText(title)
    // How many and until when (#96) — how many depends on the weekday it runs on.
    const firstWindow = page.getByTestId('goal-first-window')
    await expect(firstWindow).toContainText(/Dein erstes Fenster: \d Nachweise? bis/)

    // Three a week, cut to the days left (ADR 0051): three from Monday to
    // Wednesday, two on Thursday and Friday, one at the weekend. The card has
    // to ask for what the summary promised, whichever day this runs on (#144).
    const required = /Dein erstes Fenster: (\d) Nachweis/.exec(await firstWindow.innerText())?.[1]
    expect(required).toMatch(/^[123]$/)

    await page.getByTestId('goal-submit').click()

    // Due this week, so it is among your own with its camera (#77).
    const created = goalLink(page, title)
    await expect(created).toContainText('3× pro Woche')
    await expect(created.getByTestId('window-remaining')).toContainText(`Noch ${required} von ${required}`)
  })

  test('a goal can be due on named weekdays, which the old model could not say', async ({ page }) => {
    await page.goto('/goals?create=1')

    const title = `E2E weekdays goal ${Date.now()}`
    await createGoal(page, {
      title,
      friend: seeded.friend,
      rhythm: async (sheet) => {
        await sheet.getByTestId('schedule-kind-Weekdays').click()
        await sheet.getByTestId('schedule-weekday-Thursday').click()
      },
    })

    await expect(page.getByTestId('goal-list').filter({ hasText: title })).toBeVisible()
  })

  test('creating a goal without a title goes no further than the first step', async ({ page }) => {
    await page.goto('/goals?create=1')

    // The button stays out of reach rather than sending something the server
    // would only reject.
    await expect(page.getByTestId('goal-next')).toBeDisabled()
  })

  test('an idea fills the first step in one tap', async ({ page }) => {
    await page.goto('/goals?create=1')

    await page.getByTestId('goal-template-read').click()
    await expect(page.getByTestId('goal-title-input')).toHaveValue('Jeden Tag lesen')
    await expect(page.getByTestId('goal-next')).toBeEnabled()
  })
})

test.describe('chats', () => {
  test('the list shows who wrote last and what is unread', async ({ page }) => {
    await page.goto('/chats')

    const row = page.getByTestId('chat-row').filter({ hasText: seeded.friend })

    await expect(row).toContainText('E2E unread reply')
    await expect(page.getByTestId('chat-list')).toContainText(seeded.groupChat)
  })

  test('the list keeps your goals, friends\' goals and conversations apart', async ({ page }) => {
    await page.goto('/chats')

    await expect(page.getByTestId('chat-section-mine')).toContainText(seeded.sharedGoal)
    await expect(page.getByTestId('chat-section-conversations')).toContainText(seeded.friend)
    await expect(page.getByTestId('chat-section-conversations')).toContainText(seeded.groupChat)
    await expect(page.getByTestId('chat-section-mine')).not.toContainText(seeded.friend)
  })

  test('a goal\'s thread carries the goal and what happened to it', async ({ page }) => {
    await page.goto('/chats')

    await page.getByTestId('chat-section-mine').getByTestId('chat-row').filter({ hasText: seeded.sharedGoal }).click()

    await expect(page.getByRole('heading', { name: seeded.sharedGoal })).toBeVisible()
    await expect(page.getByTestId('chat-goal-banner')).toContainText(seeded.sharedGoal)
    await expect(page.getByTestId('chat-event').first()).toContainText('Du hast es angelegt.')

    // Everybody on the goal is in it, and nobody can walk out of it. The
    // header opens who they are, and the way to the goal itself.
    await page.getByTestId('chat-header-info').click()
    const members = page.getByTestId('chat-info-member')
    await expect(members.first()).toContainText('Du')
    await expect(members.filter({ hasText: seeded.friend })).toHaveCount(1)
    await expect(page.getByTestId('leave-group')).toHaveCount(0)
    await page.getByTestId('chat-info-goal').click()
    await expect(page).toHaveURL(`/goals/${seeded.sharedGoalId}`)
  })

  test('opening a thread marks it read', async ({ page }) => {
    await page.goto('/chats')

    await page.getByTestId('chat-row').filter({ hasText: seeded.friend }).click()

    await expect(page.getByRole('heading', { name: seeded.friend })).toBeVisible()
    await expect(page.getByTestId('chat-goal-banner')).toHaveCount(0)
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
    await gotoHydrated(page, '/chats')
    await page.getByTestId('chat-row').filter({ hasText: seeded.friend }).click()

    const text = `E2E message ${Date.now()}`
    await page.getByTestId('chat-input').fill(text)
    await page.getByTestId('chat-send').click()

    await expect(page.getByTestId('chat-messages')).toContainText(text)
    await expect(page.getByTestId('chat-input')).toHaveValue('')
  })

  test('a photograph can be sent and appears in the thread', async ({ page }) => {
    await page.goto('/chats')
    await page.getByTestId('chat-row').filter({ hasText: seeded.groupChat }).click()

    // No ready-made replies any more; the composer offers a photograph instead.
    await expect(page.getByTestId('quick-cheer')).toHaveCount(0)

    const before = await page.getByTestId('chat-photo').count()

    await page.getByTestId('chat-attach').click()
    await deliverPhoto(page)

    const photo = page.getByTestId('chat-photo').last()
    await expect(page.getByTestId('chat-photo')).toHaveCount(before + 1)

    // Loaded through the session, not merely a broken frame with a src on it.
    await expect(photo.locator('img')).toHaveJSProperty('complete', true)
    expect(await photo.locator('img').evaluate(img => (img as HTMLImageElement).naturalWidth)).toBeGreaterThan(0)
    await expect(photo).toBeInViewport()

    await page.getByTestId('back-link').click()
    await expect(page.getByTestId('chat-row').filter({ hasText: seeded.groupChat })).toContainText('Foto')
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
    await page.getByTestId('group-icon-sprout').click()

    // Every friend is offered, and the server refuses anybody else.
    await page.getByTestId('group-create-form').getByRole('checkbox').first().check()
    await page.getByTestId('group-create-submit').click()

    await expect(page).toHaveURL(/\/chats\//)
    await expect(page.getByRole('heading', { name: title })).toBeVisible()

    // Leaving is behind the header, with the members, not beside the mute switch.
    await page.getByTestId('chat-header-info').click()
    await expect(page.getByTestId('chat-info-member').first()).toContainText('Du')
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
    await page.goto('/search')

    await expect(page.getByTestId('friend-request')).toContainText(seeded.requester)
    await expect(page.getByTestId('friend-suggestion')).toContainText(seeded.suggested)
    await expect(page.getByTestId('friend-list')).toContainText(seeded.friend)
  })

  test('the person behind a request or a suggestion opens their profile', async ({ page }) => {
    for (const [row, link, name] of [
      ['friend-request', 'request-person', seeded.requester],
      ['friend-suggestion', 'suggestion-person', seeded.suggested],
    ] as const) {
      await page.goto('/search')

      // Somebody to look at before deciding about them (#87).
      await page.getByTestId(row).filter({ hasText: name }).getByTestId(link).click()

      await expect(page).toHaveURL(/\/people\//)
      await expect(page.getByTestId('content-panel')).toContainText(name)
    }
  })

  test('asking a suggested person moves them into the sent requests', async ({ page }) => {
    await page.goto('/search')

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
    await page.goto('/search')

    const request = page.getByTestId('friend-request').filter({ hasText: seeded.requester })
    await request.getByTestId('request-accept').click()

    await expect(page.getByTestId('friend-list')).toContainText(seeded.requester)
    await expect(page.getByTestId('friend-request')).toHaveCount(0)
  })

  test('searching finds somebody who is not a friend yet, and adds them', async ({ page }) => {
    await gotoHydrated(page, '/search')

    // The one box searches everybody, which is what makes it possible to add
    // somebody the app has not already suggested.
    await page.getByTestId('friend-search').fill('E2E Lena')

    const result = page.getByTestId('person-result').filter({ hasText: 'E2E Lena' })
    await expect(result).toBeVisible()

    // Already a friend, so the row offers a message rather than an add.
    await expect(result.getByTestId('result-message')).toBeVisible()
  })

  test('search matches a handle as well as a name', async ({ page }) => {
    await gotoHydrated(page, '/search')

    await page.getByTestId('friend-search').fill('e2e.jonas')

    await expect(page.getByTestId('person-results')).toContainText(seeded.friend)
  })

  test('a term too short to be useful asks for more rather than listing everybody', async ({ page }) => {
    await gotoHydrated(page, '/search')

    await page.getByTestId('friend-search').fill('E')

    await expect(page.getByTestId('search-empty')).toBeVisible()
  })

  test('writing to a friend opens the conversation with them', async ({ page }) => {
    await page.goto('/search')

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
  test('the profile shows the streak once, what was completed, and your own history', async ({ page }) => {
    await page.goto('/profile')

    await expect(page.getByRole('heading', { name: seeded.me })).toBeVisible()
    await expect(page.getByText('Abgeschlossen', { exact: true })).toBeVisible()

    // Every badge, earned or not, with what it takes (#22). The E2E seed has
    // given this person two; others are earned from the seeded record as the
    // run goes on — a week kept in full, say — so the count is not fixed.
    await expect(page.getByTestId('badge-grid')).toBeVisible()
    await expect(page.getByTestId('badge-count')).toHaveText(/^[2-6] von 6$/)
    await expect(page.getByTestId('badge-StreakHero')).toHaveAttribute('data-earned', 'true')
    await expect(page.getByTestId('badge-Bookworm')).toHaveAttribute('data-earned', 'true')
    await expect(page.getByTestId('badge-Marathon')).toHaveAttribute('data-earned', 'false')
    await expect(page.getByTestId('badge-Marathon')).toContainText('42 Fenster im Sport')
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

    // The server paints English itself rather than German that turns English
    // after hydration (#85) — on a screen that does not read the settings
    // itself, which the settings screen does. From the language this device
    // remembers…
    const serverHtml = async () => await (await page.request.get('/goals/archive')).text()
    let html = await serverHtml()
    expect(html).toMatch(/<html[^>]*\slang="en"/)
    expect(html).toContain('<title>Archive · Qdos</title>')

    // …and from the account's own setting on a device that remembers nothing.
    await page.context().clearCookies({ name: 'q2-language' })
    html = await serverHtml()
    expect(html).toMatch(/<html[^>]*\slang="en"/)

    // Put it back for whatever runs next.
    await page.getByTestId('segment-German').click()
    await expect(page.getByRole('heading', { name: 'Einstellungen' })).toBeVisible()
  })

  test('notifications have a screen of their own under the settings', async ({ page }) => {
    await page.goto('/settings')

    await page.getByTestId('open-notification-settings').click()

    await expect(page).toHaveURL('/settings/notifications')
    await expect(page.getByRole('heading', { name: 'Benachrichtigungen' })).toBeVisible()
  })

  test('the profile is edited from the settings too, in the same sheet as on the profile', async ({ page }) => {
    await page.goto('/settings')

    // A row that opens something now, not a greyed-out label (#92).
    await page.getByTestId('settings-edit-profile').click()

    const name = page.getByTestId('profile-name')
    await expect(name).toBeVisible()
    await expect(name).toHaveValue('E2E Mara')

    // Closing it leaves you where you were.
    await page.keyboard.press('Escape')
    await expect(name).toBeHidden()
    await expect(page).toHaveURL('/settings')
  })
})

test.describe('the shell', () => {
  test('every screen is reachable from the bottom navigation', async ({ page }) => {
    await page.goto('/')

    for (const [testId, url] of [
      ['nav-search', '/search'],
      ['nav-dos', '/goals'],
      ['nav-chats', '/chats'],
      ['nav-profile', '/profile'],
      ['nav-home', '/'],
    ] as const) {
      await page.getByTestId(testId).click()
      await expect(page).toHaveURL(url)
    }
  })

  test('the middle of the bar is the Dos, and a goal is made from its plus', async ({ page }) => {
    await page.goto('/')

    await page.getByTestId('nav-dos').click()
    await expect(page).toHaveURL('/goals')
    await expect(page.getByTestId('nav-dos')).toHaveAttribute('aria-current', 'page')
    await expect(page.getByTestId('nav-dos')).toHaveText('Dos')
    await expect(page.getByRole('heading', { level: 1, name: 'Dos & Don’ts' })).toBeVisible()
    // Reached through the tab bar, not `page.goto`: a client-side navigation
    // builds the screen off-document, which is where the radios used to be
    // named by their values rather than their labels (#65).
    await expect(page.getByRole('radio', { name: 'Dos', exact: true })).toBeChecked()
    await expect(page.getByRole('radio', { name: 'Don’ts', exact: true })).not.toBeChecked()
    await expect(page.getByRole('radio', { name: 'donts' })).toHaveCount(0)

    await page.getByTestId('open-create-goal').click()

    // A URL rather than component state, so the sheet survives a reload and
    // closes with the back gesture.
    await expect(page).toHaveURL('/goals?create=1')
    await expect(page.getByTestId('goal-create-form')).toBeVisible()
  })

  test('the addresses of the lists from before still open it', async ({ page }) => {
    // `?tab=goals` was the list while it was called "Ziele" (#60), and
    // `?tab=all` while it had a "Heute" beside it; both land on the Dos (#77).
    for (const address of ['/goals?tab=goals', '/goals?tab=all']) {
      await page.goto(address)

      await expect(page.getByRole('radio', { name: 'Dos', exact: true })).toBeChecked()
      await expect(page.getByTestId('goal-list')).toBeVisible()
    }
  })

  test('the current screen is marked for assistive technology', async ({ page }) => {
    await page.goto('/chats')

    await expect(page.getByTestId('nav-chats')).toHaveAttribute('aria-current', 'page')
    await expect(page.getByTestId('nav-home')).not.toHaveAttribute('aria-current', 'page')
  })

  test('the environment is on screen, so a screenshot is never ambiguous', async ({ page }) => {
    await page.goto('/settings')

    await expect(page.getByText('Qdos · e2e')).toBeVisible()
  })
})
