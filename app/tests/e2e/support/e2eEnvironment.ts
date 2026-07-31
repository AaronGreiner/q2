import { mkdirSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join, resolve } from 'node:path'

/**
 * Paths and environment for one E2E run.
 *
 * Resolved at module load, before Playwright builds its `webServer` entries,
 * and written back into `process.env` so that everything which re-imports this
 * module — the config, the global setup, each worker process — agrees on the
 * same directory.
 *
 * Each run gets its own temporary directory, so a developer's run and a CI job
 * can never touch the same database. The file name contains `e2e` and starts
 * with `q2-` because DatabaseResetGuard refuses to delete anything that does
 * not look like a local test database.
 */

process.env.Q2_E2E_RUN_ID ??= `${Date.now()}-${process.pid}`

/** Identifies this run. Also tagged onto Sentry events as `test.run_id`. */
export const runId = process.env.Q2_E2E_RUN_ID

process.env.Q2_E2E_RUN_DIR ??= join(tmpdir(), `q2-e2e-${runId}`)

/** Temporary directory holding the database and the recorded Sentry events. */
export const runDirectory = process.env.Q2_E2E_RUN_DIR

// Created here rather than in globalSetup because Playwright starts the web
// servers *before* globalSetup runs, and the API needs somewhere to put its
// database file. Creating a directory is idempotent, so it is safe that worker
// processes re-import this module.
mkdirSync(runDirectory, { recursive: true })

export const repoRoot = resolve(import.meta.dirname, '..', '..', '..', '..')
export const apiProject = join(repoRoot, 'api', 'src', 'Q2.Api')

export const apiPort = Number(process.env.Q2_E2E_API_PORT ?? 5081)
export const appPort = Number(process.env.Q2_E2E_APP_PORT ?? 3001)

export const apiBaseUrl = `http://127.0.0.1:${apiPort}`
export const appBaseUrl = `http://127.0.0.1:${appPort}`

/** Unique per run, and named so the reset guard accepts it. */
export const databasePath = join(runDirectory, `q2-e2e-${runId}.db`)

/** Where the backend's recording transport appends captured Sentry events. */
export const sentryEventsPath = join(runDirectory, 'sentry-events.jsonl')

/**
 * The signed-in session every spec starts from.
 *
 * globalSetup signs in through the real form once and writes the cookie here;
 * playwright.config points `use.storageState` at it. Signing in per test would
 * be the same three steps repeated forty times to assert nothing.
 */
export const storageStatePath = join(runDirectory, 'storage-state.json')

/** The seeded account the suite signs in as. See E2ESeed.cs. */
export const e2eAccount = {
  email: 'e2e.mara@kudos.example',
  password: 'kudos-demo-2026',
} as const

/**
 * Environment for the API process.
 *
 * Sentry is deliberately *on*: the suite asserts that a technical failure
 * produces an event tagged `environment=e2e` that contains no sensitive data.
 * The recording transport keeps this entirely local — no DSN, no network.
 */
export function apiEnvironment(): Record<string, string> {
  return {
    ASPNETCORE_ENVIRONMENT: 'E2E',
    ASPNETCORE_URLS: apiBaseUrl,
    ConnectionStrings__Database: `Data Source=${databasePath}`,
    Database__SeedProfile: 'E2E',
    Database__AllowDestructiveReset: 'true',

    // The API rebuilds its own database on startup — delete, migrate, seed —
    // exactly as the ManualTesting environment does, and subject to the same
    // DatabaseResetGuard. Doing it here rather than in globalSetup avoids a
    // race: Playwright starts the web servers before globalSetup runs, so a
    // separate reset would fight the server for the same file.
    Database__ResetOnStartup: 'true',
    Cors__AllowedOrigins__0: appBaseUrl,
    SENTRY_TEST_TRANSPORT: 'recording',
    SENTRY_TEST_TRANSPORT_FILE: sentryEventsPath,
    Sentry__Release: 'q2@e2e',
    TEST_RUN_ID: runId,
    TEST_SEED_PROFILE: 'E2E',
    Q2_E2E_RUN_ID: runId,
    Q2_E2E_RUN_DIR: runDirectory,
  }
}

export function appEnvironment(): Record<string, string> {
  return {
    NUXT_PUBLIC_API_BASE_URL: apiBaseUrl,
    NUXT_PUBLIC_APP_ENV: 'e2e',
    NUXT_PUBLIC_DIAGNOSTICS_ENABLED: 'true',

    // The suite signs in through the real form once, in globalSetup, and
    // reuses the session. These are what that form is filled with.
    NUXT_PUBLIC_DEMO_EMAIL: e2eAccount.email,
    NUXT_PUBLIC_DEMO_PASSWORD: e2eAccount.password,
    NUXT_PUBLIC_SENTRY_ENVIRONMENT: 'e2e',
    NUXT_PUBLIC_SENTRY_RELEASE: 'q2@e2e',
    // Syntactically valid, points nowhere. Playwright intercepts the request
    // in the browser, so no event ever leaves the machine.
    NUXT_PUBLIC_SENTRY_DSN: 'https://0000000000000000000000000000000@sentry.q2.invalid/1',
    NUXT_PUBLIC_SENTRY_TRACES_SAMPLE_RATE: '1',
    NITRO_PORT: String(appPort),
    NITRO_HOST: '127.0.0.1',
    Q2_E2E_RUN_ID: runId,
    Q2_E2E_RUN_DIR: runDirectory,
  }
}
