import { expect, test, type APIRequestContext, type Page } from '@playwright/test'
import { apiBaseUrl, e2eAccount } from './support/e2eEnvironment'

/**
 * Notifications, through a real browser: the bell, the switches, muting, and
 * the live connection.
 *
 * Deliberately narrow. Who gets told, and what stops it, is decided on the
 * server and asserted there (`NotificationPipelineTests`, `LiveHubTests`); the
 * encryption is checked against RFC 8291's own worked example in the unit
 * tests. What only a browser can prove is that the screens mean what they say:
 * a switch that survives a reload, a badge that moves while nobody touches the
 * page.
 *
 * The friend on the other end is played through the API, with a cookie jar of
 * their own. What is under test is what arrives on this screen, not how their
 * screen sent it.
 *
 * The per-device switch is not exercised. It needs a real push service and a
 * granted permission, and a headless browser can only demonstrate the state
 * this deployment is actually in: no VAPID keys, so nothing to switch on.
 */

/** From the E2E seed — see api/.../Seeding/E2ESeed.cs. Every seeded account shares one password. */
const friend = { name: 'E2E Jonas', email: 'e2e.jonas@kudos.example' }
const me = 'E2E Mara'

/** Resolves when a settings screen has actually saved something. */
function saved(page: Page) {
  return page.waitForResponse(response =>
    response.url().includes('/api/settings') && response.request().method() === 'PUT')
}

/**
 * Opens a screen and waits until the server is listening for this person.
 *
 * The app refreshes mounted reads when its live connection is up
 * (`onCaughtUp` in useLiveConnection), after the handshake — so that read is
 * the sign that what the friend does next will arrive, rather than land in the
 * moment before the connection existed. The server-rendered first paint reads
 * the badges on the server, where this page cannot see it, so the first read
 * the browser makes is that one. A thread uses the plain layout without badges,
 * so its catch-up reads the thread itself instead.
 */
async function openListening(page: Page, path: string) {
  const endpoint = path.startsWith('/chats/') ? `/api${path}` : '/api/counts'
  const listening = page.waitForResponse(response => response.url().endsWith(endpoint))
  await page.goto(path)
  await listening
}

async function signInAsFriend(request: APIRequestContext) {
  const response = await request.post('/api/auth/login', {
    data: { email: friend.email, password: e2eAccount.password },
  })

  expect(response.ok(), `signing in as ${friend.email}`).toBe(true)
}

async function myPersonId(page: Page): Promise<string> {
  const response = await page.request.get(`${apiBaseUrl}/api/auth/session`)
  return (await response.json() as { person: { id: string } }).person.id
}

test.describe('arriving while the app is open', () => {
  let other: APIRequestContext

  test.beforeAll(async ({ playwright }) => {
    other = await playwright.request.newContext({ baseURL: apiBaseUrl })
    await signInAsFriend(other)
  })

  test.afterAll(async () => {
    await other.dispose()
  })

  test('a message appears in the open chat list, and nothing reloads', async ({ page }) => {
    const personId = await myPersonId(page)
    await openListening(page, '/chats')

    // Survives anything but a real page load, so it is still there at the end
    // only if nothing was reloaded to show the message.
    await page.evaluate(() => Object.assign(window, { q2StillThisPage: true }))

    const chat = await (await other.post('/api/chats/direct', { data: { personId } })).json() as { id: string }
    const text = `E2E live message ${Date.now()}`
    expect((await other.post(`/api/chats/${chat.id}/messages`, { data: { text } })).ok()).toBe(true)

    await expect(page.getByTestId('chat-row').filter({ hasText: friend.name })).toContainText(text)
    await expect(page.getByTestId('nav-chats')).toContainText(/\d/)
    expect(await page.evaluate(() => (window as { q2StillThisPage?: boolean }).q2StillThisPage)).toBe(true)
  })

  test('an incoming message keeps the draft and focus while the thread refreshes', async ({ page }) => {
    const personId = await myPersonId(page)
    const chat = await (await other.post('/api/chats/direct', { data: { personId } })).json() as { id: string }
    await openListening(page, `/chats/${chat.id}`)

    const input = page.getByTestId('chat-input')
    const draft = 'My unfinished reply'
    await input.fill(draft)

    // Hold the live refresh in flight: a skeleton here would unmount the
    // composer, erase the draft and take the keyboard away.
    let started!: () => void
    let release!: () => void
    const refreshing = new Promise<void>((resolve) => {
      started = resolve
    })
    const released = new Promise<void>((resolve) => {
      release = resolve
    })
    await page.route(`**/api/chats/${chat.id}`, async (route) => {
      if (route.request().method() === 'GET') {
        started()
        await released
      }
      await route.continue()
    })

    const text = `E2E incoming while typing ${Date.now()}`
    expect((await other.post(`/api/chats/${chat.id}/messages`, { data: { text } })).ok()).toBe(true)
    try {
      await refreshing
      await expect(input).toHaveValue(draft)
      await expect(input).toBeFocused()
    }
    finally {
      release()
    }

    await expect(page.getByText(text, { exact: true })).toBeVisible()
    await expect(input).toHaveValue(draft)
    await expect(input).toBeFocused()
  })

  test('kudos from a friend ring the bell, and opening it is seeing it', async ({ page }) => {
    // Everything already there is seen, so the number that appears is this test's.
    await page.goto('/notifications')
    await openListening(page, '/')
    await expect(page.getByTestId('bell-count')).toHaveCount(0)

    const feed = await (await other.get('/api/feed')).json() as Array<{
      id: string
      actor: { displayName: string }
      hasMyKudos: boolean
    }>
    const mine = feed.find(entry => entry.actor.displayName === me)
    expect(mine, `${friend.name}'s feed has an entry by ${me}`).toBeDefined()

    // A toggle: take back any kudos already there first, so this one is new.
    if (mine!.hasMyKudos) await other.post(`/api/feed/${mine!.id}/kudos`)
    await other.post(`/api/feed/${mine!.id}/kudos`)

    await expect(page.getByTestId('bell-count')).toHaveText('1')

    await page.getByTestId('open-notifications').click()
    await expect(page).toHaveURL('/notifications')

    const fresh = page.getByTestId('notifications-fresh')
    await expect(fresh.getByTestId('notification-row').first()).toContainText(`${friend.name} hat dir Kudos`)

    await page.goBack()
    await expect(page.getByTestId('open-notifications')).toBeVisible()
    await expect(page.getByTestId('bell-count')).toHaveCount(0)

    // Put back, because the suite shares one database.
    await other.post(`/api/feed/${mine!.id}/kudos`)
  })
})

test.describe('notification settings', () => {
  test('every kind has a switch of its own, and it is remembered', async ({ page }) => {
    await page.goto('/settings/notifications')

    const reactions = page.getByRole('switch', { name: 'Kudos & Reaktionen' })

    // Every switch starts on: nothing is quieter than somebody chose.
    await expect(reactions).toHaveAttribute('aria-checked', 'true')

    await Promise.all([saved(page), reactions.click()])
    await expect(reactions).toHaveAttribute('aria-checked', 'false')

    await page.reload()
    await expect(page.getByRole('switch', { name: 'Kudos & Reaktionen' })).toHaveAttribute('aria-checked', 'false')

    // One switch moved, and only that one: the others did not come back off.
    await expect(page.getByRole('switch', { name: 'Nachrichten' })).toHaveAttribute('aria-checked', 'true')

    // Put back, because the suite shares one database.
    await Promise.all([saved(page), page.getByRole('switch', { name: 'Kudos & Reaktionen' }).click()])
    await expect(page.getByRole('switch', { name: 'Kudos & Reaktionen' })).toHaveAttribute('aria-checked', 'true')
  })

  test('quiet hours can be turned off and back on, and the window is remembered', async ({ page }) => {
    await page.goto('/settings/notifications')

    // On by default: a product that has to be told not to buzz at three in the
    // morning has already buzzed at three in the morning.
    await expect(page.getByTestId('quiet-hours')).toBeVisible()
    await expect(page.getByTestId('quiet-hours-from')).toHaveValue('22:00')
    await expect(page.getByTestId('quiet-hours-to')).toHaveValue('07:00')

    // The wait is armed before the action: the request is sent the moment the
    // control changes, so setting it up afterwards is a race the test loses
    // whenever the server is quick.
    await Promise.all([
      saved(page),
      page.getByTestId('quiet-hours-from').fill('21:30'),
    ])

    await page.reload()
    await expect(page.getByTestId('quiet-hours-from')).toHaveValue('21:30')

    // Off hides the window rather than leaving two controls that decide
    // nothing.
    await Promise.all([saved(page), page.getByRole('switch', { name: 'Ruhezeiten' }).click()])
    await expect(page.getByTestId('quiet-hours')).toBeHidden()

    await page.reload()
    await expect(page.getByTestId('quiet-hours')).toBeHidden()

    // Put back, because the suite shares one database.
    await Promise.all([saved(page), page.getByRole('switch', { name: 'Ruhezeiten' }).click()])
    await expect(page.getByTestId('quiet-hours')).toBeVisible()
    await Promise.all([
      saved(page),
      page.getByTestId('quiet-hours-from').fill('22:00'),
    ])
  })

  test('a deployment with no keys says so instead of offering a dead switch', async ({ page }) => {
    await page.goto('/settings/notifications')

    // The E2E host configures no VAPID pair, which is the default and a
    // supported state.
    await expect(page.getByTestId('push-unavailable')).toBeVisible()
    await expect(page.getByTestId('push-toggle')).toHaveCount(0)
  })
})

test.describe('muting a conversation', () => {
  test('is one tap in the thread, and the list says so', async ({ page }) => {
    await page.goto('/chats')
    await page.getByTestId('chat-row').filter({ hasText: friend.name }).click()

    const mute = page.getByTestId('mute-chat')
    await expect(mute).toHaveAttribute('aria-pressed', 'false')

    await mute.click()
    await expect(mute).toHaveAttribute('aria-pressed', 'true')

    await page.getByTestId('back-link').click()
    await expect(page.getByTestId('chat-row').filter({ hasText: friend.name }).getByTestId('chat-muted')).toBeVisible()

    // Put back, because the suite shares one database.
    await page.getByTestId('chat-row').filter({ hasText: friend.name }).click()
    await page.getByTestId('mute-chat').click()
    await expect(page.getByTestId('mute-chat')).toHaveAttribute('aria-pressed', 'false')
  })
})
