# CLAUDE.md — frontend

**Read [AGENTS.md](AGENTS.md) in this folder first**, and
[../AGENTS.md](../AGENTS.md) for the repository-wide rules. This file only adds
Claude Code specifics.

## Fast orientation

```
app/pages/index.vue                  the dashboard — how a page composes
app/components/goals/GoalCard.vue    the reference component
app/composables/useGoals.ts          state, loading, error handling
app/api/errors.ts                    every failure is normalised here
app/middleware/auth.global.ts        the route guard
app/composables/useSession.ts        who is signed in
sentry.shared.ts                     what never reaches Sentry
tests/component/GoalCard.spec.ts     how to write a component test
```

## Traps

- **Mobile format, always.** The app is developed in a browser but ships via
  Capacitor, so build and verify the phone layout first: 390 × 844, device
  toolbar on. Base classes are the phone layout, `sm:`/`md:` only widen it. A
  change you only saw in a maximised window has not been looked at
  ([AGENTS.md](AGENTS.md) section 8).
- **`app/app/` is correct.** Nuxt 4's source directory is `app/` inside the
  project, so pages are at `app/app/pages/`.
- **Component names ignore the folder** (`pathPrefix: false`).
  `components/goals/GoalCard.vue` is `<GoalCard>`, not `<GoalsGoalCard>`. A
  nested component that renders nothing usually means a name mismatch.
- **`app/api/generated/schema.d.ts` is generated.** Run `bun run api:openapi`
  from the repository root; never hand-edit it.
- **Components take `ApiFailure`, not `ApiError`.** Class instances do not
  survive the SSR payload; a `ref` set during server rendering is empty after
  hydration. Failures travel inside `useAsyncData`.
- **Do not pass `aria-label` to `UProgress`** expecting it on the progress bar —
  it lands on the wrapper. The bar carries `role="progressbar"` and
  `aria-valuenow` itself.
- **Do not format dates with `Intl`.** ICU data differs between Node versions
  and browsers (`Sep` vs `Sept`), which causes hydration mismatches. Use
  `formatDate` in `app/utils/goalDisplay.ts`.
- **The session is a cookie, not a token.** `useQ2Api` sends
  `credentials: 'include'` in the browser and copies the `cookie` header off the
  incoming request during SSR. Drop either and the app renders signed-out and
  then flickers.
- **Do not disable Sentry outside production.**

## Verifying a change

```bash
bun run lint
```

```bash
bun run typecheck
```

```bash
bun run test
```

Every E2E spec starts signed in — `globalSetup` does it once through the real
form and `storageState` carries it. `authentication.spec.ts` opts out, because
it is the spec that is about signing in.

The E2E suite starts its own API and frontend on ports 5081/3001, so it can run
while `bun run dev` is up:

```bash
bun run test:e2e
```

Its only project is `mobile-chromium` (Pixel 7, viewport pinned to 390 × 844),
so the assertions are made at phone width — but they are assertions about
behaviour and text, never about geometry. Anything user-visible therefore also
gets looked at by hand at 390 × 844, in the browser device toolbar or with a
resized viewport if you are driving the page yourself.

Report the commands you actually ran and their real output — never a summary of
checks that were not executed.
