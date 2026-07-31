# 0009 — One known person, flagged in the database

**Status:** Superseded by
[0011 — Accounts with ASP.NET Core Identity and a session cookie](0011-authentication-with-identity.md)
**Date:** 2026-07-30
**Supersedes:** nothing. **Extended:**
[0006 — Authentication deliberately deferred](0006-authentication-deferred.md).

> `Person.IsCurrentUser` is gone. The prediction this record made held: the day
> authentication arrived, exactly one implementation changed — `CurrentPerson`
> started reading the request's principal instead of a column. Kept for that
> reasoning, not as a description of the model.

## Context

The Kudos design is social from the first screen. A goal is shared with named
people, a chat is with somebody, kudos are given *by* one person *to* another,
and a leaderboard is meaningless without a "you" on it.

Until now the model avoided identity entirely: a participant was a display name
typed into a box. That worked while goals were the only concept. It does not
survive contact with chats — a message needs a sender, an unread badge needs a
reader, and a friend request needs two sides.

Authentication itself is still deferred, and for the reasons ADR 0006 gives:
it is a security-relevant design step with real consequences (password storage,
session handling, account recovery, deletion), and doing it badly is worse than
not doing it yet.

So the question is narrower than "should q2 have accounts": **what does the API
answer as, while it has none?**

## Options considered

1. **Keep names, add none of this.** Rejected: it makes the design
   unimplementable. There is no honest way to render "2 ungelesen" without
   knowing whose unread count it is.

2. **Infer identity from the request** — a header, a query parameter, a cookie
   the client sets. Rejected: it looks like authentication and is not. Anything
   that can be set by the caller is not an identity, and a header named
   `X-User-Id` invites exactly one obvious attack the day the service is
   reachable.

3. **A `Person` entity, with exactly one row flagged as the signed-in one.**
   Accepted.

## Decision

`Person` exists and is a real entity: goals, messages, kudos and friendships all
point at it. It is **not** an account — no password, no email address, no login.

Exactly one row carries `Person.IsCurrentUser`. One type,
`CurrentPerson`, resolves it, and every feature that needs an identity goes
through that type rather than looking the flag up itself.

`CurrentPerson` **fails loudly** when the flag is missing or ambiguous. A silent
`FirstOrDefault` would hand somebody else's conversations to whoever asked
first — a bug that is only ever noticed in production.

Friendships are stored one-sided, relative to that person: a row means "this is
where *you* stand with them". Modelling the symmetric pair would double every
write for a second side nobody can sign in as.

## Consequences

**Good**

- The whole design is implementable, and every screen is backed by real data
  rather than by a fixture the client invented.
- The day authentication arrives, exactly one implementation changes:
  `CurrentPerson` starts reading the request's principal instead of a column.
  Every call site already asks it the right question.
- The placeholder is explicit and greppable. `IsCurrentUser` is impossible to
  mistake for a real account system, which is what a nullable `OwnerId` quietly
  becoming load-bearing would have looked like.

**Bad, and accepted**

- The API serves one person's data to anyone who can reach it. That is
  unchanged from ADR 0006 and is why q2 is not exposed publicly: Staging is the
  furthest it goes, and the data in it is synthetic.
- Friendship rows are one-sided, so "who are Lena's friends?" is not a question
  this schema can answer. Nothing asks it yet. When it does, the migration is a
  second row per friendship or a composite key — not a rewrite.
- The `KudosExperience` migration deletes the goals that existed before it. The
  old model stored a participant as a typed-in name with nobody to point at, and
  a percentage with no steps behind it; neither can be converted honestly. The
  migration says so in a comment rather than inventing data.

## When to revisit

When any of these becomes true:

- a second person needs to sign in;
- the service becomes reachable by anyone who is not trusted;
- somebody needs to see the app as somebody else — support, or a demo.

The first of those is the real trigger, and it is the point at which ADR 0006 is
reconsidered too.
