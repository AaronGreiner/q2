import AxeBuilder from '@axe-core/playwright'
import { expect, test } from '@playwright/test'
import { apiBaseUrl } from './support/e2eEnvironment'
import { undersizedTargets } from './support/touchTargets'
import { waitForMotion } from './support/waitForMotion'

const sharedGoalId = 'e2e00000-0000-4000-8000-000000000001'

/** The representative screens every accessibility run covers. */
const accessibilityScreens = [
  { name: 'dashboard', path: '/' },
  { name: 'goal detail', path: `/goals/${sharedGoalId}` },
  { name: 'notifications', path: '/notifications' },
  { name: 'notification settings', path: '/settings/notifications' },
  { name: 'diagnostics', path: '/diagnostics' },
] as const

const signedInScreens = [
  '/',
  '/goals',
  '/goals?tab=donts',
  `/goals/${sharedGoalId}`,
  '/chats',
  '/search',
  '/profile',
  '/notifications',
  '/activity',
  '/settings',
  '/settings/notifications',
  '/diagnostics',
] as const

test.describe('server-rendered HTML', () => {
  test('contains personal data, progress and icons before JavaScript runs', async ({ page }) => {
    const response = await page.request.get('/')
    expect(response.ok()).toBe(true)

    // On the start screen, only what every state of it renders: the name and
    // the streak in its header. Which goals fill its few slots depends on the
    // hour — from 20:00 the evening's risk cards take them (ADR 0019), and
    // between local and UTC midnight the seed's windows and the owner's "today"
    // disagree about the day (#131).
    const html = await response.text()
    expect(html).toContain('E2E Mara')
    expect(html).toContain('data-testid="streak-flame"')

    // Nuxt Icon renders SVG on the server. Client-side hydration used to hide
    // that these were all missing from the first paint.
    expect((html.match(/<svg/g) ?? []).length).toBeGreaterThan(5)

    // The goals themselves, with a rhythm, are read on the Dos screen, which
    // lists every running goal at any hour (#115, #131).
    const goals = await page.request.get('/goals')
    expect(goals.ok()).toBe(true)

    const goalsHtml = await goals.text()
    expect(goalsHtml).toContain('E2E shared goal with participants')
    expect(goalsHtml).toContain('E2E goal three times a week')
    expect(goalsHtml).toContain('3× pro Woche')
  })
})

test.describe('WCAG A and AA', () => {
  // The account defaults to Dark, which overrides the emulated OS scheme.
  // Use System for this group so the light cases actually render light.
  let previousTheme: string
  test.beforeAll(async ({ request }) => {
    const settings = await request.get(`${apiBaseUrl}/api/settings`)
    expect(settings.ok()).toBe(true)
    previousTheme = (await settings.json() as { theme: string }).theme
    expect((await request.put(`${apiBaseUrl}/api/settings`, { data: { theme: 'System' } })).ok()).toBe(true)
  })

  test.afterAll(async ({ request }) => {
    expect((await request.put(`${apiBaseUrl}/api/settings`, { data: { theme: previousTheme } })).ok()).toBe(true)
  })

  for (const screen of accessibilityScreens) {
    for (const colorScheme of ['light', 'dark'] as const) {
      test(`${screen.name} has no ${colorScheme} accessibility violations`, async ({ page }) => {
        await page.emulateMedia({ colorScheme })
        await page.goto(screen.path)
        await expect(page.locator('html')).toHaveClass(new RegExp(`\\b${colorScheme}\\b`))

        await waitForMotion(page)
        const results = await new AxeBuilder({ page })
          .withTags(['wcag2a', 'wcag2aa'])
          // The installed-app zoom decision is the one dated exception in
          // ADR 0013. Keeping only this rule out makes every other AA rule bite.
          .disableRules(['meta-viewport'])
          .analyze()

        const violations = results.violations.flatMap(violation => violation.nodes.map(node =>
          `${violation.id}: ${node.target.join(' ')}`,
        ))

        expect(violations).toEqual([])
      })
    }
  }
})

test.describe('phone geometry', () => {
  for (const path of signedInScreens) {
    test(`${path} never needs horizontal scrolling`, async ({ page }) => {
      await page.goto(path)
      await waitForMotion(page)

      const geometry = await page.evaluate(() => ({
        viewport: document.documentElement.clientWidth,
        content: document.documentElement.scrollWidth,
      }))

      expect(geometry.content).toBeLessThanOrEqual(geometry.viewport)
    })
  }

  /**
   * The shell is exactly one viewport tall, and nothing may make the document
   * taller: a screen-reader text laid out against the page used to poke out
   * below it, so the header, the list and the tab bar could be dragged up as
   * one piece (#83). Checked again with the list scrolled to its end, because
   * that is where the stray text followed it.
   */
  for (const path of ['/', '/goals', '/goals?tab=donts', '/chats', '/search', '/profile'] as const) {
    test(`${path} never makes the document taller than the screen`, async ({ page }) => {
      await page.goto(path)
      await waitForMotion(page)

      const height = () => page.evaluate(() => ({
        viewport: window.innerHeight,
        document: document.documentElement.scrollHeight,
      }))

      let geometry = await height()
      expect(geometry.document).toBeLessThanOrEqual(geometry.viewport)

      await page.getByTestId('content-panel').locator('[data-slot="body"]').first().evaluate((area) => {
        area.scrollTop = area.scrollHeight
      })

      geometry = await height()
      expect(geometry.document).toBeLessThanOrEqual(geometry.viewport)
      expect(await page.evaluate(() => window.scrollY)).toBe(0)
    })
  }

  /**
   * A centred question box is as tall as its title and its two buttons. The
   * keyboard rule for sheets used to give it a bottom edge too, which made it
   * half the screen tall with an empty block under the buttons (#84).
   */
  test('a confirm dialog is as tall as its content, and centred', async ({ page }) => {
    await page.goto('/settings')
    await page.getByTestId('invite-replace-setting').click()

    const dialog = page.getByRole('dialog')
    await expect(dialog.getByTestId('confirm-accept')).toBeVisible()
    await waitForMotion(page)

    const geometry = await dialog.evaluate((element) => {
      const box = element.getBoundingClientRect()
      const content = Array.from(element.children).reduce((sum, child) => sum + child.getBoundingClientRect().height, 0)
      return { top: box.top, bottom: window.innerHeight - box.bottom, height: box.height, content }
    })

    expect(geometry.height).toBeLessThanOrEqual(geometry.content + 2)
    expect(Math.abs(geometry.top - geometry.bottom)).toBeLessThanOrEqual(2)

    await dialog.getByTestId('confirm-cancel').click()
    await expect(dialog).toBeHidden()
  })

  test('visible controls meet the 44px touch-target rule', async ({ page }) => {
    const undersized: string[] = []

    for (const path of ['/', '/goals', '/goals?tab=donts', '/goals/archive', '/search', '/notifications', '/settings/notifications', '/diagnostics'] as const) {
      await page.goto(path)
      await waitForMotion(page)

      undersized.push(...await undersizedTargets(page, path))
    }

    expect(undersized).toEqual([])
  })
})
