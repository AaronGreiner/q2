# 0025 — Banners in the open app

**Status:** Accepted
**Date:** 2026-09-14

Amends [0024](0024-one-notification-pipeline.md): the live connection now
carries one piece of content, and a switch governs a banner as well as a push.
[`BENACHRICHTIGUNGEN.md`](https://github.com/AaronGreiner/q2/blob/505fafb/BENACHRICHTIGUNGEN.md#10-bewusst-nicht-enthalten) §10 had left in-app banners out — "der Zähler ist das
Signal". This reverses that, at the product owner's request.

## Context

After 0024 somebody with q2 open learned about a message, a verdict or kudos
from a badge moving, if they happened to look at the right one. Their phone
stayed quiet on purpose, because they were looking — but nothing in front of
them said what had arrived. A closed app got a push with the words in it; an
open one got a number.

The toaster, meanwhile, was full of confirmations of the person's own taps:
"Kudos gesendet!", "Ziel erstellt!", "Stimme abgegeben" and two dozen more,
most of them repeating what the screen had just shown. A welcome after signing
up. Four entries that nothing showed any more.

## Decision

### A banner is the push for somebody who is looking

When a notification may interrupt somebody, it does so exactly one way.
`NotificationRules.InterruptionFor` answers `Banner` for somebody with the app
open and `Push` for somebody without, after the same three checks: a muted
conversation, their switch for that kind, their quiet hours in their own zone.
`InterruptionPlanner` makes that decision once per person, so "is this person
looking" is read once. Reading it separately for each route would let somebody
who puts the app away in between be told twice, or not at all.

The banner therefore obeys everything a push obeys. A muted chat interrupts
nobody on screen either; the switches decide "what may interrupt you" rather
than "what makes your phone ring", and the settings screen says so; quiet hours
still mean that nothing arrives. The bell, the badges and the open screen move
whatever is switched off — 0024's rule that a switch decides what may
interrupt, not what may be found.

### The live connection carries the notification, and only that

A third event, `notification`, carries a `NotificationResponse`: the push
payload, built by the same code for the same person after the same rules, sent
to their connections instead of their devices. It is not a second read path.
Nothing in it comes from anywhere a push would not have taken it from, and every
screen is still read again over REST after `changed`. The hub still has no
method a client can call.

### The app decides only what it alone knows: where it is

The client shows the banner at the top of the screen (`useNotificationToast`),
worded by `notificationText` as the bell and the lock screen are, and replaced
rather than stacked by `notificationTag`, as on a lock screen. It is not shown
for the screen it would open, nor — for a message — over the chat list, which
shows every conversation's newest line as it arrives. A banner already up goes
as soon as its screen is reached another way. Tapping it opens that screen.

The whole banner is a real link, stretched over it, so a keyboard reaches it
and a screen reader announces it as one. It navigates only while the banner is
still open, because a swipe that dismissed it ends in a click on the same
element. It has no close button, like a banner on a phone: one target for a
thumb rather than two side by side. It goes by itself after five seconds, is
swiped up, or closed with Escape.

It is neutral rather than the accent: it is news, not something to do this
second ([0015](0015-qdos-design-language.md)). Session Replay masks the name and
the goal title and blocks a message's words, through Replay's own classes,
because Nuxt UI teleports a toast out of reach of a `data-q2-*` attribute.

### The toaster is for what arrived, and for what the screen cannot show

Toasts come in at the top, below a notch, where a phone shows what has just
arrived. A confirmation of somebody's own tap stays only where nothing on screen
would otherwise say it worked: a link copied, a report received, the feedback
dialog failing to open, a goal deleted for good, a group left, a person
blocked. The welcome after signing up, the other confirmations and the four
unused entries are gone.

## Consequences

- **The switches mean slightly more than before**, and the screen says so: they
  decide what may interrupt, as a push or as a banner.
- **`LiveEvents` has three members**, mirrored by hand in `useLiveConnection`
  like the other two.
- **The challenge reaches people who are looking.** It is still addressed to
  everybody; the candidates are now everybody with a device and everybody
  connected, and the switch and quiet hours decide per person as for any kind.
- **What a push carries now also crosses the live connection** — to the same
  person, over TLS rather than RFC 8291 encryption, and kept nowhere on the
  device ([privacy.md](../privacy.md)).
- **A banner covers the header for five seconds.** It can be swiped away or
  closed with Escape, and it never appears over the screen it is about.
