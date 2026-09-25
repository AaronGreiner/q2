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
DNS record: the browser's request to `/api/goals` is same-origin, and
`NUXT_PUBLIC_API_BASE_URL` is the same value for server-side rendering and for
the client. The one cross-origin caller is the iOS app, whose WebView is the
origin `capacitor://localhost`; `appsettings.Staging.json` lists it in
`Cors:AllowedOrigins`, and it signs in with bearer tokens rather than the
cookie ([adr/0034](adr/0034-bearer-tokens-for-the-native-app.md)). The app is
not deployed by this workflow — it goes to TestFlight from a Mac, by hand
(section 11).

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
7. **Prove it is a working release, not merely a running one.** Two more
   assertions, because the two above are both satisfied by an application that
   is up and useless — see section 7:
   - an anonymous `GET /api/profile` must answer **401**. A 5xx there means the
     API cannot work out who is asking.
   - the page served at `/` must not contain `data-testid="error-state"`. That
     is `AppErrorState`, the component every failed screen renders.

**Any failure from step 3 onwards rolls back**: the previous binaries *and* the
previous configuration are restored, the services are restarted, and the script
exits non-zero so the workflow fails. That includes a failed health check or
proof: `fail` triggers the rollback itself once it is armed, because `exit` does
not fire an `ERR` trap, and a failing check that ended the script without
restoring anything would leave precisely the broken release it just rejected
still running.

The configuration is part of the rollback deliberately — restoring the old
binaries while leaving the new `release.env` in place would leave the service
reporting a release to Sentry that was never successfully deployed, and every
later error would be filed under a version that does not exist.

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
| `MAIL_SMTP_HOST` | The mail provider's SMTP server. Without it Staging runs with password reset off, and the release says so |
| `MAIL_SMTP_PORT` | Optional. 587 with STARTTLS unless set; 465 means TLS from the first byte |
| `MAIL_SMTP_USERNAME`, `MAIL_SMTP_PASSWORD` | The SMTP login. Neither may contain a single quote or a line break — the release refuses to render them |

Without `SENTRY_AUTH_TOKEN` both builds still succeed and the workflow says so
loudly in each job: no source maps and no debug symbols are uploaded, so stack
traces in Sentry have no line numbers. The frontend maps are additionally
deleted from the artefact in that case — everything under `.output/public` is
publicly fetchable, so a map that reached the server would be readable by
anyone.

**Mail** goes out as `noreply@q2.aarongreiner.dev` (`MAIL_FROM` at the top of
`release.yml`), through whichever EU provider the four `MAIL_SMTP_*` secrets
belong to. Before the first release with them, the provider has to be allowed to
send for that domain: its SPF, DKIM and DMARC records go into the DNS zone of
`q2.aarongreiner.dev`, or the mail lands in spam or is refused outright. Every
link in a mail starts with `PUBLIC_URL`, never with whatever host a request
named. Without `MAIL_SMTP_HOST` the release renders `Q2__Mail__Transport=Off`
and warns; the reset screen then says that this environment sends no mail. A
Production host refuses to start that way — see
[adr/0026-mail-and-password-reset.md](adr/0026-mail-and-password-reset.md).

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
success — see section 8 for why.

## 7. When the release deploys, the URL answers, and the app is broken anyway

This is what happened to **v0.0.3**, and it is the reason steps 6 and 7 of the
deployment now assert more than "something answered".

The release deployed cleanly and every check passed: `/health` reported
`healthy`, the frontend returned a valid HTML document, and the workflow went
green. The site was nevertheless unusable — `/api/profile`, `/api/feed` and
`/api/leaderboard` all answered **500**, so every screen rendered the error
state.

The cause was a design mismatch rather than a deployment fault. v0.0.3 shipped
the model in [adr/0009](adr/0009-single-known-person.md): identity was a single
`Person` row flagged `IsCurrentUser`, and `CurrentPerson` threw an ordinary
`InvalidOperationException` — a 500 — when that row was missing. **Staging is
never seeded**: its `SeedProfile` is `None`, there is no Staging seed profile to
choose, and `DatabaseResetGuard` refuses destructive work in a protected
environment. So the row could not exist there, and that release could not work
on this host no matter how well it deployed.

Both checks were satisfied by the broken app for the same reason — they asserted
liveness, not correctness:

| Check | Why it passed anyway |
| --- | --- |
| `/health` reports `healthy` | It only calls `CanConnectAsync`, and an empty database connects perfectly well. |
| The frontend returns HTML | The SSR error state is a well-formed HTML document. |

The fix for the site is [adr/0011](adr/0011-authentication-with-identity.md),
which supersedes 0009: real accounts, `CurrentPerson` reads the session, and an
unauthenticated request is refused with **401** instead of failing with 500.
Staging needs no seed — an account is created through `/register`.

The fix for the *pipeline* is the two proofs in section 3. Both were run against
this host while it was broken, and both fail it.

Note for the next release: an anonymous `GET /` is now answered with a **302**
to the sign-in screen, so anything checking that page has to follow redirects.
`deploy.sh` and the workflow both use `curl -L`; a check without it sees an
empty redirect body and reports a healthy release as broken.

## 8. When the release deploys but the URL does not answer

The deploy job's last step requests `$PUBLIC_URL` from GitHub's runner. When it
is the *only* failing step, both services are already running and healthy on
loopback — `deploy.sh` proved that before it exited. The curl exit code says
what is actually wrong, and it is not always the host:

| Exit | Meaning | Fix |
| --- | --- | --- |
| 6 | `q2.aarongreiner.dev` does not resolve | Create the A record |
| 35 | Resolves, but Caddy has no certificate for it | Reload Caddy, below |
| 23 | Nothing is wrong. The check broke its own pipe | Fix the workflow, not the server |

Exit 23 is worth knowing about because it cost a release once. The step used to
pipe curl into `grep -q`; `grep -q` exits the instant it matches, and
`<!DOCTYPE html>` is the first thing in the document, so the pipe closed while
curl still had ~25 KB to write. curl died with a write error, `pipefail` made
that the step's status, and the step failed *having already proved the page was
correct*. It reproduced only when the response arrived in several chunks, so it
passed by hand and failed from a runner. The step now captures the body and
matches it in bash — no pipe, no race. Nothing analogous is left in `deploy.sh`
or `bootstrap.sh`.

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

## 9. Sentry on this host

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

## 10. What this deliberately is not

- **No zero-downtime deployment.** Services stop, files swap, services start —
  a few seconds. Two SSR processes behind a load balancer would be a different
  system, and nothing here needs one yet
  ([#17](https://github.com/AaronGreiner/q2/issues/17)).
- **No Docker.** The host runs Caddy and two Node services directly; q2 follows
  the same pattern. See [adr/0008](adr/0008-deployment-topology.md).
- **No production environment.** This host is `Staging`. A production
  deployment is a second host and a second set of secrets, not a flag
  ([#14](https://github.com/AaronGreiner/q2/issues/14)).
- **No secret rotation automation.** Changing a DSN is: update the secret,
  re-run the release.
- **No backup.** Neither the SQLite database nor the image files under
  `.data/` are copied anywhere. There is no real data on this test host yet;
  the first time the data matters, this is the first gap to close
  ([#13](https://github.com/AaronGreiner/q2/issues/13)).

## 11. The iOS app and TestFlight

The iOS app is not part of the release workflow. It is archived and uploaded
from a Mac with Xcode, and App Store Connect turns the upload into a TestFlight
build ([#51](https://github.com/AaronGreiner/q2/issues/51)):

```bash
bun run app:ios:testflight
```

That is `bun run app:ios` — the native web build against **Staging**, copied
into `app/ios` — followed by `xcodebuild archive` and `xcodebuild
-exportArchive` with [`app/ios/App/ExportOptions.plist`](../app/ios/App/ExportOptions.plist),
which uploads. The build carries:

| | from |
| --- | --- |
| Version (`CFBundleShortVersionString`) | the latest `v*` tag — tag first, then upload |
| Build number (`CFBundleVersion`) | `git rev-list --count HEAD`; `Q2_IOS_BUILD_NUMBER` overrides it |
| Sentry | `Q2_IOS_SENTRY_DSN`, the public DSN of q2-app; without it the app reports nothing |

The script says so when HEAD is past the tag or the working tree is not clean,
because then the version on the build is not only that release's code.

### Once, before the first upload

These are a person's steps — an account, a contract and a signing identity
belong to whoever pays for the Apple Developer Program.

1. **Apple Developer Program** membership for the Apple ID that will own the
   app (developer.apple.com/programs). Until it is active there is no
   distribution signing and no App Store Connect.
2. **Xcode → Settings → Accounts**: add that Apple ID. Signing is automatic;
   Xcode creates the distribution certificate and profiles on first use.
3. **The team in the project**: `bun run app:ios --open`, target *App* →
   *Signing & Capabilities* → *Team*. That writes `DEVELOPMENT_TEAM` into
   `project.pbxproj`, and it is committed — the script refuses to run without
   it.
4. **The App ID**: developer.apple.com → *Certificates, Identifiers &
   Profiles* → *Identifiers* → + → *App IDs* → *App*, explicit bundle ID
   `dev.aarongreiner.q2`, no capabilities. Automatic signing does **not** do
   this for q2: an app with no capabilities is signed with the team's wildcard
   profile, so no App ID is ever registered, and App Store Connect has nothing
   to offer in step 5.
5. **App Store Connect → Apps → + → New App**: platform iOS, name *Qdos* (or
   the nearest free name — the name shown under the icon stays `Qdos` either
   way), primary language German, bundle ID `dev.aarongreiner.q2`, SKU `q2`.
   The bundle ID cannot be changed once the record exists.

### After each upload

Processing takes a few minutes to half an hour; App Store Connect mails when
the build is ready. Then, under *TestFlight*:

- **Internal testers** — people on the App Store Connect team, up to 100 —
  get the build as soon as it is processed, with no review. Add them to an
  internal group once, with *automatic distribution* on. That setting only
  picks up builds uploaded after the group exists; an older build has to be
  added to the group by hand. A tester who is not yet on the team has to be
  invited under *Users and Access* first, since only team members can be
  internal testers. The account holder usually gets no invitation mail, and
  the app simply appears in TestFlight.
- **External testers** — anyone with an e-mail address or a public link —
  need the first build of each version through Beta App Review. That asks for
  *Test Information*: a feedback e-mail, a privacy policy URL, and a sign-in
  for the reviewer on Staging.

A tester installs the *TestFlight* app from the App Store and accepts the
invitation from there.

Two questions App Store Connect would otherwise ask for every build are
answered in the project: `ITSAppUsesNonExemptEncryption` is `false` in
`Info.plist` (q2 only uses HTTPS, which is exempt), and the app is
**iPhone-only** (`TARGETED_DEVICE_FAMILY = 1`). The second is deliberate:
once a build with iPad support is on the App Store, iPad support cannot be
withdrawn, and q2 is laid out for a phone.

### When the upload fails

| Message | Meaning |
| --- | --- |
| *No Accounts* / *No signing certificate "iOS Distribution" found* | Xcode is not signed in with a member of the team (step 2) |
| *Error Downloading App Information* (the distribution log says `missingApp`), or *No suitable application records were found* | The App Store Connect record is missing, or has another bundle ID (steps 4 and 5) |
| `codesign` fails on `Capacitor.framework` | macOS asked for the login keychain password and nobody answered. Enter it and choose *Always Allow* |
| *The bundle version must be higher than the previously uploaded version* | That build number is used — `Q2_IOS_BUILD_NUMBER=… bun run app:ios:testflight` |
| *Invalid Pre-Release Train* / *train version is closed* | That version is on the App Store already — tag a new one |
