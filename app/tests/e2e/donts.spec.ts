import { expect, test } from '@playwright/test'

/**
 * A Don't, end to end at phone width (docs/adr/0037-donts.md): made in the
 * sheet as "Ich will nicht mehr …", a use logged in one tap, the relapse
 * asked about first and then stated, in the goal and in its conversation.
 *
 * Every Don't created here is named after its test, because the suite shares
 * one database and a relapse cannot be taken back.
 */

const friend = 'E2E Lena'

/** From the E2E seed: a weekly Don't with one use logged this week and a relapse behind it. */
const seededDont = 'E2E don\'t with a weekly limit'

test.use({ serviceWorkers: 'block' })

test('the seeded Don\'t is on the start screen with what its week has used', async ({ page }, testInfo) => {
  await page.goto('/')

  const row = page.getByTestId('dont-row').filter({ hasText: seededDont })
  await expect(row).toBeVisible()
  await expect(row.getByTestId('dont-used')).toHaveText('1 von 2 genutzt')
  await expect(row).toContainText('Höchstens 2× pro Woche')

  await row.scrollIntoViewIfNeeded()
  await page.screenshot({ animations: 'disabled', path: testInfo.outputPath('start.png') })
})

test('a Don\'t is made, a use is logged, and the relapse is asked about and then said', async ({ page }, testInfo) => {
  const title = `E2E no sugar ${Date.now()}`

  await page.goto('/goals?create=1')
  await page.getByTestId('segment-Dont').click()
  await page.getByTestId('goal-title-input').fill(title)
  await page.getByTestId('goal-next').click()

  // "Höchstens 1× pro Tag", on the owner's word.
  await page.getByTestId('dont-limit-more').click()
  await expect(page.getByTestId('dont-limit')).toHaveText('1×')
  await page.getByTestId('dont-mode-HonestOnly').click()
  await page.screenshot({ animations: 'disabled', path: testInfo.outputPath('limit-step.png') })
  await page.getByTestId('goal-next').click()

  await page.getByTestId('goal-friend-picker').getByText(friend).click()
  await page.getByTestId('goal-next').click()
  await expect(page.getByTestId('goal-create-review')).toContainText('Höchstens 1× pro Tag')
  await page.getByTestId('goal-submit').click()

  await page.getByTestId('goal-card').filter({ hasText: title }).click()
  await expect(page.getByTestId('dont-run')).toHaveText('Ab heute')
  await expect(page.getByTestId('dont-period')).toHaveText(/^\s*0 von 1 genutzt$/)

  // Within the limit: one tap, no question.
  await page.getByTestId('dont-log').click()
  await expect(page.getByTestId('dont-period')).toHaveText(/^\s*1 von 1 genutzt$/)

  // Over it: asked first, then a relapse — held, not missed.
  await page.getByTestId('dont-log').click()
  await expect(page.getByTestId('confirm-dialog')).toBeVisible()
  await page.screenshot({ animations: 'disabled', path: testInfo.outputPath('confirm.png') })
  await page.getByTestId('confirm-accept').click()

  await expect(page.getByTestId('dont-period')).toHaveText(/^\s*Rückfall$/)
  await expect(page.getByTestId('goal-relapses')).toHaveText('1 Rückfall')
  await expect(page.getByTestId('dont-run')).toHaveText('Ab morgen')
  await expect(page.getByTestId('goal-balance')).toContainText('0 · 0')
  await page.screenshot({ animations: 'disabled', path: testInfo.outputPath('relapse.png') })

  // The conversation says each use and the relapse, as lines.
  await page.getByTestId('goal-open-chat').click()
  await expect(page.getByTestId('chat-event').filter({ hasText: 'Du hast eingetragen · 1 von 1' })).toBeVisible()
  await expect(page.locator('[data-testid="chat-event"][data-kind="WindowRelapse"]')).toContainText('Rückfall')
  await page.screenshot({ animations: 'disabled', path: testInfo.outputPath('chat.png') })
})
