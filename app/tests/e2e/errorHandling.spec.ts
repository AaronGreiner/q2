import { readFileSync } from 'node:fs'
import { expect, test } from '@playwright/test'
import { apiBaseUrl, sentryEventsPath } from './support/e2eEnvironment'

/**
 * The controlled failure path, end to end.
 *
 * Asserts three things at once, which is the point of doing it here rather
 * than in a unit test:
 *   - the user gets a friendly message and no internals;
 *   - the backend produced a Sentry event tagged `environment=e2e`;
 *   - that event contains none of the sensitive data that was in flight.
 */

interface RecordedEvent {
  environment?: string
  release?: string
  level?: string
  server_name?: string
  tags?: Record<string, string>
  exception?: { values?: { type?: string, value?: string }[] }
}

/** Events the API's recording transport appended, newest last. */
function readRecordedEvents(): { events: RecordedEvent[], raw: string } {
  let raw: string
  try {
    raw = readFileSync(sentryEventsPath, 'utf8')
  }
  catch {
    // The file only exists once the first event has been recorded.
    return { events: [], raw: '' }
  }

  const events = raw
    .split('\n')
    .filter(line => line.trim().length > 0)
    .map(line => JSON.parse(line) as RecordedEvent)

  return { events, raw }
}

async function waitForRecordedEvent(predicate: (event: RecordedEvent) => boolean) {
  let result: { events: RecordedEvent[], raw: string } = { events: [], raw: '' }

  await expect.poll(
    () => {
      result = readRecordedEvents()
      return result.events.some(predicate)
    },
    { timeout: 15_000, message: 'The API did not record a matching Sentry event.' },
  ).toBe(true)

  return result
}

test.describe('error handling', () => {
  test('a server error shows a friendly message and leaks nothing', async ({ page }) => {
    await page.goto('/diagnostics')

    await page.getByTestId('trigger-server-error').click()

    const state = page.getByTestId('error-state')
    await expect(state).toBeVisible()
    await expect(state).toContainText('The server could not complete your request')

    // The reference id lets a user quote the incident.
    await expect(state).toContainText('Reference:')

    const body = await page.textContent('body')
    expect(body).not.toContain('DiagnosticsTestException')
    expect(body).not.toContain('Synthetic diagnostics error')
    expect(body).not.toContain('at Q2.Api')
    expect(body).not.toContain('.cs:line')
  })

  test('the backend records the failure in the e2e Sentry environment', async ({ page }) => {
    await page.goto('/diagnostics')
    await page.getByTestId('trigger-server-error').click()
    await expect(page.getByTestId('error-state')).toBeVisible()

    const { events } = await waitForRecordedEvent(
      event => event.exception?.values?.some(v => v.type?.includes('DiagnosticsTestException')) ?? false,
    )

    const event = events.find(
      e => e.exception?.values?.some(v => v.type?.includes('DiagnosticsTestException')),
    )!

    expect(event.environment).toBe('e2e')
    expect(event.release).toBe('q2@e2e')
    expect(event.level).toBe('error')
    expect(event.tags?.['service.name']).toBe('q2-api')
    expect(event.tags?.['test.run_id']).toBeTruthy()
  })

  test('the recorded event contains no sensitive data', async ({ page }) => {
    await page.goto('/diagnostics')
    await page.getByTestId('trigger-server-error').click()
    await expect(page.getByTestId('error-state')).toBeVisible()

    const { raw } = await waitForRecordedEvent(
      event => event.exception?.values?.some(v => v.type?.includes('DiagnosticsTestException')) ?? false,
    )

    // No credential, no machine, and nothing that names a person.
    //
    // The checks are deliberately shaped like JSON keys and value prefixes:
    // a bare `Authorization` also matches the assembly name
    // `Microsoft.AspNetCore.Authorization` in the event's module list, which is
    // a version number, not a header.
    //
    // The IP address is the deliberate exception — SendDefaultPii is on for the
    // backend too (docs/privacy.md section 4) — so it is asserted present
    // rather than absent, and the identity fields around it still are not.
    expect(raw).toContain('"ip_address"')
    expect(raw).not.toContain('"email"')
    expect(raw).not.toContain('"username"')
    expect(raw).not.toContain('"cookies"')
    expect(raw).not.toContain('"Authorization"')
    expect(raw).not.toContain('Bearer ')
    expect(raw.toLowerCase()).not.toContain('data source=')
    expect(raw).not.toContain('"server_name"')

    // And no goal content: titles and descriptions are personal.
    expect(raw).not.toContain('E2E shared goal with participants')
  })

  test('the health endpoint produces no Sentry noise', async ({ request }) => {
    const before = readRecordedEvents().events.length

    for (let i = 0; i < 3; i++) {
      const response = await request.get(`${apiBaseUrl}/health`)
      expect(response.ok()).toBe(true)
    }

    // Give any event a chance to be written before asserting none was.
    await new Promise(resolve => setTimeout(resolve, 1_000))

    expect(readRecordedEvents().events.length).toBe(before)
  })

  test('an unknown page shows the error page rather than a blank screen', async ({ page }) => {
    await page.goto('/this-page-does-not-exist')

    const error = page.getByTestId('app-error')
    await expect(error).toBeVisible()
    await expect(error).toContainText('Page not found')

    await error.getByTestId('app-error-home').click()
    await expect(page.getByRole('heading', { name: 'Shared goals' })).toBeVisible()
  })
})

test.describe('frontend Sentry', () => {
  test('a client error is sent with the e2e environment, identity and replay, and no secrets', async ({ page }) => {
    const envelopes: string[] = []

    // Intercepted in the browser, so nothing leaves the machine even though a
    // DSN is configured.
    await page.route('**/sentry.q2.invalid/**', async (route) => {
      envelopes.push(route.request().postData() ?? '')
      await route.fulfill({ status: 200, body: '{}' })
    })

    await page.goto('/diagnostics')
    await page.getByTestId('trigger-client-error').click()
    await expect(page.getByTestId('client-error-sent')).toBeVisible()

    await expect.poll(() => envelopes.length, { timeout: 15_000 }).toBeGreaterThan(0)

    const payload = envelopes.join('\n')
    expect(payload).toContain('"environment":"e2e"')
    expect(payload).toContain('Synthetic q2 frontend diagnostics error')

    /*
     * The two deliberate privacy exceptions, asserted on the wire rather than
     * on the configuration — see docs/privacy.md section 4. Both were
     * previously off, and both are easy to disable again by accident:
     * reinstating `delete event.user` in scrubEvent silently undoes
     * sendDefaultPii, and dropping replayIntegration() from
     * sentry.client.config.ts leaves the sample rates recording nothing.
     */
    expect(payload).toContain('"ip_address"')
    expect(payload).toContain('"Replay"')
    expect(payload).toContain('"replay_id"')

    // What must still never be attached.
    expect(payload).not.toContain('"cookies"')
  })
})
