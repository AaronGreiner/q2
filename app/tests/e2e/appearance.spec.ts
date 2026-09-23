import AxeBuilder from '@axe-core/playwright'
import { expect, test } from '@playwright/test'
import { apiBaseUrl } from './support/e2eEnvironment'
import { waitForMotion } from './support/waitForMotion'

const accents = ['iris', 'sage', 'rose', 'ochre'] as const

let previousTheme: string

test.beforeAll(async ({ request }) => {
  const settings = await request.get(`${apiBaseUrl}/api/settings`)
  previousTheme = (await settings.json() as { theme: string }).theme
  expect((await request.put(`${apiBaseUrl}/api/settings`, { data: { theme: 'System' } })).ok()).toBe(true)
})

test.afterAll(async ({ request }) => {
  expect((await request.put(`${apiBaseUrl}/api/settings`, { data: { theme: previousTheme } })).ok()).toBe(true)
})

for (const colorScheme of ['light', 'dark'] as const) {
  test(`all accent palettes remain accessible in ${colorScheme}`, async ({ page }, testInfo) => {
    await page.emulateMedia({ colorScheme })
    await page.goto('/settings')
    await expect(page.locator('html')).toHaveClass(new RegExp(`\\b${colorScheme}\\b`))
    const picker = page.getByTestId('accent-picker')

    for (const [index, accent] of accents.entries()) {
      await picker.locator('label[data-slot="item"]').nth(index).click()
      await expect(page.locator('html')).toHaveAttribute('data-accent', accent)
      await expect(picker.getByRole('radio').nth(index)).toBeChecked()
      await waitForMotion(page)
      const results = await new AxeBuilder({ page })
        .withTags(['wcag2a', 'wcag2aa'])
        .disableRules(['meta-viewport'])
        .analyze()
      expect(results.violations.map(v => ({ id: v.id, nodes: v.nodes.map(n => n.target) }))).toEqual([])
    }

    // Exercise roving focus after the interactive selection, before reloading.
    await picker.getByRole('radio').nth(3).focus()
    await expect(picker.getByRole('radio').nth(3)).toBeFocused()
    // Reka selects on its deferred focus event while the arrow is held.
    await page.keyboard.down('ArrowLeft')
    try {
      await expect(page.locator('html')).toHaveAttribute('data-accent', 'rose')
    }
    finally {
      await page.keyboard.up('ArrowLeft')
    }

    await page.reload()
    await expect(page.locator('html')).toHaveAttribute('data-accent', 'rose')
    const html = await (await page.request.get('/chats')).text()
    expect(html).toContain('data-accent="rose"')

    await picker.locator('label[data-slot="item"]').first().click()
    await page.screenshot({ animations: 'disabled', path: testInfo.outputPath(`settings-${colorScheme}.png`) })
    for (const [name, path] of [['home', '/'], ['chats', '/chats'], ['friends', '/search'], ['profile', '/profile']] as const) {
      await page.goto(path)
      await expect(page.getByTestId('content-panel')).toBeVisible()
      await page.screenshot({ animations: 'disabled', path: testInfo.outputPath(`${name}-${colorScheme}.png`) })
    }
  })
}
