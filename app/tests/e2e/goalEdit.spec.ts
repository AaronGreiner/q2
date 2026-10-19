import { expect, test } from '@playwright/test'
import { apiBaseUrl, e2eAccount } from './support/e2eEnvironment'
import { createGoal } from './support/createGoal'
import { goalLink } from './support/goalList'

/**
 * Editing a running goal, end to end, from both ends
 * (docs/adr/0044-editing-a-running-goal.md): the owner changes it in the sheet
 * and is told before saving what the checkers can do about it; a checker sees
 * the change waiting and objects to it in a browser of their own.
 *
 * The goal is created by the test itself, because an edit changes what every
 * later test would see.
 */

const lena = { name: 'E2E Lena', email: 'e2e.lena@kudos.example' }

test('a goal made easier waits for its checkers, who can object to it', async ({ page, browser }, testInfo) => {
  const title = `E2E edit ${Date.now()}`
  const renamed = `${title} renamed`

  await createGoal(page, { title, friend: 'E2E Jonas' })
  await goalLink(page, title).click()
  await expect(page.getByTestId('goal-detail')).toBeVisible()
  const goalUrl = page.url()

  await page.getByTestId('goal-exits').click()
  await page.getByRole('menuitem', { name: 'Bearbeiten' }).click()

  // What: the title, starting from the one it has.
  await expect(page.getByTestId('goal-edit-title')).toHaveValue(title)
  await page.getByTestId('goal-edit-title').fill(renamed)
  await page.getByTestId('goal-edit-next').click()

  // How often: every other day instead of every day — easier.
  await page.getByTestId('schedule-interval-2').click()
  await page.getByTestId('goal-edit-next').click()

  // Who: Lena checks it too, from now on.
  await page.getByTestId('goal-friend-picker').getByText(lena.name).click()
  await page.getByTestId('goal-edit-next').click()

  // The summary is the server's: what applies now, what waits, and that the
  // checkers can object — before anything is saved.
  await expect(page.getByTestId('goal-edit-now')).toContainText('E2E Lena prüft jetzt mit')
  await expect(page.getByTestId('goal-edit-later')).toContainText('Rhythmus: Jeden Tag → Alle 2 Tage')
  await expect(page.getByTestId('goal-edit-notice')).toContainText('widersprechen')

  await page.getByTestId('goal-edit-submit').click()

  // The title applies at once; the rhythm waits, and says so.
  await expect(page.getByRole('heading', { name: renamed })).toBeVisible()
  const banner = page.getByTestId('change-banner')
  await expect(banner).toContainText('Änderung ausstehend')
  await expect(banner).toContainText('Rhythmus: Jeden Tag → Alle 2 Tage')
  await expect(page.getByTestId('goal-team-member').filter({ hasText: lena.name })).toBeVisible()

  // Lena's side, in a browser of her own.
  const theirs = await browser.newContext({ ...testInfo.project.use, storageState: { cookies: [], origins: [] } })

  try {
    const signIn = await theirs.request.post(`${apiBaseUrl}/api/auth/login`, {
      data: { email: lena.email, password: e2eAccount.password },
    })
    expect(signIn.ok(), `signing in as ${lena.email}`).toBe(true)

    const lenaPage = await theirs.newPage()
    await lenaPage.goto(goalUrl)

    const seen = lenaPage.getByTestId('change-banner')
    await expect(seen).toContainText('0 von 2 Einsprüchen')
    await expect(lenaPage.getByTestId('change-withdraw')).toHaveCount(0)

    await lenaPage.getByTestId('change-veto').click()
    await expect(seen).toContainText('1 von 2 Einsprüchen')
    await expect(lenaPage.getByTestId('change-veto')).toHaveText('Einspruch zurückziehen')

    // The conversation says the same, derived from the edit.
    await lenaPage.getByTestId('goal-open-chat').click()
    const line = lenaPage.getByTestId('chat-event').filter({ hasText: 'hat es geändert' })
    await expect(line).toContainText('Rhythmus: Jeden Tag → Alle 2 Tage')
    await expect(line).toContainText('E2E Lena prüft jetzt mit')
  }
  finally {
    await theirs.close()
  }

  // And the owner can take it back.
  await page.reload()
  await page.getByTestId('change-withdraw').click()
  await expect(page.getByTestId('change-banner')).toBeHidden()
})
