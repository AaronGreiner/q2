/**
 * Starts the MANUAL TESTING environment from a known, reproducible state.
 *
 *   bun run test:manual:start
 *
 * Every start is destructive on purpose:
 *   1. delete api/.data/q2-manual-testing.db (if present)
 *   2. create it again
 *   3. apply all EF Core migrations
 *   4. insert the ManualTesting seed
 *   5. serve API + Nuxt against that database
 *
 * Steps 1-4 are not performed by this script but by the API itself: the
 * ManualTesting environment sets `Database:ResetOnStartup`, so the guarantee
 * holds however the environment is started, not only through this entry point.
 * The reset still has to pass DatabaseResetGuard.
 *
 * `bun run dev` never triggers any of this — Development has the flag off and
 * is not even an environment the guard would accept.
 */
import { join } from 'node:path'
import { envFileDefaults } from './lib/dotenv.ts'
import { apiDir, apiProjectDir, appDir, repoRoot } from './lib/paths.ts'
import { heading, info, runConcurrently, type Step } from './lib/proc.ts'

const environment = 'ManualTesting'
const env = {
  ...envFileDefaults(join(apiDir, '.env')),
  ASPNETCORE_ENVIRONMENT: environment,
  TEST_SEED_PROFILE: 'ManualTesting',
}

heading('Manual testing environment')
info('  Rebuilds api/.data/q2-manual-testing.db from scratch and inserts the ManualTesting seed.')
info('  Your Development database (api/.data/q2-development.db) is not touched.')

const steps: Step[] = [
  {
    label: 'api  — ASP.NET Core, ManualTesting (http://localhost:5080)',
    cmd: 'dotnet',
    args: ['run', '--project', apiProjectDir, '--no-launch-profile'],
    cwd: repoRoot,
    env,
  },
  {
    label: 'app  — Nuxt, manual-testing (http://localhost:3000)',
    cmd: 'bun',
    args: ['run', 'dev'],
    cwd: appDir,
    env: {
      NUXT_PUBLIC_APP_ENV: 'manual-testing',
      NUXT_PUBLIC_SENTRY_ENVIRONMENT: 'manual-testing',
      NUXT_PUBLIC_DIAGNOSTICS_ENABLED: 'true',
    },
  },
]

process.exit(await runConcurrently(steps))
