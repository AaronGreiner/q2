import { expect, test } from '@playwright/test'
import { deliverPhoto, pngBytes } from './support/proofPhoto'
import { createGoal } from './support/createGoal'
import { goalLink } from './support/goalList'

/**
 * Live or screenshot, end to end (#61): both routes are offered for every
 * proof, the camera screen says which label the friends will read, and the
 * proof carries it into its conversation.
 *
 * The synthetic camera needs launch flags, which Playwright accepts only at the
 * top of a file — hence a file of its own, like haptics-camera.spec.ts.
 */
test.use({
  launchOptions: { args: ['--use-fake-device-for-media-stream', '--use-fake-ui-for-media-stream'] },
  permissions: ['camera'],
  serviceWorkers: 'block',
})

// Not Jonas: files with launch flags run in a worker of their own after the
// rest, and by then safety.spec.ts has blocked him and ended the friendship.
const friend = 'E2E Lena'

async function openGoalChat(page: import('@playwright/test').Page, title: string) {
  await createGoal(page, { title, friend })
  await goalLink(page, title).click()
  await page.getByTestId('goal-open-chat').click()
  await expect(page.getByRole('heading', { name: title })).toBeVisible()
}

test('a photograph from the shutter is delivered as live', async ({ page }, testInfo) => {
  const title = `E2E live proof ${Date.now()}`
  await openGoalChat(page, title)

  await page.getByTestId('chat-deliver').click()

  const camera = page.getByTestId('photo-camera')
  await expect.poll(() => camera.evaluate(element => (element as HTMLVideoElement).videoWidth)).toBeGreaterThan(0)

  // Both routes are there: the shutter, and the screenshot beside it.
  await expect(page.getByRole('button', { name: 'Screenshot auswählen' })).toBeVisible()

  await page.screenshot({ animations: 'disabled', path: testInfo.outputPath('camera.png') })

  await page.getByTestId('photo-shutter').click()
  await expect(page.getByTestId('photo-source-note')).toContainText('Live')
  await page.screenshot({ animations: 'disabled', path: testInfo.outputPath('preview.png') })
  await page.getByTestId('photo-confirm').click()

  const card = page.getByTestId('chat-proof').last()
  await expect(card.getByTestId('proof-source')).toHaveText('Live')
  await card.scrollIntoViewIfNeeded()
  await page.screenshot({ animations: 'disabled', path: testInfo.outputPath('card.png') })
})

test('a picture from the phone is delivered as a screenshot, and says so before it is sent', async ({ page }, testInfo) => {
  const title = `E2E screenshot proof ${Date.now()}`
  await openGoalChat(page, title)

  await page.getByTestId('chat-deliver').click()
  await page.getByTestId('photo-file').setInputFiles({
    name: 'screen-time.png',
    mimeType: 'image/png',
    buffer: pngBytes(),
  })

  await expect(page.getByTestId('photo-source-note')).toContainText('Screenshot')
  await page.getByTestId('photo-confirm').click()

  const card = page.getByTestId('chat-proof').last()
  await expect(card.getByTestId('proof-source')).toHaveText('Screenshot')

  // Full screen says it too.
  await card.getByTestId('proof-enlarge').click()
  await expect(page.getByTestId('photo-viewer-info')).toBeVisible()
  await expect(page.getByTestId('photo-viewer-info').getByTestId('proof-source')).toHaveText('Screenshot')
  await page.screenshot({ animations: 'disabled', path: testInfo.outputPath('viewer.png') })
})

test('a challenge picture from the shutter is not called a gallery pick', async ({ page }, testInfo) => {
  await page.goto('/challenge')
  await page.getByTestId('challenge-join').click()

  const camera = page.getByTestId('photo-camera')
  await expect.poll(() => camera.evaluate(element => (element as HTMLVideoElement).videoWidth)).toBeGreaterThan(0)
  await page.getByTestId('photo-shutter').click()
  await page.getByTestId('photo-confirm').click()

  // Until #64 every contribution was sent as live, so this passed for the
  // wrong reason; challenge.spec.ts asserts the gallery half.
  await expect(page.getByTestId('challenge-image')).toBeVisible()
  await expect(page.getByTestId('challenge-origin')).toHaveCount(0)
  await page.screenshot({ animations: 'disabled', path: testInfo.outputPath('challenge.png') })

  // Back out again, which leaves the room as this spec found it.
  await page.getByTestId('challenge-remove').click()
  await page.getByTestId('confirm-accept').click()
  await expect(page.getByTestId('challenge-join')).toBeVisible()
})

test('your own gallery labels each photograph', async ({ page }, testInfo) => {
  const title = `E2E gallery source ${Date.now()}`
  await openGoalChat(page, title)

  await page.getByTestId('chat-deliver').click()
  await deliverPhoto(page)
  await expect(page.getByTestId('chat-proof').last().getByTestId('proof-source')).toHaveText('Screenshot')

  await page.goto('/profile/photos')
  await expect(page.getByTestId('proof-gallery-tile').first().getByTestId('proof-source')).toHaveText('Screenshot')
  await page.screenshot({ animations: 'disabled', path: testInfo.outputPath('gallery.png') })
})
