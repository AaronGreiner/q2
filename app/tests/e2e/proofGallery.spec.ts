import { expect, test } from '@playwright/test'
import { deliverPhoto } from './support/proofPhoto'

/**
 * Your own photographs, found again on your profile (issue #42).
 *
 * A photograph is delivered through the real sheet, because seeds write no
 * image bytes — and then followed from the profile's preview to the whole
 * gallery, to the full-size view, and back to the goal it was delivered for.
 * The goal is made by the test and named after it, because the suite shares
 * one database; the tile is found by that name rather than by position.
 */

const friend = { name: 'E2E Jonas' }

test('a delivered photograph is on your profile, in your gallery and full size', async ({ page }) => {
  const title = `E2E gallery ${Date.now()}`

  await page.goto('/goals?create=1')
  await page.getByTestId('goal-title-input').fill(title)
  await page.getByTestId('goal-friend-picker').getByText(friend.name).click()
  await page.getByTestId('goal-submit').click()

  await page.getByTestId('goal-card').filter({ hasText: title }).click()
  await expect(page).toHaveURL(/\/goals\/[^/?]+$/)
  const goalUrl = page.url()

  await page.getByTestId('goal-open-chat').click()
  await page.getByTestId('chat-deliver').click()
  await deliverPhoto(page)
  await expect(page.getByTestId('chat-proof').last().getByTestId('proof-status')).toContainText('Dein eigener Beweis')

  // The newest photograph leads the preview, and says it is still being checked.
  await page.goto('/profile')
  const preview = page.getByTestId('own-proofs').getByRole('button', { name: title })
  await expect(preview).toBeVisible()
  await expect(preview.getByTestId('proof-gallery-status')).toHaveText('Wird geprüft')

  // The whole gallery, grouped by month.
  await page.getByTestId('open-proof-gallery').click()
  await expect(page).toHaveURL(/\/profile\/photos$/)
  await expect(page.getByRole('heading', { level: 1, name: 'Deine Beweisfotos' })).toBeVisible()

  const tile = page.getByTestId('proof-gallery').getByRole('button', { name: title })
  await expect(tile).toBeVisible()

  // Full size: the goal, the outcome, and the picture itself — loaded through
  // the session, which is the part a unit test cannot show.
  await tile.click()
  const viewer = page.getByRole('dialog')
  await expect(viewer).toContainText(title)
  await expect(viewer).toContainText('Wird geprüft')

  const image = viewer.getByTestId('proof-viewer').locator('img')
  await expect(image).toBeVisible()
  await expect.poll(() => image.evaluate((element: HTMLImageElement) => element.naturalWidth)).toBeGreaterThan(0)

  // Closing puts you back in the grid; the goal is one tap away.
  await viewer.getByTestId('proof-viewer-close').click()
  await expect(viewer).toHaveCount(0)
  await expect(tile).toBeVisible()

  await tile.click()
  await page.getByTestId('proof-viewer-goal').click()
  await expect(page).toHaveURL(goalUrl)
})
