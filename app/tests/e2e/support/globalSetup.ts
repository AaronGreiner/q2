import { apiBaseUrl, databasePath, runId } from './e2eEnvironment'

/**
 * Verifies the suite is about to test what it thinks it is.
 *
 * The database itself is built by the API on startup (delete, migrate, seed —
 * see `Database__ResetOnStartup` in e2eEnvironment.ts), because Playwright
 * starts the web servers before this hook runs. What is left for this hook is
 * the check that matters: the API is answering, it is in the E2E environment,
 * and it is serving the E2E seed rather than someone's development data.
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

  const goals = await fetchJson<{ id: string, title: string }[]>(`${apiBaseUrl}/api/goals`)
  const seeded = goals.filter(goal => goal.title.startsWith('E2E '))

  if (seeded.length === 0) {
    throw new Error(
      `The API returned ${goals.length} goal(s), none of them from the E2E seed. `
      + `Expected a freshly seeded database at ${databasePath}.`,
    )
  }

  console.info(`[e2e] run ${runId}: ${seeded.length} seeded goals, database at ${databasePath}`)
}

async function fetchJson<T>(url: string): Promise<T> {
  const response = await fetch(url)

  if (!response.ok) {
    throw new Error(`GET ${url} returned ${response.status}.`)
  }

  return await response.json() as T
}
