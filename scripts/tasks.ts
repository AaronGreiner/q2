/**
 * Central task pipelines for the repository.
 *
 * Every root `package.json` script that is more than one command delegates
 * here, so the ordering of a pipeline lives in exactly one place and behaves
 * identically on every platform and in CI.
 *
 *   bun scripts/tasks.ts <task>
 *   bun scripts/tasks.ts --list
 */
import { appDir, apiSolution, repoRoot } from './lib/paths.ts'
import { failure, info, runSequence, type Step } from './lib/proc.ts'

const dotnet = (label: string, args: string[]): Step => ({ label, cmd: 'dotnet', args, cwd: repoRoot })
const app = (label: string, script: string[]): Step => ({ label, cmd: 'bun', args: ['run', ...script], cwd: appDir })

const apiUnitTests = dotnet('api unit tests', ['test', 'api/tests/Q2.Api.UnitTests', '--nologo'])
const apiIntegrationTests = dotnet('api integration tests', ['test', 'api/tests/Q2.Api.IntegrationTests', '--nologo'])
const apiBuild = dotnet('api build (warnings as errors)', ['build', apiSolution, '-c', 'Release', '--nologo'])
const appLint = app('app lint (eslint incl. formatting)', ['lint'])
const appTypecheck = app('app typecheck (vue-tsc, strict)', ['typecheck'])
const appUnitTests = app('app unit tests', ['test:unit'])
const appComponentTests = app('app component tests', ['test:component'])
const appBuild = app('app production build', ['build'])
const dotnetFormatCheck = dotnet('api format check', ['format', apiSolution, '--verify-no-changes'])

const tasks: Record<string, { description: string, steps: Step[] }> = {
  'lint': {
    description: 'ESLint for the frontend (includes stylistic/formatting rules)',
    steps: [appLint],
  },
  'format:check': {
    description: 'Verify formatting of backend (dotnet format) and frontend (eslint)',
    steps: [dotnetFormatCheck, appLint],
  },
  'test:unit': {
    description: 'Unit tests without I/O — backend domain logic and frontend logic',
    steps: [apiUnitTests, appUnitTests],
  },
  'test:component': {
    description: 'Vue component tests rendered in a Nuxt environment',
    steps: [appComponentTests],
  },
  'test:integration': {
    description: 'API integration tests against the real pipeline and SQLite in-memory',
    steps: [apiIntegrationTests],
  },
  'test:migrations': {
    description: 'Apply every EF Core migration to an empty database and verify the result',
    steps: [dotnet('migration tests', ['test', 'api/tests/Q2.Api.IntegrationTests', '--nologo', '--filter', 'Category=Migrations'])],
  },
  'test:sentry': {
    description: 'Sentry integration tests (real SDK, local recording transport)',
    steps: [
      dotnet('api sentry tests', ['test', 'api/tests/Q2.Api.IntegrationTests', '--nologo', '--filter', 'Category=Sentry']),
      app('app sentry tests', ['test:sentry']),
    ],
  },
  'test:api': {
    description: 'All backend tests',
    steps: [apiUnitTests, apiIntegrationTests],
  },
  'test:app': {
    description: 'All frontend tests except E2E',
    steps: [app('app tests', ['test'])],
  },
  'test': {
    description: 'All automated tests except E2E (see `bun run test:e2e`)',
    steps: [apiUnitTests, apiIntegrationTests, appUnitTests, appComponentTests],
  },
  'build:api': {
    description: 'Release build of the backend',
    steps: [apiBuild],
  },
  'build': {
    description: 'Release build of backend and frontend',
    steps: [apiBuild, appBuild],
  },
  'validate': {
    description: 'The full gate: format, lint, types, all tests, E2E and both builds',
    steps: [
      dotnetFormatCheck,
      appLint,
      appTypecheck,
      apiUnitTests,
      apiIntegrationTests,
      appUnitTests,
      appComponentTests,
      apiBuild,
      appBuild,
      app('e2e tests (fresh temporary SQLite database)', ['test:e2e']),
    ],
  },
}

const requested = process.argv[2]

if (!requested || requested === '--list' || !(requested in tasks)) {
  if (requested && requested !== '--list') failure(`Unknown task: ${requested}`)
  info('Available tasks:\n')
  for (const [name, task] of Object.entries(tasks)) {
    info(`  ${name.padEnd(18)} ${task.description}`)
  }
  process.exit(requested && requested !== '--list' ? 2 : 0)
}

process.exit(await runSequence(tasks[requested]!.steps))
