# 0015 — The Qdos design language: black, one accent, no emoji

**Status:** Accepted
**Date:** 2026-09-06

## Context

q2 was built from the Kudos design: a self-care product in green, on a light
background, with rounded cards, friendly copy and emoji in the interface. It is
a well-made version of that design — two complete themes with documented
contrast ratios, a four-value radius scale with a written rationale, systematic
accessibility checked in component tests.

The product it now has to carry is a different one. Qdos is built on social
accountability: you commit to something, your friends witness it, you photograph
the proof, they confirm or doubt it, and a missed window breaks the streak and
shows up in a balance the people involved can see. The plan for that change is
in `QDOS-UEBERNAHME.md`.

Two things follow.

**The tone is wrong.** A product that tells you your friends will find out when
you miss cannot look like a meditation app. Green, rounded and `👋` is the
register of encouragement, and this one is about consequence.

**The material changes.** Photographs become the thing the app is made of.
A photo on a white card is a postage stamp; on black it is a picture. That is
why BeReal, Letterboxd and Instagram's full-screen views are all dark, and it is
a functional reason rather than a fashion.

The alternative considered was to keep the light, green design and change only
the copy. It was rejected because the first proof photo would have made the
mismatch obvious, and because the accent question below has to be answered
before there are forty screens to answer it in.

## Decision

**Take the design language of Qdos and keep the craft of q2.** The palette,
the typography and the restraint come from the new product; the token layer,
the documented contrast ratios, the radius scale, the Replay privacy attributes,
the component tests and the accessibility rules stay exactly as they were.

### Black, and dark by default

`--ui-bg` in the dark theme is `#000000` — black rather than charcoal, so a
photograph has nothing competing with it. The greys above it are a ladder
(`#0a0a0a`, `#0e0e0e`, `#141414`, `#1f1f1f`) because on black an eight-point
step is clearly visible and a card, a sheet and a pressed row have to be told
apart without a border on each. A card is separated by one lit pixel along its
top edge instead of a drop shadow, which black cannot cast.

Dark is the default rather than an option: `UserSettings.Theme` starts at
`Dark`, and Nuxt's colour mode has `preference` and `fallback` set to dark so
the first frame of a first launch is already the app. The manifest and both
`theme-color` metas follow.

**Light stays fully supported.** It is not equal, it is the alternative: it is
what somebody reads in sunlight and what somebody with astigmatism reads at all.
Every token is defined in `:root` and overridden in `.dark`; a screen that only
works dark has to say so in `main.css` rather than happen to be that way.

### One accent, and a test for it

The accent is lime `#cbee4a` — deliberately held back from the `#d7ff3e` the
design started as, which reads as a toy on pure black. In the light theme the
same idea is `#4a6600`, dark enough to carry white text: a bright lime with
white on it is 1.3:1, which is how a primary button stays invisible for months.

Three colours carry meaning and nothing else carries any:

| token | means |
| --- | --- |
| `--ui-primary` / `--q2-accent-*` | an action the person can take **right now** |
| `--q2-flame-*` | a streak, and only a streak |
| `--q2-error-action`, `--q2-danger` | something final: delete, block, leave |

Everything else is black, white, a grey between them, or a photograph.

**The test before reaching for the accent is one question: *is this something
they can do now?*** A state is not. Progress bars and rings, rhythm chips,
switches, badges, percentages, counts and the tick on a finished task are all
drawn in `--ui-text`, not in the accent. The design this comes from had to walk
that rule back through 67 places in 29 files, and losing it again is the most
likely way to ruin this palette.

Two corollaries, both learned the expensive way in the source project:

- **text on an accent fill comes from `--q2-accent-contrast`**, never from
  `text-white`;
- **a disabled primary button is an outline**, not a faded accent fill. Washed-out
  accent reads as a mistake; an outline reads as "not yet".

### No emoji in the interface

Material and Lucide icons instead. An emoji is a picture with a platform and a
language behind it: it is a different drawing on Android, it has no accessible
name we control, and it cannot be styled. This reaches the database — a message
reaction is a `KudosKind` (`Fire`, `Strong`, `Applause`) and a group's avatar is
a name from `ConversationIcons`, both migrated from the emoji they used to be.

People may still type emoji into their own messages. The rule is about the
interface, not about what anybody writes in it.

### Editorial typography

Public Sans stays — a self-hosted typeface is the cheapest difference between an
app and a website, and it goes tight and heavy. A screen title is 27px/800 at
−0.026em (`q2-title`), set the way a masthead is; a section label is 11px/700
uppercase and dimmed (`q2-eyebrow`), because it says what the next block *is*,
which is a state and gets no accent.

### The streak is the one loud thing

The flame gradient (`#ffcc33` → `#ff4d2d`) appears on the start screen's streak
card and nowhere else, set in ink rather than white: `#0f0f0f` is 12.7:1 on the
pale end and 5.8:1 on the hot end, where white would have been 1.5. **A streak
of zero is not lit** — it is the same black card as everything else. Lighting it
anyway would spend the app's one loud surface on the absence of the thing it
celebrates.

### The tab bar creates

Home · Suche · **+** · Chats · Profil. Making a commitment is what people come
here to do, so it belongs under the thumb rather than behind a header button on
one screen. The friends screen became the lower half of Suche — you find a
person by looking for them — and the pending-requests badge moved with it.

The create button is a **link** to `/goals?create=1`, not a button over
component state, so the sheet survives a reload and closes with the back
gesture.

### The ranking is gone

`GET /api/leaderboard` and its card are deleted. q2 is about to start telling
your friends when you miss a window; a table that sorts everybody by how well
they are doing turns that into a scoreboard somebody comes last on. The
viewer-scoped balance planned for stage 5 is the counterpart and rests on the
same principle: a failure concerns the people it was promised to, and nobody
else. `SocialEndpointTests.ThereIsNoRanking` is what would notice one coming
back by accident.

## Consequences

- The Development database's theme default changed, so an existing account keeps
  whatever it had; only new accounts start dark.
- `MessageReactions.Emoji` and `Conversations.Emoji` became `Kind` and `Icon`.
  The migration renames rather than drops, and translates the values, so no
  reaction anybody ever gave is lost.
- The icon set was redrawn: a lime Q on black, from
  `app/scripts/generate-icons.ts`.
- Kudos and reactions are now the same thing with three registers, still counted
  as kudos on a profile. The name of the product is therefore still the name of
  a gesture it has.
- Goals with steps and the tasks under them survive this stage unchanged; they
  are replaced in stage 2 of `QDOS-UEBERNAHME.md`, not here.
- The light theme is now the secondary one and will get less attention. That is
  a stated risk, not an accident: if it decays, this ADR is what says it was not
  supposed to.
