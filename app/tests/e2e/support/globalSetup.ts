import { chromium, devices } from '@playwright/test'
import {
  apiBaseUrl,
  appBaseUrl,
  databasePath,
  e2eAccount,
  runId,
  storageStatePath,
} from './e2eEnvironment'

/**
 * Verifies the suite is about to test what it thinks it is, and signs it in.
 *
 * The database itself is built by the API on startup (delete, migrate, seed —
 * see `Database__ResetOnStartup` in e2eEnvironment.ts), because Playwright
 * starts the web servers before this hook runs. What is left for this hook is
 * the check that matters — the API is answering, it is in the E2E environment,
 * and it is serving the E2E seed rather than someone's development data — plus
 * one sign-in.
 *
 * The sign-in goes through the real form rather than posting to the API. It is
 * the same three fields a person fills in, so the screen every other test
 * depends on is exercised before any of them run; a broken sign-in then fails
 * here, once, with a clear message, instead of failing forty tests with
 * "expected the goal list, found the login page".
 *
 * Failing here gives one clear message instead of a dozen confusing assertion
 * failures later.
 */
export default async function globalSetup() {
  const status = await fetchJson<{ enabled: boolean, environment: string, release: string }>(
    `${apiBaseUrl}/api/diagnostics/sentry`,
  )

  if (status.environment !== 'e2e') {
    throw new Error(
      `The API reports Sentry environment '${status.environment}', expected 'e2e'. `
      + 'The suite is talking to the wrong server.',
    )
  }

  const browser = await chromium.launch()

  try {
    // The same descriptor and viewport the tests run in, so the sign-in happens
    // at the width the screen is designed for rather than in a desktop window.
    const context = await browser.newContext({
      ...devices['Pixel 7'],
      viewport: { width: 390, height: 844 },
    })

    const page = await context.newPage()
    await page.goto(`${appBaseUrl}/login`)

    // The environment sets NUXT_PUBLIC_DEMO_*, so the seeded credentials are
    // one tap away and are not repeated in this file.
    await page.getByTestId('demo-fill').click()
    await page.getByTestId('login-submit').click()
    await page.waitForURL(`${appBaseUrl}/`, { timeout: 30_000 })

    // Now that there is a session, the seeded world is readable. Through the
    // browser context, so this uses the very cookie the tests will reuse.
    const response = await context.request.get(`${apiBaseUrl}/api/goals`)

    if (!response.ok()) {
      throw new Error(
        `Signed in as ${e2eAccount.email}, but GET /api/goals returned ${response.status()}.`,
      )
    }

    const goals = await response.json() as { id: string, title: string }[]
    const seeded = goals.filter(goal => goal.title.startsWith('E2E '))

    if (seeded.length === 0) {
      throw new Error(
        `The API returned ${goals.length} goal(s), none of them from the E2E seed. `
        + `Expected a freshly seeded database at ${databasePath}.`,
      )
    }

    await context.storageState({ path: storageStatePath })
    await context.close()

    console.info(`[e2e] run ${runId}: ${seeded.length} seeded goals, database at ${databasePath}`)
  }
  finally {
    await browser.close()
  }
}

async function fetchJson<T>(url: string): Promise<T> {
  const response = await fetch(url)

  if (!response.ok) {
    throw new Error(`GET ${url} returned ${response.status}.`)
  }

  return await response.json() as T
}
