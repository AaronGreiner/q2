import { expect, test } from '@playwright/test'
import { deliverPhoto } from './support/proofPhoto'

/**
 * The daily challenge, end to end.
 *
 * Deliberately narrow. What only a browser can prove is the round trip — a real
 * picture through the file picker, into the room, into the archive, and back
 * out — so that is what is here. Who ends up in whose room and what a covered
 * entry contains are decided on the server and asserted there
 * (`ChallengeEndpointTests`); how a covered card and the start screen's
 * strip draw is asserted in `tests/component/Challenge.spec.ts`.
 *
 * There is one challenge in the seeded world and every spec signs in as the
 * same person, so each test that joins in takes itself back out again — which is
 * also the feature's own rule, and therefore an assertion rather than
 * housekeeping (docs/testing.md).
 */
const prompt = 'E2E challenge of the day'
const promptInEnglish = 'E2E challenge of the day, in English'

test.describe('the daily challenge', () => {
  /**
   * The card on the start screen has its own camera (#135), and what it takes
   * shows up in its strip straight away. Taking it back is the room's, so the
   * test leaves through it — which also proves the room is one tap away.
   */
  test('the start screen\'s card joins in, and the contribution shows up in its strip', async ({ page }) => {
    await page.goto('/')

    const card = page.getByTestId('challenge-card')
    await expect(card).toContainText(prompt)

    await card.getByTestId('challenge-card-join').click()
    await deliverPhoto(page)

    await expect(card.getByTestId('challenge-card-done')).toContainText('Erledigt')
    await expect(card.getByTestId('challenge-card-join')).toHaveCount(0)

    const own = card.getByTestId('challenge-tile').first()
    await expect(own.getByTestId('challenge-tile-name')).toHaveText('Du')
    await expect(own.getByTestId('proof-image')).toBeVisible()

    await card.getByTestId('challenge-card-room').click()
    await expect(page.getByTestId('challenge-prompt')).toContainText(prompt)

    await page.getByTestId('challenge-remove').click()
    await page.getByTestId('confirm-accept').click()
    await expect(page.getByTestId('challenge-join')).toBeVisible()
  })

  test('a contribution goes in, lands in your archive, and can be taken back', async ({ page }) => {
    await page.goto('/challenge')

    await page.getByTestId('challenge-join').click()
    await deliverPhoto(page)

    await expect(page.getByTestId('challenge-entry')).toBeVisible()
    await expect(page.getByTestId('challenge-entry').getByTestId('proof-image')).toBeVisible()

    // The file picker, not the shutter — and the card says so, as a
    // screenshot, the way a proof does (#64, #152).
    await expect(page.getByTestId('challenge-origin')).toHaveText('Screenshot')

    await page.getByTestId('challenge-archive-link').click()
    await expect(page.getByTestId('challenge-archive-tile').first()).toBeVisible()

    // Back out again, which leaves the world as this spec found it.
    await page.goto('/challenge')
    await page.getByTestId('challenge-remove').click()
    await page.getByTestId('confirm-accept').click()

    await expect(page.getByTestId('challenge-join')).toBeVisible()
  })

  /**
   * The prompt is the one text on the challenge's screens the server words,
   * so it follows the language chosen in the settings like everything around
   * it (#105) — on the start screen's card and in the room, without a reload.
   */
  test('in English, the card and the room read the prompt in English', async ({ page }) => {
    await page.goto('/profile')
    await page.getByTestId('open-settings').click()
    await page.getByTestId('segment-English').click()
    await expect(page.getByRole('heading', { name: 'Settings' })).toBeVisible()

    try {
      await page.getByTestId('back-link').click()
      await page.getByRole('link', { name: 'Home' }).click()

      const card = page.getByTestId('challenge-card')
      await expect(card).toContainText(promptInEnglish)

      await card.getByTestId('challenge-card-room').click()
      await expect(page.getByTestId('challenge-prompt')).toContainText(promptInEnglish)
    }
    finally {
      // Put it back for whatever runs next.
      await page.goto('/settings')
      await page.getByTestId('segment-German').click()
      await expect(page.getByRole('heading', { name: 'Einstellungen' })).toBeVisible()
    }
  })
})
