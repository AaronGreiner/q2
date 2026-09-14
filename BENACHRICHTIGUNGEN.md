# Benachrichtigungen: Plan

Stand: 14. September 2026 · Grundlage: Code-Stand `643023c`

> **Zur Sprache dieser Datei.** [AGENTS.md](AGENTS.md) verlangt englische
> Dokumentation. Das hier ist — wie `QDOS-UEBERNAHME.md` — bewusst eine
> Ausnahme: ein Entscheidungspapier für dich, kein Repository-Dokument. Was
> davon Bestand hat, wird in Etappe 1 zu einem englischen ADR
> (`docs/adr/0024-…`).

---

## 0. Kurzfassung

- **Eine Pipeline für alles.** Ein Ereignis hat Empfänger und bis zu drei Wege
  zu ihnen: die **Glocke** (gespeichert), **live** über SignalR (solange die
  App offen ist) und **Push** über Web Push (wenn nicht).
- **Das Bestehende zieht um, statt daneben weiterzulaufen.** Abend-Warnung und
  Challenge laufen danach durch dieselbe Pipeline wie alles Neue. `PushKind`,
  der zweite Satzbau im Service Worker, die Badges über das Profil und die drei
  Schalter ohne Wirkung verschwinden (Abschnitt 4).
- **Jede Art hat einen eigenen Schalter**, unter Profil → Einstellungen →
  Benachrichtigungen. Ein Schalter erscheint erst in der Etappe, in der seine
  Art wirklich zugestellt wird.
- **Fünf Etappen**, jede endet mit etwas Benutzbarem und grünem
  `bun run validate`.

---

## 1. Ausgangslage

| Baustein | Heute | Wo |
| --- | --- | --- |
| Web Push | VAPID, RFC 8291/8292, selbst implementiert und gegen das RFC-Beispiel getestet. Zwei Arten: `WindowAtRisk`, `ChallengePublished` | `api/…/Features/Notifications/` |
| Wer benachrichtigt | nur `GoalMaintenanceWorker` und `ChallengeQueueWorker` | |
| Glocke | auf der Startseite, Link zum Freundes-Feed `/activity`, **bewusst ohne Zahl** ([ADR 0019](docs/adr/0019-warning-and-balance.md)) | `app/app/pages/index.vue` |
| Abend-Warnung | ein `ActivityEvent` im Feed **und** ein Push an die Freunde | `ActivityKind.WindowAtRisk` |
| Badges (Chats, Anfragen) | Felder in `ProfileResponse`, nachgeladen per `refreshNuxtData('profile')` an drei Stellen | `app/app/layouts/default.vue` |
| Schalter | fünf Booleans in `UserSettings`; Kudos, Nachrichten und Wochenrückblick haben nichts dahinter | `Features/Settings/UserSettings.cs` |
| Service Worker | eigene, handgeschriebene Kopie von `PushPayload` und ein eigener Satzbau `compose()` | `app/service-worker/sw.ts` |
| Echtzeit | keine — ein Bildschirm lädt beim Öffnen | |

**Auslöser ohne jede Benachrichtigung:** Nachricht, Freundschaftsanfrage,
Annahme, Beweisfoto hochgeladen, Abstimmungsergebnis, Kudos und Reaktionen,
Einladung zu einem Ziel, Pause, Einspruch.

---

## 2. Entschieden (deine Antworten)

| Frage | Antwort |
| --- | --- |
| Ereignisse | Nachrichten, Freundschaftsanfragen (erhalten und angenommen), Abstimmung fällig, Ergebnis deines Nachweises, Kudos und Reaktionen, Einladung zu Zielen und Pausen — dazu die beiden bestehenden |
| Ort in der App | Push **und** Glocke |
| Echtzeit | **SignalR** (WebSockets) |
| Native Push (APNs/FCM) | später, mit Capacitor |
| Bestehendes | wird umgestellt, **keine zwei Systeme nebeneinander** |
| Einstellungen | jede Benachrichtigung einzeln an- und abschaltbar, im Profil |

---

## 3. Das Modell

```
Auslöser (Service oder Worker)
  │  im selben SaveChanges wie die Änderung: die Zeile(n) für die Glocke
  ▼
Notifier.PublishAsync(Ereignis, Empfänger)            ← erst nach dem Speichern
  ├─ Filter:  man selbst? blockiert (in beide Richtungen)? Chat stummgeschaltet?
  ├─ live:    SignalR an person:{id}  → neue Zähler + „das hat sich geändert“
  └─ Push:    nur wer nicht verbunden ist → sein Schalter → seine Ruhezeit → Gerät
```

Die Zeile für die Glocke entsteht **in derselben Transaktion** wie das, was sie
auslöst. Zugestellt wird erst danach, genau wie heute in den beiden Workern
([ADR 0023](docs/adr/0023-web-push.md): *sending never happens inside a
transaction*).

### 3a. Ein Ort pro Ereignis

> **Jedes Ereignis hat genau einen Ort in der App und genau einen Zähler. Hat es
> schon einen, bleibt es dort. Alles andere bekommt die Glocke.**

- Nachrichten → **Chats** (Badge, aus den Lesemarken abgeleitet)
- erhaltene Anfragen → **Suche** (Badge)
- fällige Abstimmungen → **Abstimmen-Banner** und `/vote`
- die Challenge → **Challenge-Banner**
- alles andere → **Glocke**

Ohne diese Regel würde eine neue Freundschaftsanfrage zwei Zähler hochzählen
und müsste zweimal „weggeklickt“ werden. Genau solche Mischungen soll es nicht
geben.

Feed und Glocke überschneiden sich danach nicht mehr. Der **Feed** zeigt, was
deine Freunde getan haben: für alle gleich, beim Lesen berechnet. Die **Glocke**
zeigt, was dich betrifft: pro Empfänger gespeichert.

### 3b. Die Arten

| `NotificationKind` | Ausgelöst in | Empfänger | Glocke | Push-Tipp öffnet | Schalter |
| --- | --- | --- | --- | --- | --- |
| `MessageReceived` | `ChatService.SendAsync` | Teilnehmer außer dem Absender, sofern nicht stummgeschaltet | nein (Chats) | `/chats/{id}` | Nachrichten |
| `FriendRequestReceived` | `FriendsService.RequestAsync` (neue Anfrage) | Adressat | nein (Suche) | `/search` | Freundschaften |
| `FriendshipStarted` | `AcceptAsync`, `RequestAsync` mit Gegenanfrage, Registrierung über Einladungslink | wer gefragt oder eingeladen hat | ja | `/people/{id}` | Freundschaften |
| `ProofAwaitingVote` | `ProofService.SubmitAsync` | alle mit `goal.CanVote`, außer dem Hochladenden | nein (Abstimmen) | `/vote` | Abstimmung fällig |
| `ProofDecided` | `ProofService.Settle`, auch nach 12 h im `GoalMaintenanceWorker` | Hochladender (bestätigt, abgelehnt, Nachreichen möglich) | ja | `/goals/{id}` | Ergebnisse |
| `ReactionReceived` | Reaktion auf Nachweis, Nachricht, Challenge-Beitrag; Kudos auf Feed-Eintrag | Urheber dessen, worauf reagiert wurde | ja, zusammengefasst | das Ziel der Reaktion | Kudos & Reaktionen |
| `GoalInvitation` | `GoalService.CreateAsync` | eingeladene Freunde | ja | `/goals/{id}` | Einladungen & Pausen |
| `GoalPaused` | `GoalService.PauseAsync` | Teilnehmer (können Einspruch einlegen) | ja | `/goals/{id}` | Einladungen & Pausen |
| `PauseLifted` | `GoalService.VetoPauseAsync`, wenn Einsprüche die Pause beenden | Besitzer | ja | `/goals/{id}` | Einladungen & Pausen |
| `FriendWindowAtRisk` | `GoalMaintenanceWorker` (heute `WindowAtRisk`) | Freunde des Besitzers — unverändert nach ADR 0019 | ja (zieht aus dem Feed um) | `/people/{id}` | Wird knapp bei Freunden |
| `ChallengePublished` | `ChallengeQueueWorker.AnnounceAsync` | alle | nein (Banner) | `/challenge` | Challenge des Tages |

Absichtlich **ohne** Benachrichtigung bleibt ein einzelner Einspruch gegen eine
Pause. Einsprüche sind anonym, und eine Meldung pro Einspruch wäre Druck.
Gemeldet wird erst die Folge, also `PauseLifted`.

### 3c. Die Schalter

Acht Schalter in vier Gruppen, auf einem eigenen Bildschirm
`/settings/notifications`. Der wird von der Einstellungsseite aus erreicht, so
wie heute `/settings/blocked`:

| Gruppe | Schalter | Herkunft |
| --- | --- | --- |
| Freunde | Nachrichten · Freundschaften | `NotifyMessages` · neu |
| Ziele | Abstimmung fällig · Ergebnisse · Einladungen & Pausen · Wird knapp bei Freunden | neu · neu · neu · `NotifyReminders` |
| Rückmeldungen | Kudos & Reaktionen | `NotifyKudos` |
| Challenge | Challenge des Tages | `NotifyChallenge` |

Darunter, wie heute: **dieses Gerät** (Erlaubnis und Abo, geräteweise) und die
**Ruhezeiten**.

**Ein Schalter steuert den Push, nicht die Glocke.** Nur so heißt ein Schalter
bei jeder Art dasselbe: Der Nachrichten-Schalter kann keine Nachricht aus einem
Chat entfernen, also darf der Kudos-Schalter keinen Kudos aus der Glocke
entfernen. Zähler und Live-Aktualisierung bleiben ebenfalls unberührt.

Welche Art welchem Schalter gehorcht, entscheidet weiterhin **ein**
`switch`-Ausdruck. So sieht der Compiler jede neue Art (heute
`NotificationService.Wants`).

### 3d. Die Glocke

- **Route `/notifications`, mit Zahl.** Die Zahl zählt, was seit dem letzten
  Öffnen dazugekommen ist. ADR 0019 wollte keine Zahl, weil „haben meine
  Freunde etwas getan“ nichts zum Abhaken sein soll. Die Glocke beantwortet
  jetzt aber eine andere Frage: *was dich betrifft*. → Abschnitt 9, Punkt 1.
- **„Gesehen“ ist ein einziger Zeitstempel pro Person**, gesetzt beim Öffnen
  der Glocke. Das ist dasselbe Prinzip wie `ConversationParticipant.LastReadAt`
  und kommt ohne Gelesen-Flag pro Zeile aus. Zeilen, die neuer sind als der
  vorige Stempel, werden hervorgehoben.
- **Zustände werden abgeleitet, nicht gespeichert.** Ob eine Pause noch
  Einspruch zulässt oder ein Ziel noch existiert, liest die Glocke beim
  Anzeigen am Ziel selbst ab.
- **Reaktionen werden zusammengefasst.** Solange eine Zeile ungesehen ist, gibt
  es eine pro Empfänger, Art und Ziel: „Mara und 2 weitere …“. Wer eine
  Reaktion zurücknimmt, verschwindet aus einer ungesehenen Zeile, und es wird
  nichts verschickt.
- **Aufbewahrung: 30 Tage**, danach löscht ein nächtlicher Durchlauf. Das ist
  eine dokumentierte Automatik, und `privacy.md` §8 verlangt eine
  Aufbewahrungsregel.
- **Der Freunde-Feed bleibt**, erreichbar über „Alle anzeigen“ in der
  Feed-Sektion der Startseite statt über die Glocke. Sein Abschnitt für
  Warnungen entfällt, weil die Warnungen in die Glocke umziehen.

---

## 4. Umstellung des Bestehenden

Das ist der Abschnitt zu „keine sinnlosen Mischungen“. Jede Zeile ersetzt etwas
Altes, statt etwas daneben zu stellen.

| # | Heute | Danach | Warum |
| --- | --- | --- | --- |
| 1 | `PushKind` mit 2 Werten | `NotificationKind` mit 11 Werten, dieselbe Aufzählung für Glocke, live und Push | ein Begriff statt zwei |
| 2 | `NotificationService.NotifyAsync`, aufgerufen von zwei Workern | `Notifier.PublishAsync`, aufgerufen von **jedem** Auslöser, beide Worker eingeschlossen. Der Geräte-Teil (Schlüssel, Abo, Abmelden) bleibt als eigener Service | ein Einstieg für alle |
| 3 | Abend-Warnung als `ActivityEvent` im Feed | Glocken-Eintrag pro Freund. `ActivityKind.WindowAtRisk`, `isWarning()` und der Warnungsabschnitt in `/activity` entfallen. Die Migration löscht die vorhandenen Warnzeilen: Sie galten für einen einzigen Abend, und sie aufzubewahren widerspräche „kein Nachtreten“. `GoalInstance.RiskNotifiedAt` bleibt die „höchstens einmal“-Marke | Der Kommentar an `ActivityKind.WindowAtRisk` sieht diesen Umzug schon vor: *„until stage 9 the feed is the delivery“* |
| 4 | Challenge an „alle mit Gerät“ (liest `PushSubscriptions`) | an alle Personen: live an die Verbundenen, Push an die Geräte | derselbe Weg wie alles andere |
| 5 | Glocke → `/activity`, ohne Zahl | Glocke → `/notifications`, mit Zahl. Der Feed über „Alle anzeigen“ | ein Symbol, eine Bedeutung |
| 6 | `unreadChats` und `pendingFriendRequests` in `ProfileResponse`, 3× `refreshNuxtData('profile')` | eigener `CountsResponse` (`GET /api/counts`: `unreadChats`, `pendingFriendRequests`, `unseenNotifications`, `proofsAwaitingVote`), den der Hub nach jeder Änderung neu schickt. Beide Felder verlassen `ProfileResponse`, und die drei Nachlade-Stellen entfallen | Zähler kommen aus einer Quelle |
| 7 | 5 Schalter, 3 ohne Wirkung | 8 Schalter, alle wirksam. Sie bleiben in `UserSettings` (ein Ort für Einstellungen), aber als eigener Wertetyp `NotificationPreferences` statt fünf loser Booleans. Die Migration benennt um, übernimmt die gespeicherten Werte und verwirft `NotifyWeeklyReview` | kein Schalter ohne Wirkung |
| 8 | Service Worker mit handgeschriebener `PushPayload`-Kopie und eigenem `compose()` | Die Nutzlast **ist** `NotificationResponse`, der generierte Typ, den die Glocke über REST liefert. Den Satz baut `notificationSentence()` in `utils/display.ts`, und zwar für Glocke und Service Worker gemeinsam. Geprüft: `display.ts` hat nur Typ-Importe, und die `tsconfig` des Workers löst `~/` auf | ein Wortlaut, ein Typ |
| 9 | Push-`tag` pro Art | `tag` pro Ziel (Unterhaltung, Nachweis, Ziel). `renotify: false` bleibt: Die nächste Nachricht im selben Chat ersetzt die vorige still, statt erneut zu vibrieren | Zwei Chats ersetzen sich nicht gegenseitig |
| 10 | `NotificationEndpointTests` (16 Tests) | aufgeteilt: Gerät (bleibt), Zustellregeln (→ Notifier), Warnung und Challenge (gleiche Zusicherungen, neuer Weg). `RecordingPushSender` bleibt. `notifications.spec.ts` zieht auf den neuen Bildschirm | |

Die Werte der heute wirkungslosen Schalter bleiben erhalten. Die Schalter selbst
verschwinden in Etappe 1 vom Bildschirm und kommen in der Etappe zurück, in der
ihre Art zugestellt wird.

---

## 5. Technik

### 5a. SignalR

- **Server:** Teil von ASP.NET Core, kein NuGet-Paket. `LiveHub` liegt unter
  `/api/live`. Caddy leitet `/api/*` schon an die API weiter und reicht
  WebSockets ohne Konfigurationsänderung durch. Der Hub bekommt
  `RequireAuthorization`, und `AccountEndpointTests` prüft auch ihn: ohne
  Sitzung 401.
- **Anmeldung:** über das Sitzungscookie, ohne Token. Lokal (3000 → 5080) ist
  das dieselbe Site, also schickt der Browser das Lax-Cookie mit. CORS erlaubt
  schon Credentials mit ausdrücklichen Origins. Auf Staging ist es ohnehin
  derselbe Origin.
- **Gruppen:** `person:{id}`, beigetreten in `OnConnectedAsync` über
  `CurrentPerson`. Der Hub liest keinen Claim selbst (AGENTS.md §8).
- **Nur Server → Client.** Der Hub hat keine Methode, die ein Client aufrufen
  kann. Jede Änderung bleibt REST. Damit bleibt OpenAPI der einzige Vertrag,
  und die Berechtigungen bleiben, wo sie sind. Das passt zu ADR 0023: *„Nothing
  can ask the server to send a notification.“*
- **Dünne Ereignisse.** Der Hub meldet nur, *was* sich geändert hat:
  - `counts` mit einem `CountsResponse`
  - `changed` mit Bereich und Id

  Ein offener Bildschirm lädt dann über den bestehenden REST-Lesezugriff nach.
  So gibt es genau einen Lesepfad mit allen Zuschnitten (Blockieren,
  Teilnehmer), statt eines zweiten im Hub, der sie noch einmal richtig haben
  müsste. Die beiden Ereignisnamen sind das einzige, was handgeschrieben
  gespiegelt wird.
- **Verbunden nur, solange die App sichtbar ist** (`visibilitychange`). Die
  Verbindung trennt nach kurzer Schonfrist und baut sich beim Zurückkehren neu
  auf. „Verbunden“ heißt dann „schaut gerade hin“, und wer hinschaut, bekommt
  keinen Push zusätzlich. iOS legt Hintergrund-Sockets ohnehin schlafen.
- **Ein Host, kein Backplane.** Gruppen leben im Prozess. Eine zweite Instanz
  bräuchte Redis oder einen Dienst. Das kommt erst, wenn es gebraucht wird,
  wie PostgreSQL.
- **Neue Abhängigkeiten, beide gepinnt:** `@microsoft/signalr` im Frontend
  (nur Client, nie beim SSR) und `Microsoft.AspNetCore.SignalR.Client`
  **nur** im Integrationstest-Projekt. AGENTS.md §6 verlangt dafür eine
  Diskussion: Das ist diese, und der ADR hält sie fest.
- **Fehler:** Abbrüche und Wiederverbindungen sind erwartete Netzfehler und
  erzeugen kein Sentry-Issue.
- **Capacitor später:** Ein WebSocket von `capacitor://` mit Cookie hat
  dasselbe bekannte Problem wie die restliche API (next-steps #15).
  `accessTokenFactory` ist die Nahtstelle, sobald Identity Bearer-Tokens
  ausgibt.

### 5b. Web Push

- **Bleibt, wie er ist:** `WebPushCrypto`, `WebPushSender`, ein Abo pro Gerät,
  404/410 löscht, zehn Fehlschläge in Folge ebenfalls, leere VAPID-Schlüssel
  schalten ab.
- **Neu: Zustellung außerhalb der Anfrage.** Auslöser sitzen jetzt in
  HTTP-Anfragen. Eine Nachricht in eine Gruppe mit 20 Leuten sind 19 Empfänger
  mal ihre Geräte. Deshalb geht der Push über eine Warteschlange im Prozess
  (`Channel`) an einen `NotificationDeliveryWorker`, und die Anfrage kehrt nach
  Speichern und Live-Meldung zurück.
  - Bei einem Neustart gehen wartende Pushes verloren. Das passt zu „verwerfen
    statt aufheben“ (ADR 0023), denn der Eintrag steht ja in der Glocke.
  - In `AutomatedTest` wird synchron zugestellt, damit Tests deterministisch
    bleiben.
- **Native Push später:** `IPushSender` bleibt die Nahtstelle. `PushSubscription`
  bekommt dann eine Spalte `Channel` (WebPush, APNs, FCM), jetzt noch nicht.

### 5c. Zustellregeln, in dieser Reihenfolge

1. nie an den Auslösenden selbst
2. nicht, wenn Auslöser und Empfänger sich blockiert haben (in einer der beiden
   Richtungen). Die Glocke filtert beim Lesen ebenfalls, also verschwinden bei
   einem späteren Block auch ältere Einträge, und das lässt sich rückgängig
   machen, wie bei Direktchats
3. Nachrichten: nicht, wenn der Empfänger den Chat stummgeschaltet hat
4. Glocken-Zeile und Live-Meldung, **unabhängig** von Schalter und Ruhezeit
5. Push nur: ohne Live-Verbindung → Schalter an → keine Ruhezeit in der eigenen
   Zone → Gerät vorhanden

Die Regeln 1–3 und 5 werden eine reine Funktion (wie `QuietHours`), damit sie
ohne Datenbank testbar sind.

---

## 6. Datenschutz und Sicherheit

- **Mehr Inhalt in der Nutzlast.** Absendername und bei Nachrichten ein
  Textauszug. Beides ist Ende-zu-Ende verschlüsselt (RFC 8291), der
  Push-Dienst liest nichts davon. Ob Text auf dem Sperrbildschirm erscheint,
  regelt das Betriebssystem („Vorschauen“ auf iOS, die Sperrbildschirm-Option
  auf Android), deshalb gibt es dafür keinen eigenen Schalter.
- **Neue gespeicherte Daten:** Glocken-Zeilen mit Empfänger, Art, Auslöser,
  Betreff, Ziel und Zeitpunkt. Aufbewahrt 30 Tage, dann automatisch gelöscht.
  Das kommt in die Tabelle in `privacy.md`.
- **Konto löschen:** Zeilen als Empfänger gehen mit. Zeilen, in denen die
  Person der Auslöser ist, ebenfalls: Ein Name, den es nicht mehr gibt, gehört
  in niemandes Glocke. Das ergänzt ADR 0022.
- **Logs:** nur Anzahlen und Arten, nie Betreff, Text oder Endpunkt. Die
  Sentry-Filter bleiben unverändert. Session Replay: Glocken-Zeilen bekommen
  `data-q2-private` bzw. `data-q2-block`, jeweils mit Komponententest.
- **Kein Endpunkt und keine Hub-Methode kann eine Benachrichtigung auslösen.**

---

## 7. Etappen

Jede Etappe endet mit etwas Benutzbarem und grünem `bun run validate`, mit
angesehenen Bildschirmen bei 390 × 844 und mit aktualisierten Dokumenten für
das, was sie ändert (README §1/§2, `privacy.md`, `next-steps.md`,
`architecture.md`). Größen wie im Übernahmepapier: **S** = Tage,
**M** = ein bis zwei Wochen, **L** = mehr.

### Etappe 1 — Fundament und Umstellung · M–L

Für Nutzer kommt noch keine neue Art hinzu, aber die beiden bestehenden laufen
über den neuen Weg.

- `NotificationKind`, `Notifier`, Tabelle `Notifications` samt Migration. Die
  Migration löscht außerdem die Warnzeilen im Feed und baut die Schalter um.
- `GET /api/notifications`, `POST /api/notifications/seen`, `GET /api/counts`
  sowie `bun run api:openapi` für Vertrag und Typen
- Glocke mit Zahl, Seite `/notifications`, Feed über „Alle anzeigen“
- `/settings/notifications` mit Gerätezeile, den zwei heute wirksamen
  Schaltern und den Ruhezeiten
- Service Worker auf `NotificationResponse` und `notificationSentence()`
  umgestellt, Zustell-Warteschlange
- ADR 0024 geschrieben, ADR 0019 und 0023 als „teilweise abgelöst“ markiert

*Fertig, wenn:* die Abend-Warnung in der Glocke der Freunde steht (und mit
Schlüsseln auf ihren Geräten ankommt), die Challenge wie bisher ankommt, der
Feed keine Warnzeilen mehr zeigt und kein Schalter ohne Wirkung auf dem
Bildschirm steht.

### Etappe 2 — Echtzeit · M

- `LiveHub`, Verbindungs-Composable (nur im Client, sichtbarkeitsgesteuert)
- Zähler live, `changed` für Chats, Freunde, Nachweise und Challenge
- ein offener Chat aktualisiert sich selbst
- kein Push an Verbundene

*Fertig, wenn:* bei zwei Browsern mit 390 × 844 eine Nachricht von A ohne Neuladen
in Bs offenem Chat erscheint, Bs Chat-Badge hochzählt und, sobald B den Tab
schließt, die nächste Nachricht als Push ankommt.

### Etappe 3 — Nachrichten und Freundschaften · M

- `MessageReceived`, dazu Stummschalten pro Unterhaltung
  (`ConversationParticipant.MutedAt`, Aktion im Kopf des Chats)
- `FriendRequestReceived`, `FriendshipStarted` (auch über den Einladungslink)
- Schalter „Nachrichten“ und „Freundschaften“ erscheinen

*Fertig, wenn:* eine Nachricht in einem stummgeschalteten Gruppenchat den Badge
erhöht, aber nicht vibriert, und eine angenommene Anfrage in der Glocke dessen
steht, der gefragt hat.

### Etappe 4 — Nachweise, Abstimmung, Reaktionen · M

- `ProofAwaitingVote`, `ProofDecided` (auch wenn die 12-Stunden-Frist ohne
  offene App entscheidet), `ReactionReceived` zusammengefasst
- Schalter „Abstimmung fällig“, „Ergebnisse“, „Kudos & Reaktionen“

*Fertig, wenn:* ein hochgeladenes Foto jeden erreicht, der abstimmen darf, der
Besitzer das Ergebnis auch nach Fristablauf erfährt und fünf Kudos
hintereinander eine Zeile ergeben: „Mara und 4 weitere“.

### Etappe 5 — Ziele · S

- `GoalInvitation`, `GoalPaused`, `PauseLifted`
- Schalter „Einladungen & Pausen“

*Fertig, wenn:* ein eingeladener Freund vom Ziel erfährt, eine Pause mit dem Weg
zum Einspruch bei den Teilnehmern ankommt, der Besitzer erfährt, dass zwei
Einsprüche sie beendet haben, und ein einzelner Einspruch stumm bleibt.

---

## 8. Tests

- **Unit:** Zustellregeln (Abschnitt 5c) als reine Funktion, Zusammenfassen
  von Reaktionen, `notificationSentence()` für jede Art in beiden Sprachen (der
  bestehende Katalog-Test fängt fehlende Schlüssel).
- **Integration:** jeder Auslöser erzeugt die richtigen Zeilen, Pushes über
  `RecordingPushSender` und Hub-Ereignisse. Der Hub wird mit einem echten
  SignalR-Client angesprochen, über das echte Cookie aus `SignIn.AsAsync`,
  ohne Fake-Anmeldung. Geprüft von beiden Enden (geseedete Konten).
  Blockieren, Stummschalten, sich selbst, Ruhezeit und „verbunden“ verhindern
  jeweils, was sie sollen. Anonymes Aushandeln ergibt 401. Kontolöschen
  entfernt die Zeilen.
- **Komponente:** eine Glocken-Zeile pro Art, Zahl an der Glocke, die Schalter,
  `data-q2-private`.
- **E2E:** Glocke mit Zahl, Einstellungsbildschirm, Live-Chat mit zwei
  Browser-Kontexten (dafür braucht `globalSetup` einen zweiten angemeldeten
  Zustand), Überlauf und Tippflächen der neuen Bildschirme.
- **Seeds:** Development und ManualTesting bekommen ein paar Glocken-Einträge,
  E2E feste, AutomatedTest das Nötigste. Alles über `SeedBuilder` und
  deterministisch.

---

## 9. Offene Entscheidungen — mit meiner Empfehlung

Ich habe sie im Plan so angenommen. Widersprich, wo du es anders willst.

1. **Glocke mit Zahl.** Revidiert ADR 0019. *Empfehlung: ja*, denn die Glocke
   zählt jetzt, was dich betrifft, nicht was deine Freunde getan haben.
2. **Ein Ort pro Ereignis** (Abschnitt 3a): Nachrichten, Anfragen, Abstimmen
   und Challenge erscheinen nicht zusätzlich in der Glocke. *Empfehlung: ja.*
3. **Schalter steuern den Push, nicht die Glocke** (3c). *Empfehlung: ja*, sonst
   bedeutet derselbe Schalter bei jeder Art etwas anderes.
4. **Den Schalter „Wochenrückblick“ streichen**, bis es einen Wochenrückblick
   gibt. *Empfehlung: ja.*
5. **Stummschalten pro Chat** in Etappe 3. *Empfehlung: ja*, sonst schalten
   Leute wegen einer lauten Gruppe alle Nachrichten ab.
6. **Nachrichtentext in der Push-Nachricht**, und den Sperrbildschirm regelt
   das Betriebssystem. *Empfehlung: ja.*
7. **Einstieg über Profil → Einstellungen → Benachrichtigungen.** Wenn du den
   Eintrag direkt auf dem Profilbildschirm willst, ist das eine zusätzliche
   Zeile.

---

## 10. Bewusst nicht enthalten

- **Native Push (APNs/FCM)** — kommt mit Capacitor, `IPushSender` ist die
  Nahtstelle.
- **E-Mail** — es gibt keinen Mailweg (next-steps #7).
- **Wochenrückblick** — dafür fehlt der Rückblick selbst.
- **Erinnerung kurz vor Ende der Abstimmungsfrist**, „tippt …“, Präsenz über den
  Hub — mit SignalR später möglich, jetzt nicht verlangt.
- **Einblendungen in der App bei Live-Ereignissen** — der Zähler ist das
  Signal. Eine Einblendung pro Kudos wäre genau der Lärm, den das Design
  vermeidet.

---

## 11. Was ich nicht geprüft habe

- **Kein Code geändert.** Diese Datei ist der einzige Zusatz im
  Arbeitsverzeichnis.
- **SignalR mit dem Sitzungscookie** habe ich in diesem Repository nicht
  ausprobiert. Dass es trägt, schließe ich aus der Konfiguration (CORS mit
  Credentials, `SameSite=Lax`, lokal dieselbe Site, auf Staging derselbe
  Origin, Caddy reicht WebSockets durch). Das ist das Erste, was Etappe 2
  bestätigen muss.
- **Push auf echten Geräten** (iOS als Home-Screen-App, Android) ist
  ungetestet, wie schon heute. Lokal gibt es keine VAPID-Schlüssel.
- **Keine Zeitschätzung in Tagen.** S/M/L sind Größenordnungen.

---

## 12. Stand

**Umgesetzt am 14. September 2026**, mit allen sieben Empfehlungen aus
Abschnitt 9 so, wie sie dort stehen. Was davon gilt, steht jetzt auf Englisch in
[docs/adr/0024-one-notification-pipeline.md](docs/adr/0024-one-notification-pipeline.md).
Diese Datei bleibt als Entscheidungspapier stehen und wird nicht weiter
nachgeführt; Abschnitt 11 beschreibt den Stand *vor* der Umsetzung.

Zwei Abweichungen vom Plan:

- Aus `ProofDecided` wurden zwei Arten, `ProofConfirmed` und `ProofRefused`:
  Die Glocke und der Sperrbildschirm sagen beides verschieden, und ein
  abgelehnter Beweis sagt zusätzlich, ob noch ein Versuch bleibt.
- SignalR mit dem Sitzungscookie ist jetzt geprüft, in `LiveHubTests` (echter
  Anmeldeweg, kein Test-Handler) und im E2E-Test über zwei Personen.
