# 0022 — Getting away, asking for help, and leaving

**Status:** Accepted
**Date:** 2026-09-09

## Context

[0021](0021-daily-challenge.md) finished the product half of the migration.
What it also finished was the argument for putting this stage off: q2 now holds
photographs of people's rooms and faces, has a room where friends' pictures
appear, and has nothing whatsoever for somebody who wants any of it to stop.

Three gaps, and none of them is a feature request:

- **Nothing can be reported.** With user-generated pictures that is an
  obligation rather than a nicety ([privacy.md](privacy.md) section 8).
- **Nobody can be blocked.** The only way out of a friendship is to end it, and
  ending it leaves the other person able to ask again tomorrow.
- **No account can be deleted.** Art. 17, over a face
  ([next-steps.md](next-steps.md) item 8).

And one that is: a new account has no friends, so almost everything in q2 is
something it cannot do. `QDOS-UEBERNAHME.md` puts onboarding in this stage
precisely because the challenge had to exist first — there is now something to
*do* on the first day, and what was missing is the first friend.

## Decision

### A block is an entity, works both ways, and is never announced

`Block` is its own table rather than a status on `Friendship`, because blocking
somebody **ends** the friendship — it is not a state one can be in. As a status
it would have to answer questions that make no sense of it (who requested it,
when was it accepted), and every query asking "are we friends" would need a
second branch for a row meaning the opposite.

The row records a direction, and exactly one thing uses it: only the person who
set a block can lift it. Everything else is symmetric — `BlockList` turns the
row into "who must this person not see", and the person blocked stops seeing the
blocker too. A one-way block leaves the person who acted visible to the one they
were getting away from, which is the half that matters.

**It is never announced, and that is enforced by answering 404 rather than 403.**
A refusal that names its reason is a notification. Search, suggestions, friend
requests, profiles and direct chats all behave as though the other person were
not there. Two of those need no code at all: goal participants and the challenge
room are already scoped to friends, and the friendship is gone.

**Nothing is deleted.** A direct conversation is hidden and comes back if the
block is lifted; a group is left alone entirely, because it is other people's
conversation too and leaving it is a decision rather than a side effect. Erasing
somebody's messages is what deleting an account does, and it is not a decision
one person gets to make about another's copy of a shared history.

### A report has a recipient, and the alert is not the letter

`Report` is written to the database *and* delivered through `IReportSink`. The
seam is the point: privacy.md's own wording is that a report landing in a table
nobody looks at is worse than no report button, because it promises somebody
that something will happen.

Today the sink rings the one bell this deployment already answers — a Sentry
event at warning level. That is a deliberate stretch of what an error tracker is
for, and still better than a silent table. A mailbox, a queue or a moderation
console is a registration and a class.

**The alert carries the report's id, the kind, the reason and the target's id.
It does not carry the note**, which is free text somebody typed, **or the
reporter**, who is nobody's business. Whoever answers the bell reads the row.

Two rules stop the endpoint becoming something else:

- **You may only report what you can already see.** Without it, "report this id"
  would answer whether a given photograph, contribution or account exists —
  which is the question every other read here answers with 404.
- **There is no way to read a report back.** No list, no status, no outcome.
  Telling somebody what happened to their report means telling them what
  happened to another person's account.

`Report.TargetId` has **no foreign key**, and that is the exact opposite of the
decision taken for `ChallengeReaction` in [0021](0021-daily-challenge.md) — for
the opposite requirement. A reaction must die with the thing it hangs off; a
report must survive it. The whole point of reporting a photograph is that the
photograph may be removed afterwards, and a cascade would delete the record of
why.

### Deleting an account erases rather than anonymises

No tombstone row. The person goes and the cascades follow. An anonymised row
kept so other tables still resolve leaves a shape of somebody in the database
after they asked to be gone, and it is the kind of half-erasure that gets
defended rather than explained.

Three things the cascades cannot express, and one that is not in the database at
all:

1. **Direct conversations go whole.** The thread was between two people and one
   no longer exists. There is precedent for the trade in
   [0020](0020-pause-and-archive.md), where deleting a goal takes its chat.
2. **Groups stay, minus that person's messages.** A group is other people's
   conversation as well.
3. **Counters are repaired.** `ActivityEvent.KudosCount` and
   `Person.KudosReceived` are stored, so a cascade would leave every friend they
   ever cheered counting something that is gone.
4. **Images are removed explicitly.** `Images` deliberately has no foreign key
   to `Person` ([0017](0017-image-storage.md)), so nothing cascades — an erasure
   that trusted the database would leave every photograph behind. A test says
   so, and it is the test that found it.

The bytes go last and outside the transaction, because they are not
transactional. A half-finished attempt leaves unreferenced files on disk, which
is the safe direction.

**The password is asked for again.** A session cookie authorises reading
somebody's screens; it does not authorise erasing their year from a borrowed
phone.

### An invite link is a secret, and redeeming one makes a friendship

`Person.InviteCode` is 96 random bits, not the handle. A handle is public and
searchable, so a link built from one would let anybody force a friendship on
anybody by guessing it. The code is created on first use — a link nobody has
asked for is a secret with no purpose — and replacing it is what makes a leaked
one recoverable.

**Redeeming makes an accepted friendship rather than a pending request.** The
person who sent the link has already said yes by sending it, and a request
neither of them can act on until one opens the app is the dead end this feature
exists to remove. It is safe precisely because the code is unguessable: the only
way to arrive with one is to have been given it.

A code that means nothing is **ignored rather than refused**. Registration is
the worst possible moment to fail over a stale link somebody was forwarded — the
account is what they came for, and the friendship is the bonus.

The server returns the code and never a URL: it does not know which host the app
is served from, and a link with the wrong origin in it is worse than no link.
The browser builds it from the page it is on.

## Consequences

- **`BlockList` sits beside `CurrentPerson`** as the second question every
  read asks. Anything added later that can put a person on a screen has to ask
  it, and the compiler will not say so — this is the one cross-cutting rule in
  q2 that is carried by convention rather than by a type.
- **A group chat is the documented gap.** Blocking somebody you share a group
  with hides neither them nor their messages there; leaving is the answer, and
  it is a real limitation rather than an oversight.
- **A blocked person's goals are the other one.** Blocking ends the friendship
  but does not remove either of them from a goal they were already sharing —
  the windows and photographs behind it belong to a promise they both made.
- **Deleting an account is the only irreversible action in q2**, and the only
  one that asks for a credential a second time.
- **Registration gained a fourth field it never shows.** `InviteCode` is never
  typed; it comes off the link, through one `useState` that is deliberately not
  a cookie — a code outliving the visit would sit in the browser of somebody who
  decided not to sign up.
- **Reports are write-only and nobody in the app can see them.** Reading them
  means opening the database, which is the honest state of a product with no
  moderation staff — and the reason the sink exists rather than a table alone.
