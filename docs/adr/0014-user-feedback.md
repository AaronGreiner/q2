# 0014 — User feedback: anonymous, from our own controls

**Status:** Accepted
**Date:** 2026-08-03

## Context

Sentry says what broke. It cannot say what somebody was trying to do, what they
expected instead, or that a screen is simply confusing — and none of those
produce an exception at all. q2 has had no way for a person to say anything back
since it was built: the "Hilfe & Support" row on the settings screen is one of
three that are visibly unavailable because nothing is behind them.

Sentry's User Feedback integration is already paid for by a dependency that is
already here. The question is not whether to use it but what shape it takes,
because the default shape contradicts three decisions this repository has
already made:

- it **injects a floating button** into every screen, positioned bottom-right,
  labelled "Report a Bug" in English — on top of a tab bar, in a German-first
  app ([0010](0010-german-first-interface.md));
- it **asks for a name and an email address**, and prefills them from the Sentry
  scope, while `scrubEvent` removes exactly those two fields from every other
  event q2 sends ([privacy.md](../privacy.md) section 4);
- it can **attach a screenshot**, of an app whose every screen is somebody's
  goals, messages and friends.

And one thing that is true regardless of configuration: a feedback event is not
an error event. `beforeSend` is never called for one, so the central scrubber
that everything else in q2 passes through does not apply. Whatever the form
collects is what leaves the browser.

## Decision

**Feedback is anonymous.** No name field, no email field, and `useSentryUser`
pointed at empty keys so the hidden inputs the SDK still submits cannot be
filled from a `Sentry.setUser` added later. The message is the whole event.

The consequence is that nobody can be written back to. That is honest rather
than unfortunate: there is no support inbox in this version, an address
collected here would be an address held by a processor for no purpose
(Art. 5(1)(c)), and the alternative — asking for one and never answering — is
worse than not asking.

**No screenshots.** `enableScreenshot: false`. A screenshot is a photograph of
personal content that no scrubber can read into and no replay mask applies to.

**No injected button.** `autoInject: false`. q2 opens the dialog from two of its
own controls, through `useFeedback`:

- a **Feedback section on the settings screen**, above signing out, with a note
  saying where the message goes and what is not sent with it;
- a **"Sag uns, was passiert ist" button on the error page**, for anything that
  is not a 404 — the one screen where somebody knows something Sentry does not.

Both are hidden when no DSN is configured, because a disabled client sets up no
integrations and the dialog would have nothing to open.

**The dialog speaks the app's language and wears the app's theme**, applied per
opening rather than at `Sentry.init`, because both are preferences that change
while the app runs — on the very screen the settings entry point sits on. The
words live in the catalogue like every other word in q2.

**The message itself is sent as written.** That is the deliberate exception, and
the reason the placeholder asks for it to be kept impersonal.

## What this costs, stated plainly

**One event type in q2 carries user-authored text to Sentry.** Everything in
[privacy.md](../privacy.md) section 4 that says otherwise is about error events,
and this is not one. A person can type their name, their address or their
diagnosis into the box, and it will be transmitted.

What limits it: the form asks a specific question rather than offering an empty
box, the placeholder says to leave personal details out, the note above the
button says where the message goes, and nothing is prefilled or collected
alongside it. The feedback event still carries the session replay id and the URL
of the page, both of which were already being sent under the rules in
[0005](0005-observability-and-sentry.md).

Retention is the open question here, as it is for replays — [privacy.md](../privacy.md)
section 8 items 4 and 5 now cover feedback as well.

## Consequences

**Good**

- Somebody can report the thing that has no stack trace.
- Feedback from the error page arrives next to the replay of the crash that
  produced it, tagged `q2.feedback_source`, which is worth more than either half
  alone.
- The dialog is bundled (`feedbackIntegration` is the synchronous build), so
  nothing is fetched from a CDN at runtime and an installed q2 on a slow
  connection still opens the form.

**Bad, and accepted**

- Feedback cannot be answered. When a support path exists, this decision is the
  one to revisit — with a lawful basis for the address, not just a field.
- The dialog is not q2's own component. It renders in a shadow DOM, which means
  two rules in `main.css` have to reach it from the outside: the message field
  would otherwise inherit `user-select: none` from `<body>` and refuse a caret
  ([0013](0013-app-like-input.md) predicted exactly this), and the keyboard
  inset that `body > [role="dialog"]` applies to every other overlay cannot
  match it. Both are marked, tested and explained where they sit.
- The SDK writes the widget's tags onto the current scope on submit, where they
  would outlive the dialog and label unrelated later errors as having come from
  the feedback form. `useFeedback` takes the tag off again when the form closes.

## When to revisit

**When there is somewhere for a reply to go.** Then, and only then, an optional
address field with a stated purpose.

**Before real users**, together with the two switches in
[privacy.md](../privacy.md) section 8 item 13: a privacy notice that mentions
replays has to mention this too.
