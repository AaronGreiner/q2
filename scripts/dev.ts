/**
 * Starts the normal development environment.
 *
 *   bun run dev            # API + Nuxt
 *   bun run dev:api        # API only
 *   bun run dev:app        # Nuxt only
 *
 * This is the NON-destructive start: the Development database keeps its data
 * and only pending migrations are applied. Use `bun run test:manual:start`
 * when you want a freshly seeded database.
 */
import { join } from 'node:path'
import { envFileDefaults } from './lib/dotenv.ts'
import { apiDir, appDir, apiProjectDir, repoRoot } from './lib/paths.ts'
import { failure, type Step, runConcurrently } from './lib/proc.ts'

const args = process.argv.slice(2)
const onlyIndex = args.indexOf('--only')
const only = onlyIndex === -1 ? 'all' : args[onlyIndex + 1]

if (!['all', 'api', 'app'].includes(only ?? '')) {
  failure(`--only expects "api" or "app", got: ${only}`)
  process.exit(2)
}

const apiStep: Step = {
  label: 'api  — ASP.NET Core (http://localhost:5080)',
  cmd: 'dotnet',
  args: ['watch', 'run', '--project', apiProjectDir, '--no-launch-profile', '--non-interactive'],
  cwd: repoRoot,
  env: {
    ...envFileDefaults(join(apiDir, '.env')),
    ASPNETCORE_ENVIRONMENT: process.env.ASPNETCORE_ENVIRONMENT ?? 'Development',
  },
}

const appStep: Step = {
  label: 'app  — Nuxt (http://localhost:3000)',
  cmd: 'bun',
  args: ['run', 'dev'],
  cwd: appDir,
}

const steps = only === 'api' ? [apiStep] : only === 'app' ? [appStep] : [apiStep, appStep]

process.exit(await runConcurrently(steps))
