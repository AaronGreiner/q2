# 0042 — Field errors travel as codes, and the app words them

**Status:** Accepted
**Date:** 2026-10-18
**Issues:** [#80](https://github.com/AaronGreiner/q2/issues/80)
**Extends:** [0010 — German first](0010-german-first-interface.md), whose rule
that every word a person reads lives in the catalogue had one way round it.

## Context

- **The forms showed the API's English.** A validation problem carries a
  message per field, written in English by the validators
  (`AccountRequestValidator`, `CreateGoalRequestValidator` and the rest). The
  sign-in, registration, password reset and goal creation screens showed the
  first message for a field as it came. Somebody who reads q2 in German — the
  default — submitted an empty registration and read "A name is required."
  under "Name".
- **The profile editor did not have this problem**, because it checked the
  name itself and worded it from the catalogue. So the same mistake read German
  in one place and English in another — and the fix it used, checking in the
  browser, is the one the repository rules out: the frontend never
  re-implements a rule the server enforces (AGENTS.md section 3).
- **The rest of the API already sends structure.** The feed sends a kind and
  a subject, a refused sign-in a `reason`; `activitySentence` and
  `signInMessage` choose the words (app/AGENTS.md section 11). A field error
  was the one sentence the server still wrote for a person.

## Decision

**A field error carries a code beside its message.** `FieldErrors`
(`api/src/Q2.Api/Infrastructure/Errors/FieldErrors.cs`) records, per field, an
English message and a `FieldErrorCode` — a code from a short closed list
(`required`, `tooShort`, `tooLong`, `outOfRange`, `tooMany`, `email`, `taken`,
`unknownOption`, `resetLink`, `noChecker`, `noWeekday`, `pastDay`,
`tooFarAhead`, `mustRepeat`, `invalid`) and the bounds the sentence needs
(`min`, `max`). It travels as the validation problem's `codes` extension, under
the same field names as `errors`:

```json
{
  "errors": { "Password": ["A password must be at least 10 characters long."] },
  "codes":  { "Password": [{ "code": "tooShort", "min": 10 }] }
}
```

- The **messages stay**, unchanged, for whoever reads a raw response — a
  developer, a log line, a breadcrumb. Read as a dictionary, `FieldErrors` is
  exactly what a validator returned before, so code that only asks whether a
  field is wrong did not change.
- An endpoint answers with `errors.ToProblem()`; a service throws
  `DomainValidationException(errors)`, or `(field, message, code)` for one
  field, and the global handler writes the same `codes` beside the messages.
- The **numbers come from the domain constants** the rule is enforced with
  (`AccountPolicy.MinimumPasswordLength`, `Goal.MaxTitleLength`, …), so the
  number on the screen cannot disagree with the number that is enforced.

**The app words the code, never the message.** `normalizeApiError` reads
`codes` into `ApiFailure.fieldCodes`, and every form shows
`fieldErrorText(failure, field, t)` (`app/utils/fieldErrors.ts`), which words
the code from the `fieldErrors` block of the catalogue in the reader's
language. A field the server names without a code — or with a code this build
does not know yet — gets the generic "Das passt so nicht. Bitte prüf die
Angabe." rather than the English message. Wrong language is never the
fallback.

Coded so far: everything the sign-in, registration, password reset, goal
creation and waiting-Do forms can be refused for, Identity's "address taken",
the profile and pause requests. Every other `DomainValidationException` still
carries a message only, which is worded generically if a form ever shows it.

## Consequences

- **A new rule a form can trip needs a code.** It either fits one of the
  existing ones or adds one on both sides — `FieldErrorCodes` on the server, the
  `fieldErrors` block in both languages of the catalogue and `wordFieldError` —
  and the type checker makes the catalogue's two halves agree.
- **The client does not check the obvious rules first.** An empty
  registration is a round trip, as before; what changed is only which words
  come back. That keeps one source for every rule.
- **`codes` is not in the OpenAPI document**, like `reason`, `traceId` and
  `errorId` before it: ASP.NET Core describes a validation problem without its
  extensions. `FieldErrorCode` in `app/app/api/errors.ts` is the hand-written
  mirror, and `AccountEndpointTests` and `GoalsEndpointTests` assert the shape
  on the wire.
- **The generic sentence is less specific than the English it replaces** for
  the few rules without a code. That is the trade: a vaguer sentence in the
  right language over a precise one in the wrong one.
