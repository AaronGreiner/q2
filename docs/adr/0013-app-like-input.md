# 0013 — App-like input: no zoom, no selection, a capped safe area

**Status:** Accepted
**Date:** 2026-07-31

## Context

q2 became installable in
[0012](0012-installable-pwa.md). Installed, it has no address bar, no tabs and
no browser chrome — and that is exactly when the remaining browser behaviours
start to feel wrong, because there is no longer a browser around them to
explain why they are there:

- a two-finger pinch zooms the whole app and leaves it at an arbitrary scale
  with no visible way back, since there is no address bar to reset;
- holding a finger on a goal title selects it, raises a magnifier and offers
  "Copy";
- the tab bar, padded by the full `env(safe-area-inset-bottom)` for the first
  time, ends up with its icons near its top edge and a wide empty band beneath
  them.

None of these were visible while q2 only ran in a browser tab, where the insets
are zero and the chrome makes the page look like a page.

## Decision

**Pinch-zoom is off.** Two halves, because neither is enough alone:
`touch-action: pan-x pan-y` on `html` — the gesture list without `pinch-zoom`
in it — and `maximum-scale=1, user-scalable=no` in the viewport meta. Safari
ignores the meta in a browser tab and honours it once q2 is installed, which is
the case this is for.

**Text is not selectable**, via `user-select: none` on `body` and
`-webkit-touch-callout: none` for the iOS long-press menu, which is a separate
switch and stays on without it.

**Everything a person types in stays selectable** — `input`, `textarea`,
`select`, `[contenteditable]` opt back in. This is not a nicety: without a
caret and a selection, correcting a word inside a goal title is impossible.

**The safe-area inset is a ceiling to clear, not a target.** `--q2-safe-bottom`
in main.css takes `min(env(safe-area-inset-bottom), 1.25rem)`, with a `0.75rem`
floor for devices that report nothing. Apple's own guidance is to use the whole
inset, and for a full-height native tab bar that is right; for a compact bar
like this one it puts 34px of empty background under a 49px row and the bar
stops looking like it sits on the edge of the screen.

## The cost, stated plainly

**Disabling zoom fails WCAG 2.2 success criterion 1.4.4 (Resize Text, level
AA)**, and works against 1.4.10 (Reflow). Somebody who needs to magnify a
screen to read it cannot, inside q2, do the thing they do everywhere else.

That is a real cost and it is not cancelled by the reasons for it. What softens
it, and what has to keep being true:

- **operating-system magnification still works** — iOS Zoom and Android
  Magnification are outside the page and unaffected;
- **the browser's own page zoom still works** on desktop, where the layout is a
  centred phone column anyway;
- **the layout is built for the largest text, not the smallest.** The German
  catalogue is the longer of the two languages and the design is checked against
  it at 390 × 844 ([app/AGENTS.md](../../app/AGENTS.md) section 8), so the
  screens do not depend on being able to zoom out of a squeeze;
- **text scales with the OS font size**, because nothing in the layout is
  measured in a unit that ignores it.

If q2 ever ships to people outside a small circle, this is the first decision to
put back on the table — and the honest version of "revisit" is: turn zoom back
on, and find another way to stop a stray pinch, rather than keep it off because
it looks tidier.

## Consequences

**Good**

- A long press, a double tap and a pinch all do what they do in a native app:
  nothing surprising.
- Dropping the double-tap gesture also drops the delay browsers keep while
  waiting for a second tap, so every tap in q2 registers sooner.
- The tab bar sits on the bottom edge on a device with a home indicator and on
  one without, from one token rather than a number per component.

**Bad, and accepted**

- The WCAG failure above.
- Copying a chat message is no longer possible on a touch device. Nobody has
  asked for it; when somebody does, the answer is an explicit "copy" action on
  the message, not turning selection back on globally.
- `user-select: none` is inherited, so a future component that genuinely needs
  selectable content has to opt back in and will not think to. The four
  selectors in main.css are the list, and it is worth keeping short.

## When to revisit

**The accessibility work in [next-steps.md](../next-steps.md) item 3 has
landed.** `@axe-core/playwright` flags `user-scalable=no` under its
`meta-viewport` rule. That one rule is disabled next to a link back to this
decision; every other WCAG A/AA rule must return zero violations. Revisit the
exception before q2 ships outside a small circle.

**When somebody asks to copy a message**, which is the first thing selection was
actually good for here.
