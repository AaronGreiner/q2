/**
 * Regenerates the API contract and the typed frontend client.
 *
 *   bun run api:openapi      # export OpenAPI document, then generate TS types
 *   bun run api:types        # only regenerate TS types from the committed doc
 *
 * Both artefacts are committed:
 *   api/openapi/q2-api.json                      — the contract
 *   app/app/api/generated/schema.d.ts            — generated, never edit by hand
 *
 * Run this after changing any endpoint or DTO; CI verifies that the committed
 * files match the code.
 */
import { mkdirSync } from 'node:fs'
import { dirname, relative } from 'node:path'
import { appDir, dotnetRunApi, openApiDocument, repoRoot } from './lib/paths.ts'
import { failure, info, run, success } from './lib/proc.ts'

const typesOnly = process.argv.includes('--types-only')
const generatedTypes = 'app/api/generated/schema.d.ts'

if (!typesOnly) {
  mkdirSync(dirname(openApiDocument), { recursive: true })

  const exportCode = await run({
    label: 'export OpenAPI document',
    cmd: 'dotnet',
    args: dotnetRunApi(['openapi', '--output', openApiDocument]),
    cwd: repoRoot,
    env: { ASPNETCORE_ENVIRONMENT: 'Development' },
  })

  if (exportCode !== 0) {
    failure('Could not export the OpenAPI document.')
    process.exit(exportCode)
  }
}

const typesCode = await run({
  label: 'generate frontend API types',
  cmd: 'bun',
  args: ['x', 'openapi-typescript', relative(appDir, openApiDocument), '-o', generatedTypes],
  cwd: appDir,
})

if (typesCode !== 0) process.exit(typesCode)

success('API contract is up to date')
info(`  ${relative(repoRoot, openApiDocument)}`)
info(`  app/${generatedTypes}`)
