# 0058 — Your data as one file

**Status:** Accepted
**Date:** 2026-10-20
**Issues:** [#36](https://github.com/AaronGreiner/q2/issues/36),
[#141](https://github.com/AaronGreiner/q2/issues/141) (found by it)
**Amends:** [0022 — Blocking, reporting and erasure](0022-blocking-reporting-and-erasure.md),
whose erasure now reaches votes and reactions as well.

## Context

Deleting an account existed and was tested; getting a copy of what q2 holds
about you (Art. 20 GDPR) did not exist at all. Which tables hold something of a
person's was known only implicitly — in `AccountService`'s erasure and the
database's cascades — so a new table could be covered by one and missed by the
other.

## Decision

- **One file.** `GET /api/auth/account/export` answers with a ZIP: `data.json`
  and the pictures that are the person's own under `images/`. Settings has
  "Meine Daten herunterladen"; the browser downloads it, the iOS app hands it
  to the share sheet, because a WebView has no download manager.
- **The person's share, decided in the session.** What they wrote, made, chose
  and were sent: profile, account address, settings, check-ins and badges,
  friendships and blocks, their goals with every window, use and spot check,
  their photographs with how many believed and doubted and why — counted, never
  who (0055) —, pauses and edits, the goals they check, their objections,
  drafts, votes and reactions, challenge contributions, feed lines and kudos
  given, conversations and their own messages, the bell, devices and reports.
  Not anybody else's messages, photographs or votes. Other people appear by id,
  and once each under `people` with the name and handle the app shows anyway.
- **Field by field.** Every section names its fields; nothing is serialised
  whole, so a password hash, a security stamp or a device's push keys cannot
  slip in because a column was added. A device appears as its push service's
  host, not its address, which is a capability.
- **One inventory** (`PersonalData`): every column in the model that names a
  person, with the section it lands in or the written reason it does not —
  who blocked you, and the lines in other people's bells that name you, are
  theirs. `DataExportTests` holds it against the EF model (every foreign key to
  `Person` and every property ending in `PersonId`), and checks the export has
  each section; `AccountDeletionTests` holds the same list against an erasure:
  afterwards no column in it names the person.
- **That test found a gap** (#141): an erasure left the person's votes on
  friends' photographs and their reactions to photographs and challenge
  contributions, which have no foreign key to a person and so did not cascade.
  The erasure now removes them; a vote on a photograph still being decided goes
  with its voter, and a verdict already reached stays.
- **Streamed from a temporary file** that deletes itself, because a ZIP is
  written synchronously and the image allowance does not belong in memory;
  `Cache-Control: no-store`.

## Consequences

- A table added tomorrow that names a person fails the build until it is placed
  in `PersonalData`, and then the export test asks for its section.
- The export reads every window of the person's goals with their photographs:
  it is the one read that is meant to be whole.
- Backups and Sentry are still outside both erasure and export (#35).
