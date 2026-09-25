# 0028 — Ruhe: a continuous surface and selectable accents

**Status:** Accepted
**Date:** 2026-09-22

## Context

The requested redesign uses variant **2B Ruhe** from the supplied
“Neues App-Design mit Varianten” export. Its defining features are warm neutral
backgrounds, one continuous content surface with a 26px upper radius, quiet
separators, circular avatars and restrained accents. The export is a visual
reference; its sample contacts, encryption claims and unimplemented messaging
features are not product requirements.

## Decision

Replace the black/lime palette and individually raised cards from ADR 0015
with the Ruhe palette. Dark remains the default. Light uses a warm grey shell
and white content; dark uses a near-black warm shell and a charcoal content
surface. Typography retains the self-hosted Public Sans, the existing mobile
navigation and all current q2 flows.

`AppContentPanel` wraps Nuxt UI's `UCard` and owns the shared surface, optional
fixed toolbar and scrolling content. Search fields use `UFormField`/`UInput`,
segmented controls and the accent picker use `URadioGroup`, and switches use
`USwitch`. Existing feature components continue to receive data and emit intent.
Nuxt UI defaults live in `app/app.config.ts`; colour and radius tokens live in
`assets/css/main.css`. Avatars retain photographs but use neutral initials. `ChatContactStrip` shows
existing friends above the chat list and opens the existing direct-chat flow;
it does not introduce stored favourites or pinning.

Iris is the default accent. Sage, rose and ochre are paired light/dark presets,
with contrast colours supplied centrally to both Nuxt UI and feature controls.
The colour picker changes the entire app immediately. It stores only a palette
name in the `q2-accent` cookie for one year (`SameSite=Lax`, path `/`). The cookie
is a device preference, shared by accounts using that browser, and is read by
SSR to avoid an initial flash in the wrong palette. Invalid values fall back to
iris. It is not an account field and does not require a migration or API change.
Theme and language retain their existing account persistence.

The streak hero uses a neutral surface and confines its gradient to the flame
icon. The existing meaning of the flame gradient (streaks), destructive red, privacy
attributes and the prohibition on decorative emoji remain. Sample export copy
is not imported into the application. All new labels are in the German/English
message catalogue.

## Motion

The shared motion rules in `assets/css/motion.css` extend Ruhe with short,
one-shot responses: page and layout fades, a 6px arrival of content sections
with stagger delays capped at 105ms, a sliding navigation marker and segmented
selection, press feedback, progress interpolation and a small acknowledgement
on kudos. New chat messages enter through Vue's `TransitionGroup`, without
replaying the history or remounting the composer. Nuxt UI retains ownership of
drawer, dialog and switch transitions.

Durations are 140ms for controls, 240ms for navigation and 380ms for progress
and content arrival; page exits take 90ms. There are no idle animation loops,
timers or extra dependencies. Motion changes no displayed server-derived
value. Query-only navigation preserves the page and keyboard focus. The OS
reduced-motion preference disables custom movement and removes animation and
transition delays globally, including Nuxt UI overlays.

## Haptic feedback

`utils/haptics.ts` requests a single 15ms vibration directly on taps on feed
kudos, chat and challenge reactions, either proof verdict, and the live camera
shutter once a video frame is available. It acknowledges the interaction, not
the success of an upload or server request; incoming updates never vibrate.
There is no feedback on navigation, file selection or ordinary form controls.

The helper checks `navigator.vibrate` and the OS reduced-motion preference on
each interaction. Server rendering, unsupported browsers, declined requests
and thrown platform errors silently skip feedback. None creates a log or a
Sentry event, and the original action always continues. No native dependency,
device fingerprint or stored preference is introduced. Safari/iPhone PWAs do
not gain arbitrary vibration; native haptics belong to the Capacitor work in
[#7](https://github.com/AaronGreiner/q2/issues/7), which left them to
[#52](https://github.com/AaronGreiner/q2/issues/52). This implements the haptic
portion of [#19](https://github.com/AaronGreiner/q2/issues/19).

## Verification

Component and unit tests cover palette normalization, cookie persistence,
selection and the shared panel slots. Mobile E2E checks all four palettes in
both themes for WCAG A/AA, keyboard selection, reload and SSR persistence,
and captures the principal screens at 390 × 844 for visual inspection.

Motion E2E checks navigation across both layouts, keyboard tab selection,
sheet dismissal, chat continuity and phone geometry with normal and reduced
motion. Existing integration and privacy tests continue to cover the unchanged
API and reporting paths.

Haptic tests cover SSR, absent/refused vibration, reduced motion, reaction and
verdict wiring, and real camera capture with a synthetic browser camera. Browser
tests record vibration requests; physical pulse strength requires a real phone.
