# Privacy and data protection

q2 is a self-care application. What people record here — what they are trying
to change about their lives, who they are doing it with, how it is going — is
personal by nature. Some of it would count as special-category data under
Art. 9 GDPR if it touches health.

That is the reason for the rules below. They are implemented and tested, not
aspirational.

> **This is a technical baseline, not a legal assessment.** A data protection
> review, a record of processing activities, a legal basis per purpose and a
> privacy notice are still required before production. See section 8.

---

## 1. Principles applied

| GDPR principle | How it shows up here |
| --- | --- |
| Data minimisation (Art. 5(1)(c)) | An account is an address and a password hash, and nothing else. No profiles, no analytics, no tracking, no location. |
| Purpose limitation (Art. 5(1)(b)) | Stored data is used to render goals. It is not sent to Sentry, not logged and not aggregated. |
| Transparency (Art. 5(1)(a)) | The environment is visible in the UI; the diagnostics page states plainly what reporting is active. |
| Storage limitation (Art. 5(1)(e)) | Local and test databases are throwaway by design. Production retention is an open decision — section 8. |
| Integrity and confidentiality (Art. 5(1)(f)) | Secrets never in source; internal detail never in responses; central scrubbing before anything leaves the process. |
| Data protection by default (Art. 25) | No request-body logging, no location, no analytics. **Two switches are deliberately not on the private setting in the frontend** — `sendDefaultPii` and Session Replay — see section 4. |

## 2. What is processed

**Stored in the database:**

| Category | Field | Note |
| --- | --- | --- |
| Goal content | title, description | User-authored. Potentially health-related. |
| Task content | title, measured amount and unit | User-authored. Same treatment. |
| Progress | steps done, days worked on, task completion dates | A day-by-day record of somebody's habits. |
| Time | creation timestamp, optional target date, reminder time | |
| People | display name, handle, initials, avatar colour | Separate from the account, and the only thing other people ever see of somebody. |
| Accounts | email address, password **hash**, security stamp, lockout state, the person it signs in as | ASP.NET Core Identity. No plain password is stored anywhere, ever. Nothing beyond what Identity requires — no phone number is collected, and the columns Identity creates for one stay empty. See [adr/0011-authentication-with-identity.md](adr/0011-authentication-with-identity.md). |
| Presence | last-seen timestamp | Written by the seeds, and updated when somebody signs in or registers. |
| Social graph | friendships, who asked whom, and when | One row per pair, holding both ends. Mutual-friend counts are derived on read, never stored. |
| Activity | what somebody did, when, and who cheered it | The subject is a goal or task title, so it inherits their treatment. |
| Messages | text, sender, timestamp, reactions | **The most personal thing q2 stores.** |
| Preferences | theme, language, notification switches | |

**Not processed at all:**

plain passwords, phone numbers, postal addresses, dates of birth, payment data,
location or coordinates, device identifiers, IP addresses beyond the transport
layer and what Sentry attaches (section 4), biometric data, advertising or
analytics identifiers, third-party profile data, photographs — an avatar is
initials on a colour, so there is no face to lose control of.

**The email address is the newest category here and the one to watch.** It is
the credential and nothing else: it is never shown to another person, never
searchable — `GET /api/friends/search` matches display names and handles only,
which is deliberate, because matching addresses would turn it into a way to
check whether a given address has an account here — and never sent to Sentry.

Every name in the database is invented — but a goal description, a task title
and a message all can identify somebody, so all three are treated as personal
data throughout.

Chat messages, the social graph and now the account itself are the categories
that need a retention and erasure answer before real people are in the database.
The initial version does not have one; see [next-steps.md](next-steps.md)
items 7 and 8.

**Stored on the device:** the service worker's cache holds the build output —
JavaScript, CSS, fonts, icons, the web app manifest — and no rendered page and
no API response. That is a deliberate limit and the reason the PWA does not
work offline: a cached document would be one person's goals sitting on a device
somebody else may pick up, and it would outlive the session cookie. It is also
what makes signing out complete: there is no content cache to purge.
`app/tests/e2e/pwa.spec.ts` asserts it against what the browser actually
stored, rather than against the configuration that was meant to produce it. See
[adr/0012-installable-pwa.md](adr/0012-installable-pwa.md).

Anything that changes this — offline use, a write queue, cached feeds — makes
sign-out a data-deletion path, and that has to be designed before the cache is.

## 3. Logging rules

**May be logged:** goal, task, conversation, activity and person *ids*,
participant and step *counts*, status and rhythm values, progress numbers,
durations, HTTP status codes, route templates, environment, release, migration
and seed names, correlation ids.

**Must never be logged:** goal titles and descriptions, task titles, message
text, person names and handles, credentials or tokens, connection strings,
cookies, request or response bodies, `Authorization` headers, email addresses,
IP addresses, coordinates, developer or machine names.

The services follow this literally: `ChatService` logs that a message was sent
in a conversation and nothing about what it said.

Request logging with headers or bodies is not enabled. Where a log line refers
to a goal, it refers to its **id** — see `GoalService.CreateAsync`.

## 4. Sentry rules

The complete filtering behaviour is in
[observability.md](observability.md#filtering); the privacy-relevant summary:

**Never sent:**

- passwords, tokens, API keys, `Authorization` headers, cookies, session ids
- database connection strings, DSNs, any secret
- full request or response bodies
- goal titles and descriptions, participant names — any user-authored content
- email addresses and user names
- exact location data, in events, breadcrumbs, tags, contexts or traces
- machine names, developer names, local file paths
- keystrokes (`ui.input` breadcrumbs are dropped entirely)

**Sent:** exception type and stack, route template, HTTP method and status,
environment, release, service name, a correlation id, and — in test runs — the
run id and seed profile.

### IP addresses and Session Replay

These were previously off and are now on, so that a failure on the Staging host
can be reconstructed rather than guessed at.

| | Frontend | Backend |
| --- | --- | --- |
| `sendDefaultPii` | `true` — the SDK attaches the IP address | `true` — same |
| User id, email, username | never set; the app does not tell the SDK who is signed in | cleared by `ScrubUser`; the SDK would otherwise fill the id with an installation identifier that identifies *our host* |
| Session Replay | every session (`replaysSessionSampleRate: 1`) | not applicable — replay is browser-only |
| Request bodies | never attached | never attached (`MaxRequestBodySize.None`, unaffected by `SendDefaultPii`) |

An IP address is personal data under the GDPR. Recording it, and recording the
screen, are the two most intrusive things q2 does, and they are switched on
deliberately rather than by oversight.

What still limits the damage:

- **Replay masks text and inputs** (`maskAllText`, `maskAllInputs`,
  `blockAllMedia`). A goal title is personal — AGENTS.md section 9 — and must
  not reach Sentry through a screen recording any more than through an event.
  The replay therefore shows layout, navigation and interaction, not content.
- **`beforeSend` and `beforeBreadcrumb` still run on every event.** Everything
  in the "never sent" list above is still removed.
- **`ui.input` breadcrumbs are still dropped**, so keystrokes are not captured.
- **Cookies and all but a handful of headers are still stripped**, on both
  sides, and request bodies never leave the process.

Both halves also upload their build artefacts to Sentry so a stack trace has
line numbers — source maps from the frontend, debug symbols and **sources**
from the backend. That means the application's source code is held by Sentry.
It contains no secrets (none are committed), but it is a disclosure to a
processor and belongs in the Art. 28 agreement listed in section 8.

Before real users' data is processed, this needs what section 8 lists: a
retention decision, a lawful basis, and a privacy notice that says the screen
is recorded. It is not a decision that should survive unexamined into
production.

Enforced by `SentryEventScrubber` (backend) and `sentry.shared.ts` (frontend),
both covered by unit tests, plus integration and E2E tests that assert on the
**serialised payload** — so "not in the event" means it would not have been
transmitted.

## 5. Location data

**No location functionality exists in this version, deliberately.** Building
one "for the demo" would mean processing location data with no purpose to
justify it, which is exactly what Art. 5(1)(b) forbids.

When one is introduced, it must satisfy all of:

- a concrete feature genuinely requires it;
- the user triggers it actively — never a background query;
- consent or permission is requested in comprehensible language, stating the
  purpose;
- the permission is revocable, and revocation takes effect immediately;
- the coarsest useful granularity is used — a region or a place name, not
  coordinates;
- it is short-lived: used and discarded, not accumulated into a history;
- it is processed on the device where possible, rather than transmitted;
- it is off by default.

Regardless of the feature, **exact location must never appear in a log, a
Sentry event, a breadcrumb, a tag, a context or a trace.** The scrubbers
already remove coordinate-shaped values and location-named keys, and that is
covered by tests on both sides.

## 6. Test data

All seed data is synthetic and obviously so: `Robin Sample`, `Kim Example`,
`Alex Placeholder`, `Test Participant One`, `E2E Participant Two`.

Seeds must never contain real personal data, real credentials, production data
extracts or anything resembling a secret. A test asserts that no seed contains
an `@` or a secret-looking word.

Test databases are throwaway: E2E creates and deletes its own file per run,
automated tests use in-memory databases that vanish with the process, and no
database file is ever committed or cached.

## 7. Security measures in place

- `.env` files are git-ignored; only `.env.example` with placeholders is
  committed. No DSN, token or connection string exists in source.
- Internal exception detail never reaches an API client; unexpected failures
  return a generic message plus a correlation id.
- Input is validated at the request boundary and again in the domain.
- CORS is an explicit allow-list per environment; empty means same-origin only.
- Dependencies are pinned centrally, and known-vulnerable transitive packages
  are pinned forward (`Directory.Packages.props`, "Security pins").
- The build treats warnings as errors in Release.
- Destructive database operations are refused unless five independent
  conditions agree.

## 8. Required before production

Technical preparation is not compliance. Before q2 processes real users' data:

1. **Record of processing activities** (Art. 30) for each purpose.
2. **Legal basis** per purpose — consent, or legitimate interest with a
   balancing test. Goal content may be health data (Art. 9), which needs
   explicit consent.
3. **Privacy notice** (Art. 13) in plain language: what, why, how long, who
   with, which rights.
4. **Data processing agreements** (Art. 28) with every processor, including
   Sentry, and a documented decision on their data region and retention.
5. **Retention policy**: how long goals, activity history and Sentry events are
   kept, and automated deletion when that period expires.
6. **Right to erasure** (Art. 17): a working deletion path covering the
   database, backups and error-reporting data.
7. **Right to data portability** (Art. 20): export in a machine-readable
   format.
8. **Account recovery and deletion**: there is no password reset and no way to
   delete an account. The second is Art. 17 with a deadline attached to it. See
   [next-steps.md](next-steps.md) items 7 and 8.
9. **DPIA** (Art. 35) if health-related content, community features or any
   location processing are confirmed — likely for this product.
10. **Breach process** (Art. 33/34): who is notified, by whom, within 72 hours.
11. **Third-country transfers** (Art. 44 ff.) if Sentry or hosting are outside
    the EEA.
12. **Deletion and export must be tested**, not merely documented.
13. **Re-decide `sendDefaultPii` and Session Replay** (section 4). Both are on
    in the frontend for the Staging host. Recording every session of a real
    user, plus their IP address, needs a lawful basis, a retention period, a
    line in the privacy notice, and almost certainly a DPIA — and it is a
    strong argument for masking staying on permanently.

## 9. When adding a feature

Ask, in order:

1. Do we need this data at all? If not, do not collect it.
2. Can it be less precise, or shorter-lived?
3. Can it be processed on the device instead of the server?
4. Is the purpose comprehensible to the person it is about?
5. Can it be deleted and exported later?
6. Could it reach a log or a Sentry event? If so, add a filter **and a test**.
7. Does this change the record of processing activities or the privacy notice?

Anything that answers "yes" to 6 without a test is not finished.
