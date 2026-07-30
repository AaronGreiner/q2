# 0008 — One host, two systemd services, one origin

- **Status:** Accepted
- **Date:** 2026-07-30

## Context

[docs/next-steps.md](../next-steps.md) section 11 listed deployment as "decide
where the two applications run, then …". The decision was taken: a single Linux
host that already runs two unrelated Nuxt projects behind Caddy.

The constraints that shaped it:

- **The host is shared.** Two other projects and their Caddy site must keep
  working, which rules out anything that takes over port 80/443 or rewrites
  shared configuration.
- **q2 is two applications.** A Nuxt SSR server and an ASP.NET Core API, which
  have to be reachable as one product.
- **There is no real data yet.** SQLite, no database server, per
  [0004](0004-sqlite-first.md).
- **The repository already refuses to migrate on startup** in Staging and
  Production, so a deployment has to run migrations itself.

## Decision

**Two systemd services on the existing host, behind the existing Caddy, sharing
one origin.** Released by tag through GitHub Actions.

### One origin, not two

Caddy serves `q2.aarongreiner.dev` and routes by path: `/api/*`, `/health` and
`/openapi/*` to the API, everything else to the Nuxt server.

The alternative — `q2.aarongreiner.dev` plus `api-q2.aarongreiner.dev` — would
need a second DNS record, a CORS allow-list in the API, and preflight requests
on every mutation. Path routing needs none of that: the browser's request to
`/api/goals` is same-origin. `Cors:AllowedOrigins` stays empty, which is the
configuration that cannot be got wrong.

It also means `NUXT_PUBLIC_API_BASE_URL` is one value for both server-side
rendering and the client, rather than two that can drift apart.

### systemd, not Docker

The host runs Caddy and two Node services directly, under systemd. q2 follows
that pattern rather than introducing a container runtime for one project: an
operator reading `q2-app.service` sees the same shape as `tram.service`.

Docker is installed on the host, and was being used for a q2 infrastructure
stack (Postgres, Valkey, MinIO, Mailpit) that no longer corresponded to
anything the application does. That stack was removed.

### Framework-dependent, runtime from apt

`dotnet publish` without a runtime identifier; the host carries
`aspnetcore-runtime-10.0` from Ubuntu's archive. Security updates arrive the
same way they do for Node and Caddy, and the artefact stays small.

A self-contained publish would remove the host dependency at the cost of
shipping a runtime per release and patching it ourselves.

### The deployment logic lives on the server

`deploy/deploy.sh` is uploaded and executed; the workflow only moves files.

The interesting part of a deployment is what happens when a step fails halfway
through. That belongs in one readable file that can also be run by hand during
an incident — not spread across YAML steps that abort wherever they happen to
be, leaving the host in whatever state the last successful step produced.

The script keeps the previous release, restores it — **binaries and
configuration together** — when the new one does not answer, and exits
non-zero so the workflow reports the failure.

### The tag is the version

`v1.2.3` becomes the backend assembly version, the frontend package version,
the Sentry release id `q2@1.2.3`, and the release the source maps are uploaded
under. One value, computed once in the workflow, passed to every job.

They have to agree: a source map uploaded under a different release than the
one the running app reports cannot be used to resolve a stack trace, which is
the whole point of uploading it.

### The release workflow calls the CI workflow

`ci.yml` gained a `workflow_call` trigger and `release.yml` invokes it, rather
than repeating the jobs. A release must not be able to pass a weaker gate than
a pull request, and two copies of a test matrix drift.

## Consequences

- One DNS record is the only manual step to bring the environment up.
- The other projects on the host are untouched; the Caddy block was appended,
  and nothing shared was rewritten.
- Deployments have a few seconds of downtime. Acceptable here, and the thing to
  revisit first if it stops being acceptable.
- A failed release cannot leave the site down: it rolls back and reports.
- Configuration lives in repository secrets and is rendered onto the host by
  every deployment, so the server holds no configuration that is not also in
  the secret store.
- The host is `Staging`. Production is a second host and a second set of
  secrets, not a flag — which also means `/api/diagnostics/*` and the
  `sentry canary` command are unavailable here, both being guarded against
  Protected environments by design.

## Alternatives considered

- **A PaaS (Fly, Render, Azure App Service).** Removes the host management, adds
  a bill, a vendor and a second place where configuration lives. The host
  already exists and already runs comparable services.
- **Docker Compose on the same host.** Would isolate q2 from the other projects,
  at the cost of a build-and-registry step and a second operational model on one
  box. Worth revisiting if q2 ever needs a service the host cannot provide
  directly.
- **Two subdomains for frontend and API.** Rejected above: more DNS, more
  configuration, and CORS to get wrong.
- **Deploying from the workflow with a list of SSH commands.** Simple until a
  step fails; then there is no rollback and no single place to read what the
  deployment actually does.
- **`rsync --delete` straight onto the running directories.** No atomicity and
  no way back. The swap-and-keep-previous approach costs one extra directory.
