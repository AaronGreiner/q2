import { expect, test, type Page } from '@playwright/test'
import { createGoal } from './support/createGoal'

/**
 * A proof from both cameras, and a proof that is a clip (#124, #125) — the
 * order swapped before it is taken, the corner dragged out of the way, and a
 * clip filmed from both sides at once (#151) — through the real camera
 * screen, the real upload and the goal's conversation; and the same camera
 * for the daily challenge and a chat photograph (#152).
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

/** Drags the corner of `scope` across to the other top corner, the way a thumb would. */
async function dragCorner(page: Page, corner: ReturnType<Page['getByTestId']>) {
  const box = (await corner.boundingBox())!
  const y = box.y + box.height / 2

  await page.mouse.move(box.x + box.width / 2, y)
  await page.mouse.down()
  await page.mouse.move(box.x + box.width / 2 + 260, y + 4, { steps: 12 })
  await page.mouse.up()
}

/**
 * Waits for the front camera to be open in the viewfinder — mirrored, with a
 * picture — after a tap on the corner put it first: the shutter does nothing
 * while a camera is being opened again.
 */
async function frontCameraOpen(page: Page) {
  const camera = page.getByTestId('photo-camera')
  await expect(camera).toHaveClass(/-scale-x-100/)
  await expect.poll(() => camera.evaluate(element => (element as HTMLVideoElement).videoWidth)).toBeGreaterThan(0)
}

/** Whether this Chromium records H.264, which a clip needs (see the clip test below). */
async function recordsMp4(page: Page) {
  return page.evaluate(() => MediaRecorder.isTypeSupported('video/mp4;codecs=avc1.42E01E'))
}

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

  // A tap on the corner makes the front camera the large one before anything
  // is taken (#151); a drag moves the corner to the other top corner.
  const corner = page.getByTestId('photo-corner')
  await corner.click()
  await expect(corner).toHaveAttribute('aria-label', 'Rückkamera groß zeigen')
  await expect(front.locator('..')).toHaveClass(/inset-0/)
  await dragCorner(page, corner)
  await expect(corner).toHaveAttribute('data-side', 'end')
  // The drag was not also a tap: the front camera is still the large one.
  await expect(corner).toHaveAttribute('aria-label', 'Rückkamera groß zeigen')
  await page.screenshot({ animations: 'disabled', path: testInfo.outputPath('both-at-once-front-large.png') })

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

  // Friends start with the rear picture large, whichever was large in the camera.
  await expect(card.getByTestId('proof-inset')).toHaveAttribute('aria-label', 'Frontkamera groß zeigen')

  // A tap on the corner makes the front picture the large one.
  const large = card.getByTestId('proof-image')
  const rear = await large.getAttribute('src')
  await card.getByTestId('proof-inset').click()
  await expect(large).not.toHaveAttribute('src', rear!)
  await expect(card.getByTestId('proof-inset')).toHaveAttribute('aria-label', 'Hauptbild groß zeigen')

  // And a drag moves the corner out of the way, without swapping back.
  await dragCorner(page, card.getByTestId('proof-inset'))
  await expect(card.getByTestId('proof-inset')).toHaveAttribute('data-side', 'end')
  await expect(card.getByTestId('proof-inset')).toHaveAttribute('aria-label', 'Hauptbild groß zeigen')
  await page.screenshot({ animations: 'disabled', path: testInfo.outputPath('both-at-once-card-moved.png') })
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

/**
 * In turn, front first (#151): a tap on the corner opens the front camera
 * now, and the rear one follows after "Jetzt hinten" — and is still the main
 * picture friends see large.
 */
test('both cameras in turn, front first: a tap on the corner, then "Jetzt hinten"', async ({ page }, testInfo) => {
  await page.addInitScript(() => localStorage.setItem('q2-camera-both-at-once', 'no'))

  await openProofCamera(page, `E2E front first ${Date.now()}`)

  const corner = page.getByTestId('photo-front-after')
  await corner.click()
  await expect(corner).toContainText('Danach hinten')
  await frontCameraOpen(page)
  await page.screenshot({ animations: 'disabled', path: testInfo.outputPath('front-first-live.png') })

  await page.getByTestId('photo-shutter').click()

  await expect(page.getByTestId('photo-now-you')).toContainText('Jetzt hinten')
  await expect(page.getByTestId('photo-first-shot')).toBeVisible()
  await page.screenshot({ animations: 'disabled', path: testInfo.outputPath('front-first-now-rear.png') })
  await expect(page.getByTestId('photo-preview').getByTestId('proof-inset')).toBeVisible()

  await page.getByTestId('photo-confirm').click()

  await expect(page).toHaveURL(/\/chats\//)
  const card = page.getByTestId('chat-proof').last()
  await expect(card.getByTestId('proof-inset')).toHaveAttribute('aria-label', 'Frontkamera groß zeigen')
})

/**
 * In turn, front first, for a clip (#151): the front picture first, then
 * "Jetzt hinten", and the rear camera starts recording on its own — one press
 * to start, one to stop — while the front picture waits in the corner.
 */
test('a clip in turn, front first: the front picture, then the rear camera records on its own', async ({ page }, testInfo) => {
  await page.addInitScript(() => localStorage.setItem('q2-camera-both-at-once', 'no'))
  const title = `E2E clip front first ${Date.now()}`
  await createGoal(page, { title, friend })
  test.skip(!await recordsMp4(page), 'This Chromium cannot record H.264, so it offers no video mode.')

  await page.getByTestId('window-row').filter({ hasText: title }).getByTestId('window-deliver').click()
  const camera = page.getByTestId('photo-camera')
  await expect.poll(() => camera.evaluate(element => (element as HTMLVideoElement).videoWidth)).toBeGreaterThan(0)

  await page.getByTestId('photo-modes').getByText('Video').click()
  await page.getByTestId('photo-front-after').click()
  await expect(page.getByTestId('photo-front-after')).toContainText('Danach hinten')
  await frontCameraOpen(page)

  const shutter = page.getByTestId('photo-shutter')
  await shutter.click()
  await expect(page.getByTestId('photo-now-you')).toContainText('Jetzt hinten')

  // Recording on its own after the countdown, with the front picture in the corner.
  await expect(shutter).toHaveAttribute('aria-label', 'Aufnahme beenden')
  await expect(page.getByTestId('photo-first-shot')).toBeVisible()
  await expect(page.getByTestId('photo-front-after')).toHaveCount(0)
  await expect(page.getByTestId('photo-recording-time')).toContainText('0:01 / 0:10')
  await page.screenshot({ animations: 'disabled', path: testInfo.outputPath('clip-front-first-recording.png') })
  await shutter.click()

  const preview = page.getByTestId('photo-preview')
  await expect(preview.getByTestId('proof-video')).toBeVisible()
  await expect(preview.getByTestId('proof-inset-image')).toBeVisible()

  await page.getByTestId('photo-confirm').click()
  await expect(page).toHaveURL(/\/chats\//)
  await expect(page.getByTestId('chat-proof').last().getByTestId('proof-video')).toBeVisible()
})

/**
 * Both cameras filmed at once (#151): two clips, recorded together, delivered
 * with a still each, and played together in the conversation — the corner
 * kept on the large one's moment, and swapped in place.
 */
test('a clip from both cameras at once: two clips, played as one', async ({ page }, testInfo) => {
  const title = `E2E both clips ${Date.now()}`
  await createGoal(page, { title, friend })
  test.skip(!await recordsMp4(page), 'This Chromium cannot record H.264, so it offers no video mode.')

  await page.getByTestId('window-row').filter({ hasText: title }).getByTestId('window-deliver').click()
  const front = page.getByTestId('photo-front-camera')
  await expect.poll(() => front.evaluate(element => (element as HTMLVideoElement).videoWidth)).toBeGreaterThan(0)

  await page.getByTestId('photo-modes').getByText('Video').click()
  const shutter = page.getByTestId('photo-shutter')
  await shutter.click()
  await expect(page.getByTestId('photo-recording-time')).toContainText('0:02 / 0:10')
  await page.screenshot({ animations: 'disabled', path: testInfo.outputPath('both-clips-recording.png') })
  await shutter.click()

  const preview = page.getByTestId('photo-preview')
  await expect(preview.getByTestId('proof-video')).toBeVisible()
  await expect(preview.getByTestId('proof-inset-video')).toBeVisible()
  // Both play in the preview, each with a picture, before anything is sent.
  for (const id of ['proof-video', 'proof-inset-video']) {
    await expect.poll(() => preview.getByTestId(id).evaluate(element => (element as HTMLVideoElement).readyState)).toBeGreaterThan(1)
  }
  await page.screenshot({ animations: 'disabled', path: testInfo.outputPath('both-clips-preview.png') })

  await page.getByTestId('photo-confirm').click()

  await expect(page).toHaveURL(/\/chats\//)
  const card = page.getByTestId('chat-proof').last()
  await card.scrollIntoViewIfNeeded()
  const rear = card.getByTestId('proof-video')
  const frontClip = card.getByTestId('proof-inset-video')

  await expect(rear).toHaveAttribute('poster', /\/api\/images\//)
  await expect(frontClip).toHaveAttribute('poster', /\/api\/images\//)

  // Both play, from the API, and the corner keeps to the large one's moment.
  const playing = (clip: typeof rear) => clip.evaluate((element) => {
    const video = element as HTMLVideoElement
    return video.readyState > 2 && !video.paused && video.currentTime > 0
  })
  await expect.poll(() => playing(rear)).toBe(true)
  await expect.poll(() => playing(frontClip)).toBe(true)
  await page.screenshot({ animations: 'disabled', path: testInfo.outputPath('both-clips-card.png') })

  const apart = () => card.evaluate((element) => {
    const [large, corner] = ['proof-video', 'proof-inset-video']
      .map(id => element.querySelector<HTMLVideoElement>(`[data-testid="${id}"]`)!)
    return Math.abs(large!.currentTime - corner!.currentTime)
  })
  expect(await apart()).toBeLessThan(0.5)

  // A tap swaps them in place: the front clip large, still playing, the rear one silent in the corner.
  const frontSrc = await frontClip.getAttribute('src')
  await card.getByTestId('proof-inset').click()
  await expect(card.getByTestId('proof-video')).toHaveAttribute('src', frontSrc!)
  await expect.poll(() => playing(card.getByTestId('proof-video'))).toBe(true)
  expect(await card.getByTestId('proof-inset-video').evaluate(element => (element as HTMLVideoElement).muted)).toBe(true)
  expect(await apart()).toBeLessThan(0.5)
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

/**
 * The full camera everywhere but a profile picture (#152): the challenge
 * takes both cameras at once, and the room shows both, swappable. The seeded
 * world has one challenge, so the test takes its contribution back out.
 */
test('the daily challenge takes both cameras, and the room shows both', async ({ page }, testInfo) => {
  await page.goto('/challenge')
  await page.getByTestId('challenge-join').click()

  const front = page.getByTestId('photo-front-camera')
  await expect.poll(() => front.evaluate(element => (element as HTMLVideoElement).videoWidth)).toBeGreaterThan(0)
  await expect(page.getByTestId('photo-both')).toHaveAttribute('aria-pressed', 'true')

  // The gallery is offered as a screenshot, like a proof's.
  await expect(page.getByTestId('photo-choose')).toHaveAttribute('aria-label', 'Screenshot auswählen')

  await page.getByTestId('photo-shutter').click()
  await expect(page.getByTestId('photo-preview').getByTestId('proof-inset')).toBeVisible()
  await page.getByTestId('photo-confirm').click()

  const entry = page.getByTestId('challenge-entry').first()
  await expect(entry.getByTestId('proof-inset')).toBeVisible()
  await expect(entry.getByTestId('challenge-origin')).toHaveCount(0)
  await entry.scrollIntoViewIfNeeded()
  await page.screenshot({ animations: 'disabled', path: testInfo.outputPath('challenge-both.png') })

  await entry.getByTestId('proof-inset').click()
  await expect(entry.getByTestId('proof-inset')).toHaveAttribute('aria-label', 'Hauptbild groß zeigen')

  await page.getByTestId('challenge-remove').click()
  await page.getByTestId('confirm-accept').click()
  await expect(page.getByTestId('challenge-join')).toBeVisible()
})

/** A chat photograph may be two clips filmed at once, played together in the bubble (#152). */
test('a chat photograph can be two clips filmed at once', async ({ page }, testInfo) => {
  await page.goto('/chats')
  test.skip(!await recordsMp4(page), 'This Chromium cannot record H.264, so it offers no video mode.')

  await page.getByTestId('chat-row').filter({ hasText: 'E2E group chat' }).click()
  await page.getByTestId('chat-attach').click()

  const front = page.getByTestId('photo-front-camera')
  await expect.poll(() => front.evaluate(element => (element as HTMLVideoElement).videoWidth)).toBeGreaterThan(0)

  await page.getByTestId('photo-modes').getByText('Video').click()
  const shutter = page.getByTestId('photo-shutter')
  await shutter.click()
  await expect(page.getByTestId('photo-recording-time')).toContainText('0:01 / 0:10')
  await shutter.click()
  await expect(page.getByTestId('photo-preview').getByTestId('proof-inset-video')).toBeVisible()
  await page.getByTestId('photo-confirm').click()

  const bubble = page.getByTestId('chat-photo').last()
  await bubble.scrollIntoViewIfNeeded()
  await expect(bubble.getByTestId('proof-video')).toHaveAttribute('poster', /\/api\/images\//)
  await expect(bubble.getByTestId('proof-inset-video')).toHaveAttribute('src', /\/api\/images\//)
  await expect.poll(() => bubble.getByTestId('proof-inset-video').evaluate(element => (element as HTMLVideoElement).readyState)).toBeGreaterThan(0)
  await page.screenshot({ animations: 'disabled', path: testInfo.outputPath('chat-both-clips.png') })
})
