# 0001 — One repository, two applications, one task runner

- **Status:** Accepted
- **Date:** 2026-07-29

## Context

q2 needs a Nuxt frontend and an ASP.NET Core backend. They have different
toolchains (Bun and the .NET SDK), different test runners and different build
outputs, but they are developed together, released together and share a
contract that must not drift.

A new contributor — human or agent — has to be able to clone, install and run
everything without reading a wiki first.

## Decision

**One repository**, with `app/` and `api/` as independent applications, and a
**single task runner at the root** implemented in TypeScript and executed by
Bun.

- `package.json` at the root defines every command a person needs to know.
- Bun workspaces give one lockfile for the JavaScript side.
- `scripts/tasks.ts` holds the multi-step pipelines (lint, test, build,
  validate) as a declarative map, so a pipeline's order lives in exactly one
  place.
- Longer flows that need real logic — `dev`, `db`, `manual-testing`, `openapi`,
  `setup` — are their own small scripts.
- Everything is spawned with an argv array and never through a shell, so there
  is no quoting difference between macOS, Linux and Windows.
- The `dotnet-ef` tool is pinned in `.config/dotnet-tools.json`.

The script named `setup` rather than `install`, because a `package.json` script
called `install` is an npm lifecycle hook and would recurse on `bun install`.

## Consequences

- One command to learn per task, regardless of which half it touches:
  `bun run test`, `bun run validate`, `bun run db:migrate`.
- CI runs the same commands a developer runs; there is no separate CI script to
  drift.
- The root scripts are the only place that knows about both halves, so neither
  application depends on the other's tooling.
- Bun becomes a prerequisite even for backend-only work. Acceptable: it is a
  single binary, and the raw `dotnet` commands are documented in
  `api/AGENTS.md` for when it is not available.
- Cross-platform behaviour needs care in the scripts rather than in shell
  snippets — a deliberate trade in favour of portability.

## Alternatives considered

- **Two repositories.** Contract drift becomes a cross-repository coordination
  problem, and "does the frontend still work against this API?" stops being
  answerable in one CI run.
- **A Makefile.** Not portable to Windows, and worse at expressing conditional
  logic than a small TypeScript file.
- **npm scripts with shell one-liners.** Quoting differs between platforms, and
  multi-step pipelines become unreadable strings.
- **Nx or Turborepo.** Real value for many packages with a shared dependency
  graph; here there are two applications that do not share code, so it would be
  configuration without benefit.
