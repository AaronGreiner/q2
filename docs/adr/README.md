# Architecture decision records

One file per decision that would otherwise be re-litigated every few months.

An ADR records *why*, not *how* — the how is in the code. If a decision here is
reversed, add a new record that supersedes the old one rather than editing it,
so the reasoning at the time stays readable.

| # | Decision | Status |
| --- | --- | --- |
| [0001](0001-repository-layout-and-task-runner.md) | One repository, two applications, one task runner | Accepted |
| [0002](0002-frontend-architecture.md) | Nuxt with a component architecture on top of Nuxt UI | Accepted |
| [0003](0003-backend-architecture.md) | Feature-organised minimal APIs, no repository layer | Accepted |
| [0004](0004-sqlite-first.md) | SQLite first, PostgreSQL-ready | Accepted |
| [0005](0005-observability-and-sentry.md) | Sentry in every environment, filtered centrally | Accepted |
| [0006](0006-authentication-deferred.md) | Authentication deliberately deferred | Superseded by [0011](0011-authentication-with-identity.md) |
| [0007](0007-testing-strategy.md) | Real providers, real pipeline, real SDK | Accepted |
| [0008](0008-deployment-topology.md) | One host, two systemd services, one origin | Accepted |
| [0009](0009-single-known-person.md) | One known person, flagged in the database | Superseded by [0011](0011-authentication-with-identity.md) |
| [0010](0010-german-first-interface.md) | A German-first interface, with a hand-written catalogue | Accepted |
| [0011](0011-authentication-with-identity.md) | Accounts with ASP.NET Core Identity and a session cookie | Accepted |
| [0012](0012-installable-pwa.md) | Installable as a PWA, with a service worker that caches no content | Accepted |
| [0013](0013-app-like-input.md) | App-like input: no zoom, no selection, a capped safe area | Accepted |
| [0014](0014-user-feedback.md) | User feedback: anonymous, from our own controls | Accepted |
| [0015](0015-qdos-design-language.md) | The Qdos design language: black, one accent, no emoji | Accepted |
| [0016](0016-windows-instead-of-steps.md) | Windows instead of steps: a goal you can miss | Accepted |
| [0017](0017-image-storage.md) | Images: bytes on disk, behind a session | Accepted |
| [0018](0018-proof-and-vote.md) | A window is closed by other people | Accepted |
| [0019](0019-warning-and-balance.md) | The warning before, and the record after | Accepted |
| [0020](0020-pause-and-archive.md) | The exits: a pause, an ending, and a deletion | Accepted |
| [0021](0021-daily-challenge.md) | The daily challenge: one prompt, and nothing at stake | Accepted |
| [0022](0022-blocking-reporting-and-erasure.md) | Getting away, asking for help, and leaving | Accepted |
| [0023](0023-web-push.md) | Notifications: a delivery route, not a second product | Accepted |
