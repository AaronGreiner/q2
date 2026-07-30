import { existsSync, rmSync } from 'node:fs'
import { runDirectory } from './e2eEnvironment'

/**
 * Removes the run's temporary directory, and with it the SQLite file and the
 * recorded Sentry events.
 *
 * Nothing survives a run, which is also why no database file can ever end up
 * in a CI cache.
 */
export default function globalTeardown() {
  if (existsSync(runDirectory)) {
    rmSync(runDirectory, { recursive: true, force: true })
    console.info(`[e2e] removed ${runDirectory}`)
  }
}
