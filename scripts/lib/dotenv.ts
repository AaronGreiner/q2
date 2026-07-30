import { existsSync, readFileSync } from 'node:fs'

/**
 * Minimal `.env` reader.
 *
 * Bun already loads the repository-root `.env` into `process.env` before a
 * script runs. The ASP.NET Core process does not read `.env` files at all, so
 * the root scripts read `api/.env` themselves and pass the values on as real
 * environment variables. That keeps `api/.env.example` meaningful without
 * pulling in a dotenv dependency.
 *
 * Supported syntax: `KEY=value`, `#` comments, blank lines, optional `export`
 * prefix and optional single/double quotes around the value. Nothing else —
 * anything more elaborate belongs in real configuration.
 */
export function readEnvFile(path: string): Record<string, string> {
  if (!existsSync(path)) return {}

  const result: Record<string, string> = {}

  for (const rawLine of readFileSync(path, 'utf8').split(/\r?\n/)) {
    const line = rawLine.trim()
    if (line.length === 0 || line.startsWith('#')) continue

    const withoutExport = line.startsWith('export ') ? line.slice('export '.length) : line
    const separator = withoutExport.indexOf('=')
    if (separator <= 0) continue

    const key = withoutExport.slice(0, separator).trim()
    let value = withoutExport.slice(separator + 1).trim()

    if (
      (value.startsWith('"') && value.endsWith('"') && value.length >= 2)
      || (value.startsWith('\'') && value.endsWith('\'') && value.length >= 2)
    ) {
      value = value.slice(1, -1)
    }

    result[key] = value
  }

  return result
}

/**
 * Values from `path` that are not already set in the real environment.
 * Real environment variables always win, so CI and shell exports override
 * whatever a developer happens to have in their local file.
 */
export function envFileDefaults(path: string): Record<string, string> {
  const fromFile = readEnvFile(path)
  const defaults: Record<string, string> = {}

  for (const [key, value] of Object.entries(fromFile)) {
    if (process.env[key] === undefined && value !== '') {
      defaults[key] = value
    }
  }

  return defaults
}
