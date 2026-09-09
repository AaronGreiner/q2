// Relative rather than `~/`, which is the convention everywhere else in the
// app: this file is also read from outside the Nuxt alias — nuxt.config.ts
// takes the app name and description for the web app manifest, and the service
// worker takes the offline page's words — and neither resolves `~`.
import type { ApiErrorKind } from '../api/errors'
import type { BadgeKey, GoalStatus, KudosKind, QuotaPeriod } from '../api/types'

/**
 * Every word q2 says, in both languages it speaks.
 *
 * Hand-written rather than a library: there are two languages and a few
 * hundred strings, the app has no plural rules beyond "one / many", and
 * `@nuxtjs/i18n` would bring routing, lazy loading and a message compiler for
 * none of that. When a third language or real pluralisation arrives, this file
 * is what gets replaced — and it will be one replacement, because nothing else
 * contains a user-visible string.
 *
 * The rule that keeps it that way: **no component or page may contain literal
 * user-facing text.** A German string in a template is a string that cannot be
 * translated and, worse, one nobody will find.
 *
 * `en` is typed as `Messages`, so leaving a key out of it is a build error
 * rather than a screen that falls back to German without telling anyone.
 */
export const de = {
  app: {
    name: 'Qdos',
    description: 'Qdos (q2) — dranbleiben, weil Freunde es sehen.',
    skipToContent: 'Zum Inhalt springen',
  },

  nav: {
    label: 'Hauptnavigation',
    home: 'Start',
    search: 'Suche',
    create: 'Neu',
    chats: 'Chats',
    profile: 'Profil',
  },

  common: {
    back: 'Zurück',
    retry: 'Erneut versuchen',
    showAll: 'Alle anzeigen',
    more: 'Mehr',
    search: 'Suchen',
    loading: 'Wird geladen …',
    reference: 'Referenz',
    cancel: 'Abbrechen',
    close: 'Schließen',
    toHome: 'Zur Startseite',
  },

  auth: {
    signInHeading: 'Willkommen zurück',
    signInIntro: 'Melde dich an und mach da weiter, wo du aufgehört hast.',
    signUpHeading: 'Konto erstellen',
    signUpIntro: 'Ein Name, eine E-Mail-Adresse, ein Passwort — mehr braucht es nicht.',
    name: 'Name',
    namePlaceholder: 'Wie sollen dich deine Freunde sehen?',
    email: 'E-Mail-Adresse',
    emailPlaceholder: 'du@beispiel.de',
    password: 'Passwort',
    passwordPlaceholder: 'Mindestens 10 Zeichen',
    passwordHint: (minimum: number) => `Mindestens ${minimum} Zeichen. Länger ist besser als komplizierter.`,
    signIn: 'Anmelden',
    signUp: 'Registrieren',
    signOut: 'Abmelden',
    noAccount: 'Noch kein Konto?',
    haveAccount: 'Schon ein Konto?',
    toSignUp: 'Jetzt registrieren',
    toSignIn: 'Zur Anmeldung',
    invalidCredentials: 'E-Mail-Adresse oder Passwort stimmt nicht.',
    lockedOut: 'Zu viele Versuche. Bitte warte einen Moment und versuch es dann erneut.',
    signedInAs: (name: string) => `Angemeldet als ${name}`,
    demoHeading: 'Zum Ausprobieren',
    demoHint: 'Diese Umgebung enthält Beispieldaten. Ein Tipp füllt die Zugangsdaten aus.',
    demoFill: 'Demo-Zugang einsetzen',
    noRecovery: 'Ein vergessenes Passwort kann diese Version noch nicht zurücksetzen — es wird keine E-Mail verschickt.',
  },

  home: {
    greeting: (hour: number): string => (hour < 11 ? 'Guten Morgen' : hour < 18 ? 'Hallo' : 'Guten Abend'),
    streakLabel: 'Dein Streak',
    streakDays: (days: number): string => (days === 1 ? 'Tag in Folge' : 'Tage in Folge'),
    streakEncouragement: 'Stark dran! Heute dranbleiben hält die Serie am Leben.',
    streakStart: 'Noch keine Serie — der erste Haken von heute startet sie.',
    todayHeading: 'Heute',
    todayDone: 'Heute geschafft',
    todaySummary: (done: number, total: number) => `${done} von ${total} heute geliefert`,
    todayShort: 'heute',
    goalsHeading: 'Deine Ziele',
    feedHeading: 'Aktivität deiner Freunde',
    noTasks: 'Für heute steht nichts an.',
    noTasksHint: 'Leg ein Ziel an, dann taucht es hier auf.',
    noGoals: 'Noch keine Ziele.',
    noFeed: 'Noch nichts von deinen Freunden.',
    noFeedHint: 'Sobald jemand etwas schafft, steht es hier.',
  },

  goals: {
    heading: 'Ziele',
    new: 'Neu',
    tabToday: 'Heute',
    tabGoals: 'Ziele',
    group: 'GRUPPE',
    detailHeading: 'Ziel-Details',
    recordProof: 'Erledigt',
    recordedProof: 'Eingetragen',
    streak: 'Streak',
    streakDays: (days: number) => (days === 1 ? '1 Fenster' : `${days} Fenster`),
    reminder: 'Erinnerung',
    noReminder: 'Keine',
    sharedWith: 'Gemeinsam mit',
    cheer: 'Anfeuern',
    balance: 'Bilanz',
    balanceValue: (done: number, missed: number) => `${done} · ${missed}`,
    balanceCaption: 'geschafft · verpasst',
    historyHeading: 'Verlauf',
    noHistory: 'Noch kein abgeschlossener Zeitraum.',
    noHistoryHint: 'Sobald das erste Fenster vorbei ist, steht es hier.',
    nothingDueToday: 'Heute steht nichts an.',
    nothingDueTodayHint: 'Alles geliefert, oder für heute nichts fällig.',
    noGoals: 'Noch keine Ziele',
    noGoalsHint: 'Leg dein erstes Ziel an und verfolge euren Fortschritt gemeinsam.',
    notFound: 'Ziel nicht gefunden',
    notFoundHint: 'Wir konnten dieses Ziel nicht finden. Vielleicht wurde es entfernt.',
    overdue: 'Überfällig',
    archive: 'Archiv',
  },

  /**
   * Die Pause — der Ausweg für Krankheit und Urlaub.
   *
   * Der Ton ist bewusst nüchtern: Wer aussetzt, hat nichts falsch gemacht. Die
   * Knappheit steht daneben, nicht als Drohung, sondern als Zahl.
   */
  pause: {
    open: 'Aussetzen',
    heading: 'Ziel aussetzen',
    subtitle: 'Krank, verreist, Gips — sag kurz, warum.',
    reasonLabel: 'Begründung',
    reasonPlaceholder: 'z. B. Grippe, seit Freitag im Bett',
    reasonHint: (minimum: number) => `Mindestens ${minimum} Zeichen. Alle Eingeladenen lesen sie.`,
    daysLabel: 'Wie lange?',
    days: (days: number) => (days === 1 ? '1 Tag' : `${days} Tage`),
    submit: 'Aussetzen',
    remaining: (left: number) => (left === 1 ? 'Noch 1 Pause diesen Monat' : `Noch ${left} Pausen diesen Monat`),
    exhausted: 'Diesen Monat ist keine Pause mehr übrig.',
    windowNote: 'Das laufende Fenster zählt dann weder als geschafft noch als verpasst.',
    bannerTitle: 'Ausgesetzt',
    until: (day: string) => `Bis ${day}`,
    end: 'Pause beenden',
    veto: 'Einspruch',
    vetoWithdraw: 'Einspruch zurückziehen',
    vetoCount: (count: number, required: number) => `${count} von ${required} Einsprüchen`,
    vetoAnonymous: 'Einsprüche sind anonym.',
  },

  /** Die beiden Ausgänge: durchgezogen oder aufgegeben. */
  close: {
    open: 'Beenden',
    heading: 'Ziel beenden',
    subtitle: 'Es bleibt im Archiv — mit Verlauf, Streak und allen Bildern.',
    completed: 'Durchgezogen',
    completedHint: 'Du hast es geschafft.',
    archived: 'Aufgegeben',
    archivedHint: 'Du hörst auf. Deine Bilanz bleibt, wie sie ist.',
    submit: 'Beenden',
    note: 'Ein laufendes Fenster zählt weder als geschafft noch als verpasst.',
  },

  archive: {
    heading: 'Archiv',
    subtitle: 'Beendete Ziele',
    note: 'Hier passiert nichts mehr. Verlauf und Beweisbilder bleiben, bis du sie endgültig löschst.',
    empty: 'Noch nichts beendet',
    emptyHint: 'Ein Ziel, das du beendest, landet hier — mit Verlauf und allen Beweisbildern.',
    closedOn: (day: string) => `Beendet am ${day}`,
    completedOn: (day: string) => `Abgeschlossen am ${day}`,
    delete: 'Endgültig löschen',
    deleteHeading: 'Endgültig löschen?',
    deleteBody: 'Verlauf, Nachrichten und alle Beweisbilder werden entfernt — für dich und für alle Beteiligten. Das lässt sich nicht rückgängig machen.',
    deleteConfirm: 'Endgültig löschen',
  },

  create: {
    heading: 'Neues Ziel',
    subtitle: 'Wozu verpflichtest du dich?',
    titleLabel: 'Titel',
    titlePlaceholder: 'z. B. dreimal die Woche laufen',
    scheduleLabel: 'Fälligkeit',
    iconLabel: 'Symbol',
    intervalLabel: 'Alle wie viele Tage?',
    weekdaysLabel: 'An welchen Tagen?',
    timesLabel: 'Wie oft?',
    periodLabel: 'Pro',
    dueOnLabel: 'Bis wann?',
    reminderLabel: 'Tägliche Erinnerung',
    submit: 'Ziel erstellen',
    open: 'Neues Ziel anlegen',
  },

  /**
   * How often something is due.
   *
   * The server sends the schedule as data and the client composes the sentence
   * — see `scheduleLabel` in app/utils/display.ts. A sentence assembled on the
   * server could only ever be one language.
   */
  schedule: {
    once: 'Einmalig',
    everyDay: 'Jeden Tag',
    everyNDays: (days: number) => (days === 1 ? 'Jeden Tag' : days === 7 ? 'Jede Woche' : `Alle ${days} Tage`),
    timesPer: (times: number, period: string) => `${times}× pro ${period}`,
    period: {
      Week: 'Woche',
      Month: 'Monat',
    } satisfies Record<QuotaPeriod, string>,
    kind: {
      Interval: 'Regelmäßig',
      Weekdays: 'Wochentage',
      Times: 'X-mal',
      Once: 'Einmalig',
    },
    weekdayShort: ['Mo', 'Di', 'Mi', 'Do', 'Fr', 'Sa', 'So'],
    explainOnce: 'Einmal erledigen, bis zum gewählten Tag.',
    explainDaily: 'Jeden Tag ein Nachweis, bis Mitternacht.',
    explainInterval: 'Im gewählten Abstand ein Nachweis, jeweils bis Mitternacht.',
    explainWeekdays: 'An den gewählten Tagen ein Nachweis, jeweils bis Mitternacht.',
    explainTimes: (times: number, period: string) =>
      `${times} Nachweise pro ${period} — wann genau, ist dir überlassen.`,
  },

  /** One window: what it still wants, and what became of it. */
  window: {
    remaining: (remaining: number, required: number) => `Noch ${remaining} von ${required}`,
    dueToday: 'Heute fällig',
    dueBy: (day: string) => `Bis ${day}`,
    done: 'Geschafft',
    missed: 'Verpasst',
    open: 'Offen',
    paused: 'Ausgesetzt',
  },

  status: {
    Active: 'Aktiv',
    Completed: 'Abgeschlossen',
    Archived: 'Archiviert',
  } satisfies Record<GoalStatus, string>,

  chats: {
    heading: 'Chats',
    searchPlaceholder: 'Suchen',
    you: 'Du',
    members: (count: number) => (count === 1 ? '1 Mitglied' : `${count} Mitglieder`),
    online: 'Online',
    lastSeen: (label: string) => `${label} aktiv`,
    offline: 'Offline',
    sharedGoal: 'Gemeinsames Ziel',
    cheerButton: 'Anfeuern',
    messagePlaceholder: 'Nachricht …',
    send: 'Senden',
    clap: 'Applaudieren',
    empty: 'Noch keine Nachrichten',
    emptyHint: 'Schreib die erste — Anfeuern hilft mehr als man denkt.',
    noChats: 'Noch keine Chats',
    noChatsHint: 'Sobald du dich mit jemandem verbindest, entsteht hier ein Chat.',
    notFound: 'Chat nicht gefunden',
    notFoundHint: 'Diese Unterhaltung gibt es nicht — oder sie gehört nicht zu dir.',
    quickCheers: [
      { label: 'Stark!', text: 'Stark gemacht!' },
      { label: 'Weiter so!', text: 'Weiter so!' },
      { label: 'Kudos', text: 'Kudos für dich!' },
      { label: 'Du schaffst das', text: 'Du schaffst das!' },
    ],
    cheerText: 'Los geht’s — ich feuere dich an!',
    newChat: 'Neuer Chat',
    newChatHeading: 'Neuer Chat',
    newChatSubtitle: 'Schreib jemandem direkt oder starte eine Gruppe.',
    tabDirect: 'Direkt',
    tabGroup: 'Gruppe',
    pickFriend: 'Freund auswählen',
    noFriends: 'Du hast noch keine Freunde',
    noFriendsHint: 'Such auf der Freunde-Seite nach jemandem — danach könnt ihr schreiben.',
    groupName: 'Name der Gruppe',
    groupNamePlaceholder: 'z. B. Frühaufsteher',
    groupIcon: 'Symbol',
    groupMembers: 'Mitglieder',
    groupMembersHint: 'Mindestens eine Person auswählen.',
    createGroup: 'Gruppe erstellen',
    leaveGroup: 'Gruppe verlassen',
    leaveGroupConfirm: 'Diese Gruppe verlassen? Du siehst neue Nachrichten dann nicht mehr.',
    threadMenu: 'Weitere Aktionen',
  },

  friends: {
    heading: 'Freunde',

    // The screen is called "Suche" — you get to it by looking for somebody —
    // while `heading` stays the word for the section of people you already
    // know, which is further down the same screen.
    pageHeading: 'Suche',
    searchPlaceholder: 'Nach Name oder @handle suchen …',
    searchHeading: 'Suchergebnisse',
    searchHint: (minimum: number) => `Gib mindestens ${minimum} Zeichen ein.`,
    requests: 'Anfragen',
    sentRequests: 'Gesendete Anfragen',
    suggestions: 'Vorschläge für dich',
    yours: 'Deine Freunde',
    mutual: (count: number) => (count === 1 ? '1 gemeinsamer Freund' : `${count} gemeinsame Freunde`),
    accept: 'Annehmen',
    decline: 'Ablehnen',
    add: 'Hinzufügen',
    requested: 'Angefragt',
    withdraw: 'Zurückziehen',
    remove: 'Entfernen',
    removeConfirm: (name: string) => `${name} als Freund entfernen? Ihr seht dann gegenseitig keine Aktivität mehr.`,
    friends: 'Befreundet',
    you: 'Du',
    message: 'Nachricht schreiben',
    streak: (days: number) => `${days}-Tage-Streak`,
    none: 'Noch keine Freunde',
    noneHint: 'Such oben nach jemandem, mit dem du dranbleiben möchtest.',
    noMatches: 'Niemand gefunden',
    noMatchesHint: 'Versuch es mit einem anderen Namen oder @handle.',
  },

  profile: {
    heading: 'Profil',
    streak: 'Streak',
    kudos: 'Kudos',
    goals: 'Ziele',
    streakBadge: (days: number) => `${days}-Tage-Streak`,
    badges: 'Abzeichen',
    badgeLocked: 'noch nicht erreicht',
    activity: 'Letzte Aktivität',
    noActivity: 'Noch nichts passiert.',
    noActivityHint: 'Hak eine Aufgabe ab — sie steht dann hier.',
    openSettings: 'Einstellungen öffnen',
  },

  /**
   * Taking a picture, on a device that may or may not have a camera the
   * browser is allowed to open.
   */
  photo: {
    heading: 'Foto aufnehmen',
    cameraHint: 'Halte drauf und drück auf den Auslöser.',
    fileHint: 'Wähle ein Bild von deinem Gerät.',
    shutter: 'Auslösen',
    switchCamera: 'Kamera wechseln',
    chooseFile: 'Bild auswählen',
    preview: 'Vorschau',
    retake: 'Noch einmal',
    use: 'Verwenden',
    uploading: 'Wird hochgeladen …',
    cameraBlocked: 'Kein Zugriff auf die Kamera. Du kannst stattdessen ein Bild auswählen.',
    unreadable: 'Diese Datei konnten wir nicht als Bild lesen.',
    tooLarge: 'Das Bild ist zu groß. Bitte nimm ein kleineres.',
    failed: 'Das Bild konnte nicht hochgeladen werden.',
  },

  /**
   * The photograph, and the friends who judge it.
   *
   * The words that make q2 a different product. "Erledigt" never appears on
   * anything somebody said about themselves — only on something other people
   * believed.
   */
  proof: {
    deliver: 'Beweis liefern',
    deliverHint: 'Zeig, dass du es gemacht hast.',
    waiting: 'Wird geprüft',
    waitingHint: (names: number) => (names === 1
      ? 'Ein Freund schaut es sich an.'
      : `${names} Freunde schauen es sich an.`),
    confirmed: 'Bestätigt',
    rejected: 'Nicht anerkannt',
    attemptLeft: 'Du hast noch einen Versuch.',
    noAttemptLeft: 'Kein Versuch mehr — dieses Fenster ist verpasst.',
    expiresIn: (hours: number) => (hours <= 1 ? 'Läuft in unter einer Stunde ab' : `Noch ${hours} Stunden`),
    expired: 'Frist abgelaufen',
    capturedLive: 'Live aufgenommen',
    fromLibrary: 'Aus der Galerie',
    confirmedByNobody: 'Noch niemand hat bestätigt.',
    confirmedBy: (names: string) => `Bestätigt von ${names}`,
    doubtCount: (count: number) => (count === 1 ? '1 Zweifel' : `${count} Zweifel`),
    doubtAnonymous: 'Wer zweifelt, bleibt anonym.',
    yourVerdict: 'Dein Urteil',
    votedConfirm: 'Du hast bestätigt.',
    votedDoubt: 'Du hast angezweifelt.',
    ownProof: 'Dein eigener Beweis — abstimmen dürfen die anderen.',
  },

  vote: {
    heading: 'Glaubst du das?',
    confirm: 'Bestätigen',
    doubt: 'Anzweifeln',
    confirmHint: 'Sieht echt aus.',
    doubtHint: 'Überzeugt mich nicht.',
    empty: 'Nichts zu prüfen.',
    emptyHint: 'Wenn Freunde etwas liefern, landet es hier.',
    waitingHeading: 'Warten auf dich',
    remaining: (count: number) => (count === 1 ? 'Noch 1' : `Noch ${count}`),
    banner: (count: number) => (count === 1
      ? 'Ein Beweis wartet auf dein Urteil'
      : `${count} Beweise warten auf dein Urteil`),
    open: 'Ansehen',
  },

  /**
   * The half of the promise that bites.
   *
   * Every sentence here is flat on purpose. What is missing and how long there
   * is left — no "schon wieder", no count of past failures. The numbers are
   * unpleasant enough on their own, and at the point the warning goes out
   * nothing has actually gone wrong yet.
   */
  /**
   * The daily challenge.
   *
   * The one part of q2 with nothing at stake, and the words have to say so.
   * Nothing here is "Aufgabe", "fällig" or "verpasst" — those belong to the
   * half of the product with a deadline. Joining in is an offer, and sitting
   * one out costs nothing and is not mentioned.
   */
  challenge: {
    heading: 'Challenge des Tages',
    open: 'Mitmachen',
    joined: 'Erledigt',
    join: 'Foto aufnehmen',
    replace: 'Ersetzen',
    remove: 'Entfernen',
    yours: 'Dein Beitrag',
    friends: 'Deine Freunde',
    covered: 'Verdeckt, bis du mitmachst',
    coveredHint: 'Mach mit, um zu sehen',
    coveredAlt: 'Verdeckter Beitrag',
    entryAlt: (name: string) => `Beitrag von ${name}`,
    notLive: 'Nicht live aufgenommen',
    nobody: 'Noch keine Freunde dabei',
    participation: (joined: number, friends: number) => (friends === 1
      ? `${joined} von 1 Freund dabei`
      : `${joined} von ${friends} Freunden dabei`),
    noFriendsYet: 'Noch keiner deiner Freunde hat mitgemacht.',
    none: 'Gerade läuft keine Challenge.',
    noneHint: 'Morgen gibt es eine neue.',
    remaining: (hours: number) => (hours <= 1 ? 'Läuft in unter einer Stunde ab' : `Noch ${hours} Stunden`),
    over: 'Für heute vorbei',
    removeHeading: 'Beitrag entfernen',
    removeBody: 'Dein Bild wird gelöscht. Die Beiträge deiner Freunde sind danach wieder verdeckt, bis du erneut mitmachst.',
    removeConfirm: 'Entfernen',
    archive: 'Dein Archiv',
    archiveSubtitle: 'Nur deine eigenen Beiträge',
    archiveCount: (count: number) => (count === 1 ? '1 Mal mitgemacht' : `${count} Mal mitgemacht`),
    archiveEmpty: 'Noch nichts hier.',
    archiveEmptyHint: 'Sobald du bei einer Challenge mitmachst, bleibt dein Bild in diesem Archiv.',
  },

  /**
   * Melden, blockieren, und der Weg hinaus.
   *
   * Der Ton ist hier anders als überall sonst: sachlich, ohne Ermutigung und
   * ohne Beschwichtigung. Wer diese Wörter liest, hat gerade etwas erlebt, das
   * nicht schön war — eine App, die darauf freundlich reagiert, wirkt, als
   * nähme sie es nicht ernst.
   */
  safety: {
    report: 'Melden',
    reportHeading: 'Was stimmt nicht?',
    reportIntro: 'Wir sehen uns das an. Die gemeldete Person erfährt nicht, wer gemeldet hat.',
    reportSubmit: 'Melden',
    reportNote: 'Kurz beschreiben (optional)',
    reportNotePlaceholder: 'Was sollen wir wissen?',
    reasonFaked: 'Das Bild zeigt nicht, was es behauptet',
    reasonInappropriate: 'Unangemessener Inhalt',
    reasonHarassment: 'Belästigung',
    reasonSpam: 'Spam',
    reasonOther: 'Etwas anderes',
    block: 'Blockieren',
    blockHeading: 'Person blockieren?',
    blockBody: 'Ihr seht euch danach gegenseitig nicht mehr. Eure Freundschaft endet, offene Anfragen verschwinden, und euer Chat ist ausgeblendet — gelöscht wird nichts.',
    blockConfirm: 'Blockieren',
    unblock: 'Freigeben',
    blocked: 'Blockiert',
    blockedHeading: 'Blockierte Personen',
    blockedSubtitle: 'Nur die, die du blockiert hast',
    blockedEmpty: 'Du hast niemanden blockiert.',
    blockedEmptyHint: 'Wen du blockierst, siehst du nirgendwo mehr — und er dich auch nicht.',
    unblockHeading: 'Freigeben?',
    unblockBody: 'Ihr seht euch danach wieder. Eure Freundschaft kommt nicht zurück — die müsstet ihr neu schließen.',
  },

  /**
   * Der Einstieg für jemanden, der hier noch niemanden kennt.
   *
   * Kein Verkaufstext. Der Satz muss erklären, warum die App gerade leer
   * aussieht, und genau eine Sache anbieten, die dagegen hilft.
   */
  invite: {
    heading: 'Noch keine Freunde',
    body: 'q2 lebt davon, dass jemand zusieht. Schick jemandem deinen Link — wer darüber ankommt, ist sofort mit dir befreundet.',
    copy: 'Link kopieren',
    copied: 'Link kopiert',
    share: 'Teilen',
    shareTitle: 'Komm zu q2',
    shareText: 'Mach mit bei q2 — wir bleiben gemeinsam dran.',
    replace: 'Neuen Link erzeugen',
    replaceHeading: 'Link ersetzen?',
    replaceBody: 'Der alte Link funktioniert danach nicht mehr. Wer ihn schon benutzt hat, bleibt dein Freund.',
    replaceConfirm: 'Ersetzen',
    meanwhile: 'Bis dahin: Bei der Challenge des Tages kannst du auch allein mitmachen.',
  },

  /** Das Konto löschen — der einzige Weg, der nicht zurückführt. */
  deleteAccount: {
    open: 'Konto löschen',
    heading: 'Konto endgültig löschen',
    body: 'Deine Ziele, deine Fenster, deine Bilder, deine Direktchats und dein Streak werden gelöscht. In Gruppen verschwinden deine Nachrichten, der Rest bleibt bei den anderen. Das lässt sich nicht rückgängig machen.',
    password: 'Passwort zur Bestätigung',
    passwordHint: 'Wir fragen noch einmal, weil das hier nicht zurückzunehmen ist.',
    confirm: 'Endgültig löschen',
    wrongPassword: 'Das Passwort stimmt nicht.',
    failed: 'Das Konto konnte nicht gelöscht werden.',
  },

  /**
   * What a notification says.
   *
   * Composed in the service worker from a kind and its parameters, never sent
   * as text by the server — which is what makes a notification arrive in the
   * language the person chose. The wording is the feed's wording, because it is
   * the same event; only the title differs, because a lock screen has no
   * context around it.
   *
   * Flat, like everything on the shame half. What is missing and whose, and
   * nothing else.
   */
  push: {
    generic: 'Es gibt etwas Neues.',
    riskTitle: 'Wird knapp',
    riskBody: (goal: string, missing: number) => (missing === 1
      ? `${goal}: es fehlt noch ein Nachweis für heute.`
      : `${goal}: es fehlen noch ${missing} Nachweise für heute.`),
    challengeTitle: 'Challenge des Tages',
  },

  activityOverview: {
    heading: 'Aktivität',
    open: 'Aktivität öffnen',
    empty: 'Noch nichts passiert.',
    emptyHint: 'Wenn deine Freunde etwas liefern, steht es hier.',
    warnings: 'Wird knapp bei deinen Freunden',
    everythingElse: 'Alles andere',
  },

  risk: {
    heading: 'Wird knapp',
    lastDayOne: 'Heute ist der letzte Tag.',
    lastDayMany: (missing: number) => (missing === 1
      ? 'Noch ein Nachweis fehlt. Heute ist der letzte Tag.'
      : `Noch ${missing} Nachweise fehlen. Heute ist der letzte Tag.`),
    tight: (missing: number, days: number) => (missing === 1
      ? `Ein Nachweis fehlt in ${days} Tagen.`
      : `${missing} Nachweise fehlen in ${days} Tagen.`),
    friendLastDay: (name: string) => `${name} hat noch nichts eingereicht — heute ist der letzte Tag.`,
    friendTight: (name: string, missing: number) => (missing === 1
      ? `${name} fehlt noch ein Nachweis.`
      : `${name} fehlen noch ${missing} Nachweise.`),
    badge: 'Knapp',
  },

  balance: {
    heading: 'Bilanz',
    done: 'geschafft',
    missed: 'verpasst',
    clean: 'Noch nichts verpasst.',
    /** What a profile says when the two of them share nothing. */
    nothingShared: 'Ihr habt noch nichts gemeinsam.',
    nothingSharedHint: 'Die Bilanz zeigt nur To-Dos, auf die ihr beide eingeladen seid.',
    sharedGoals: (count: number) => (count === 1 ? '1 gemeinsames To-Do' : `${count} gemeinsame To-Dos`),
  },

  person: {
    heading: 'Profil',
    streak: 'Streak',
    kudos: 'Kudos',
    completed: 'Abgeschlossen',
    message: 'Nachricht',
    addFriend: 'Hinzufügen',
    requestSent: 'Angefragt',
    requestReceived: 'Möchte dich hinzufügen',
    notFound: 'Diese Person gibt es nicht.',
    notFoundHint: 'Vielleicht wurde das Konto gelöscht.',
  },

  profileEdit: {
    heading: 'Profil bearbeiten',
    subtitle: 'Name und Bild sehen deine Freunde.',
    open: 'Profil bearbeiten',
    name: 'Name',
    namePlaceholder: 'Wie sollen dich deine Freunde sehen?',
    nameRequired: 'Bitte gib einen Namen ein.',
    photo: 'Profilbild',
    photoNone: 'Noch kein Bild — deine Initialen stehen an seiner Stelle.',
    addPhoto: 'Bild hinzufügen',
    changePhoto: 'Bild ändern',
    removePhoto: 'Bild entfernen',
    removeTitle: 'Bild entfernen?',
    removeBody: 'Das Bild wird endgültig gelöscht. Deine Initialen treten wieder an seine Stelle.',
    remove: 'Löschen',
    save: 'Speichern',
    handleFixed: 'Dein Kürzel bleibt, wie es ist — deine Freunde haben es aufgeschrieben.',
  },

  badges: {
    StreakHero: 'Streak-Held',
    EarlyBird: 'Frühaufsteher',
    Bookworm: 'Bücherwurm',
    KudosGiver: 'Kudos-Geber',
    Marathon: 'Marathon',
    WeeklyWinner: 'Wochensieger',
  } satisfies Record<BadgeKey, string>,

  settings: {
    notificationsOn: 'Auf diesem Gerät einschalten',
    notificationsOff: 'Auf diesem Gerät ausschalten',
    notificationsUnavailable: 'Dieses Deployment verschickt keine Benachrichtigungen.',
    notificationsBlocked: 'Dein Browser hat Benachrichtigungen blockiert. Das lässt sich nur in den Browsereinstellungen ändern.',
    notificationsUnsupported: 'Dieser Browser kann keine Benachrichtigungen empfangen.',
    quietHours: 'Ruhezeiten',
    quietHoursNote: 'In diesem Zeitraum kommt nichts an. Was hineinfällt, wird nicht nachgeliefert — es steht ohnehin in der App.',
    quietHoursFrom: 'Von',
    quietHoursTo: 'Bis',
    heading: 'Einstellungen',
    appearance: 'Darstellung',
    theme: 'Theme',
    themeSystem: 'System',
    themeLight: 'Hell',
    themeDark: 'Dunkel',
    language: 'Sprache',
    languageGerman: 'Deutsch',
    languageEnglish: 'English',
    notifications: 'Benachrichtigungen',
    notifyReminders: 'Erinnerungen',
    notifyKudos: 'Kudos',
    notifyMessages: 'Nachrichten',
    notifyWeeklyReview: 'Wochenrückblick',
    notificationsNote: 'Gilt für jedes Gerät, auf dem du sie eingeschaltet hast.',
    account: 'Konto',
    editProfile: 'Profil bearbeiten',
    privacy: 'Privatsphäre',
    help: 'Hilfe & Support',
    accountNote: 'Privatsphäre-Einstellungen und Hilfe gibt es noch nicht.',
    version: (version: string) => `Q2 · Qdos — ${version}`,
  },

  /**
   * The feedback dialog is Sentry's, not ours: it renders in its own shadow DOM
   * from the strings handed to it. These are those strings — see
   * sentry.feedback.ts, which maps them onto the SDK's option names.
   */
  feedback: {
    section: 'Feedback',
    open: 'Feedback senden',
    note: 'Deine Nachricht geht an unser Fehler-Reporting, zusammen mit der Seite, auf der du gerade bist. Name und E-Mail schicken wir nicht mit.',
    fromErrorPage: 'Sag uns, was passiert ist',
    title: 'Feedback geben',
    messageLabel: 'Was möchtest du uns sagen?',
    messagePlaceholder: 'Was hat nicht funktioniert, oder was fehlt dir? Bitte ohne persönliche Angaben.',
    submit: 'Absenden',
    cancel: 'Abbrechen',
    success: 'Danke! Deine Nachricht ist angekommen.',
    required: '(Pflichtfeld)',
    errorEmpty: 'Bitte schreib noch etwas, bevor du absendest.',
    errorUnavailable: 'Feedback ist gerade nicht verfügbar.',
    errorTimeout: 'Das hat zu lange gedauert. Bitte versuch es noch einmal.',
    errorForbidden: 'Von hier aus können wir dein Feedback nicht annehmen.',
    errorGeneric: 'Wir konnten deine Nachricht nicht senden. Bitte versuch es noch einmal.',
  },

  activity: {
    taskCompleted: (subject: string) => `hat „${subject}“ abgeschlossen`,
    streakReached: (days: number) => `hat einen ${days}-Tage-Streak erreicht`,
    goalProgress: (subject: string, percent: number) => `ist bei „${subject}“ auf ${percent}\u00A0%`,
    goalCreated: (subject: string) => `hat ein neues Ziel erstellt: ${subject}`,
    windowAtRisk: (subject: string) => `droht „${subject}“ zu verpassen`,
    giveKudos: 'Kudos geben',
    takeBackKudos: 'Kudos zurücknehmen',
  },

  /**
   * The three ways to give kudos.
   *
   * They are drawn as icons, so these words are what a screen reader says and
   * what the button is labelled with — an icon on its own is not a name.
   */
  kudos: {
    Fire: 'Stark gemacht',
    Strong: 'Respekt',
    Applause: 'Applaus',
  } satisfies Record<KudosKind, string>,

  /** How numbers are written. Used by formatAmount in app/utils/display.ts. */
  numbers: {
    decimal: ',',
  },

  time: {
    justNow: 'gerade eben',
    minutesAgo: (minutes: number) => `vor ${minutes} Min`,
    hoursAgo: (hours: number) => (hours === 1 ? 'vor 1 Std' : `vor ${hours} Std`),
    yesterday: 'gestern',
    daysAgo: (days: number) => `vor ${days} Tagen`,
    weekdays: ['So', 'Mo', 'Di', 'Mi', 'Do', 'Fr', 'Sa'],
    weekdayInitials: ['M', 'D', 'M', 'D', 'F', 'S', 'S'],
  },

  errors: {
    title: {
      network: 'Keine Verbindung',
      notFound: 'Nicht gefunden',
      unauthorized: 'Kein Zugriff',
      other: 'Etwas ist schiefgelaufen',
    },
    validation: 'Bitte prüf die markierten Felder und versuch es erneut.',
    notFound: 'Das konnten wir nicht finden. Vielleicht wurde es entfernt.',
    conflict: 'Diese Änderung passt nicht zum aktuellen Stand. Lade neu und versuch es erneut.',
    unauthorized: 'Darauf hast du keinen Zugriff.',
    network: 'Wir konnten den Server nicht erreichen. Prüf deine Verbindung und versuch es erneut.',
    server: 'Der Server konnte die Anfrage nicht abschließen. Bitte versuch es gleich noch einmal.',
    unknown: 'Bitte versuch es erneut. Wenn es weiter auftritt, nenne uns die Referenz unten.',
    pageNotFound: 'Seite nicht gefunden',
    pageNotFoundHint: 'Diese Seite gibt es nicht. Vielleicht wurde sie verschoben oder entfernt.',
    unexpected: 'Wir sind auf ein unerwartetes Problem gestoßen. Es wurde aufgezeichnet — bitte versuch es erneut.',
  },

  /*
   * The page the service worker shows when a navigation cannot reach the
   * server. It is the one screen in q2 that is rendered without the
   * application running, so it repeats what `errors.network` says rather than
   * reusing it — the wording there is an inline notice next to content that
   * did load, and this is the whole window.
   */
  offline: {
    title: 'Offline',
    heading: 'Du bist offline',
    body: 'Qdos braucht eine Verbindung, um deine Ziele zu laden. Sobald du wieder Empfang hast, geht es weiter.',
    retry: 'Erneut versuchen',
  },

  /*
   * An icon name rather than an emoji, like everywhere else in the interface.
   * Every one of these has to be in the bundled icon list in nuxt.config.ts —
   * the scanner only sees literal strings, and these are the literals.
   */
  toast: {
    taskDone: { icon: 'i-lucide-check', text: 'Stark gemacht!' },
    kudosSent: { icon: 'i-lucide-hand-heart', text: 'Kudos gesendet!' },
    progressSaved: { icon: 'i-lucide-check', text: 'Fortschritt gespeichert!' },
    goalReached: { icon: 'i-lucide-trophy', text: 'Ziel erreicht!' },
    goalCreated: { icon: 'i-lucide-plus', text: 'Ziel erstellt!' },
    cheerSent: { icon: 'i-lucide-megaphone', text: 'Anfeuerung gesendet!' },
    requestSent: { icon: 'i-lucide-send', text: 'Anfrage gesendet!' },
    requestWithdrawn: { icon: 'i-lucide-undo-2', text: 'Anfrage zurückgezogen' },
    friendAdded: (name: string) => ({ icon: 'i-lucide-user-check', text: `${name} ist jetzt dein Freund!` }),
    friendRemoved: { icon: 'i-lucide-user-minus', text: 'Freundschaft beendet' },
    groupCreated: { icon: 'i-lucide-users', text: 'Gruppe erstellt!' },
    groupLeft: { icon: 'i-lucide-log-out', text: 'Gruppe verlassen' },
    welcome: (name: string) => ({ icon: 'i-lucide-sparkles', text: `Willkommen, ${name}!` }),
    titleRequired: { icon: 'i-lucide-pencil', text: 'Bitte gib einen Titel ein' },
    profileSaved: { icon: 'i-lucide-check', text: 'Profil gespeichert' },
    proofDelivered: { icon: 'i-lucide-camera', text: 'Beweis abgeschickt' },
    proofConfirmed: { icon: 'i-lucide-check', text: 'Bestätigt!' },
    voteCast: { icon: 'i-lucide-gavel', text: 'Stimme abgegeben' },
    challengeJoined: { icon: 'i-lucide-zap', text: 'Du bist dabei!' },
    challengeWithdrawn: { icon: 'i-lucide-undo-2', text: 'Beitrag entfernt' },
    reported: { icon: 'i-lucide-flag', text: 'Danke — wir sehen es uns an' },
    blocked: { icon: 'i-lucide-shield', text: 'Person blockiert' },
    unblocked: { icon: 'i-lucide-undo-2', text: 'Person freigegeben' },
    inviteCopied: { icon: 'i-lucide-link', text: 'Link kopiert' },
    photoRemoved: { icon: 'i-lucide-image-off', text: 'Bild entfernt' },
    goalPaused: { icon: 'i-lucide-pause', text: 'Ziel ausgesetzt' },
    pauseEnded: { icon: 'i-lucide-play', text: 'Pause beendet' },
    pauseVetoed: { icon: 'i-lucide-gavel', text: 'Einspruch erhoben' },
    pauseVetoWithdrawn: { icon: 'i-lucide-undo-2', text: 'Einspruch zurückgezogen' },
    goalClosed: { icon: 'i-lucide-archive', text: 'Ziel beendet' },
    goalDeleted: { icon: 'i-lucide-trash-2', text: 'Endgültig gelöscht' },
    feedbackUnavailable: { icon: 'i-lucide-circle-alert', text: 'Feedback lässt sich gerade nicht öffnen' },
  },
}

export type Messages = typeof de

export const en: Messages = {
  app: {
    name: 'Qdos',
    description: 'Qdos (q2) — keep at it, because your friends can see.',
    skipToContent: 'Skip to content',
  },

  nav: {
    label: 'Main navigation',
    home: 'Home',
    search: 'Search',
    create: 'New',
    chats: 'Chats',
    profile: 'Profile',
  },

  common: {
    back: 'Back',
    retry: 'Try again',
    showAll: 'Show all',
    more: 'More',
    search: 'Search',
    loading: 'Loading …',
    reference: 'Reference',
    cancel: 'Cancel',
    close: 'Close',
    toHome: 'Back to home',
  },

  auth: {
    signInHeading: 'Welcome back',
    signInIntro: 'Sign in and pick up where you left off.',
    signUpHeading: 'Create an account',
    signUpIntro: 'A name, an email address, a password — that is all it takes.',
    name: 'Name',
    namePlaceholder: 'How should your friends see you?',
    email: 'Email address',
    emailPlaceholder: 'you@example.com',
    password: 'Password',
    passwordPlaceholder: 'At least 10 characters',
    passwordHint: (minimum: number) => `At least ${minimum} characters. Longer beats complicated.`,
    signIn: 'Sign in',
    signUp: 'Sign up',
    signOut: 'Sign out',
    noAccount: 'No account yet?',
    haveAccount: 'Already have an account?',
    toSignUp: 'Sign up now',
    toSignIn: 'Go to sign in',
    invalidCredentials: 'That email address or password is not right.',
    lockedOut: 'Too many attempts. Wait a moment and try again.',
    signedInAs: (name: string) => `Signed in as ${name}`,
    demoHeading: 'Just looking around',
    demoHint: 'This environment contains sample data. One tap fills in the credentials.',
    demoFill: 'Use the demo account',
    noRecovery: 'This version cannot reset a forgotten password yet — no email is sent.',
  },

  home: {
    greeting: (hour: number) => (hour < 11 ? 'Good morning' : hour < 18 ? 'Hello' : 'Good evening'),
    streakLabel: 'Your streak',
    streakDays: (days: number) => (days === 1 ? 'day in a row' : 'days in a row'),
    streakEncouragement: 'Going strong. One more today keeps it alive.',
    streakStart: 'No streak yet — the first tick today starts one.',
    todayHeading: 'Today',
    todayDone: 'Done today',
    todaySummary: (done: number, total: number) => `${done} of ${total} delivered today`,
    todayShort: 'today',
    goalsHeading: 'Your goals',
    feedHeading: 'What your friends did',
    noTasks: 'Nothing on today.',
    noTasksHint: 'Add a goal and it will show up here.',
    noGoals: 'No goals yet.',
    noFeed: 'Nothing from your friends yet.',
    noFeedHint: 'As soon as somebody gets something done, it appears here.',
  },

  goals: {
    heading: 'Goals',
    new: 'New',
    tabToday: 'Today',
    tabGoals: 'Goals',
    group: 'GROUP',
    detailHeading: 'Goal details',
    recordProof: 'Done',
    recordedProof: 'Logged',
    streak: 'Streak',
    streakDays: (days: number) => (days === 1 ? '1 window' : `${days} windows`),
    reminder: 'Reminder',
    noReminder: 'None',
    sharedWith: 'Together with',
    cheer: 'Cheer on',
    balance: 'Balance',
    balanceValue: (done: number, missed: number) => `${done} · ${missed}`,
    balanceCaption: 'kept · missed',
    historyHeading: 'History',
    noHistory: 'No closed window yet.',
    noHistoryHint: 'Once the first one is over, it shows up here.',
    nothingDueToday: 'Nothing on today.',
    nothingDueTodayHint: 'All delivered, or nothing due today.',
    noGoals: 'No goals yet',
    noGoalsHint: 'Add your first goal and track your progress together.',
    notFound: 'Goal not found',
    notFoundHint: 'We could not find that goal. It may have been removed.',
    overdue: 'Overdue',
    archive: 'Archive',
  },

  pause: {
    open: 'Set aside',
    heading: 'Set this goal aside',
    subtitle: 'Ill, away, arm in plaster — say briefly why.',
    reasonLabel: 'Reason',
    reasonPlaceholder: 'e.g. flu, in bed since Friday',
    reasonHint: (minimum: number) => `At least ${minimum} characters. Everybody invited reads it.`,
    daysLabel: 'For how long?',
    days: (days: number) => (days === 1 ? '1 day' : `${days} days`),
    submit: 'Set aside',
    remaining: (left: number) => (left === 1 ? '1 pause left this month' : `${left} pauses left this month`),
    exhausted: 'No pause left this month.',
    windowNote: 'The running window then counts as neither kept nor missed.',
    bannerTitle: 'Set aside',
    until: (day: string) => `Until ${day}`,
    end: 'End the pause',
    veto: 'Object',
    vetoWithdraw: 'Withdraw objection',
    vetoCount: (count: number, required: number) => `${count} of ${required} objections`,
    vetoAnonymous: 'Objections are anonymous.',
  },

  close: {
    open: 'Stop',
    heading: 'Stop this goal',
    subtitle: 'It stays in the archive — history, streak and every photograph.',
    completed: 'Carried through',
    completedHint: 'You did it.',
    archived: 'Given up',
    archivedHint: 'You are stopping. Your record stays as it is.',
    submit: 'Stop',
    note: 'A running window counts as neither kept nor missed.',
  },

  archive: {
    heading: 'Archive',
    subtitle: 'Goals that have stopped',
    note: 'Nothing happens here any more. History and photographs stay until you delete them for good.',
    empty: 'Nothing stopped yet',
    emptyHint: 'A goal you stop lands here — with its history and every photograph.',
    closedOn: (day: string) => `Stopped on ${day}`,
    completedOn: (day: string) => `Completed on ${day}`,
    delete: 'Delete for good',
    deleteHeading: 'Delete for good?',
    deleteBody: 'History, messages and every photograph are removed — for you and for everybody on it. This cannot be undone.',
    deleteConfirm: 'Delete for good',
  },

  create: {
    heading: 'New goal',
    subtitle: 'What are you committing to?',
    titleLabel: 'Title',
    titlePlaceholder: 'e.g. run three times a week',
    scheduleLabel: 'Due',
    iconLabel: 'Icon',
    intervalLabel: 'Every how many days?',
    weekdaysLabel: 'On which days?',
    timesLabel: 'How often?',
    periodLabel: 'Per',
    dueOnLabel: 'By when?',
    reminderLabel: 'Daily reminder',
    submit: 'Create goal',
    open: 'Add a goal',
  },

  schedule: {
    once: 'Once',
    everyDay: 'Every day',
    everyNDays: (days: number) => (days === 1 ? 'Every day' : days === 7 ? 'Every week' : `Every ${days} days`),
    timesPer: (times: number, period: string) => `${times}× per ${period}`,
    period: {
      Week: 'week',
      Month: 'month',
    },
    kind: {
      Interval: 'Regularly',
      Weekdays: 'Weekdays',
      Times: 'X times',
      Once: 'Once',
    },
    weekdayShort: ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'],
    explainOnce: 'Do it once, by the chosen day.',
    explainDaily: 'One proof every day, by midnight.',
    explainInterval: 'One proof at the chosen interval, each by midnight.',
    explainWeekdays: 'One proof on each chosen day, by midnight.',
    explainTimes: (times: number, period: string) =>
      `${times} proofs per ${period} — when exactly is up to you.`,
  },

  window: {
    remaining: (remaining: number, required: number) => `${remaining} of ${required} left`,
    dueToday: 'Due today',
    dueBy: (day: string) => `By ${day}`,
    done: 'Kept',
    missed: 'Missed',
    open: 'Open',
    paused: 'Set aside',
  },

  status: {
    Active: 'Active',
    Completed: 'Completed',
    Archived: 'Archived',
  },

  chats: {
    heading: 'Chats',
    searchPlaceholder: 'Search',
    you: 'You',
    members: (count: number) => (count === 1 ? '1 member' : `${count} members`),
    online: 'Online',
    lastSeen: (label: string) => `active ${label}`,
    offline: 'Offline',
    sharedGoal: 'Shared goal',
    cheerButton: 'Cheer on',
    messagePlaceholder: 'Message …',
    send: 'Send',
    clap: 'Applaud',
    empty: 'No messages yet',
    emptyHint: 'Write the first one — encouragement helps more than you think.',
    noChats: 'No chats yet',
    noChatsHint: 'A chat appears here as soon as you connect with somebody.',
    notFound: 'Chat not found',
    notFoundHint: 'That conversation does not exist — or it is not yours.',
    quickCheers: [
      { label: 'Strong!', text: 'Nicely done!' },
      { label: 'Keep going!', text: 'Keep going!' },
      { label: 'Kudos', text: 'Kudos to you!' },
      { label: 'You got this', text: 'You got this!' },
    ],
    cheerText: 'Go on — I am cheering for you!',
    newChat: 'New chat',
    newChatHeading: 'New chat',
    newChatSubtitle: 'Write to somebody directly, or start a group.',
    tabDirect: 'Direct',
    tabGroup: 'Group',
    pickFriend: 'Choose a friend',
    noFriends: 'You have no friends yet',
    noFriendsHint: 'Find somebody on the friends screen — then you can write to each other.',
    groupName: 'Group name',
    groupNamePlaceholder: 'e.g. Early risers',
    groupIcon: 'Icon',
    groupMembers: 'Members',
    groupMembersHint: 'Choose at least one person.',
    createGroup: 'Create group',
    leaveGroup: 'Leave group',
    leaveGroupConfirm: 'Leave this group? You will stop seeing new messages.',
    threadMenu: 'More actions',
  },

  friends: {
    heading: 'Friends',
    pageHeading: 'Search',
    searchPlaceholder: 'Search by name or @handle …',
    searchHeading: 'Search results',
    searchHint: (minimum: number) => `Type at least ${minimum} characters.`,
    requests: 'Requests',
    sentRequests: 'Sent requests',
    suggestions: 'Suggestions for you',
    yours: 'Your friends',
    mutual: (count: number) => (count === 1 ? '1 mutual friend' : `${count} mutual friends`),
    accept: 'Accept',
    decline: 'Decline',
    add: 'Add',
    requested: 'Requested',
    withdraw: 'Withdraw',
    remove: 'Remove',
    removeConfirm: (name: string) => `Remove ${name} as a friend? Neither of you will see the other's activity.`,
    friends: 'Friends',
    you: 'You',
    message: 'Write a message',
    streak: (days: number) => `${days}-day streak`,
    none: 'No friends yet',
    noneHint: 'Search above for somebody to keep at it with.',
    noMatches: 'Nobody found',
    noMatchesHint: 'Try another name or @handle.',
  },

  profile: {
    heading: 'Profile',
    streak: 'Streak',
    kudos: 'Kudos',
    goals: 'Goals',
    streakBadge: (days: number) => `${days}-day streak`,
    badges: 'Badges',
    badgeLocked: 'not earned yet',
    activity: 'Recent activity',
    noActivity: 'Nothing has happened yet.',
    noActivityHint: 'Tick a task off and it will show up here.',
    openSettings: 'Open settings',
  },

  photo: {
    heading: 'Take a photo',
    cameraHint: 'Point at it and press the shutter.',
    fileHint: 'Pick a picture from your device.',
    shutter: 'Shutter',
    switchCamera: 'Switch camera',
    chooseFile: 'Choose a picture',
    preview: 'Preview',
    retake: 'Again',
    use: 'Use it',
    uploading: 'Uploading …',
    cameraBlocked: 'No access to the camera. You can pick a picture instead.',
    unreadable: 'We could not read that file as a picture.',
    tooLarge: 'That picture is too large. Please use a smaller one.',
    failed: 'The picture could not be uploaded.',
  },

  proof: {
    deliver: 'Deliver proof',
    deliverHint: 'Show that you did it.',
    waiting: 'Being checked',
    waitingHint: (names: number) => (names === 1
      ? 'One friend is looking at it.'
      : `${names} friends are looking at it.`),
    confirmed: 'Confirmed',
    rejected: 'Not accepted',
    attemptLeft: 'You have one more try.',
    noAttemptLeft: 'No tries left — this window is missed.',
    expiresIn: (hours: number) => (hours <= 1 ? 'Closes in under an hour' : `${hours} hours left`),
    expired: 'Deadline passed',
    capturedLive: 'Taken live',
    fromLibrary: 'From the library',
    confirmedByNobody: 'Nobody has confirmed it yet.',
    confirmedBy: (names: string) => `Confirmed by ${names}`,
    doubtCount: (count: number) => (count === 1 ? '1 doubt' : `${count} doubts`),
    doubtAnonymous: 'Doubting stays anonymous.',
    yourVerdict: 'Your verdict',
    votedConfirm: 'You confirmed it.',
    votedDoubt: 'You doubted it.',
    ownProof: 'Your own proof — the others do the voting.',
  },

  vote: {
    heading: 'Do you believe it?',
    confirm: 'Confirm',
    doubt: 'Doubt it',
    confirmHint: 'Looks real.',
    doubtHint: 'Not convinced.',
    empty: 'Nothing to check.',
    emptyHint: 'When friends deliver something, it turns up here.',
    waitingHeading: 'Waiting for you',
    remaining: (count: number) => (count === 1 ? '1 left' : `${count} left`),
    banner: (count: number) => (count === 1
      ? 'One proof is waiting for your verdict'
      : `${count} proofs are waiting for your verdict`),
    open: 'Look',
  },

  challenge: {
    heading: 'Challenge of the day',
    open: 'Join in',
    joined: 'Done',
    join: 'Take a photo',
    replace: 'Replace',
    remove: 'Remove',
    yours: 'Your contribution',
    friends: 'Your friends',
    covered: 'Covered until you join in',
    coveredHint: 'Join in to see',
    coveredAlt: 'Covered contribution',
    entryAlt: (name: string) => `Contribution by ${name}`,
    notLive: 'Not taken live',
    nobody: 'No friends in yet',
    participation: (joined: number, friends: number) => (friends === 1
      ? `${joined} of 1 friend in`
      : `${joined} of ${friends} friends in`),
    noFriendsYet: 'None of your friends has joined in yet.',
    none: 'No challenge is running right now.',
    noneHint: 'There will be a new one tomorrow.',
    remaining: (hours: number) => (hours <= 1 ? 'Less than an hour left' : `${hours} hours left`),
    over: 'Over for today',
    removeHeading: 'Remove contribution',
    removeBody: 'Your picture will be deleted. Your friends\' contributions are covered again afterwards, until you join in once more.',
    removeConfirm: 'Remove',
    archive: 'Your archive',
    archiveSubtitle: 'Your own contributions only',
    archiveCount: (count: number) => (count === 1 ? 'Joined in once' : `Joined in ${count} times`),
    archiveEmpty: 'Nothing here yet.',
    archiveEmptyHint: 'As soon as you join a challenge, your picture stays in this archive.',
  },

  safety: {
    report: 'Report',
    reportHeading: 'What is wrong?',
    reportIntro: 'We will look at it. The person reported is not told who reported them.',
    reportSubmit: 'Report',
    reportNote: 'Describe it briefly (optional)',
    reportNotePlaceholder: 'What should we know?',
    reasonFaked: 'The picture does not show what it claims',
    reasonInappropriate: 'Inappropriate content',
    reasonHarassment: 'Harassment',
    reasonSpam: 'Spam',
    reasonOther: 'Something else',
    block: 'Block',
    blockHeading: 'Block this person?',
    blockBody: 'You will not see each other afterwards. Your friendship ends, any open request disappears, and your chat is hidden — nothing is deleted.',
    blockConfirm: 'Block',
    unblock: 'Unblock',
    blocked: 'Blocked',
    blockedHeading: 'Blocked people',
    blockedSubtitle: 'Only the ones you blocked',
    blockedEmpty: 'You have not blocked anybody.',
    blockedEmptyHint: 'Whoever you block disappears everywhere — and you disappear for them.',
    unblockHeading: 'Unblock?',
    unblockBody: 'You will see each other again. Your friendship does not come back — you would have to make it again.',
  },

  invite: {
    heading: 'No friends yet',
    body: 'q2 works because somebody is watching. Send somebody your link — whoever arrives through it is your friend straight away.',
    copy: 'Copy link',
    copied: 'Link copied',
    share: 'Share',
    shareTitle: 'Join me on q2',
    shareText: 'Come and keep at it with me on q2.',
    replace: 'Make a new link',
    replaceHeading: 'Replace the link?',
    replaceBody: 'The old link stops working. Whoever already used it stays your friend.',
    replaceConfirm: 'Replace',
    meanwhile: 'Until then: the challenge of the day is something you can do on your own.',
  },

  deleteAccount: {
    open: 'Delete account',
    heading: 'Delete your account for good',
    body: 'Your goals, your windows, your pictures, your direct chats and your streak will be deleted. In groups your messages disappear and the rest stays with the others. This cannot be undone.',
    password: 'Password to confirm',
    passwordHint: 'We ask again because this cannot be taken back.',
    confirm: 'Delete for good',
    wrongPassword: 'That password is not correct.',
    failed: 'The account could not be deleted.',
  },

  push: {
    generic: 'Something new happened.',
    riskTitle: 'Getting tight',
    riskBody: (goal: string, missing: number) => (missing === 1
      ? `${goal}: one proof still missing today.`
      : `${goal}: ${missing} proofs still missing today.`),
    challengeTitle: 'Challenge of the day',
  },

  activityOverview: {
    heading: 'Activity',
    open: 'Open activity',
    empty: 'Nothing has happened yet.',
    emptyHint: 'When your friends deliver something, it turns up here.',
    warnings: 'Your friends who are running out of time',
    everythingElse: 'Everything else',
  },

  risk: {
    heading: 'Getting tight',
    lastDayOne: 'Today is the last day.',
    lastDayMany: (missing: number) => (missing === 1
      ? 'One proof still missing. Today is the last day.'
      : `${missing} proofs still missing. Today is the last day.`),
    tight: (missing: number, days: number) => (missing === 1
      ? `One proof missing, ${days} days left.`
      : `${missing} proofs missing, ${days} days left.`),
    friendLastDay: (name: string) => `${name} has not handed anything in — today is the last day.`,
    friendTight: (name: string, missing: number) => (missing === 1
      ? `${name} is one proof short.`
      : `${name} is ${missing} proofs short.`),
    badge: 'Tight',
  },

  balance: {
    heading: 'Record',
    done: 'done',
    missed: 'missed',
    clean: 'Nothing missed yet.',
    nothingShared: 'You have nothing together yet.',
    nothingSharedHint: 'A record only covers to-dos you are both on.',
    sharedGoals: (count: number) => (count === 1 ? '1 shared to-do' : `${count} shared to-dos`),
  },

  person: {
    heading: 'Profile',
    streak: 'Streak',
    kudos: 'Kudos',
    completed: 'Completed',
    message: 'Message',
    addFriend: 'Add',
    requestSent: 'Requested',
    requestReceived: 'Wants to add you',
    notFound: 'There is no such person.',
    notFoundHint: 'The account may have been deleted.',
  },

  profileEdit: {
    heading: 'Edit profile',
    subtitle: 'Your name and picture are what friends see.',
    open: 'Edit profile',
    name: 'Name',
    namePlaceholder: 'How should your friends see you?',
    nameRequired: 'Please enter a name.',
    photo: 'Profile picture',
    photoNone: 'No picture yet — your initials stand in for one.',
    addPhoto: 'Add a picture',
    changePhoto: 'Change picture',
    removePhoto: 'Remove picture',
    removeTitle: 'Remove the picture?',
    removeBody: 'The picture is deleted for good. Your initials take its place again.',
    remove: 'Delete',
    save: 'Save',
    handleFixed: 'Your handle stays as it is — your friends have written it down.',
  },

  badges: {
    StreakHero: 'Streak hero',
    EarlyBird: 'Early bird',
    Bookworm: 'Bookworm',
    KudosGiver: 'Kudos giver',
    Marathon: 'Marathon',
    WeeklyWinner: 'Weekly winner',
  },

  settings: {
    notificationsOn: 'Turn on for this device',
    notificationsOff: 'Turn off for this device',
    notificationsUnavailable: 'This deployment does not send notifications.',
    notificationsBlocked: 'Your browser has blocked notifications. That can only be changed in the browser settings.',
    notificationsUnsupported: 'This browser cannot receive notifications.',
    quietHours: 'Quiet hours',
    quietHoursNote: 'Nothing arrives during this window. What falls into it is not delivered later — it is in the app anyway.',
    quietHoursFrom: 'From',
    quietHoursTo: 'To',
    heading: 'Settings',
    appearance: 'Appearance',
    theme: 'Theme',
    themeSystem: 'System',
    themeLight: 'Light',
    themeDark: 'Dark',
    language: 'Language',
    languageGerman: 'Deutsch',
    languageEnglish: 'English',
    notifications: 'Notifications',
    notifyReminders: 'Reminders',
    notifyKudos: 'Kudos',
    notifyMessages: 'Messages',
    notifyWeeklyReview: 'Weekly review',
    notificationsNote: 'Applies to every device you have turned them on for.',
    account: 'Account',
    editProfile: 'Edit profile',
    privacy: 'Privacy',
    help: 'Help & support',
    accountNote: 'Privacy settings and help do not exist yet.',
    version: (version: string) => `Q2 · Kudos — ${version}`,
  },

  feedback: {
    section: 'Feedback',
    open: 'Send feedback',
    note: 'Your message goes to our error reporting, together with the page you are on. Your name and address are not sent with it.',
    fromErrorPage: 'Tell us what happened',
    title: 'Give feedback',
    messageLabel: 'What would you like to tell us?',
    messagePlaceholder: 'What did not work, or what is missing? Please leave personal details out.',
    submit: 'Send',
    cancel: 'Cancel',
    success: 'Thank you! Your message arrived.',
    required: '(required)',
    errorEmpty: 'Please write something before sending.',
    errorUnavailable: 'Feedback is not available right now.',
    errorTimeout: 'That took too long. Please try again.',
    errorForbidden: 'We cannot accept your feedback from here.',
    errorGeneric: 'We could not send your message. Please try again.',
  },

  activity: {
    taskCompleted: (subject: string) => `completed “${subject}”`,
    streakReached: (days: number) => `reached a ${days}-day streak`,
    goalProgress: (subject: string, percent: number) => `is at ${percent}% on “${subject}”`,
    goalCreated: (subject: string) => `started a new goal: ${subject}`,
    windowAtRisk: (subject: string) => `is about to miss “${subject}”`,
    giveKudos: 'Give kudos',
    takeBackKudos: 'Take kudos back',
  },

  kudos: {
    Fire: 'Nicely done',
    Strong: 'Respect',
    Applause: 'Applause',
  },

  numbers: {
    decimal: '.',
  },

  time: {
    justNow: 'just now',
    minutesAgo: (minutes: number) => `${minutes} min ago`,
    hoursAgo: (hours: number) => (hours === 1 ? '1 hr ago' : `${hours} hrs ago`),
    yesterday: 'yesterday',
    daysAgo: (days: number) => `${days} days ago`,
    weekdays: ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'],
    weekdayInitials: ['M', 'T', 'W', 'T', 'F', 'S', 'S'],
  },

  errors: {
    title: {
      network: 'No connection',
      notFound: 'Not found',
      unauthorized: 'No access',
      other: 'Something went wrong',
    },
    validation: 'Please check the highlighted fields and try again.',
    notFound: 'We could not find that. It may have been removed.',
    conflict: 'That change conflicts with the current state. Reload and try again.',
    unauthorized: 'You do not have access to this.',
    network: 'We could not reach the server. Check your connection and try again.',
    server: 'The server could not complete your request. Please try again in a moment.',
    unknown: 'Please try again. If it keeps happening, let us know and quote the reference below.',
    pageNotFound: 'Page not found',
    pageNotFoundHint: 'That page does not exist. It may have been moved or removed.',
    unexpected: 'We hit an unexpected problem. The incident has been recorded — please try again.',
  },

  offline: {
    title: 'Offline',
    heading: 'You are offline',
    body: 'Qdos needs a connection to load your goals. As soon as you have signal again, you can pick up where you left off.',
    retry: 'Try again',
  },

  toast: {
    taskDone: { icon: 'i-lucide-check', text: 'Nicely done!' },
    kudosSent: { icon: 'i-lucide-hand-heart', text: 'Kudos sent!' },
    progressSaved: { icon: 'i-lucide-check', text: 'Progress saved!' },
    goalReached: { icon: 'i-lucide-trophy', text: 'Goal reached!' },
    goalCreated: { icon: 'i-lucide-plus', text: 'Goal created!' },
    cheerSent: { icon: 'i-lucide-megaphone', text: 'Cheer sent!' },
    requestSent: { icon: 'i-lucide-send', text: 'Request sent!' },
    requestWithdrawn: { icon: 'i-lucide-undo-2', text: 'Request withdrawn' },
    friendAdded: (name: string) => ({ icon: 'i-lucide-user-check', text: `${name} is now your friend!` }),
    friendRemoved: { icon: 'i-lucide-user-minus', text: 'Friendship ended' },
    groupCreated: { icon: 'i-lucide-users', text: 'Group created!' },
    groupLeft: { icon: 'i-lucide-log-out', text: 'Left the group' },
    welcome: (name: string) => ({ icon: 'i-lucide-sparkles', text: `Welcome, ${name}!` }),
    titleRequired: { icon: 'i-lucide-pencil', text: 'Please enter a title' },
    profileSaved: { icon: 'i-lucide-check', text: 'Profile saved' },
    proofDelivered: { icon: 'i-lucide-camera', text: 'Proof sent' },
    proofConfirmed: { icon: 'i-lucide-check', text: 'Confirmed!' },
    voteCast: { icon: 'i-lucide-gavel', text: 'Vote cast' },
    challengeJoined: { icon: 'i-lucide-zap', text: 'You are in!' },
    challengeWithdrawn: { icon: 'i-lucide-undo-2', text: 'Contribution removed' },
    reported: { icon: 'i-lucide-flag', text: 'Thank you — we will look at it' },
    blocked: { icon: 'i-lucide-shield', text: 'Person blocked' },
    unblocked: { icon: 'i-lucide-undo-2', text: 'Person unblocked' },
    inviteCopied: { icon: 'i-lucide-link', text: 'Link copied' },
    photoRemoved: { icon: 'i-lucide-image-off', text: 'Picture removed' },
    goalPaused: { icon: 'i-lucide-pause', text: 'Goal set aside' },
    pauseEnded: { icon: 'i-lucide-play', text: 'Pause ended' },
    pauseVetoed: { icon: 'i-lucide-gavel', text: 'Objection raised' },
    pauseVetoWithdrawn: { icon: 'i-lucide-undo-2', text: 'Objection withdrawn' },
    goalClosed: { icon: 'i-lucide-archive', text: 'Goal stopped' },
    goalDeleted: { icon: 'i-lucide-trash-2', text: 'Deleted for good' },
    feedbackUnavailable: { icon: 'i-lucide-circle-alert', text: 'Feedback cannot be opened right now' },
  },
}

export const messages = { de, en }

/** Maps the API's language preference onto the dictionaries above. */
export const languageKeys = { German: 'de', English: 'en' } as const

/** The error copy for a failure kind. Kept next to the kinds it covers. */
export function errorMessage(t: Messages, kind: ApiErrorKind): string {
  return t.errors[kind]
}
