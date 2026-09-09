import { expect, test } from '@playwright/test'

/**
 * The exits, end to end: setting a goal aside, coming back, stopping it, and
 * deleting it for good.
 *
 * Every test here works on a goal it creates itself. That is not tidiness — a
 * pause spends part of a monthly allowance and stopping a goal is one-way, so a
 * test that used a seeded goal would change what every later test sees
 * (docs/testing.md).
 */

/** Long enough to pass the ten-character rule the sheet enforces. */
const reason = 'Grippe, seit Freitag im Bett.'

/** Creates a daily goal and returns its title. */
async function createGoal(page: import('@playwright/test').Page, name: string): Promise<string> {
  const title = `${name} ${Date.now()}`

  await page.goto('/goals?create=1')
  await page.getByTestId('goal-title-input').fill(title)
  await page.getByTestId('goal-submit').click()

  await expect(page.getByTestId('goal-card').filter({ hasText: title })).toBeVisible()

  return title
}

async function openGoal(page: import('@playwright/test').Page, title: string) {
  await page.goto('/goals?tab=goals')
  await page.getByTestId('goal-card').filter({ hasText: title }).click()
  await expect(page.getByTestId('goal-detail')).toBeVisible()
}

test.describe('setting a goal aside', () => {
  test('a reason and a few days suspend the window, and the way back is on the banner', async ({ page }) => {
    const title = await createGoal(page, 'E2E pause goal')
    await openGoal(page, title)

    await page.getByTestId('goal-pause-open').click()

    // The allowance is on the sheet rather than behind it: it is the thing that
    // makes somebody spend a pause on the week they are actually ill.
    await expect(page.getByTestId('pause-allowance')).toContainText('Noch 2 Pausen')

    // An excuse that is not a sentence does not submit.
    await page.getByTestId('pause-reason').fill('krank')
    await expect(page.getByTestId('pause-submit')).toBeDisabled()

    await page.getByTestId('pause-reason').fill(reason)
    await page.getByTestId('pause-days-3').click()
    await page.getByTestId('pause-submit').click()

    const banner = page.getByTestId('pause-banner')
    await expect(banner).toBeVisible()
    await expect(banner).toContainText(reason)

    // No window while it is set aside, so nothing to deliver into and nothing
    // to fail.
    await expect(page.getByTestId('goal-deliver-proof')).toBeHidden()
    await expect(page.getByTestId('goal-pause-open')).toBeHidden()

    // And it says so in the list as well, in grey rather than as a failure.
    await page.goto('/goals?tab=goals')
    await expect(
      page.getByTestId('goal-card').filter({ hasText: title }).getByTestId('goal-paused'),
    ).toContainText('Ausgesetzt')

    await openGoal(page, title)
    await page.getByTestId('pause-end').click()
    await expect(page.getByTestId('pause-banner')).toBeHidden()
  })
})

test.describe('the archive', () => {
  /**
   * The point of the whole stage: stopping keeps the record. A goal that has
   * been carried through leaves the list, keeps its balance, and can be found
   * again.
   */
  test('a goal that is stopped keeps its record and moves to the archive', async ({ page }) => {
    const title = await createGoal(page, 'E2E archive goal')
    await openGoal(page, title)

    const balance = (await page.getByTestId('goal-balance').innerText()).trim()

    await page.getByTestId('goal-close-open').click()
    await page.getByTestId('close-completed').click()

    // The exits disappear once it has stopped, which is how we know the write
    // landed before the record is compared.
    await expect(page.getByTestId('goal-close-open')).toBeHidden()

    // Stopping is not failing: the record is exactly what it was.
    expect((await page.getByTestId('goal-balance').innerText()).trim()).toBe(balance)

    // Gone from the list of things still owed …
    await page.goto('/goals?tab=goals')
    await expect(page.getByTestId('goal-card').filter({ hasText: title })).toHaveCount(0)

    // … and present in the archive, with the day it ended.
    await page.getByTestId('goals-archive-link').click()
    await expect(page).toHaveURL('/goals/archive')
    await expect(page.getByTestId('archive-row').filter({ hasText: title })).toBeVisible()
  })

  test('deleting from the archive asks first and then really removes it', async ({ page }) => {
    const title = await createGoal(page, 'E2E delete goal')
    await openGoal(page, title)

    await page.getByTestId('goal-close-open').click()
    await page.getByTestId('close-archived').click()

    await page.goto('/goals/archive')

    const row = page.getByTestId('archive-row').filter({ hasText: title })
    await row.getByTestId('archive-delete').click()

    // Deleting is the second decision, and it is asked in q2's own dialog
    // rather than the browser's.
    await expect(page.getByTestId('confirm-dialog')).toBeVisible()
    await page.getByTestId('confirm-accept').click()

    await expect(page.getByTestId('archive-row').filter({ hasText: title })).toHaveCount(0)
  })
})
