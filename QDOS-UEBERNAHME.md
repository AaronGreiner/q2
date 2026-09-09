# Qdos → q2: Übernahme des Frontends

Stand: 6. September 2026 · Verglichen wurden
`/Users/nathangreiner/Documents/source/q2` (Hauptprojekt, Nuxt 4 + ASP.NET Core)
und `/Users/nathangreiner/Documents/source/Q2_alt` (Privatprojekt „Qdos", Vue 3 +
Quasar + Vite, Commit `22f34d6`).

> **Zur Sprache dieser Datei.** [AGENTS.md](AGENTS.md) verlangt englische
> Dokumentation. Das hier ist bewusst eine Ausnahme: kein Repository-Dokument,
> sondern ein Entscheidungspapier für dich. Was davon Bestand hat, gehört
> anschließend als englischer ADR nach `docs/adr/`.

**Grundlage der Aufgabe (deine Vorgabe):** Die Funktionen aus Qdos werden
übernommen, einschließlich der Tages-Challenge. Das Backend behält den Kern von
q2 und wird nur so weit erweitert, dass alles Neue funktioniert.

---

## 1. Die wichtigste Erkenntnis vorweg

Das sind nicht zwei Frontends desselben Produkts. Es sind **zwei verschiedene
Produkte mit demselben Versprechen.**

| | q2 (Hauptprojekt) | Qdos (Privatprojekt) |
|---|---|---|
| Kernschleife | Ziel anlegen → Aufgaben abhaken → Fortschritt in Schritten → Freunde geben Kudos | To-Do anlegen → Freunde einladen → Beweisfoto → Freunde stimmen ab → Streak steigt oder fällt |
| Wer entscheidet, ob etwas erledigt ist | **du selbst** (Häkchen) | **deine Freunde** (Abstimmung) |
| Was passiert beim Scheitern | nichts — Scheitern existiert nicht | Streak fällt auf null, Bilanz zeigt „verpasst", Freunde werden vorgewarnt |
| Grundton | Wellness, Ermutigung, Belohnung | Verbindlichkeit, Nachweis, Konsequenz |

Daraus folgt zweierlei, und beides zieht sich durch das ganze Dokument:

1. **Es ist keine Portierung, es ist ein Umbau.** Die Frage „welche Datei kopiere
   ich wohin" hat für fast keine Datei eine Antwort (Abschnitt 6). Was übertragen
   wird, sind Entwürfe und Regeln — nicht Code.
2. **Die Geschäftsregeln wechseln die Seite.** In Qdos liegen sie im Frontend
   (`src/domain/`), weil es kein Backend gibt. In q2 dürfen sie dort nicht
   bleiben: Wer im Browser entscheidet, ob eine Abstimmung durchgeht, hat eine
   App gebaut, in der man seine Streak mit den Entwicklerwerkzeugen fälscht.
   `voting.ts`, `risk.ts`, `pause.ts`, `challenge.ts`, `streaks.ts` und
   `schedule.ts` gehören nach C#.

---

## 2. Was Qdos kann und q2 nicht

Sortiert nach Gewicht. „Aufwand" ist grob: **S** = Tage, **M** = ein bis zwei
Wochen, **L** = mehr, inklusive Backend.

| # | Funktion | Wo es in Qdos steckt | Fehlt in q2 | Aufwand |
|---|---|---|---|---|
| 1 | **Beweisfoto**, live in der App aufgenommen, mit Herkunftskennzeichnung (`capturedInApp`) | `chat/CameraCapture.vue`, `domain/image.ts` | vollständig — q2 kennt **keine Bilder und keine Dateiablage** | L |
| 2 | **Abstimmung**: bestätigen/anzweifeln, Zweifel anonym, 12-h-Frist, Aberkennung ab 2 Zweifeln *und* >⅓, genau ein Nachreichversuch | `domain/voting.ts`, `chat/ProofMessage.vue`, `mock/outcome.ts` | vollständig | L |
| 3 | **Zeitfenster** (`TodoInstance`) als eigene Entität: `startsAt`, `dueAt`, `requiredProofs`, `confirmedProofs`, `status` | `types/index.ts`, `mock/maintenance.ts` | vollständig — q2 zählt Schritte, kennt aber keine Fälligkeit mit Ergebnis | L |
| 4 | **Vier Fälligkeitsarten**: einmalig, alle N Tage, feste Wochentage, **X-mal je Woche/Monat** | `domain/schedule.ts`, `todo/SchedulePicker.vue` | teilweise — q2s `GoalRhythm` kennt Daily/Weekdays/Weekly/Once, aber **nicht** „dreimal die Woche" | M |
| 5 | **Streak-Verlust** — die Kette reißt bei einem verpassten Fenster | `domain/streaks.ts`, `mock/outcome.ts` | vollständig. q2s `Streak.Count` leitet aus vorhandenen Tagen ab; ein Tag kann fehlen, aber nichts kann *scheitern* | M |
| 6 | **Vorwarnung** an die eingeladenen Freunde („Wird knapp"), erst ab 20 Uhr, höchstens einmal je Fenster, laufende Abstimmungen zählen als geliefert | `domain/risk.ts`, `home/RiskAlertItem.vue` | vollständig | M |
| 7 | **Bilanz** „47 erledigt · 5 verpasst", betrachterabhängig, nur zwischen Beteiligten | `getBalance` | vollständig | M |
| 8 | **Tages-Challenge**: gemeinsamer Anstoß, Raum nur mit eigenen Freunden, verdeckt bis zum eigenen Beitrag, eigenes Archiv | `domain/challenge.ts`, `pages/Challenge*.vue`, `home/ChallengeBanner.vue` | vollständig | L |
| 9 | **Pause**: 1–7 Tage, Pflichtbegründung, 2 je Monat, anonymer Einspruch | `domain/pause.ts`, `chat/PauseDialog.vue`, `chat/PauseBanner.vue` | vollständig | M |
| 10 | **Abschließen und Archiv** — aufhören, ohne die eigene Bilanz zu vernichten | `completeTodo`, `pages/ArchivePage.vue` | vollständig | S |
| 11 | **Wisch-Feed**: Karte folgt dem Finger, Neigung, farbige Vorschau, Haptik | `feed/ProofCard.vue`, `feed/FeedStack.vue` | vollständig — q2s Feed ist eine Liste von Textzeilen | M |
| 12 | **Reaktionen** (Feuer/Stark/Applaus) auf Beweisbilder **und** Challenge-Beiträge | `chat/ReactionBar.vue` | teilweise — q2 hat Reaktionen nur auf Chatnachrichten | S |
| 13 | **Melden und Blockieren**, beidseitig, mit Verwaltungsseite | `common/ReportDialog.vue`, `pages/BlockedUsersPage.vue` | vollständig | M |
| 14 | **Profil bearbeiten**, Avatarbild hochladen und entfernen | `pages/EditProfilePage.vue` | vollständig — q2 sagt auf dem Einstellungsbildschirm selbst, dass es das nicht gibt | M |
| 15 | **Fremdes Profil** über jedes Profilbild, mit gemeinsamen Freunden | `pages/PublicProfilePage.vue` | vollständig | S |
| 16 | **Aktivitätsübersicht** als eigene Seite mit Glocke im Kopf | `pages/ActivityPage.vue` | teilweise — q2 hat einen Freundes-Feed auf der Startseite, aber keine Ereignisliste für einen selbst | S |
| 17 | **Chatverwaltung**: anheften, stumm schalten, verlassen, Chat-Info, Galerie, Verlaufs-Raster, Nachricht ändern/löschen mit „bearbeitet"-Vermerk | `pages/ChatInfoPage.vue`, `pages/GalleryPage.vue`, `todo/HistoryGrid.vue` | vollständig | M |
| 18 | **Wochenrückblick** auf der Startseite | `home/WeekSummaryCard.vue` | teilweise — q2 zeigt die Woche als sieben Kästchen, aber ohne Zahlen | S |
| 19 | Kategorien (Sport, Lernen, Lesen, Achtsamkeit, Sonstiges) | `domain/labels.ts` | teilweise — q2 hat zwölf freie Symbole statt Kategorien | S |
| 20 | Haptik, Ziehen zum Aktualisieren, Skelett-Ladezustände | `domain/haptics.ts`, `common/ListSkeleton.vue` | teilweise — Skelette gibt es, Haptik und Pull-to-Refresh nicht | S |

**Und eine Lücke, die beide haben:** Onboarding. Qdos benennt sie selbst als
Punkt 1 der eigenen Roadmap — ohne Freunde lässt sich kein To-Do erstellen, ein
neuer Nutzer steckt fest. Das wird durch die Übernahme nicht besser, sondern
schlimmer, weil q2 zusätzlich eine Registrierung davor hat. Siehe Abschnitt 9,
Etappe 8.

---

## 3. Was verloren ginge — deine eigentliche Frage

Ich trenne hier scharf zwischen zwei Dingen, weil sonst die Antwort erschreckender
klingt, als sie ist.

### 3a. Was *nicht* verloren geht (und auch gar nicht kann)

Alles, was q2 unter der Oberfläche kann, ist produktneutral und bleibt
unangetastet. Es ist zugleich das, was Qdos komplett fehlt:

- **Ein echtes Backend** — ASP.NET Core, EF Core, Migrationen, sechs Umgebungen,
  Seeds, geschützter Reset. Qdos hat eine Attrappe im Arbeitsspeicher; beim
  Neuladen ist alles weg.
- **Anmeldung** — Identity, Registrierung, http-only-Sitzungscookie, Sperre nach
  Fehlversuchen, globaler Routen-Wächter. Qdos hat *keine Anmeldung*, sondern
  eine Konstante `CURRENT_USER_ID`.
- **Zugriffsschutz** — jeder Lesevorgang ist auf den Anfragenden zugeschnitten.
  Genau das braucht die Bilanz und der Challenge-Raum später (Abschnitt 7).
- **OpenAPI-Vertrag + generierte TypeScript-Typen**, beide eingecheckt und von CI
  geprüft.
- **Tests**: Unit, Komponente, Integration, Migration, E2E in `mobile-chromium`,
  Barrierefreiheit mit axe.
- **Sentry** in Front- und Backend, mit Datenschutzfiltern, Session Replay und
  Feedback-Dialog.
- **CI/CD**, Deployment mit Health-Gate und Rollback.
- **PWA**: Manifest, Service Worker, Icons, Offline-Seite.
- **Zwei Sprachen**, Nachrichtenkatalog, Barrierefreiheit als Regel statt als
  Zufall, normalisierte Fehlerbehandlung mit Vorgangsnummer.

Das ist ungefähr die Hälfte des Werts von q2, und sie ist von der Produktfrage
gar nicht berührt.

### 3b. Was tatsächlich zur Disposition steht

Sechs Dinge. Für jedes meine Empfehlung.

| Was | Was es ist | Empfehlung |
|---|---|---|
| **Kudos** — die namensgebende Geste | Auf einem Aktivitätseintrag eines Freundes Anerkennung geben; wird gezählt und im Profil angezeigt | **Behalten, aber verschmelzen.** Qdos' Reaktionen (Feuer/Stark/Applaus) sind dieselbe Idee mit drei Ausprägungen statt einer. Nimm die drei Ausprägungen, nenn das Ergebnis weiterhin „Kudos" und zähl es im Profil weiter. Der Markenname bleibt damit gedeckt — sonst heißt die App nach einer Geste, die es nicht mehr gibt. |
| **Abzeichen** (6 Stück, Sammlung im Profil) | Streak-Held, Frühaufsteher, Bücherwurm, Kudos-Geber, Marathon, Wochensieger | **Behalten, aber später.** Kollidiert mit nichts, kostet wenig, und ein Profil braucht etwas zum Anschauen. Zwei der sechs Kriterien (Bücherwurm, Marathon) hängen an Zielen mit Schritten und müssten neu definiert werden. Kein Blocker, aber auch nichts für Etappe 1. |
| **Bestenliste** (Wochenranking) | `GET /api/leaderboard`, Karte auf der Startseite | **Streichen.** Und das ist die einzige Empfehlung hier, bei der ich dir wirklich widerspreche, falls du sie behalten willst: Eine Rangliste widerspricht Qdos' tragendem Prinzip „kein Nachtreten". Qdos hat sich bewusst gegen die nachträgliche Bloßstellung entschieden und die Bilanz betrachterabhängig gemacht, damit ein Scheitern nur die Beteiligten etwas angeht. Eine Tabelle, die alle Freunde nach Leistung sortiert, hebelt beides an einer Stelle aus. Wenn du sie behalten willst, dann nur über *positive* Größen (gegebene Kudos, Challenge-Teilnahmen) und niemals über Verpasstes. |
| **Ziele mit Schritten** („14 von 21 Läufen", Fortschrittsring, `contribute`) | `Goal.CompletedSteps/TotalSteps` | **Entfällt.** Qdos' Zeitfenster beantworten dieselbe Frage ehrlicher: „3 von 4 diese Woche" ist ein Zeitraum mit Ergebnis, „14 von 21" ist ein Zähler, den man selbst hochdreht. Zwei Fortschrittsbegriffe nebeneinander wären der sichere Weg in eine Oberfläche, die niemand mehr erklärt bekommt. Der Fortschrittsring (`AppProgressRing`) bleibt trotzdem nützlich — für „2 von 3 Nachweisen". |
| **Aufgaben unter einem Ziel** (`GoalTask` mit eigenem Rhythmus und Häkchen) | Der „Heute"-Tab auf dem Ziele-Bildschirm | **Entfällt zunächst — und das ist der schmerzhafteste Posten.** Qdos kennt keine Unteraufgaben; ein To-Do ist atomar. Eine Aufgabe abzuhaken, ohne dass jemand sie bestätigt, ist genau die Selbstauskunft, die Qdos abschaffen will. Aber: Damit verliert die App die einzige Interaktion, die *keine* soziale Hürde hat. Wenn sich herausstellt, dass die App ohne einen leisen Modus zu anstrengend ist, ist das der Baustein, den du zurückholst. |
| **Freie Direkt- und Gruppenchats** (unabhängig von einem Ziel) | `POST /api/chats/direct`, `/groups` | **Unbedingt behalten.** In Qdos gibt es einen Chat nur zu einem To-Do — deshalb ist die App am ersten Tag leer. q2 hat den freien Chat bereits gebaut und getestet. Er ist die billigste vorhandene Antwort auf Qdos' größte offene Frage. Wegwerfen wäre der einzige Punkt dieser Liste, den ich für einen echten Fehler halten würde. |

**Zusätzlich zur Disposition, falls das Qdos-Design gewinnt** (Abschnitt 5): das
helle Design und, mit ihm, ein Teil der Barrierefreiheitsarbeit. Qdos ist reines
Schwarz und hat helles Design in den Einstellungen sichtbar *deaktiviert*. Meine
Empfehlung dazu steht in Abschnitt 5c: dunkel als Voreinstellung, aber nicht
dunkel als einzige Möglichkeit.

---

## 4. Wo sich die beiden Datenmodelle berühren

Bevor irgendetwas gebaut wird, muss klar sein, was zu was wird. Die gute
Nachricht: Es gibt mehr Deckung, als die Namen vermuten lassen.

| Qdos | q2 heute | Was daraus wird |
|---|---|---|
| `Profile` | `Person` | **Erweitern** um `username`, `bio`, `avatarUrl` und den zweiten Streak (Qdos hat Aktivitäts- *und* Community-Streak, q2 nur einen) |
| `Friendship` (`pending`/`accepted`/`blocked`) | `Friendship` (zweiseitig) | **Passt.** `blocked` kommt als eigene Entität dazu, nicht als Status — Blockieren beendet die Freundschaft, es ist kein Zustand von ihr |
| `Todo` | `Goal` | **Passt im Kern.** `Goal` hat bereits Besitzer, Teilnehmer, Symbol, Erinnerungszeit, Zieldatum. Es verliert `CompletedSteps`/`TotalSteps` und bekommt `Schedule` statt `GoalRhythm` |
| `Schedule` (4 Arten) | `GoalRhythm` (4 Werte) | **Ersetzen.** `GoalRhythm` ist ein Aufzählungstyp, `Schedule` ein Summentyp mit Nutzlast (`everyDays`, `weekdays[]`, `times`+`period`). Das ist eine Migration, keine Umbenennung |
| `TodoInstance` | — | **Neu.** Das Herzstück. Alles Weitere (Streak, Bilanz, Verlauf, Vorwarnung, Pause) hängt daran |
| `ProofPhoto`, `Vote` | — | **Neu**, samt Bildablage |
| `Reaction` (`targetId`) | `MessageReaction` | **Verallgemeinern.** Qdos' `targetId` zeigt schon heute wahlweise auf ein Beweisbild oder einen Challenge-Beitrag; q2s Reaktion hängt fest an einer Nachricht |
| `ChatMessage` (`text`/`system`/`beweisbild`) | `ChatMessage` | **Erweitern** um die beiden zusätzlichen Arten |
| `ChatOverview` | Konversationsliste | **Passt**, plus `awaitingYourVote`, `paused`, `pinned`, `muted` |
| `ActivityEntry` (7 Arten) | `ActivityEvent` (4 Arten) | **Erweitern.** q2s Modell ist die bessere Grundlage: Es speichert Art + Subjekt + Zahl statt eines fertigen Satzes und ist damit übersetzbar. Qdos speichert deutsche Sätze — das wäre in einer zweisprachigen App ein Rückschritt |
| `Challenge`, `ChallengeEntry` | — | **Neu** |
| `TodoPause`, `PauseVeto` | — | **Neu** |
| `UserBlock`, `Report` | — | **Neu** |
| `Balance`, `WeekSummary`, `RiskAlert`, `HistoryEntry`, `FeedItem`, `VoteSummary`, `ChallengeRoom` | — | **Keine Tabellen** — das sind Sichten, die der Server je Betrachter zusammenstellt. Wichtig, weil ihr Zuschnitt die Datenschutzregel *ist* (Abschnitt 7) |

Ein Detail mit Folgen: Qdos' Domänenwerte sind deutsch (`'bestaetigt'`,
`'wochentage'`, `'verpasst'`). q2s Regel lautet „Code ist Englisch, nur
Oberflächentext ist Deutsch". Beim Übertragen werden daraus `Confirmed`,
`Weekdays`, `Missed` — und die deutschen Wörter wandern in
`app/app/i18n/messages.ts`. Das klingt nach Kleinkram, betrifft aber jede
Fallunterscheidung in beiden Sprachen.

---

## 5. Welches Design ist besser?

Ich habe beide Designs am Code und an ihrer jeweiligen Design-Dokumentation
beurteilt, nicht an Bildschirmfotos (siehe Abschnitt 11).

### 5a. Was jedes von beiden richtig macht

**Qdos** ist das mutigere und, für dieses Produkt, das richtigere Design.

- **Reines Schwarz.** Nicht Geschmack, sondern Funktion: Sobald Fotos das
  Hauptmaterial sind, braucht die Fläche drumherum Ruhe. Ein Foto auf einer
  weißen Karte wird zur Briefmarke; auf Schwarz wird es zum Bild. BeReal,
  Letterboxd und Instagrams Vollbildansichten machen es aus demselben Grund.
- **Eine einzige Regel für die Akzentfarbe**, und sie ist gut: *„Ist das eine
  Handlung, die der Nutzer jetzt ausführen kann?"* Nur dann Akzent. Zustände
  bekommen keinen. Diese Regel wurde nachträglich durchgesetzt — 67 Fundstellen
  in 29 Dateien zurückgebaut. Das ist mehr Design-Disziplin, als die meisten
  Produkte je aufbringen.
- **Drei Farben mit fester Bedeutung** (Akzent = Handlung, Flamme = nur die
  Streak, Rot = nur Endgültiges), sonst kommt Farbe ausschließlich aus Fotos.
- **Keine Emoji.** Material-Symbole stattdessen. Das ist in einer App über
  Verbindlichkeit die richtige Entscheidung.
- Kräftige, eng laufende Überschriften (27 px, 800, −0,7 px). Redaktionell statt
  freundlich.

**q2** ist das sauberere und handwerklich reifere Design.

- **Zwei Themes**, beide vollständig durchdekliniert, mit **dokumentierten
  Kontrastwerten je Farbwert** (5,25:1 auf Hintergrund, 5,48:1 auf Karten). Das
  ist eine Qualitätsstufe über „sieht gut aus".
- **Eine Radienskala mit vier Werten** und einer geschriebenen Begründung, warum
  `rounded-xl` in diesem Projekt eine Falle ist.
- **Ein Ausdrucksmoment, bewusst gesetzt**: die orange Streak-Karte, „the one
  deliberately loud thing in the app". Die Begründung dafür ist genau die Art
  von Entscheidung, die ein Design zusammenhält.
- Systematische Barrierefreiheit, in Komponententests geprüft.
- Sorgfalt an den Rändern, die man nur durch echtes Benutzen findet: die
  Tastatur-Einrückung, `--q2-safe-bottom` als *Obergrenze statt Zielwert*, der
  abgeschaltete Doppeltipp-Zoom.

Wo q2 schwächer ist: Es sieht aus wie eine gute Gesundheits-App. Grün,
abgerundet, freundliche Karten, `👋` und `🔥` als Emoji in der Oberfläche. Das
ist genau der Ton, den „Do it or Shame it" nicht haben darf. Ein Produkt, das
dir sagt, dass deine Freunde von deinem Scheitern erfahren, kann nicht wie eine
Meditations-App aussehen.

### 5b. Meine Empfehlung

**Geh in die Richtung von Qdos — und nimm q2s Handwerk mit.**

Konkret: die **Design-Sprache** von Qdos (schwarz, ein Akzent, keine Emoji,
harte Typografie, Foto zuerst), umgesetzt in der **Token- und Testinfrastruktur**
von q2 (alle Farben in `main.css`, Kontrastwerte dokumentiert, Radienskala,
`data-q2-private`, Komponententests, Barrierefreiheitsregeln).

Was q2 dabei ausdrücklich *behält*:

- **Die Radienskala.** Qdos hat drei Radien (10/16/22 px), q2 vier (10/13/18/22).
  q2s Skala ist feiner und hat die dokumentierte Begründung. Behalten.
- **Public Sans** statt Systemschrift. Eine eigene Schrift ist der billigste
  Unterschied zwischen „App" und „Website", sie ist bereits selbst gehostet, und
  sie kann eng und fett — was Qdos' Typografie verlangt.
- **Lucide-Symbole**, gebündelt. Qdos benutzt Material-Symbole aus Quasar; die
  kommen mit Quasar ohnehin nicht mit.
- **Die orange Streak-Karte.** Qdos' Flammenverlauf (`#ffcc33` → `#ff4d2d`) und
  q2s Streak-Held (`#f59e0b` → `#ef6c00`) sind dieselbe Idee. Auf Schwarz wirkt
  sie noch stärker. Behalten, ohne das Emoji im Hintergrund.

### 5c. Hell und dunkel

Qdos ist dunkel-**only**, q2 kann beides. Meine Empfehlung: **dunkel als
Voreinstellung, hell als gepflegte Möglichkeit.** Gründe:

- Das dunkle Design ist die gestalterische Aussage, das helle ist die
  Zugänglichkeit. Manche Menschen lesen auf Schwarz schlecht (Astigmatismus ist
  häufiger als man denkt), und draußen bei Sonne ist Hell überlegen.
- Der Umschalter ist schon gebaut, getestet und im Kopf der Startseite sichtbar.
  Ihn zu entfernen kostet Arbeit; ihn zu behalten kostet nur, dass die neuen
  Farbwerte zweimal gesetzt werden.
- **Aber**: hell darf nicht Bremse sein. Wenn ein Bildschirm nur dunkel
  funktioniert, ist er dunkel — festgehalten in `main.css`, nicht stillschweigend.

Technisch heißt das: `:root` in `app/app/assets/css/main.css` trägt künftig die
Qdos-Palette, `.dark` bleibt, und die Voreinstellung in `useAppSettings` wechselt
auf Dunkel. Zwei Dinge müssen mitwandern, sonst blitzt beim Start die alte Farbe
auf: `app/app/utils/themeColors.ts` und die `background_color`/`theme_color` im
Manifest in `nuxt.config.ts`.

### 5d. Die Akzentfarbe

Qdos' Limette `#d7ff3e` ist laut eigener Dokumentation **ein Platzhalter**, und
du sollst sie festlegen. Zwei Hinweise dazu, beide aus den Notizen des
Privatprojekts:

- Eine Akzentfläche braucht **dunkle** Schrift. Weiß auf Limette hat etwa 1,3:1
  — der Annehmen-Knopf in der Suche war dadurch monatelang unsichtbar.
- Ein **deaktivierter** Hauptknopf gehört als Umriss dargestellt, nicht als
  blasse Akzentfläche.

Mein Rat: Nimm Limette als Ausgangspunkt (sie passt zum schwarzen, jungen,
BeReal-nahen Ton und ist maximal unverwechselbar gegenüber dem Grün, das jede
zweite Habit-App benutzt), aber prüfe sie einmal formal — Akzent auf Schwarz,
Schwarz auf Akzent, Akzent als 1-px-Rahmen — und schreib die drei Werte in
`main.css`, so wie es dort mit den heutigen Werten schon gemacht ist.

---

## 6. Der Übertragungsplan für das Frontend

### 6a. Warum fast nichts kopiert werden kann

Die beiden Frontends teilen nur die Sprache Vue. Alles darüber ist verschieden:

| | Qdos | q2 |
|---|---|---|
| Rahmen | Vite-SPA, Vue Router | Nuxt 4, dateibasiertes Routing, serverseitig gerendert |
| Bausteine | Quasar 2 (`q-page`, `q-btn`, `q-tabs`, `$q.notify`) | Nuxt UI (`UButton`, `UModal`, `useToast`) |
| Gestaltung | SCSS, `scoped`, BEM mit `qdos-`-Präfix | Tailwind v4 Utilities + `@utility`-Klassen |
| Zustand | Pinia-Stores | `useState`, `useAsyncData`, Composables |
| Daten | `dataSource`-Interface auf einer Attrappe | typisierter HTTP-Client auf einem echten Vertrag |
| Text | deutsche Zeichenketten direkt im Template | ausschließlich aus `i18n/messages.ts`, zweisprachig |

Jede `.vue`-Datei wird also **neu geschrieben**. Was übertragen wird, ist der
Entwurf: Aufbau, Reihenfolge, Wortlaut, Gestenverhalten, Randfälle — und die
Begründungen, die in `UEBERGABE.md` stehen. Die sind der eigentliche Wert; sie
sind das Ergebnis mehrerer Durchgänge auf 375 px.

### 6b. Was doch fast unverändert wandert

| Aus Qdos | Wohin | Anmerkung |
|---|---|---|
| `domain/labels.ts` | `app/app/utils/display.ts` | reine Darstellung: Kategorienamen und Symbole. Namen ins Englische, Texte nach `messages.ts` |
| `domain/datetime.ts` | `app/app/utils/display.ts` | q2 hat dort schon Verwandtes |
| `domain/image.ts` | `app/app/utils/` (neu) | Bildverkleinerung vor dem Hochladen — gehört zwingend auf den Client |
| `domain/haptics.ts` | `app/app/utils/` (neu) | funktioniert auf Android, nicht in iOS-Safari |
| `domain/schedule.ts`, `voting.ts`, `risk.ts`, `pause.ts`, `challenge.ts`, `streaks.ts` | **nach C#**, `api/src/Q2.Api/Features/…` | siehe Abschnitt 1. Der Vue-freie Zuschnitt macht die Übersetzung leicht — die Dateien lesen sich fast wie eine Spezifikation |
| `types/index.ts` | **nirgendwohin** | die Typen entstehen künftig aus dem OpenAPI-Vertrag. Als *Vorlage für die C#-DTOs* ist die Datei aber Gold wert |

### 6c. Komponenten: was existiert, was erweitert wird, was neu ist

**Existiert in q2 und passt (nur umgestalten):**
`AppAvatar` (braucht Bildunterstützung statt nur Initialen) · `AppBottomNav` ·
`AppScreenHeader` · `AppConfirmDialog` · `AppSearchField` · `AppSegmented` ·
`AppToggle` · `AppStateMessage` · `AppErrorState` · `AppProgressBar` ·
`AppProgressRing` · `ChatListRow` · `ChatBubble` · `ChatComposer` ·
`PersonSearchRow` · `FriendRow` · `FriendRequestRow` · `SentRequestRow` ·
`FriendSuggestionRow` · `SettingsSection` · `SettingsToggleRow` ·
`SettingsActionRow` · `BadgeGrid` · `AuthScreen`

**Existiert, muss aber inhaltlich erweitert werden:**

| q2-Komponente | Was dazukommt |
|---|---|
| `GoalCard` / `GoalTile` | Fälligkeit statt Schritte, Streak-Flamme, Pausen-Hinweis |
| `ChatListRow` | `awaitingYourVote` („wartet auf deine Stimme", der einzige Ort mit Akzent in der Liste), ungelesen, angeheftet, stumm, pausiert |
| `ChatGoalBanner` | Pausenbanner mit Begründung und Einspruch |
| `ActivityRow` | drei neue Ereignisarten, Datumstrenner |
| `TodayProgressCard` | Wochenrückblick mit Zahlen (erledigt/verpasst/offen) |
| `StreakHero` | Emoji raus, Flammenverlauf rein, auf Schwarz gesetzt |

**Neu zu bauen — nach Aufwand sortiert:**

| Komponente | Vorlage in Qdos | Warum sie schwierig ist |
|---|---|---|
| `ProofSwipeCard` + `ProofSwipeStack` | `feed/ProofCard.vue`, `feed/FeedStack.vue` | Zeigergesten mit Pointer-Capture, Neigung, Schwelle bei 96 px, Rückfedern, geschluckte Klicks nach dem Wischen. Die Vorlage ist vollständig und sauber — abschreiben, nicht neu erfinden |
| `ProofCamera` | `chat/CameraCapture.vue` | `getUserMedia`, Ausweichweg über Dateiauswahl, Kennzeichnung „nicht live aufgenommen". In Qdos **ungetestet**, weil der Entwicklungsrechner keine Kamera hatte |
| `SchedulePicker` | `todo/SchedulePicker.vue` | zweistufig mit Vorschau, vier Arten |
| `ProofMessage` | `chat/ProofMessage.vue` | Bild + Abstimmung + Herkunft + Menü in einer Blase; Stimme jederzeit änderbar |
| `HistoryGrid` | `todo/HistoryGrid.vue` | Verlaufsraster, ohne Akzentfarbe (siehe 5a) |
| `ChallengeEntryCard` | gleichnamig | offen und verdeckt, zwei Zustände |
| `PauseDialog` / `PauseBanner` | gleichnamig | Pflichtbegründung, Kontingent, anonymer Einspruchsstand |
| `RiskAlertRow` | `home/RiskAlertItem.vue` | führt in den Gruppenchat, nie zu einer Reaktionsfläche |
| `ChallengeBanner`, `ChallengeArchiveTile`, `GalleryTile`, `ImageViewer`, `ReportDialog`, `StreakFlame`, `StreakRow`, `StatTile`, `CategorySelector`, `FriendSelector`, `ProofUploadButton` | gleichnamig | jeweils überschaubar |

**Ungefährer Umfang:** rund 24 vorhandene Komponenten umgestalten, 6 erweitern,
etwa 20 neu — dazu 17 statt heute 11 Seiten. Das ist das ganze Frontend, plus
etwa die Hälfte noch einmal.

### 6d. Die Navigation ist eine eigene Entscheidung

| | q2 heute | Qdos |
|---|---|---|
| Tableiste | Home · Ziele · Chats · Freunde · Profil | Home · Suche · **Neu (+)** · Chats · Profil |

In Qdos gibt es keinen Ziele-Tab, weil ein To-Do **sein Chat ist** — die
Chatliste *ist* die Liste der Verpflichtungen, getrennt nach „Deine To-Dos" und
„Von Freunden". Und Freunde haben keinen eigenen Tab, weil man sie in der Suche
findet; eingegangene Anfragen stehen dort oben.

**Empfehlung: Qdos' Leiste übernehmen.** Sie hat den Erstellen-Knopf an der
Stelle, an der der Daumen ohnehin ist, und sie kommt ohne einen Tab aus, der
dasselbe zweimal zeigt. q2s Freunde-Bildschirm geht dabei nicht verloren — er
wird der obere Teil der Suche.

**Aber ein Vorbehalt**, weil du die freien Chats behältst (Abschnitt 3b): Dann
enthält die Chatliste zweierlei — Verpflichtungen und normale Gespräche. Qdos'
Zweiteilung wird zur Dreiteilung („Deine To-Dos" · „Von Freunden" ·
„Unterhaltungen"), oder die freien Chats bekommen einen eigenen Reiter darin.
Das ist die eine Stelle, an der du beide Produkte tatsächlich nebeneinander
sehen wirst.

---

## 7. Was das Backend dazubekommen muss

Der Kern bleibt, wie du es festgelegt hast: ASP.NET Core, EF Core, SQLite,
Feature-Ordner, Invarianten im Modell, jeder Lesevorgang auf den Anfragenden
zugeschnitten. Dazu kommt Folgendes.

### 7a. Neue Entitäten

Unter `api/src/Q2.Api/Features/`:

- **`Goals/GoalSchedule`** — der Summentyp mit vier Arten. Als eigenes Objekt
  (Owned Entity) am Ziel, nicht als vier nullbare Spalten.
- **`Goals/GoalInstance`** — das Zeitfenster. `StartsAt`, `DueAt`,
  `RequiredProofs`, `ConfirmedProofs`, `Status`. Alles andere hängt daran.
- **`Proofs/ProofPhoto`**, **`Proofs/Vote`** — Nachweis und Abstimmung, mit den
  Regeln aus `domain/voting.ts` als Methoden am Modell.
- **`Goals/GoalPause`**, **`Goals/PauseVeto`**.
- **`Challenges/Challenge`**, **`Challenges/ChallengeEntry`**.
- **`People/Block`**, **`Moderation/Report`**.
- **`Reactions/Reaction`** — mit `TargetId` statt fester Nachrichtenbindung.

Erweitert werden `Person` (Benutzername, Bio, Avatar, zweiter Streak), `Goal`
(Kategorie, Zeitplan statt Rhythmus), `ChatMessage` (zwei neue Arten),
`ActivityKind` (sieben statt vier Arten).

### 7b. Bildablage — das größte einzelne neue Stück

q2 speichert heute **keine einzige Datei**. Avatare sind Initialen auf einer
Farbe. Beweisfotos und Challenge-Beiträge ändern das grundlegend.

**Empfehlung:** ein `IImageStore` mit einer Dateisystem-Umsetzung auf dem
Staging-Host — dieselbe Haltung wie „SQLite zuerst"
([docs/adr/0004-sqlite-first.md](docs/adr/0004-sqlite-first.md)): das
Einfachste, was trägt, hinter einer Schnittstelle, die den Wechsel auf einen
S3-kompatiblen Speicher später zu einer Konfigurationssache macht.

Drei Regeln, die von Anfang an gelten müssen:

1. **Kein Bild bekommt eine öffentliche URL.** Ausgeliefert wird über einen
   Endpunkt, der die Sitzung prüft und den Zuschnitt anwendet — genau wie jeder
   andere Lesevorgang in q2. Eine erratbare Adresse in einem öffentlichen Ordner
   wäre die eine Stelle, an der der ganze Zugriffsschutz der App umgangen würde.
2. **Kein Bild darf in einen Cache.** Der Service Worker speichert
   ausschließlich Build-Ergebnisse, und das ist kein Detail, sondern der ADR
   ([docs/adr/0012-installable-pwa.md](docs/adr/0012-installable-pwa.md)). Die
   Muster in `nuxt.config.ts` nennen `png` und `svg` — die Bildendpunkte müssen
   außerhalb des Precache-Pfades liegen, sonst rutschen fremde Fotos hinein.
3. **Größe und Art werden serverseitig geprüft**, nicht nur im Browser
   verkleinert. Und es braucht ein Höchstmaß je Nutzer, sonst ist der erste
   Speicherplatzausfall eine Frage der Zeit.

### 7c. Der Wartungsjob

Qdos' `mock/maintenance.ts` läuft bei jedem Lesezugriff und erledigt drei Dinge:
abgelaufene Abstimmungen schließen, überfällige Fenster als verpasst markieren,
neue Fenster anlegen. In q2 wird daraus ein `BackgroundService`.

Zwei Anforderungen: **idempotent** (zweimal laufen darf nichts doppelt tun) und
**nachholend** (nach einem Ausfall von zwei Tagen müssen beide Tage sauber
aufgearbeitet werden). Am besten mit einer festgehaltenen Marke „bis wann
verarbeitet", nicht mit „was ist seit dem letzten Tick passiert".

Er trägt außerdem die Vorwarnung — und damit die Regel „erst ab 20 Uhr,
höchstens einmal je Fenster".

### 7d. Zeitzonen werden jetzt zum Problem

q2 speichert und rechnet in UTC und merkt sich nichts pro Person; das ist als
Lücke bereits in [docs/next-steps.md](docs/next-steps.md) notiert. Qdos rechnet
mit lokaler Mitternacht.

Sobald eine Frist „Mitternacht" heißt, eine Warnung „ab 20 Uhr" und eine
Challenge „für alle gleichzeitig", ist das keine Nebensache mehr: Es entscheidet,
ob jemand seine Streak verliert. **Spätestens mit dem Zeitfenster (Etappe 2)
braucht `Person` eine Zeitzone.** Für einen deutschsprachigen Start genügt eine
feste Zone in der Konfiguration — aber sie muss von Anfang an *irgendwo* stehen
und nicht implizit „was der Server gerade meint" sein.

### 7e. Was der Server entscheiden muss, nicht der Client

Diese sechs Zuschnitte sind Datenschutz- und Fairnessregeln, keine
Darstellungsfragen. Sie gehören in den Service, nicht in die Seite:

1. **Zweifel bleiben anonym** — die API gibt die Namen der Zweifelnden gar nicht
   erst heraus. Dasselbe für Einsprüche gegen eine Pause.
2. **Die Bilanz ist betrachterabhängig** — sie deckt nur gemeinsame To-Dos ab.
3. **Der Challenge-Raum enthält nur Freunde des Betrachters** — Qdos hat das
   ausdrücklich in `getChallengeRoom` gelegt, „damit keine Ansicht ihn
   versehentlich umgehen kann". Genau so.
4. **Das Challenge-Archiv enthält nur eigene Beiträge** — und geht deshalb von
   den eigenen Beiträgen aus, nicht von den Challenges. Es gibt keinen Filter,
   den man vergessen kann.
5. **Nur der Ersteller** lädt Nachweise hoch, bearbeitet und pausiert.
6. **Abstimmungsschwellen und Fristen** werden serverseitig ausgewertet.

### 7f. Datenschutz und Moderation

Mit nutzergenerierten Bildern wird aus einer Übungs-App eine mit Pflichten.
[docs/privacy.md](docs/privacy.md) und
[docs/adr/0005-observability-and-sentry.md](docs/adr/0005-observability-and-sentry.md)
müssen mitwachsen:

- **Sentry Session Replay zeichnet jede Sitzung auf.** Fotos sind das
  Persönlichste, was die App je enthalten wird. Jedes Bild braucht
  `data-q2-block`, und zwar bevor der erste Upload möglich ist — nicht danach.
- **Löschen.** q2 hat heute keinen Weg, ein Konto zu löschen; das ist bereits
  als Lücke mit gesetzlicher Frist notiert. Mit Fotos wird sie dringend.
- **Melden** braucht einen Empfänger. Eine Meldung, die in einer Tabelle landet,
  die niemand ansieht, ist schlimmer als keine.

---

## 8. Stolperfallen

### Aus diesem Repository

- **`app/app/` ist kein Tippfehler.** Nuxt 4 legt den Quellcode dort ab.
- **Komponentennamen kommen aus dem Dateinamen**, nicht aus dem Ordner
  (`pathPrefix: false`). Dateinamen müssen im ganzen Baum eindeutig sein — bei
  20 neuen Komponenten ist das eine echte Einschränkung.
- **`app/app/api/generated/schema.d.ts` ist generiert.** Nach jeder Änderung an
  Endpunkt oder DTO `bun run api:openapi`; Vertrag und Typen gehören in dieselbe
  Änderung.
- **Radien nur aus `--q2-radius-*`.** `rounded-xl` sind in diesem Projekt 36 px.
- **Keine Zeichenkette für Nutzer steht in einer Komponente.** Alles nach
  `i18n/messages.ts` — **in beiden Sprachen**. Qdos ist einsprachig deutsch; das
  ist bei jedem übernommenen Text zusätzliche Arbeit.
- **Jede neue persönliche Anzeige braucht `data-q2-private` oder `data-q2-block`
  plus einen Komponententest.**
- **Sentry wird außerhalb der Produktion nicht abgeschaltet.**
- **`useAsyncData` liefert eine flache Ref** — immer die ganze Nutzlast
  ersetzen, nie ein Feld darin.
- **Die Entwicklungsdatenbank wird nie automatisch zurückgesetzt**, und
  `bun run db:reset` verweigert das in Development. Absicht.
- **Integrationstests nutzen echtes SQLite**, nie den In-Memory-Provider.
- **390 × 844 ist die Prüffläche.** Ein grüner E2E-Lauf ist kein Blick auf das
  Layout.
- **Kein Commit ohne ausdrückliche Bitte** ([CLAUDE.md](CLAUDE.md)).
- **`bun run validate`** ist das Tor. Nichts gilt als fertig, was es nicht
  passiert hat.

### Aus dem Privatprojekt

- **Die Kamera ist ungetestet.** In Qdos gab es keine am Entwicklungsrechner.
  Für die E2E-Tests braucht Chromium `--use-fake-device-for-media-stream`, und
  auf einem echten Gerät muss es einmal von Hand geprüft werden.
- **Haptik funktioniert nicht in iOS-Safari.** Kein Grund, sie wegzulassen —
  aber kein Rückmeldeweg, auf den man sich verlassen darf.
- **Routenreihenfolge**: `/chats/archiv` muss vor `/chats/:todoId` stehen. In
  Nuxt gilt dasselbe über die Dateinamen (`archiv.vue` vor `[id].vue` — Nuxt
  ordnet statische Segmente selbst vor, aber prüfen).
- **Seiten mit Routenparameter brauchen ein `watch` darauf.** In Qdos war das
  ein echter Fehler: Beim Wechsel blieben die alten Daten stehen. In Nuxt
  erledigt `useAsyncData` mit `watch` das — aber nur, wenn man es hinschreibt.
- **Der Akzent ist knapp zu halten.** Die Prüffrage lautet: *Ist das eine
  Handlung, die der Nutzer jetzt ausführen kann?* Sonst kein Akzent. Diese Regel
  wurde in Qdos einmal an 67 Stellen zurückgebaut — sie wieder zu verlieren
  wäre der wahrscheinlichste Weg, das Design zu ruinieren.
- **Kein Nachtreten.** Ein fertiges Scheitern bekommt keine Reaktionsfläche.
  Das ist in Qdos eine Eigenschaft des Typs, keine Einstellung — halte es so.

---

## 9. Reihenfolge

Der Leitgedanke: **jede Etappe endet mit etwas Benutzbarem**, und `bun run
validate` ist nach jeder wieder grün. Kein monatelanger Umbau, an dessen Ende
sich zeigt, ob es zusammenpasst.

### Etappe 0 — Entscheiden (kein Code)

Die sieben Fragen aus Abschnitt 10 beantworten. Ohne die Akzentfarbe und die
Frage nach der Bestenliste fängt Etappe 1 zweimal an.

### Etappe 1 — Das Design umstellen

Nur Aussehen, keine neue Funktion. `main.css` bekommt die Qdos-Palette,
`themeColors.ts` und das Manifest ziehen nach, Dunkel wird Voreinstellung, die
Emoji verschwinden, die Typografie wird härter, die Tableiste bekommt ihren
neuen Zuschnitt.

*Fertig, wenn:* alle vorhandenen Bildschirme in Schwarz stehen, alle Tests grün
sind, und ein Bildschirmfoto bei 390 × 844 wie Qdos aussieht — mit q2s Inhalten.

**Das ist die günstigste Etappe mit der größten Wirkung**, und sie beweist die
Designentscheidung, bevor irgendetwas Teures gebaut wird. Wenn dir das Ergebnis
nicht gefällt, hast du wenig verloren.

### Etappe 2 — Zeitplan und Zeitfenster

`GoalSchedule` ersetzt `GoalRhythm`, `GoalInstance` kommt dazu, der Wartungsjob
legt Fenster an und markiert überfällige. Person bekommt eine Zeitzone. Im
Frontend: `SchedulePicker`, `HistoryGrid`, die Fälligkeit auf der Zielkarte.
Schritte und `contribute` fallen weg.

*Fertig, wenn:* ein Ziel „dreimal die Woche" angelegt werden kann, das laufende
Fenster „noch 2 von 3" anzeigt, und ein verstrichenes Fenster am nächsten Tag als
verpasst dasteht — mit einer Migration, die vorhandene Ziele ehrlich überführt.

### Etappe 3 — Bilder

`IImageStore`, Upload-Endpunkt, geschützter Ausliefer-Endpunkt, Verkleinerung im
Browser, `ProofCamera` mit Ausweichweg über die Dateiauswahl. Gleich mitgenommen:
Avatarbilder — dieselbe Ablage, kleinere Risiken, und es schließt nebenbei die
Lücke „Profil bearbeiten".

*Fertig, wenn:* ein Foto aufgenommen, hochgeladen, wieder angezeigt und gelöscht
werden kann, ohne Sitzung nicht abrufbar ist, und im Session Replay nicht
auftaucht.

### Etappe 4 — Abstimmung und Feed

`ProofPhoto`, `Vote`, die Regeln aus `voting.ts` in C#, der Streak-Verlust, der
Wisch-Feed, `ProofMessage` im Chat, Reaktionen. Der Wartungsjob schließt
abgelaufene Abstimmungen.

*Fertig, wenn:* ein Nachweis hochgeladen, von Freunden bestätigt oder
angezweifelt wird, das Ergebnis nach Fristablauf auch ohne offene App feststeht,
und eine Streak sowohl steigen als auch reißen kann.

**Hier ist das Produkt zum ersten Mal Qdos.**

### Etappe 5 — Die Shame-Hälfte

Vorwarnung (`risk.ts` in C#, im Wartungsjob), Bilanz mit Betrachterzuschnitt,
„Wird knapp" auf der Startseite, die Bilanz im eigenen und im fremden Profil,
die Aktivitätsübersicht mit Glocke.

*Fertig, wenn:* ein Freund abends sieht, dass jemand ein Fenster zu verpassen
droht — höchstens einmal je Fenster, nie vor 20 Uhr — und das fremde Profil
„erledigt · verpasst" **nur** für gemeinsame To-Dos zeigt.

### Etappe 6 — Pause, Abschließen, Archiv

Die Ausgänge. `GoalPause` samt Kontingent und anonymem Einspruch, `completedAt`,
Archivseite, endgültiges Löschen von dort.

*Fertig, wenn:* ein krank gemeldetes To-Do sofort ausgesetzt ist, das Fenster
weder als geschafft noch als verpasst zählt, zwei Einsprüche es wieder in Gang
setzen, und ein abgeschlossenes To-Do die Bilanz nicht nachträglich
verschlechtert.

### Etappe 7 — Tages-Challenge

`Challenge` mit Warteschlange (`publishedAt` in der Zukunft — **kein täglicher
Redaktionsdienst**), Raum mit Freundeszuschnitt, verdeckte Ansicht bis zum
eigenen Beitrag, eigenes Archiv, Banner auf der Startseite.

*Fertig, wenn:* eine Woche Aufgaben im Voraus eingetragen werden kann, der Raum
nur Freunde zeigt, vor dem eigenen Beitrag unscharf ist, und die eigenen
Beiträge nach Mitternacht im Archiv stehen — die fremden nicht.

### Etappe 8 — Sicherheit und Einstieg

Melden, Blockieren, Blockierte verwalten, Konto löschen. Und das Onboarding samt
Einladungslink, das beide Projekte offen haben. Es kommt hierher, weil erst
jetzt feststeht, was ein neuer Nutzer am ersten Tag überhaupt sehen soll — und
weil die Challenge (Etappe 7) die beste Antwort darauf ist.

*Fertig, wenn:* jemand ohne einen einzigen Freund die App öffnen und etwas
Sinnvolles tun kann.

### Etappe 9 — Benachrichtigungen

Web Push mit VAPID. Erst damit wirken Vorwarnung und Challenge überhaupt: Die
Regel, der Empfängerkreis und der Wortlaut stehen dann längst — Push ist ein
Zustellweg, kein Neubau. Ruhezeiten aus den Einstellungen werden hier zum ersten
Mal angewendet.

---

## 10. Offene Entscheidungen

Sieben. Die ersten drei blockieren Etappe 1.

1. **Name und Marke.** „Qdos" oder „Kudos"? Beide benutzen die Wortmarke `Q2`,
   insofern ist der Konflikt kleiner als er aussieht — aber der Icon-Satz in
   `app/public/` wird aus `app/scripts/generate-icons.ts` erzeugt (heute
   Funkeln-Symbol) und muss neu gezeichnet werden.
2. **Die Akzentfarbe.** Limette `#d7ff3e` ist in Qdos ausdrücklich ein
   Platzhalter (Abschnitt 5d).
3. **Bestenliste behalten oder streichen?** Meine Empfehlung: streichen, weil
   sie „kein Nachtreten" untergräbt (Abschnitt 3b).
4. **Kudos und Reaktionen zusammenführen?** Meine Empfehlung: ja — drei
   Reaktionsarten, weiterhin „Kudos" genannt und im Profil gezählt.
5. **Aufgaben unter einem Ziel (`GoalTask`) endgültig streichen?** Meine
   Empfehlung: erst einmal ja, aber als bewusste Wette notieren — es ist die
   einzige Interaktion ohne soziale Hürde.
6. **Bildablage: Dateisystem oder S3-kompatibel?** Meine Empfehlung:
   Dateisystem hinter einer Schnittstelle.
7. **Helles Design pflegen?** Meine Empfehlung: ja, aber nachrangig
   (Abschnitt 5c).

---

## 11. Was ich nicht geprüft habe

Damit klar ist, worauf dieses Dokument beruht und worauf nicht:

- **Ich habe keine der beiden Apps laufen lassen.** Das Designurteil in
  Abschnitt 5 stammt aus dem Quelltext, den Gestaltungswerten und den
  Design-Begründungen beider Projekte — nicht aus Bildschirmfotos. Das
  Privatprojekt hat keine installierten Abhängigkeiten, das Hauptprojekt
  bräuchte API und Datenbank dazu. Wenn du willst, hole ich das nach und lege
  zwei Aufnahmen bei 390 × 844 nebeneinander; das ist der ehrlichere Weg, ein
  Design zu beurteilen.
- **Ich habe keine Zeitschätzung in Personentagen gemacht.** Die Angaben S/M/L
  in Abschnitt 2 sind Größenordnungen, keine Planung.
- **Ich habe nichts am Code geändert.** Diese Datei ist der einzige Zusatz im
  Arbeitsverzeichnis.
