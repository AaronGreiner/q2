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
| Data minimisation (Art. 5(1)(c)) | An account is an address and a password hash, and nothing else. No product analytics, no tracking, no location. |
| Purpose limitation (Art. 5(1)(b)) | Stored content renders the product. It is not sent to telemetry; only anonymous operational counters are aggregated. |
| Transparency (Art. 5(1)(a)) | The environment is visible in the UI; the diagnostics page states plainly what reporting is active. |
| Storage limitation (Art. 5(1)(e)) | Local and test databases are throwaway by design. The bell's lines are deleted after thirty days; retention for everything else is open — section 8, item 5. |
| Integrity and confidentiality (Art. 5(1)(f)) | Secrets never in source; internal detail never in responses; central scrubbing before anything leaves the process. |
| Data protection by default (Art. 25) | No request-body logging, no location, no product analytics. **Two switches are deliberately not on the private setting in the frontend** — `sendDefaultPii` and Session Replay — see section 4. |

## 2. What is processed

**Stored in the database:**

| Category | Field | Note |
| --- | --- | --- |
| Goal content | title, description | User-authored. Potentially health-related. |
| Task content | title, measured amount and unit | User-authored. Same treatment. |
| Progress | steps done, days worked on, task completion dates | A day-by-day record of somebody's habits. |
| Time | creation timestamp, optional target date, reminder time | |
| People | display name, handle, initials, avatar colour | Separate from the account, and the only thing other people ever see of somebody. |
| Accounts | email address, password **hash**, security stamp, lockout state, the person it signs in as | ASP.NET Core Identity. No plain password is stored anywhere, ever. Nothing beyond what Identity requires — no phone number is collected, and the columns Identity creates for one stay empty. See [adr/0011-authentication-with-identity.md](adr/0011-authentication-with-identity.md). A reset link's token is not stored at all — Identity derives it from the security stamp, which changes with the password — and which account was last mailed one is kept in memory for two minutes, never on disk ([adr/0026-mail-and-password-reset.md](adr/0026-mail-and-password-reset.md)). |
| Presence | last-seen timestamp | Written by the seeds, and updated when somebody signs in or registers. |
| Social graph | friendships, who asked whom, and when | One row per pair, holding both ends. Mutual-friend counts are derived on read, never stored. |
| Activity | what somebody did, when, and who cheered it | The subject is a goal or task title, so it inherits their treatment. |
| Messages | text, sender, timestamp, reactions | Until images arrived, the most personal thing q2 stored. |
| Images | the bytes of a picture somebody uploaded, plus its owner, purpose, media type, size and dimensions | **The most personal thing q2 stores.** A photograph is a face, a room, a home. See [adr/0017-image-storage.md](adr/0017-image-storage.md). |
| Challenge contributions | which prompt somebody answered, with which picture, and when | The prompt is authored by the deployment, never by a user. Who may see the picture depends on what the *viewer* has done — see [adr/0021-daily-challenge.md](adr/0021-daily-challenge.md). |
| Blocks | who blocked whom, and when | The direction is stored so only the person who set one can lift it; the effect is symmetric. Never shown to the person blocked. |
| Reports | who reported what, why, and an optional note | Write-only: nothing in the app reads them back, and the person reported is never told who reported them. |
| Invite codes | 96 random bits per person, created on first use | A credential in everything but name. Replaceable, and never derived from the handle. Whoever holds one sees its owner's display name, initials and avatar colour — nothing else — before signing up (ADR 0033). |
| Push subscriptions | an endpoint, two browser keys, and when something last arrived | **A stable handle for one browser installation** — the most identifying thing q2 stores. Never logged, never sent to Sentry, and deleted with the account. See [adr/0023-web-push.md](adr/0023-web-push.md). |
| Notifications | the bell's lines: a kind, who caused it, what it is about (a goal title or a challenge prompt), a number, and when | Never a message's text — a message lives in its chat. Deleted after thirty days, and at once with either person's account or with the goal a line is about. Who blocked whom is applied when a line is written and again when it is read. See [adr/0024-one-notification-pipeline.md](adr/0024-one-notification-pipeline.md). |
| Preferences | theme, language, one switch per kind of notification, quiet hours, and which conversations are muted | |
| Device appearance | one of four accent palette names | Stored in the `q2-accent` cookie for one year, shared by accounts using that browser. No identity or content. Read during SSR; never logged or sent to Sentry. See [ADR 0028](adr/0028-ruhe-design-system.md). |
| Camera choice | `user` or `environment` — which camera the photo screen opens with | Stored in the browser's local storage under `q2-camera-facing`, shared by accounts using that browser, and only written when somebody switches cameras. Never sent to the server, never logged, never sent to Sentry. |
| Sign-in on the iOS app | a refresh token in the iOS Keychain, an access token and the pictures already fetched in memory | The browser's session cookie, in another form: the tokens are sealed by the server and name only the account and its security stamp. The Keychain entry is "this device only" and never synchronised through iCloud; signing out removes it and empties the pictures. It outlives deleting the app, as Keychain items do, and stops working after 14 days unused or when the password changes. Never logged, never sent to Sentry. See [adr/0034](adr/0034-bearer-tokens-for-the-native-app.md). |

**What a notification carries.** The payload is encrypted to the browser
(RFC 8291), so a push service — Google's, Mozilla's, Apple's — carries bytes it
cannot read. It holds the same parts as a line in the bell — a kind, a name, a
goal title or group name, a number — and the sentence is composed on the device.
For a message it also holds the first 140 characters: the sender's words on the
way to the person they were written for, never stored a second time on the
server. Whether a lock screen shows them is the operating system's setting,
where a person already decides that for every other app. What the push service
does learn is that *this endpoint* was sent something at *this moment*, which is
metadata q2 cannot hide and does not pretend to.

**What the live connection carries:** badge counts, the name of the part of the
screen that changed with an id, and — for somebody who is looking and whom a
notification may interrupt — that notification, instead of a push. It holds
exactly the parts a push holds (a kind, a name, a goal title or group name, a
number, and for a message its first 140 characters), goes only to that
person's own connections over TLS, and is shown as a banner and kept nowhere.
Everything else on screen is read again through the same endpoints as always.

**Not processed at all:**

plain passwords, phone numbers, postal addresses, dates of birth, payment data,
location or coordinates, device identifiers, IP addresses beyond the transport
layer and what Sentry attaches (section 4), biometric data, advertising or
analytics identifiers, third-party profile data.

**Photographs used to be on that list and no longer are.** Since stage 3 of the
Qdos migration, somebody can upload a profile picture, from stage 4 a photograph
is how a goal is delivered, and from stage 7 one can be contributed to the daily
challenge. Three properties make that survivable and none of them is optional:

- **No picture has a public URL.** It is served by `GET /api/images/{id}`, which
  requires a session and asks who is looking; an image that is not the caller's
  to see answers 404, because 403 would confirm it exists. An avatar is readable
  by anybody signed in — the same reach the initials it replaces always had —
  and everything else is its owner's alone until the stage that gives it an
  audience says otherwise.
- **No picture enters a cache.** The response carries
  `Cache-Control: private, no-store`. The service worker was never a risk (it
  precaches build output only), but the browser's own cache is: a phone is
  shared, and a photograph outliving the session that could see it is the same
  disclosure as one served without a session.
- **No picture reaches Sentry.** Every element that can render one carries
  `data-q2-block`, so Session Replay records a placeholder rather than a face —
  and that was true before the first upload was possible, not after.

The upload is also where **location data re-enters the picture** (section 5): a
photograph taken outdoors carries the coordinates it was taken at in its Exif
block. The browser re-encodes every upload through a canvas
(`app/app/utils/images.ts`), which strips Exif entirely, so what reaches the
server has no coordinates in it. That is the reason for the re-encode, not a
side effect of it.

**The email address is the newest category here and the one to watch.** It is
the credential and nothing else: it is never shown to another person, never
searchable — `GET /api/friends/search` matches display names and handles only,
which is deliberate, because matching addresses would turn it into a way to
check whether a given address has an account here — and never sent to Sentry.
It is also where a reset link goes: asking for one hands the address and the
link to the mail provider, a processor with the same need for an agreement as
Sentry ([#33](https://github.com/AaronGreiner/q2/issues/33)). The mail names
nobody and carries nothing but the link
([adr/0026-mail-and-password-reset.md](adr/0026-mail-and-password-reset.md)).

Every name in the database is invented — but a goal description, a task title
and a message all can identify somebody, so all three are treated as personal
data throughout.

Chat messages, the social graph, the account itself and uploaded images are the
categories that need a retention answer before real people are in the database
(section 8, item 5). Erasure exists at two levels. Deleting a stopped goal from
the archive removes its windows, every photograph behind them, the conversation
about it and the feed entries naming it, for everybody on it
([adr/0020](adr/0020-pause-and-archive.md)). Deleting an account removes the
person and everything of theirs — their goals with the conversation of each
([adr/0027](adr/0027-goal-conversations.md)) — the image files included, which
sit outside the database and outside any transaction
([adr/0022](adr/0022-blocking-reporting-and-erasure.md)).

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

**Never sent** (with one exception, below):

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
run id and seed profile. Structured operational logs and anonymous counters are
also sent; they follow the rules in sections 3 and 4 and contain no user content.

### IP addresses and Session Replay

These were previously off and are now on, so that a failure on the Staging host
can be reconstructed rather than guessed at.

| | Frontend | Backend |
| --- | --- | --- |
| `sendDefaultPii` | `true` — the SDK attaches the IP address | `true` — same |
| User id, email, username | cleared by `scrubEvent`; only the IP survives | cleared by `ScrubUser`; the SDK would otherwise fill the id with an installation identifier that identifies *our host* |
| Session Replay | every session (`replaysSessionSampleRate: 1`) | not applicable — replay is browser-only |
| Request bodies | never attached | never attached (`MaxRequestBodySize.None`, unaffected by `SendDefaultPii`) |

An IP address is personal data under the GDPR. Recording it, and recording the
screen, are the two most intrusive things q2 does, and they are switched on
deliberately rather than by oversight.

What still limits the damage:

- **Replay exposes application UI, not personal content.** Static headings,
  labels, navigation, empty states and error messages are readable. Elements
  with `data-q2-private` mask names, handles, goal/task/chat text and personal
  values. Elements with `data-q2-block` replace messages, progress, activity
  histories and avatars, because their shape or visual state reveals data even
  after text masking. All inputs remain masked. `blockAllMedia` is off because
  q2 has no user photographs or uploads; application icons stay visible.
- **`beforeSend` and `beforeBreadcrumb` still run on every event.** Everything
  in the "never sent" list above is still removed.
- **`ui.input` breadcrumbs are still dropped**, so keystrokes are not captured.
- **Cookies and all but a handful of headers are still stripped**, on both
  sides, and request bodies never leave the process.

Logs and metrics use the same central filters. Metrics are an allow-list of
business-action counters with closed attributes (`rhythm`, `shared`, `done`),
never a user id or a value somebody typed. Browser profiles contain sampled
JavaScript stacks from the shipped application code and are active only during
sampled traces; they do not contain DOM text or input values.

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

Technical preparation is not compliance. Before q2 processes real users' data,
every item below has to hold. Each one that does not yet is a GitHub issue
labelled `launch-blocker`: this list says what is required, the issue says how
far it has got.

1. **Record of processing activities** (Art. 30) for each purpose
   ([#28](https://github.com/AaronGreiner/q2/issues/28)).
2. **Legal basis** per purpose — consent, or legitimate interest with a
   balancing test. Goal content may be health data (Art. 9), which needs
   explicit consent ([#28](https://github.com/AaronGreiner/q2/issues/28)).
3. **Privacy notice** (Art. 13) in plain language: what, why, how long, who
   with, which rights ([#34](https://github.com/AaronGreiner/q2/issues/34)).
4. **Data processing agreements** (Art. 28) with every processor, including
   Sentry and the mail provider, and a documented decision on their data region
   and retention
   ([#33](https://github.com/AaronGreiner/q2/issues/33)).
5. **Retention policy**: how long goals, activity history and Sentry events are
   kept, and automated deletion when that period expires. The bell's lines are
   the only category with a period so far — thirty days
   ([#30](https://github.com/AaronGreiner/q2/issues/30)).
6. **Right to erasure** (Art. 17): a working deletion path covering the
   database, backups and error-reporting data. The database and the image files
   are covered (item 8); backups and Sentry are not
   ([#35](https://github.com/AaronGreiner/q2/issues/35)).
7. **Right to data portability** (Art. 20): export in a machine-readable
   format ([#36](https://github.com/AaronGreiner/q2/issues/36)).
8. **Account recovery and deletion.** Deletion exists: `DELETE /api/auth/account`
   erases the person and everything of theirs, asks for the password again, and
   reaches the image files as well as the rows. Recovery exists too: a link
   mailed to the account's address sets a new password, works once and for an
   hour, and ends every session of the account
   ([adr/0026-mail-and-password-reset.md](adr/0026-mail-and-password-reset.md)).
   It needs a mail account, which Production cannot start without and Staging
   does not have yet ([#3](https://github.com/AaronGreiner/q2/issues/3)).
9. **DPIA** (Art. 35) if health-related content, community features or any
   location processing are confirmed — likely for this product
   ([#29](https://github.com/AaronGreiner/q2/issues/29)).
10. **Breach process** (Art. 33/34): who is notified, by whom, within 72 hours
    ([#37](https://github.com/AaronGreiner/q2/issues/37)).
11. **Third-country transfers** (Art. 44 ff.) if Sentry or hosting are outside
    the EEA ([#33](https://github.com/AaronGreiner/q2/issues/33)).
12. **Erasure and export are tested, not only documented.**
    `AccountDeletionTests` demonstrates the erasure over every table that
    pointed at somebody, and it is the test that found the images not
    cascading. The export needs the same
    ([#36](https://github.com/AaronGreiner/q2/issues/36)).
13. **Re-decide `sendDefaultPii` and Session Replay** (section 4). Both are
    switched on unconditionally — `sendDefaultPii` in both halves, Session
    Replay for every frontend session — so they apply wherever a DSN is
    configured, not only on the Staging host. Recording every session of a real
    user, plus their IP address, needs a lawful basis, a retention period, a
    line in the privacy notice, and almost certainly a DPIA — and it is a
    strong argument for masking staying on permanently
    ([#31](https://github.com/AaronGreiner/q2/issues/31)).
14. **Reporting needs somebody to answer it.** With user-generated pictures,
    "report this" is an obligation rather than a nicety, and a report landing
    where nobody reads it is worse than none. `IReportSink` delivers every
    report to the one channel this deployment already watches, carrying ids and
    a reason but never the note or the reporter
    ([adr/0022-blocking-reporting-and-erasure.md](adr/0022-blocking-reporting-and-erasure.md));
    it is nobody's job yet to answer one, and nothing in the app reads them
    back ([#38](https://github.com/AaronGreiner/q2/issues/38)). The seam exists
    so that a mailbox or a console is a registration rather than a rewrite. No
    upload is readable by a stranger meanwhile: an avatar is visible
    to anybody signed in, a proof photograph only to the people its goal is
    shared with, and a challenge contribution only to the contributor's friends
    who have contributed to the same challenge themselves.

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
