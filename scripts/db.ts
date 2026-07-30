/**
 * Database task runner.
 *
 *   bun run db:migrate            # apply migrations (Development)
 *   bun run db:seed               # apply the Development seed if the db is empty
 *   bun run db:reset              # DESTRUCTIVE: delete + recreate the database
 *   bun run db:reset-and-seed     # DESTRUCTIVE: reset, then seed
 *   bun run db:add-migration Foo  # create a new EF Core migration
 *
 * Extra flags are passed through, e.g.
 *   bun run db:reset-and-seed --env ManualTesting
 *   bun run db:seed --profile ManualTesting --force
 *
 * `reset` is refused by the API unless the target environment *and* the target
 * database file both look like a local test database — see
 * api/src/Q2.Api/Infrastructure/Persistence/DatabaseResetGuard.cs.
 */
import { join } from 'node:path'
import { envFileDefaults } from './lib/dotenv.ts'
import { apiDir, apiProjectDir, dotnetRunApi, repoRoot } from './lib/paths.ts'
import { failure, info, run } from './lib/proc.ts'

const KNOWN_COMMANDS = ['migrate', 'seed', 'reset', 'reset-and-seed', 'add-migration'] as const
type Command = (typeof KNOWN_COMMANDS)[number]

const [command, ...rest] = process.argv.slice(2)

if (!command || !KNOWN_COMMANDS.includes(command as Command)) {
  failure(`Unknown database command: ${command ?? '(none)'}`)
  info(`Expected one of: ${KNOWN_COMMANDS.join(', ')}`)
  process.exit(2)
}

/** Reads `--env <Name>` and removes it from the argument list. */
function takeEnvironment(args: string[]): { environment: string, remaining: string[] } {
  const index = args.indexOf('--env')
  if (index === -1) return { environment: process.env.ASPNETCORE_ENVIRONMENT ?? 'Development', remaining: args }

  const value = args[index + 1]
  if (!value) {
    failure('--env requires a value, e.g. --env ManualTesting')
    process.exit(2)
  }
  return { environment: value, remaining: [...args.slice(0, index), ...args.slice(index + 2)] }
}

const { environment, remaining } = takeEnvironment(rest)
const env = {
  ...envFileDefaults(join(apiDir, '.env')),
  ASPNETCORE_ENVIRONMENT: environment,
}

if (command === 'add-migration') {
  const name = remaining[0]
  if (!name) {
    failure('A migration name is required, e.g. `bun run db:add-migration AddGoalReminders`')
    process.exit(2)
  }

  const restore = await run({
    label: 'dotnet tool restore',
    cmd: 'dotnet',
    args: ['tool', 'restore'],
    cwd: repoRoot,
  })
  if (restore !== 0) process.exit(restore)

  const code = await run({
    label: `ef migrations add ${name}`,
    cmd: 'dotnet',
    args: [
      'dotnet-ef',
      'migrations',
      'add',
      name,
      '--project',
      apiProjectDir,
      '--output-dir',
      'Infrastructure/Persistence/Migrations',
    ],
    cwd: repoRoot,
    env,
  })
  process.exit(code)
}

const code = await run({
  label: `db ${command} (${environment})`,
  cmd: 'dotnet',
  args: dotnetRunApi(['db', command, ...remaining]),
  cwd: repoRoot,
  env,
})

process.exit(code)
