# 0010 — A German-first interface, with a hand-written catalogue

**Status:** Accepted
**Date:** 2026-07-30

## Context

The Kudos design q2 is built from is written in German, down to the
encouragements ("Stark dran!", "Anfeuern 🔥"). It also contains a language
switch offering Deutsch and English.

That conflicts with a rule this repository already had: [AGENTS.md](../../AGENTS.md)
section 5 says code, comments, documentation *and UI text* are English. The rule
was written when the UI had a dozen strings in it and no design behind it.

Three things had to be decided together: which language the product speaks,
whether the switch in the design is real, and what implements it.

## Decision

**The product speaks German by default and English on request.** The rule in
AGENTS.md section 5 now applies to code, comments, documentation and commit
messages; user-facing text is explicitly excluded and lives in the catalogue.

**The switch is real.** It was tempting to ship German only and leave the
control as decoration, but a settings screen with a switch that does nothing is
a worse lie than an untranslated screen.

**The catalogue is hand-written**, in `app/app/i18n/messages.ts`, rather than
`@nuxtjs/i18n`. Two languages, a few hundred strings, and no plural rule beyond
"one / many": the module would bring routing, lazy loading and a message
compiler for none of that, and adding a dependency that is not earned is what
[AGENTS.md](../../AGENTS.md) section 6 exists to prevent.

Two rules keep this from rotting:

- **No component or page contains literal user-facing text.** A German string in
  a template cannot be translated and, worse, nobody will find it.
- **`en` is typed as `Messages`**, the type inferred from `de`. A missing key is
  a build error rather than a screen that silently falls back.

## What this forced on the API

The consequence reaches further than the frontend, and it is the part worth
recording.

**Sentences are not stored.** The activity feed sends `kind`, `subject` and
`amount`, and the client composes "hat „Joggen 5 km" abgeschlossen" or
"completed "Joggen 5 km"" from them. A stored sentence could only ever have been
one language, and re-translating it later would mean parsing it back apart.

The same applies to the smaller pieces: a chat preview sends
`lastMessageIsMine` rather than the word "Du", and a rhythm travels as
`Daily` rather than as "Täglich".

**Numbers are not formatted either.** The catalogue carries the decimal
separator, so `1,2 / 2 L` and `1.2 / 2 L` come out of the same data.

## Consequences

**Good**

- The design is implemented as designed, in the language it was written in.
- The API stays free of presentation, which was the right shape anyway — it is
  the reason the same endpoint serves both languages with no branching.
- A third language is one file and no server change.

**Bad, and accepted**

- Anyone reading the running application sees German while every file around it
  is English. The catalogue is the seam, and it is one file.
- Two translations have to be kept in step by hand. The type system catches a
  missing key; a unit test catches an empty string, a function whose signature
  drifted, and a "translation" that was pasted rather than translated.
- German is longer than English almost everywhere, so the phone layout is
  designed against the longer of the two. That is the right way round.

## When to revisit

When a third language is wanted, or when a string needs a real plural rule —
Polish and Russian have several, and the catalogue's `one / many` is not enough
for them. At that point `@nuxtjs/i18n` earns its place, and this file is what
explains why it was not there before.
