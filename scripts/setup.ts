/**
 * One-shot setup after cloning.
 *
 *   bun run setup
 *
 * Installs everything both sub-projects need and creates the local
 * Development database so `bun run dev` works immediately.
 */
import { copyFileSync, existsSync } from 'node:fs'
import { join } from 'node:path'
import { envFileDefaults } from './lib/dotenv.ts'
import { apiDir, appDir, dotnetRunApi, repoRoot } from './lib/paths.ts'
import { info, run, runSequence, success } from './lib/proc.ts'

for (const dir of [repoRoot, apiDir, appDir]) {
  const example = join(dir, '.env.example')
  const target = join(dir, '.env')
  if (existsSync(example) && !existsSync(target)) {
    copyFileSync(example, target)
    info(`Created ${target.replace(repoRoot + '/', '')} from .env.example`)
  }
}

const code = await runSequence([
  { label: 'bun install (root + app workspace)', cmd: 'bun', args: ['install'], cwd: repoRoot },
  { label: 'dotnet tool restore (dotnet-ef)', cmd: 'dotnet', args: ['tool', 'restore'], cwd: repoRoot },
  { label: 'dotnet restore', cmd: 'dotnet', args: ['restore', 'api/q2.slnx'], cwd: repoRoot },
  {
    label: 'create Development database',
    cmd: 'dotnet',
    args: dotnetRunApi(['db', 'migrate']),
    cwd: repoRoot,
    env: { ...envFileDefaults(join(apiDir, '.env')), ASPNETCORE_ENVIRONMENT: 'Development' },
  },
])

if (code !== 0) process.exit(code)

const playwright = await run({
  label: 'install Playwright browsers (chromium)',
  cmd: 'bun',
  args: ['x', 'playwright', 'install', '--with-deps', 'chromium'],
  cwd: appDir,
})

if (playwright !== 0) {
  info('Playwright browsers could not be installed automatically.')
  info('E2E tests need them — run `bun x playwright install chromium` inside app/ later.')
}

success('Setup finished. Next: `bun run dev`')
