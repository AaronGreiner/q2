import { expect, test } from '@playwright/test'
import { e2eAccount } from './support/e2eEnvironment'

/**
 * User Feedback: the one thing q2 sends to Sentry that somebody wrote by hand.
 *
 * The assertions are made against the dialog the SDK actually renders rather
 * than against the options handed to it — Playwright's selectors reach into the
 * open shadow root it lives in, which is the only way to see that a field is
 * absent because it was switched off rather than because the option was
 * renamed. The unit test covers the configuration; this covers the form.
 *
 * See docs/adr/0014-user-feedback.md.
 */

test.describe('the feedback dialog', () => {
  test('is offered on the settings screen and speaks German', async ({ page }) => {
    await page.goto('/settings')

    const open = page.getByTestId('open-feedback')
    await expect(open).toBeVisible()
    await expect(open).toContainText('Feedback senden')

    await open.click()

    await expect(page.getByRole('heading', { name: 'Feedback geben' })).toBeVisible()
    await expect(page.getByLabel('Was möchtest du uns sagen?')).toBeVisible()
    await expect(page.getByRole('button', { name: 'Absenden' })).toBeVisible()
  })

  test('asks for no name, no address and no screenshot', async ({ page }) => {
    await page.goto('/settings')
    await page.getByTestId('open-feedback').click()

    const dialog = page.locator('#sentry-feedback dialog')
    await expect(dialog).toBeVisible()

    // The privacy decision, seen from the form: scrubEvent strips identity out
    // of every other event, so this one does not collect it in the first place.
    await expect(dialog.locator('input[name="name"]:visible')).toHaveCount(0)
    await expect(dialog.locator('input[name="email"]:visible')).toHaveCount(0)
    await expect(dialog.getByRole('button', { name: /screenshot/i })).toHaveCount(0)

    // What is left is the message, and it is the only required field.
    await expect(dialog.locator('textarea[name="message"]')).toBeVisible()
  })

  test('lets the message be corrected, despite the app being unselectable', async ({ page }) => {
    await page.goto('/settings')
    await page.getByTestId('open-feedback').click()

    const message = page.locator('#sentry-feedback textarea[name="message"]')
    await message.fill('Der Ladebalken bleibt stehen')

    /*
     * `user-select: none` on <body> is inherited, and inheritance crosses into a
     * shadow tree while the `input, textarea` rule that opts back in does not.
     * Without the host rule in main.css there is no caret in here at all — see
     * ADR 0013, which names this as the trap a future component would fall into.
     */
    expect(await message.evaluate(field => getComputedStyle(field).userSelect)).toBe('text')

    const range = await message.evaluate((field: HTMLTextAreaElement) => {
      field.setSelectionRange(4, 14)
      return `${field.selectionStart}-${field.selectionEnd}`
    })
    expect(range).toBe('4-14')
  })

  test('sends the message, the replay and nothing that identifies anybody', async ({ page }) => {
    const envelopes: string[] = []

    // Intercepted in the browser, so nothing leaves the machine even though a
    // DSN is configured. The 200 is what the widget waits for before it says so.
    await page.route('**/sentry.q2.invalid/**', async (route) => {
      envelopes.push(route.request().postData() ?? '')
      await route.fulfill({ status: 200, body: '{}' })
    })

    await page.goto('/settings')
    await page.getByTestId('open-feedback').click()

    await page.locator('#sentry-feedback textarea[name="message"]').fill('Der Ladebalken bleibt stehen')
    await page.getByRole('button', { name: 'Absenden' }).click()

    await expect.poll(
      () => envelopes.filter(envelope => envelope.includes('"type":"feedback"')).length,
      { timeout: 15_000 },
    ).toBeGreaterThan(0)

    const payload = envelopes.filter(envelope => envelope.includes('"type":"feedback"')).join('\n')

    // The deliberate exception, on the wire: this is the one event in q2 that
    // carries something a person wrote — because they wrote it to be read.
    expect(payload).toContain('Der Ladebalken bleibt stehen')
    expect(payload).toContain('"environment":"e2e"')
    expect(payload).toContain('"q2.feedback_source":"settings"')

    // And the part that is not an exception. The form has no name and no email
    // field, and the hidden ones the SDK still submits stay empty because
    // `useSentryUser` points at nothing.
    expect(payload).toContain('"contact_email":""')
    expect(payload).toContain('"name":""')
    expect(payload).not.toContain(e2eAccount.email)
    expect(payload).not.toContain('"username"')

    // The replay is the context that makes a one-line report actionable.
    expect(payload).toContain('"replay_id"')

    await expect(page.getByText('Danke! Deine Nachricht ist angekommen.')).toBeVisible()
  })

  test('closes again, and leaves nothing over the app', async ({ page }) => {
    await page.goto('/settings')
    await page.getByTestId('open-feedback').click()

    await page.getByRole('button', { name: 'Abbrechen' }).click()
    await expect(page.locator('#sentry-feedback dialog')).toHaveCount(0)

    /*
     * The host element stays in <body> for good once the SDK has created it, and
     * the keyboard rule in main.css turns it into a full-screen box. Left marked
     * after a close, it would swallow every tap in the app.
     */
    await expect(page.locator('#sentry-feedback')).not.toHaveAttribute('data-q2-open')
    await page.getByTestId('sign-out').scrollIntoViewIfNeeded()
    await expect(page.getByTestId('sign-out')).toBeVisible()
  })
})

test.describe('the SDK button', () => {
  test('is not injected anywhere', async ({ page }) => {
    for (const path of ['/', '/goals', '/settings']) {
      await page.goto(path)

      // `autoInject` off. The floating actor would sit on top of the tab bar,
      // in English, on every screen in the app.
      await expect(page.locator('#sentry-feedback .widget__actor')).toHaveCount(0)
    }
  })
})
