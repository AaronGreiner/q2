import { expect, test } from '@playwright/test'

/**
 * A thread longer than a page (#44): it opens on its newest lines, and
 * scrolling to the top reads the page before, until the first message — with
 * the line that was being read staying where it was while each page arrives.
 *
 * The seeded thread (`E2ESeed.LongChatTitle`) holds 75 messages, numbered
 * from the second on. A page is 30 lines, so the newest page is lines 46–75
 * and the way back crosses two page boundaries.
 */
const longThread = 'E2E long thread'
const firstMessage = 'E2E long thread begins here'
const pageSize = 30

const line = (number: number) => `E2E long thread line ${String(number).padStart(2, '0')}`

// `page.route` cannot see a request the production build's service worker
// makes, and holding a page back is how this spec measures the screen while
// one is on its way. Nothing here is about the worker; pwa.spec.ts covers it.
test.use({ serviceWorkers: 'block' })

test('a long thread is scrolled back to its first message a page at a time', async ({ page }) => {
  await page.goto('/chats')
  await page.getByTestId('chat-row').filter({ hasText: longThread }).click()

  const thread = page.getByTestId('chat-thread')
  const bubbles = page.getByTestId('chat-bubble')
  const text = (value: string) => page.getByText(value, { exact: true })

  // Opened on the newest page, at its newest line, and nothing older read.
  await expect(text(line(75))).toBeInViewport()
  await expect(bubbles).toHaveCount(pageSize)
  await expect(text(line(45))).toHaveCount(0)

  // Hold the page before back, so the screen can be measured while the
  // reader waits for it at the top.
  let release = () => {}
  const held = new Promise<void>((resolve) => {
    release = resolve
  })
  await page.route(/\/messages\?before=/, async (route) => {
    await held
    await route.continue()
  })

  await thread.evaluate((element) => {
    element.scrollTop = 0
  })

  await expect(page.getByTestId('chat-older').getByRole('status')).toHaveAttribute('aria-busy', 'true')
  const before = await text(line(46)).boundingBox()

  release()
  await expect(bubbles).toHaveCount(pageSize * 2)
  await page.unroute(/\/messages\?before=/)

  // The page arrived above the reader without moving what they were reading.
  const after = await text(line(46)).boundingBox()
  expect(Math.abs((after?.y ?? 0) - (before?.y ?? 1000))).toBeLessThan(2)
  await expect(text(line(16))).toBeAttached()

  // And once more, to the beginning of the thread.
  await thread.evaluate((element) => {
    element.scrollTop = 0
  })

  await expect(bubbles).toHaveCount(75)
  await expect(text(line(16))).toBeInViewport()

  await thread.evaluate((element) => {
    element.scrollTop = 0
  })

  await expect(text(firstMessage)).toBeInViewport()
  await expect(page.getByTestId('chat-older')).toHaveCount(0)
})
