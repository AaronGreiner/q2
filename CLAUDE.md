# CLAUDE.md

**The conventions for this repository live in [AGENTS.md](AGENTS.md). Read it
first — everything there applies here.** This file only adds what is specific
to working in Claude Code.

## Orientation

| You want to… | Read |
| --- | --- |
| understand the product and run it | [README.md](README.md) |
| know the rules for changing code | [AGENTS.md](AGENTS.md) |
| work on the frontend | [app/AGENTS.md](app/AGENTS.md) |
| work on the backend | [api/AGENTS.md](api/AGENTS.md) |
| understand why something is the way it is | [docs/adr/](docs/adr/) |
| know what to build next | [docs/next-steps.md](docs/next-steps.md) |
| release or debug a deployment | [docs/deployment.md](docs/deployment.md) |

## Things that are easy to get wrong here

- **`app/app/` is not a typo.** Nuxt 4 puts application source in `app/`
  inside the project, so the frontend's pages live at `app/app/pages/`.
- **Component names come from the file name, not the folder.**
  `app/app/components/goals/GoalCard.vue` is `<GoalCard>`, because
  `pathPrefix: false` is set. File names must therefore be unique across the
  component tree.
- **`app/app/api/generated/schema.d.ts` is generated.** Never edit it. Run
  `bun run api:openapi` after changing an endpoint or DTO; the regenerated
  contract and types are part of the same change.
- **The Development database is never reset automatically**, and
  `bun run db:reset` refuses to run in Development. That is deliberate; do not
  "fix" it.
- **Integration tests must not use `Microsoft.EntityFrameworkCore.InMemory`.**
  The real SQLite provider is used everywhere.
- **Sentry is not disabled outside production.** Do not add
  `if (production)` around it.
- **The browser is the test surface; a phone is the target.** q2 ships as a
  Capacitor app, so anything user-visible is checked at **390 × 844**, never
  only in a wide window — and a screenshot you take for the user should be a
  mobile one. E2E runs in that viewport (`mobile-chromium`), but it asserts
  behaviour, not geometry, so a green run is not a look at the layout. See
  [AGENTS.md](AGENTS.md) section 5 and [app/AGENTS.md](app/AGENTS.md)
  section 8.

## Running things

Prefer the root scripts over ad-hoc commands — they set the environment
correctly and are what CI runs:

```bash
bun run validate
```

```bash
bun run test:api
```

```bash
bun run test:app
```

Long-running commands worth backgrounding: `bun run dev`,
`bun run test:manual:start`, `bun run test:e2e`. The E2E suite starts its own
API and frontend, so do not have `bun run dev` running expectations of it — it
uses separate ports (5081/3001) precisely so both can coexist.

## Do not commit

Leave your work in the working tree. No `git commit`, no `git push`, no tag, no
new branch — unless the user asks for it in that conversation, in those words.
"Do it properly", "finish it" and "make it production-ready" are not that ask.

Finishing a task means the change is on disk, verified, and described. See
[AGENTS.md](AGENTS.md) section 11 for why.

## Reporting

State the commands you ran and their real results. If a step failed or could
not be run in this environment, say which one and give the exact command to
reproduce it. Never present unrun checks as passing — see
[AGENTS.md](AGENTS.md) section 15.

When you finish, say what is uncommitted and what it touches, so the author can
review and commit it themselves.
