import { dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const here = dirname(fileURLToPath(import.meta.url))

/** Absolute path of the repository root (the directory holding package.json). */
export const repoRoot = resolve(here, '..', '..')

export const appDir = join(repoRoot, 'app')
export const apiDir = join(repoRoot, 'api')
export const apiProjectDir = join(apiDir, 'src', 'Q2.Api')
export const apiSolution = join(apiDir, 'q2.slnx')
export const apiDataDir = join(apiDir, '.data')
export const openApiDocument = join(apiDir, 'openapi', 'q2-api.json')

/**
 * Argv array for `dotnet run` against the API, with build noise suppressed.
 *
 * `--no-launch-profile` matters: launchSettings.json pins
 * ASPNETCORE_ENVIRONMENT=Development and would silently override the
 * environment a script just set. The listening URL therefore comes from
 * configuration (`Urls` in appsettings) rather than from the launch profile.
 */
export function dotnetRunApi(args: string[]): string[] {
  return ['run', '--project', apiProjectDir, '--no-launch-profile', '--verbosity', 'quiet', '--', ...args]
}
