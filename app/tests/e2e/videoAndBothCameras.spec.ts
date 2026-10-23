import { expect, test, type Page } from '@playwright/test'
import { createGoal } from './support/createGoal'

/**
 * A proof from both cameras, and a proof that is a clip (#124, #125), through
 * the real camera screen, the real upload and the goal's conversation.
 *
 * Chromium's synthetic camera stands in for the phone's: two of them, so both
 * routes for both cameras can be driven — at once, where the browser lets two
 * run, and in turn, as on most phones, by telling the page this device has
 * already said no. Every goal is the test's own, named after it, because the
 * suite shares one database and a delivered window cannot be given back.
 *
 * Launch flags force a worker of their own, so Playwright accepts them only at
 * the top of a file (see haptics-camera.spec.ts).
 */
test.use({
  launchOptions: { args: ['--use-fake-device-for-media-stream=device-count=2', '--use-fake-ui-for-media-stream'] },
  permissions: ['camera', 'microphone'],
  // The production build's service worker would hide the API's requests.
  serviceWorkers: 'block',
})

// Not E2E Jonas: safety.spec.ts blocks and unblocks him earlier in the run,
// and a block ends the friendship, so he is no longer in the picker by the
// time this file runs.
const friend = 'E2E Lena'

/** Creates a goal of its own and opens its camera, with a live picture in the viewfinder. */
async function openProofCamera(page: Page, title: string) {
  await createGoal(page, { title, friend })
  await page.getByTestId('window-row').filter({ hasText: title }).getByTestId('window-deliver').click()

  const camera = page.getByTestId('photo-camera')
  await expect.poll(() => camera.evaluate(element => (element as HTMLVideoElement).videoWidth)).toBeGreaterThan(0)
}

test('both cameras at once: one press takes both, and friends see the front picture in the corner', async ({ page }, testInfo) => {
  await openProofCamera(page, `E2E both at once ${Date.now()}`)

  // On at every opening, with the front camera running beside the rear one.
  await expect(page.getByTestId('photo-both')).toHaveAttribute('aria-pressed', 'true')
  const front = page.getByTestId('photo-front-camera')
  await expect.poll(() => front.evaluate(element => (element as HTMLVideoElement).videoWidth)).toBeGreaterThan(0)
  await page.screenshot({ animations: 'disabled', path: testInfo.outputPath('both-at-once-live.png') })

  await page.getByTestId('photo-shutter').click()

  const preview = page.getByTestId('photo-preview')
  await expect(preview.getByTestId('proof-inset')).toBeVisible()
  await expect(page.getByTestId('photo-source-note')).toContainText('Live')
  await page.screenshot({ animations: 'disabled', path: testInfo.outputPath('both-at-once-preview.png') })

  await page.getByTestId('photo-confirm').click()

  await expect(page).toHaveURL(/\/chats\//)
  const card = page.getByTestId('chat-proof').last()
  await expect(card.getByTestId('proof-source')).toHaveText('Live')
  await expect(card.getByTestId('proof-inset')).toBeVisible()
  await card.scrollIntoViewIfNeeded()
  await page.screenshot({ animations: 'disabled', path: testInfo.outputPath('both-at-once-card.png') })

  // A tap on the corner makes the front picture the large one.
  const large = card.getByTestId('proof-image')
  const rear = await large.getAttribute('src')
  await card.getByTestId('proof-inset').click()
  await expect(large).not.toHaveAttribute('src', rear!)
  await expect(card.getByTestId('proof-inset')).toHaveAttribute('aria-label', 'Hauptbild groß zeigen')
})

/**
 * Most phones run one camera at a time: the rear picture first, then "Jetzt
 * du", then the front one.
 */
test('both cameras in turn: the rear picture, then "Jetzt du", then the front one', async ({ page }, testInfo) => {
  await page.addInitScript(() => localStorage.setItem('q2-camera-both-at-once', 'no'))

  await openProofCamera(page, `E2E both in turn ${Date.now()}`)

  await expect(page.getByTestId('photo-front-after')).toContainText('Danach du')
  await page.screenshot({ animations: 'disabled', path: testInfo.outputPath('in-turn-live.png') })
  await page.getByTestId('photo-shutter').click()

  await expect(page.getByTestId('photo-now-you')).toContainText('Jetzt du')
  await page.screenshot({ animations: 'disabled', path: testInfo.outputPath('in-turn-now-you.png') })
  await expect(page.getByTestId('photo-preview').getByTestId('proof-inset')).toBeVisible()

  await page.getByTestId('photo-confirm').click()

  await expect(page).toHaveURL(/\/chats\//)
  await expect(page.getByTestId('chat-proof').last().getByTestId('proof-inset')).toBeVisible()
})

test('a clip: recorded, thrown away, recorded again, and played in the conversation', async ({ page }, testInfo) => {
  const title = `E2E clip ${Date.now()}`
  await createGoal(page, { title, friend })

  // The one condition this cannot meet on its own: a Chromium that records
  // H.264. A build without the encoder offers no video mode at all, which is
  // the behaviour #124 asks for, and is covered by the component tests.
  const recordsMp4 = await page.evaluate(() => MediaRecorder.isTypeSupported('video/mp4;codecs=avc1.42E01E'))
  test.skip(!recordsMp4, 'This Chromium cannot record H.264, so it offers no video mode.')

  await page.getByTestId('window-row').filter({ hasText: title }).getByTestId('window-deliver').click()
  const camera = page.getByTestId('photo-camera')
  await expect.poll(() => camera.evaluate(element => (element as HTMLVideoElement).videoWidth)).toBeGreaterThan(0)

  // One camera for this one, and a clip rather than a photograph.
  await page.getByTestId('photo-both').click()
  await expect(page.getByTestId('photo-both')).toHaveAttribute('aria-pressed', 'false')
  await page.getByTestId('photo-modes').getByText('Video').click()

  const shutter = page.getByTestId('photo-shutter')
  await expect(shutter).toHaveAttribute('aria-label', 'Aufnahme starten')

  // A clip is only ever live: there is nothing to pick it from.
  await expect(page.getByTestId('photo-choose')).toHaveCount(0)
  await page.screenshot({ animations: 'disabled', path: testInfo.outputPath('clip-ready.png') })

  async function record() {
    await shutter.click()
    await expect(shutter).toHaveAttribute('aria-label', 'Aufnahme beenden')
    await expect(page.getByTestId('photo-recording-time')).toContainText('0:01 / 0:10')
    await page.screenshot({ animations: 'disabled', path: testInfo.outputPath('clip-recording.png') })
    await shutter.click()
    await expect(page.getByTestId('photo-preview').getByTestId('proof-video')).toBeVisible()
  }

  await record()

  // "Noch einmal" throws the clip away and goes back to recording.
  await page.getByTestId('photo-retake').click()
  await expect(shutter).toHaveAttribute('aria-label', 'Aufnahme starten')

  await record()
  await page.getByTestId('photo-confirm').click()

  await expect(page).toHaveURL(/\/chats\//)
  const card = page.getByTestId('chat-proof').last()
  const clip = card.getByTestId('proof-video')

  await expect(clip).toHaveAttribute('poster', /\/api\/images\//)
  await expect(card.getByTestId('proof-sound')).toHaveAttribute('aria-pressed', 'false')
  await card.scrollIntoViewIfNeeded()

  // Played from the API — which needs it to answer byte ranges — with the
  // session cookie, not merely drawn as a box.
  await expect.poll(() => clip.evaluate(element => (element as HTMLVideoElement).readyState)).toBeGreaterThan(0)
  expect(await clip.evaluate(element => (element as HTMLVideoElement).duration)).toBeGreaterThan(0.5)
  await page.screenshot({ animations: 'disabled', path: testInfo.outputPath('clip-card.png') })
})
