# 0047 — One name on screen: Qdos

**Status:** Accepted
**Date:** 2026-10-19
**Issues:** [#109](https://github.com/AaronGreiner/q2/issues/109)
**Amends:** [0015 — The Qdos design language](0015-qdos-design-language.md),
which named the product without saying what a person is shown.

## Context

The interface used three names at once. The drawn mark on the sign-in and
invite screens reads "Q2", the page titles and the app's own name said
"Qdos", and running text said "q2": the invite link's share text ("Komm zu
q2"), the invite page and its preview ("Du bist zu q2 eingeladen"), the empty
Freunde tab and the camera's permission message. The share text and the invite
page are the first words a new person ever reads, and they named something
different from what the store and the task switcher call the app.

## Decision

- **A person is shown "Qdos", everywhere.** Every sentence that names the
  product takes it from one constant in `app/app/i18n/messages.ts`
  (`productName`, exposed as `t.app.name`), in both languages, because a name
  does not translate.
- **q2 is the internal short name** — the repository, the code, ids, metric
  names, the bundle id — and never appears in a sentence a person reads. The
  document title is "Qdos" alone, and the web app manifest's `name` lost its
  "(q2)".
- **The drawn mark stays.** `AppMark` is the logo, the way an app icon is; it
  sits beside the written name rather than replacing it, and is
  `aria-hidden` there.
- The settings footer reads "Qdos · <version>". The English one said
  "Q2 · Kudos", a third spelling.

## Consequences

- A new sentence that names the product interpolates `productName`; a literal
  "q2" or "Qdos" in the catalogue is a slip.
- Mails (`PasswordResetMail`, `PasswordChangedMail`) and the iOS permission
  texts in `Info.plist` already said Qdos and are unchanged.
