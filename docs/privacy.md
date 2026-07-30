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
| Data minimisation (Art. 5(1)(c)) | The model has seven fields. No accounts, no profiles, no analytics, no tracking, no location. |
| Purpose limitation (Art. 5(1)(b)) | Stored data is used to render goals. It is not sent to Sentry, not logged and not aggregated. |
| Transparency (Art. 5(1)(a)) | The environment is visible in the UI; the diagnostics page states plainly what reporting is active. |
| Storage limitation (Art. 5(1)(e)) | Local and test databases are throwaway by design. Production retention is an open decision — section 8. |
| Integrity and confidentiality (Art. 5(1)(f)) | Secrets never in source; internal detail never in responses; central scrubbing before anything leaves the process. |
| Data protection by default (Art. 25) | Every switch defaults to the private option: no PII to Sentry, no session replay, no request-body logging, no location. |

## 2. What is processed

**Stored in the database:**

| Category | Field | Note |
| --- | --- | --- |
| Goal content | title, description | User-authored. Potentially health-related. |
| Progress | status, progress percent | |
| Time | creation timestamp, optional target date | |
| Participants | display name | Free text. No account, no email, no identifier. |

**Not processed at all:**

names, email addresses, passwords, phone numbers, postal addresses, dates of
birth, payment data, location or coordinates, device identifiers, IP addresses
beyond the transport layer, biometric data, advertising or analytics
identifiers, third-party profile data.

There is no user account system yet, so nothing in the database identifies a
natural person on its own — but a goal description can, so it is treated as
personal data throughout.

## 3. Logging rules

**May be logged:** goal ids, participant *counts*, status values, progress
numbers, durations, HTTP status codes, route templates, environment, release,
migration and seed names, correlation ids.

**Must never be logged:** goal titles and descriptions, participant names,
credentials or tokens, connection strings, cookies, request or response
bodies, `Authorization` headers, email addresses, IP addresses, coordinates,
developer or machine names.

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
- email addresses, user names, IP addresses
- exact location data, in events, breadcrumbs, tags, contexts or traces
- machine names, developer names, local file paths
- keystrokes (`ui.input` breadcrumbs are dropped entirely)

**Sent:** exception type and stack, route template, HTTP method and status,
environment, release, service name, a correlation id, and — in test runs — the
run id and seed profile.

`SendDefaultPii` is `false` on both sides, explicitly rather than by default.
Session Replay is `0`, explicitly.

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
8. **Access control**: authentication and authorisation — until then the API
   must not be publicly exposed with real data. See
   [adr/0006-authentication-deferred.md](adr/0006-authentication-deferred.md).
9. **DPIA** (Art. 35) if health-related content, community features or any
   location processing are confirmed — likely for this product.
10. **Breach process** (Art. 33/34): who is notified, by whom, within 72 hours.
11. **Third-country transfers** (Art. 44 ff.) if Sentry or hosting are outside
    the EEA.
12. **Deletion and export must be tested**, not merely documented.

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
