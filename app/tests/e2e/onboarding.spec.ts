import AxeBuilder from '@axe-core/playwright'
import { expect, test, type Browser, type BrowserContext, type Page, type TestInfo } from '@playwright/test'
import { apiBaseUrl } from './support/e2eEnvironment'
import { undersizedTargets } from './support/touchTargets'
import { waitForMotion } from './support/waitForMotion'

/**
 * The first-run introduction, through a real browser
 * (docs/adr/0039-first-run-onboarding.md, #93).
 *
 * Three things only a run like this can show: that the guard really keeps a
 * new account on the introduction and lets it go once it is over — on any
 * device, because the server remembers — that a first Do made without friends
 * waits and then starts the moment somebody arrives through the link, and that
 * somebody who arrived through a link makes their first Do in front of whoever
 * sent it.
 *
 * Every account is made fresh: the seeded world stands for people who met the
 * introduction long ago.
 */
test.use({ storageState: { cookies: [], origins: [] } })

const password = 'ein-langes-passwort'

interface Account {
  name: string
  email: string
  context: BrowserContext
}

async function register(browser: Browser, testInfo: TestInfo, label: string, inviteCode?: string): Promise<Account> {
  const unique = `${Date.now()}${Math.floor(Math.random() * 1000)}`
  const name = `E2E ${label} ${unique}`
  const email = `e2e.${label.toLowerCase()}.${unique}@kudos.example`

  const context = await browser.newContext({ ...testInfo.project.use, storageState: { cookies: [], origins: [] } })
  const response = await context.request.post(`${apiBaseUrl}/api/auth/register`, {
    data: { name, email, password, inviteCode },
  })
  expect(response.ok(), `registering ${email}`).toBe(true)

  return { name, email, context }
}

/** Goes past the notifications step where this browser was asked about them. */
async function pastNotifications(page: Page) {
  const onboarding = page.getByTestId('onboarding')
  await expect(onboarding).not.toHaveAttribute('data-current', 'invite')

  if (await onboarding.getAttribute('data-current') === 'notifications') {
    await page.getByTestId('onboarding-skip').click()
  }

  await expect(onboarding).toHaveAttribute('data-current', 'done')
}

test('a new account is walked through once, and its first Do waits for the friend the link brings', async ({ browser }, testInfo) => {
  const newcomer = await register(browser, testInfo, 'Neu')
  const page = await newcomer.context.newPage()
  let friend: Account | null = null

  try {
    // Wherever it goes, a new account is taken to the introduction first.
    await page.goto('/goals')
    await expect(page).toHaveURL('/onboarding')

    // What Qdos is, in three slides.
    const next = page.getByTestId('onboarding-welcome-next')
    await expect(page.getByTestId('onboarding-slide-promise')).toBeVisible()
    await next.click()
    await next.click()
    await expect(next).toHaveText('Los geht’s')
    await next.click()

    // The handle can be chosen, once, and is checked while typing.
    await expect(page).toHaveURL('/onboarding?step=profile')
    const handle = `e2e.${Date.now()}`
    await page.getByTestId('onboarding-handle').fill(handle)
    await expect(page.getByTestId('onboarding-handle-status')).toHaveText('Frei')
    await page.getByTestId('onboarding-profile-next').click()

    await expect(page).toHaveURL('/onboarding?step=appearance')
    await page.getByTestId('onboarding-appearance-next').click()

    // No friend yet: the first Do is kept waiting rather than refused (#93).
    await expect(page).toHaveURL('/onboarding?step=firstDo')
    await expect(page.getByTestId('onboarding-intro')).toContainText('Noch prüft dich niemand')
    await page.getByTestId('draft-idea-read').click()
    await page.getByTestId('onboarding-first-do-submit').click()

    // The link, sent for something — and once it has gone out, the Do keeps
    // waiting for whoever comes through it.
    await expect(page).toHaveURL('/onboarding?step=invite')
    await expect(page.getByTestId('goal-draft-card')).toContainText('Jeden Tag lesen')
    await page.getByTestId('onboarding-invite-copy').click()
    await expect(page.getByTestId('onboarding-invite-leave')).toHaveCount(0)
    await page.getByTestId('onboarding-invite-next').click()

    await pastNotifications(page)
    await expect(page.getByTestId('onboarding-summary-handle')).toContainText(`@${handle}`)
    await expect(page.getByTestId('onboarding-summary-firstDo')).toContainText('Wartet auf deinen ersten Freund')
    await page.getByTestId('onboarding-finish').click()

    // Into the app, with the tour over the tab bar.
    await expect(page).toHaveURL('/?tour=1')
    await expect(page.getByTestId('app-tour')).toHaveAttribute('data-stop', 'home')
    await page.getByTestId('app-tour-next').click()
    await expect(page.getByTestId('app-tour')).toHaveAttribute('data-stop', 'search')
    await page.getByTestId('app-tour-skip').click()
    await expect(page).toHaveURL('/')
    await expect(page.getByTestId('app-tour')).toHaveCount(0)

    // The start screen's next step is the waiting Do and its link.
    await expect(page.getByTestId('home-first-step')).toContainText('Jeden Tag lesen')
    await expect(page.getByTestId('home-no-tasks')).toHaveCount(0)

    // Never again — not on a reload, not by asking for it.
    await page.goto('/onboarding')
    await expect(page).toHaveURL('/')

    // Somebody arrives through the link: the waiting Do becomes a goal with
    // them checking it, in the same save as the friendship.
    const { code } = await (await newcomer.context.request.get(`${apiBaseUrl}/api/invite`)).json() as { code: string }
    friend = await register(browser, testInfo, 'Freund', code)

    await page.goto('/goals')
    await expect(page.getByTestId('goal-draft-card')).toHaveCount(0)
    await expect(page.getByTestId('goal-list')).toContainText('Jeden Tag lesen')

    const goals = await (await newcomer.context.request.get(`${apiBaseUrl}/api/goals`)).json() as {
      title: string
      participants: { displayName: string }[]
    }[]
    expect(goals).toEqual([expect.objectContaining({
      title: 'Jeden Tag lesen',
      participants: [expect.objectContaining({ displayName: friend.name })],
    })])
  }
  finally {
    await newcomer.context.close()
    await friend?.context.close()
  }
})

test('somebody who arrived through a link makes their first Do in front of whoever sent it', async ({ browser }, testInfo) => {
  const sender = await register(browser, testInfo, 'Sender')
  expect((await sender.context.request.post(`${apiBaseUrl}/api/onboarding/complete`)).ok()).toBe(true)

  const { code } = await (await sender.context.request.get(`${apiBaseUrl}/api/invite`)).json() as { code: string }
  const guest = await register(browser, testInfo, 'Gast', code)
  const page = await guest.context.newPage()

  try {
    await page.goto('/onboarding')

    // Everything that is optional can be put off.
    await page.getByTestId('onboarding-skip').click()
    await expect(page).toHaveURL('/onboarding?step=profile')
    await page.getByTestId('onboarding-profile-next').click()
    await page.getByTestId('onboarding-appearance-next').click()

    // A friend is there: an ordinary goal, with them already chosen to check it.
    await expect(page.getByTestId('onboarding-intro')).toContainText('Wähl, wer dich prüft')
    await expect(page.getByTestId('draft-checkers')).toContainText(sender.name)
    await page.getByTestId('draft-idea-run').click()
    await expect(page.getByTestId('onboarding-first-do-submit')).toHaveText('Anlegen')
    await page.getByTestId('onboarding-first-do-submit').click()

    await expect(page).toHaveURL('/onboarding?step=invite')
    await expect(page.getByRole('heading', { level: 1 })).toHaveText('Mehr Augen, mehr Halt')
    await page.getByTestId('onboarding-invite-next').click()

    await pastNotifications(page)
    await expect(page.getByTestId('onboarding-summary-firstDo')).toContainText('Läuft')
    await expect(page.getByTestId('onboarding-summary-friends')).toContainText('1 Freund')
    await page.getByTestId('onboarding-finish').click()
    await expect(page).toHaveURL('/?tour=1')

    // The sender is on it, checking.
    const theirs = await (await sender.context.request.get(`${apiBaseUrl}/api/goals`)).json() as { title: string, isMine: boolean }[]
    expect(theirs).toEqual([expect.objectContaining({ title: '3× pro Woche laufen', isMine: false })])
  }
  finally {
    await sender.context.close()
    await guest.context.close()
  }
})

test('somebody who does not want to invite anybody yet can leave, and the waiting Do lapses', async ({ browser }, testInfo) => {
  const account = await register(browser, testInfo, 'Allein')
  const page = await account.context.newPage()

  try {
    await page.goto('/onboarding?step=firstDo')
    await page.getByTestId('draft-idea-water').click()
    await page.getByTestId('onboarding-first-do-submit').click()

    await expect(page).toHaveURL('/onboarding?step=invite')
    await page.getByTestId('onboarding-invite-leave').click()

    // It asks first, naming the Do that will lapse.
    const dialog = page.getByRole('dialog')
    await expect(dialog).toContainText('2 L Wasser am Tag')
    await dialog.getByTestId('confirm-accept').click()

    // Straight into the app, past the remaining steps, with the tour.
    await expect(page).toHaveURL('/?tour=1')
    await expect(page.getByTestId('app-tour')).toBeVisible()
    await page.getByTestId('app-tour-skip').click()

    // Nothing is waiting any more; the start screen offers the link.
    await expect(page.getByTestId('home-first-step')).toContainText('Noch keine Freunde')
    await expect(page.getByTestId('goal-draft-card')).toHaveCount(0)

    const state = await (await account.context.request.get(`${apiBaseUrl}/api/goals/draft`)).json() as { draft: unknown }
    expect(state.draft).toBeNull()

    // And the introduction is over.
    await page.goto('/onboarding')
    await expect(page).toHaveURL('/')
  }
  finally {
    await account.context.close()
  }
})

test('the introduction is remembered on the account, not on the device', async ({ browser }, testInfo) => {
  const account = await register(browser, testInfo, 'Zwei')
  expect((await account.context.request.post(`${apiBaseUrl}/api/onboarding/complete`)).ok()).toBe(true)

  // Another browser, signed in through the form: straight into the app.
  const elsewhere = await browser.newContext({ ...testInfo.project.use, storageState: { cookies: [], origins: [] } })
  const page = await elsewhere.newPage()

  try {
    await page.goto('/login')
    await page.getByTestId('login-email').fill(account.email)
    await page.getByTestId('login-password').fill(password)
    await page.getByTestId('login-submit').click()

    await expect(page).toHaveURL('/')
    await expect(page.getByTestId('onboarding')).toHaveCount(0)
  }
  finally {
    await account.context.close()
    await elsewhere.close()
  }
})

/**
 * The checks quality.spec.ts runs on the signed-in screens, on every step of
 * the introduction — which it cannot reach, because its account met the
 * introduction long ago.
 */
async function holdsAtPhoneWidth(page: Page, step: string) {
  await waitForMotion(page)

  const geometry = await page.evaluate(() => ({
    viewport: document.documentElement.clientWidth,
    content: document.documentElement.scrollWidth,
  }))
  expect(geometry.content, `${step}: horizontal scrolling`).toBeLessThanOrEqual(geometry.viewport)

  const results = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa'])
    // The installed-app zoom decision is ADR 0013's one dated exception.
    .disableRules(['meta-viewport'])
    .analyze()
  expect(
    results.violations.flatMap(violation => violation.nodes.map(node => `${violation.id}: ${node.target.join(' ')}`)),
    `${step}: WCAG A/AA`,
  ).toEqual([])

  const undersized = await undersizedTargets(page, step)
  expect(undersized, `${step}: 44px touch targets`).toEqual([])
}

test('every step holds at phone width: no sideways scroll, no WCAG A/AA violation, 44px targets', async ({ browser }, testInfo) => {
  const account = await register(browser, testInfo, 'Geometrie')
  const page = await account.context.newPage()

  try {
    await page.goto('/onboarding')
    const onboarding = page.getByTestId('onboarding')

    await holdsAtPhoneWidth(page, 'welcome')
    await page.getByTestId('onboarding-skip').click()

    await expect(onboarding).toHaveAttribute('data-current', 'profile')
    await holdsAtPhoneWidth(page, 'profile')
    await page.getByTestId('onboarding-profile-next').click()

    await expect(onboarding).toHaveAttribute('data-current', 'appearance')
    await holdsAtPhoneWidth(page, 'appearance')
    await page.getByTestId('onboarding-appearance-next').click()

    await expect(onboarding).toHaveAttribute('data-current', 'firstDo')
    await holdsAtPhoneWidth(page, 'firstDo')
    await page.getByTestId('draft-idea-read').click()
    await holdsAtPhoneWidth(page, 'firstDo filled')
    await page.getByTestId('onboarding-first-do-submit').click()

    await expect(onboarding).toHaveAttribute('data-current', 'invite')
    await holdsAtPhoneWidth(page, 'invite')
    await page.getByTestId('onboarding-invite-copy').click()
    await page.getByTestId('onboarding-invite-next').click()

    await pastNotifications(page)
    await holdsAtPhoneWidth(page, 'done')
    await page.getByTestId('onboarding-finish').click()

    await expect(page.getByTestId('app-tour')).toBeVisible()
    await holdsAtPhoneWidth(page, 'tour')
  }
  finally {
    await account.context.close()
  }
})
