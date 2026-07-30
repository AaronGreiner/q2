# Deployment

How a tag becomes a running release, and what to do when it does not.

The decision behind this shape is recorded in
[adr/0008-deployment-topology.md](adr/0008-deployment-topology.md). This file is
the operational description.

---

## 1. What runs where

One Linux host (Ubuntu 26.04) that also runs unrelated projects. q2 adds two
systemd services and one Caddy site; it shares nothing with the others.

```
                         ┌── /api/*, /health, /openapi/*  ──> 127.0.0.1:5080  q2-api
browser ──> Caddy :443 ──┤
      q2.aarongreiner.dev└── everything else               ──> 127.0.0.1:3002  q2-app
                                                                     │
                                                       SQLite  /var/www/q2/.data
```

Both applications answer on the loopback interface only. Caddy is the sole
public listener and terminates TLS.

**The frontend and the API share one origin.** That is why there is a single
DNS record and no CORS configuration: the browser's request to `/api/goals` is
same-origin, and `NUXT_PUBLIC_API_BASE_URL` is the same value for server-side
rendering and for the client.

| | |
| --- | --- |
| Host | `87.106.223.169` |
| Domain | `q2.aarongreiner.dev` |
| Environment | `Staging` — Sentry environment `staging` |
| Backend | `q2-api.service`, ASP.NET Core on `127.0.0.1:5080` |
| Frontend | `q2-app.service`, Nuxt SSR on `127.0.0.1:3002` |
| Database | SQLite at `/var/www/q2/.data/q2-staging.db` |
| Runtime | `aspnetcore-runtime-10.0` and `node` from apt |

Ports 3000 and 3001 belong to the other projects on this host. 3002 and 5080
were free and are now q2's.

## 2. Releasing

```bash
git tag v1.2.3 && git push origin v1.2.3
```

That is the whole procedure — and it is a person's action, not an agent's:
tagging is a release decision, and [AGENTS.md](../AGENTS.md) section 11 keeps
git out of an agent's hands unless it was asked for explicitly.

The tag is the only place the version is written
down; [`.github/workflows/release.yml`](../.github/workflows/release.yml) turns
`v1.2.3` into:

- the backend assembly version (`-p:Version=1.2.3`),
- the frontend `package.json` version, stamped at build time,
- the Sentry release id `q2@1.2.3`, reported by both services,
- the release the frontend source maps are uploaded under.

Those four have to agree or a Sentry stack trace cannot be resolved back to
source, which is why the id is computed once and passed to every job.

The workflow, in order:

| Job | What it does | Blocks the deploy? |
| --- | --- | --- |
| `version` | Parses and validates the version | yes |
| `verify` | Calls `ci.yml` — the full pull-request gate | yes |
| `build-api` | `dotnet publish`, framework-dependent | yes |
| `build-app` | `nuxt build`, uploads source maps to Sentry | yes |
| `deploy` | Uploads and runs `deploy/deploy.sh` on the server | — |
| `sentry-canary` | One synthetic event against a test DSN | no, and skipped without the secret |

`verify` is the CI workflow itself, not a copy of it. A release must not be
able to pass a weaker check than a pull request, which is what a second,
drifting copy of those jobs would eventually mean.

`workflow_dispatch` builds a version supplied by hand, for a re-run.

## 3. What a deployment does on the server

[`deploy/deploy.sh`](../deploy/deploy.sh) runs on the host. The workflow only
uploads; the script decides. Everything that must happen in a particular order —
and everything that must be undone when a step fails — lives there rather than
being spread across YAML steps that abort wherever they happen to be.

1. **Validate.** Both tarballs unpack and contain their entrypoint. A tarball
   without `Q2.Api.dll` is rejected before anything is touched.
2. **Snapshot.** The current `shared/` configuration is copied aside, and the
   rollback is armed.
3. **Configure.** `api.env` and `app.env` are installed from the upload;
   `release.env` is written with the new release id.
4. **Swap.** Services stop, the old `api/` and `app/` move to `previous/`, the
   new ones move in.
5. **Migrate.** `Q2.Api.dll db migrate`. Staging and Production have
   `MigrateOnStartup=false` on purpose, so this is a deliberate step.
6. **Start and prove it.** Each service is started and then polled until it
   answers — `/health` must report `healthy`, the frontend must return HTML. A
   deployment that reports success without a request having been served is not
   a deployment.

**Any failure from step 3 onwards rolls back**: the previous binaries *and* the
previous configuration are restored, the services are restarted, and the script
exits non-zero so the workflow fails. The configuration is part of the rollback
deliberately — restoring the old binaries while leaving the new `release.env` in
place would leave the service reporting a release to Sentry that was never
successfully deployed, and every later error would be filed under a version that
does not exist.

The release that failed is kept in `/var/www/q2/failed/` rather than deleted,
because otherwise the evidence is gone by the time anyone looks.

## 4. Server layout

```
/var/www/q2/
├── api/            the published backend      ← replaced by every deployment
├── app/.output/    the built frontend         ← replaced by every deployment
├── .data/          SQLite database            ← survives
├── shared/         api.env, app.env, release.env  ← survives
├── previous/       the last good release, for rollback
├── failed/         the last failed release, for inspection
└── incoming/       upload target
```

`shared/` and `.data/` sit outside `api/` and `app/` precisely because those two
are replaced wholesale.

## 5. Secrets and configuration

The repository secrets are the single source of truth. The deploy job renders
`api.env` and `app.env` from them on every release, so changing a DSN means
updating the secret and re-running the workflow — never editing a file on the
server.

| Secret | Used for |
| --- | --- |
| `DEPLOY_HOST`, `DEPLOY_USER` | Where to deploy |
| `DEPLOY_SSH_KEY` | Private half of `/root/.ssh/gh_deploy_q2` |
| `DEPLOY_KNOWN_HOSTS` | The host key, so the connection is verified |
| `SENTRY_DSN_API` | Backend reporting |
| `SENTRY_DSN_APP` | Frontend reporting |
| `SENTRY_ORG`, `SENTRY_AUTH_TOKEN` | Both uploads below |
| `SENTRY_PROJECT_APP` | Frontend source map upload |
| `SENTRY_PROJECT_API` | Backend debug symbol and source upload |
| `SENTRY_TEST_DSN` | Optional canary |

Without `SENTRY_AUTH_TOKEN` both builds still succeed and the workflow says so
loudly in each job: no source maps and no debug symbols are uploaded, so stack
traces in Sentry have no line numbers. The frontend maps are additionally
deleted from the artefact in that case — everything under `.output/public` is
publicly fetchable, so a map that reached the server would be readable by
anyone.

## 6. Operating it

```bash
ssh root@87.106.223.169
```

```bash
systemctl status q2-api q2-app
```

```bash
journalctl -u q2-api -f
```

Roll back to the previous release by hand:

```bash
systemctl stop q2-app q2-api && rm -rf /var/www/q2/api /var/www/q2/app && mv /var/www/q2/previous/api /var/www/q2/api && mv /var/www/q2/previous/app /var/www/q2/app && systemctl start q2-api q2-app
```

Re-run a deployment from what is already uploaded:

```bash
bash /var/www/q2/incoming/deploy.sh q2@1.2.3
```

Prepare the host from scratch, or repair it — the script is idempotent:

```bash
bash deploy/bootstrap.sh
```

**The A record has to exist before that runs.** The script refuses to configure
Caddy without it, and proves a certificate was issued before it reports
success — see the next section for why.

## 7. When the release deploys but the URL does not answer

The deploy job's last step requests `$PUBLIC_URL` from GitHub's runner. When it
is the *only* failing step, both services are already running and healthy on
loopback — `deploy.sh` proved that before it exited — and the fault is in front
of them. The curl exit code says which one:

| Exit | Meaning | Fix |
| --- | --- | --- |
| 6 | `q2.aarongreiner.dev` does not resolve | Create the A record |
| 35 | Resolves, but Caddy has no certificate for it | Reload Caddy, below |

Exit 35 (`tlsv1 alert internal error`) is Caddy answering a handshake for a
domain it holds no certificate for. Almost always this means DNS was still
missing at the moment the site block was first loaded: **Caddy requests the
certificate then, not on the first request**, and once that request fails it
backs off for hours rather than noticing that DNS has since appeared. The site
stays unreachable in the meantime, with nothing wrong on the host.

Confirm, then force a fresh attempt:

```bash
journalctl -u caddy --no-pager | grep -iE 'tls\.obtain|acme_client' | tail -20
```

```bash
systemctl reload caddy
```

Issuance takes a few seconds. `bootstrap.sh` now checks DNS before it touches
Caddy and waits for the certificate afterwards, so a host prepared with it
cannot end up in this state without saying so.

## 8. Sentry on this host

Both services report to Sentry with environment `staging` and the release id of
the running deployment. Error and trace sampling are both `1.0`: this host
exists to be analysed, and the volume is nowhere near a budget.

Confirm what the backend thinks it is doing:

```bash
journalctl -u q2-api --no-pager | grep -o 'Sentry active[^"]*' | tail -1
```

That line reports the transport, environment, release and both sample rates.

Two things are deliberately **not** available here:

- **`/api/diagnostics/*` does not exist in Staging.** `DiagnosticsEndpoints`
  skips Protected environments, so there is no synthetic error trigger on this
  host. The frontend `/diagnostics` page is switched off for the same reason —
  it calls those routes and would show a permanently broken panel.
- **`sentry canary` refuses to run here.** It is guarded against Staging and
  Production so a synthetic event can never reach a real project. The canary
  runs in CI against a separate test DSN instead.

Real errors report normally; that is what the environment is for.

## 9. What this deliberately is not

- **No zero-downtime deployment.** Services stop, files swap, services start —
  a few seconds. Two SSR processes behind a load balancer would be a different
  system, and nothing here needs one yet.
- **No Docker.** The host runs Caddy and two Node services directly; q2 follows
  the same pattern. See [adr/0008](adr/0008-deployment-topology.md).
- **No production environment.** This host is `Staging`. A production
  deployment is a second host and a second set of secrets, not a flag.
- **No secret rotation automation.** Changing a DSN is: update the secret,
  re-run the release.
- **No database backup.** SQLite with no real data yet, on a test host. The
  first time the data matters, this is the first gap to close.
