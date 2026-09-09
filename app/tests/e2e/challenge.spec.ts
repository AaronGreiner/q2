import { expect, test } from '@playwright/test'
import { deliverPhoto } from './support/proofPhoto'

/**
 * The daily challenge, end to end.
 *
 * Deliberately narrow. What only a browser can prove is the round trip — a real
 * picture through the file picker, into the room, into the archive, and back
 * out — so that is what is here. Who ends up in whose room and what a covered
 * entry contains are decided on the server and asserted there
 * (`ChallengeEndpointTests`); how a covered card and the banner draw is
 * asserted in `tests/component/Challenge.spec.ts`.
 *
 * There is one challenge in the seeded world and every spec signs in as the
 * same person, so the test that joins in takes itself back out again — which is
 * also the feature's own rule, and therefore an assertion rather than
 * housekeeping (docs/testing.md).
 */
const prompt = 'E2E challenge of the day'

test.describe('the daily challenge', () => {
  test('the banner is a trailer, and the room is where the camera is', async ({ page }) => {
    await page.goto('/')

    const banner = page.getByTestId('challenge-banner')
    await expect(banner).toContainText(prompt)
    await expect(banner).toContainText('Mitmachen')

    await banner.click()

    // Nothing is contributed from the banner: the covered contributions are
    // the reason to join, and they are only visible from inside the room.
    await expect(page.getByTestId('challenge-prompt')).toContainText(prompt)
    await expect(page.getByTestId('challenge-join')).toBeVisible()
    await expect(page.getByTestId('challenge-archive-link')).toBeVisible()
  })

  test('a contribution goes in, lands in your archive, and can be taken back', async ({ page }) => {
    await page.goto('/challenge')

    await page.getByTestId('challenge-join').click()
    await deliverPhoto(page)

    await expect(page.getByTestId('challenge-entry')).toBeVisible()
    await expect(page.getByTestId('challenge-image')).toBeVisible()

    await page.getByTestId('challenge-archive-link').click()
    await expect(page.getByTestId('challenge-archive-tile').first()).toBeVisible()

    // Back out again, which leaves the world as this spec found it.
    await page.goto('/challenge')
    await page.getByTestId('challenge-remove').click()
    await page.getByTestId('confirm-accept').click()

    await expect(page.getByTestId('challenge-join')).toBeVisible()
  })
})
