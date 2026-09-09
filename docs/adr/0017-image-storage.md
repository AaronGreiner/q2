# 0017 — Images: bytes on disk, behind a session

**Status:** Accepted
**Date:** 2026-09-07

## Context

Until now q2 stored no file at all. An avatar was two letters on a colour from a
closed palette, and every byte the product held was a row in SQLite.

Stage 3 of `QDOS-UEBERNAHME.md` ends that, and it does so for the feature the
whole migration exists for: in Qdos a goal is delivered by photographing the
evidence and letting friends vote on it. Stage 4 needs somewhere to put that
photograph. Avatars come along in the same stage — same storage, far smaller
risk, and they close the "edit profile" gap that has been open since
registration started deriving a profile from a name.

User-generated pictures change what this application is. A goal title is
personal; a photograph is a face, a room, a location, and the most intimate
thing q2 will ever hold. Three decisions had to be made before the first upload
was possible rather than after.

## Decision

### The bytes live behind `IImageStore`, on the file system

`FileSystemImageStore` writes to a directory beside the database
(`Q2:Images:RootPath`, defaulting to `images` next to the SQLite file). The
posture is the one from [0004](0004-sqlite-first.md): the simplest thing that
carries the product now, behind a seam that turns the move to an S3-compatible
bucket into a registration and a class rather than a search through the
features.

The interface is deliberately narrow — save, open, delete. No listing, no
enumeration, no "does this exist". The `Images` table answers all three, and a
store that could be queried would become a second, disagreeing index of what q2
holds.

The path is derived from the id (`a1/a1b2c3….bin`), never stored: one fewer
fact that can be wrong, and no directory ends up holding more than a few
hundred files. The extension is `.bin` because the media type is a column, and
a `.jpg` on disk is an invitation to point a static file handler at the folder.

### No image has a public URL

`GET /api/images/{id}` requires a session, asks `ImageService.CanRead`, and
answers **404** rather than 403 when the answer is no — "you may not see this"
confirms the picture exists.

`CanRead` switches on the purpose, in one place:

| purpose | who may read it |
| --- | --- |
| `Avatar` | anybody signed in — the same reach the initials it replaces have always had, because an avatar appears in search results, where the viewer is by definition not a friend yet |
| anything else | its owner |

A new purpose is therefore a compiler-visible decision rather than something
that defaults to visible. `Proof` is owner-only today; stage 4 gives it the
audience it is for, together with the vote it hangs off.

This is the rule the whole design turns on. Every other read in q2 is scoped to
the person asking ([AGENTS.md](../../AGENTS.md) section 8), and a guessable
address in a served folder would be the one place that is bypassed.

### Nothing caches an image

The response carries `Cache-Control: private, no-store` and
`X-Content-Type-Options: nosniff`.

The service worker was never a risk — it precaches build output from the app's
own origin and nothing else ([0012](0012-installable-pwa.md)) — but the
browser's own cache and any proxy in between are. A phone is shared, and a
photograph still in the cache after somebody signs out is the same disclosure as
one served without a session.

The cost is real: an avatar is fetched again on each navigation rather than read
from disk. At the sizes involved (an avatar leaves the browser at most 512
pixels across) that is the cheaper mistake to make.

### The server reads the bytes; it does not trust the header

`ImageFormatReader` reads the media type and the pixel dimensions out of the
first bytes of the upload. The `Content-Type` header got the request routed and
is not consulted again — the caller wrote it.

Two formats, JPEG and PNG. Everything the app uploads goes through a canvas
(`app/app/utils/images.ts`) and comes out as JPEG, so a HEIC photograph, a WebP
download and a PNG screenshot all arrive as one format; PNG is accepted because
a screenshot picked from disk often is one. A third parser would be code nothing
in the product produces.

It is a header reader, not a decoder, so nothing here allocates per pixel and a
"decompression bomb" is a rejected width rather than a stalled server. **No
image library is taken as a dependency, and nothing is re-encoded server-side**
— which is why the limits below have to be real rather than a formality:

- at most 4 MB per upload, checked against the declared length *and* while
  reading, because a chunked request declares nothing;
- between 32 and 2048 pixels on each edge;
- 100 MB and 500 images per person. Two ceilings because they bound different
  failures: bytes bound the disk, count bounds the row count and the directory.

### The upload is a raw body, not a multipart form

`POST /api/images?purpose=…` with `Content-Type: image/jpeg` and the file as the
body.

The session is a cookie, so a write endpoint needs an answer to CSRF. A browser
can be made to submit a cross-site form with no script at all, and such a form
may send `multipart/form-data` — never `image/jpeg`. Only a scripted request can
set that header, and a scripted cross-origin request is already stopped by the
CORS allow-list. **The shape of the request is the defence**, with no token to
plumb through and nothing to remember. It also means the application registers
no antiforgery services, which is why declaring the body as an `IFormFile` — even
only in the OpenAPI metadata — makes the endpoint fail.

### Removing a picture is deleting it

There is no "unset my avatar". `DELETE /api/images/{id}` removes the row, clears
any profile pointing at it in the same transaction, and then removes the bytes —
in that order, because a row without a file is a reference to nothing, while a
file without a row is only wasted disk.

One path rather than two, and the one that actually gets rid of the photograph
and gives back the storage.

## Consequences

**The privacy surface grew, and so did the obligations.** Session Replay records
every session and `sendDefaultPii` is on
([0005](0005-observability-and-sentry.md)). Every element that can render a
picture — `AppAvatar`, the capture sheet's frame — carries `data-q2-block`, with
a component test that says so, and that had to be true *before* the first upload
was possible rather than after.

**Two obligations are now overdue rather than merely open.** Account deletion
has no path in q2 at all, and with photographs it acquires a legal deadline.
Reporting needs a recipient. Both are stage 8; neither is discharged by this
decision, and [docs/privacy.md](../privacy.md) says so.

**A replaced avatar is deleted with it.** Nothing else in q2 ever points at an
avatar image and no screen lists them, so the picture somebody just replaced is
rubbish the moment the new one is in place — and unreachable rubbish, sitting in
their allowance for good. `ProfileService.UpdateAsync` therefore deletes the
previous one after the new one is saved. That order is deliberate: a failed
delete costs one unreferenced file, while the reverse would risk a profile
pointing at bytes that are gone.

**Orphaned uploads are still possible, in one narrower way.** A picture uploaded
but never attached — the sheet was dismissed between the upload and the save —
stays in the person's allowance. The quota bounds the damage and no cleanup job
exists; if that turns out to matter, the fix is a sweep over images nothing
references, not a transaction spanning a file system.

**Backups are two things now.** "Copy the data directory" still covers both,
which is why the default root is beside the database rather than somewhere
tidier. A deployment that moves one has to move the other.

**A reset does not delete files.** `DatabaseSeeder` clears the `Images` rows,
because a stale row would collide with the next id the test generator hands out,
but it leaves the directory alone: a reset owns the database, not a tree outside
any transaction. The two environments this ever runs in have a temporary
directory anyway.
