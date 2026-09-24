import { expect, test, type Page } from '@playwright/test'
import { apiBaseUrl } from './support/e2eEnvironment'

/**
 * The invite link once somebody already has friends.
 *
 * It used to live only on the "Noch keine Freunde" card, which is gone with
 * the first friend — and with it the only way to bring in anybody else, or to
 * replace a link that ended up in the wrong place. The seeded person has
 * friends, which is exactly the situation this is about.
 */
async function expectSheetWithLink(page: Page) {
  const sheet = page.getByTestId('invite-sheet')
  await expect(sheet).toBeVisible()
  await expect(sheet.getByTestId('invite-sheet-link')).toContainText('/join/')
  await expect(sheet.getByTestId('invite-sheet-share')).toBeEnabled()
}

test('the friends tab offers the link in its header', async ({ page }) => {
  await page.goto('/search')
  await page.getByTestId('open-invite').click()

  await expectSheetWithLink(page)
})

test('the profile offers the link', async ({ page }) => {
  await page.goto('/profile')
  await page.getByTestId('profile-invite').click()

  await expectSheetWithLink(page)
})

test('a search that finds nobody offers to send them the link', async ({ page }) => {
  await page.goto('/search')
  await page.getByTestId('friend-search').fill('Niemand mit diesem Namen')

  await expect(page.getByTestId('search-empty')).toContainText('Nicht dabei? Schick deinen Link.')
  await page.getByTestId('search-invite').click()

  await expectSheetWithLink(page)
})

test('copying puts the link on the clipboard and says so', async ({ page, context }) => {
  await context.grantPermissions(['clipboard-read', 'clipboard-write'])
  await page.goto('/search')
  await page.getByTestId('open-invite').click()

  const link = await page.getByTestId('invite-sheet-link').innerText()
  await page.getByTestId('invite-sheet-copy').click()

  await expect(page.getByText('Link kopiert', { exact: true })).toBeVisible()
  expect(await page.evaluate(() => navigator.clipboard.readText())).toBe(link.trim())
})

test('the settings replace the link, and the old one stops working', async ({ page, request }) => {
  const before = (await (await request.get(`${apiBaseUrl}/api/invite`)).json() as { code: string }).code

  await page.goto('/settings')
  await page.getByTestId('invite-replace-setting').click()
  await page.getByTestId('confirm-accept').click()

  await expect(page.getByText('Neuer Link — der alte gilt nicht mehr', { exact: true })).toBeVisible()

  const after = (await (await request.get(`${apiBaseUrl}/api/invite`)).json() as { code: string }).code
  expect(after).not.toBe(before)

  const old = await request.post(`${apiBaseUrl}/api/invite/preview`, { data: { code: before } })
  expect(old.status()).toBe(404)
})

/**
 * What a messenger reads: a title, a sentence and the icon — in the HTML the
 * server sends, because a link preview does not run JavaScript. Never the
 * sender's name, and never indexed.
 */
test('a pasted link previews without naming anybody', async ({ request }) => {
  const { code } = await (await request.get(`${apiBaseUrl}/api/invite`)).json() as { code: string }

  const html = await (await request.get(`/join/${code}`, { headers: { cookie: '' } })).text()
  const head = html.slice(0, html.indexOf('</head>'))

  expect(head).toContain('property="og:title" content="Du bist zu q2 eingeladen"')
  expect(head).toMatch(/property="og:image" content="http[^"]+\/pwa-512x512\.png"/)
  expect(head).toContain('name="robots" content="noindex, nofollow"')
  expect(head).not.toContain('E2E Mara')
})
