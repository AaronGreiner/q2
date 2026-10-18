// Relative rather than `~/`, which is the convention everywhere else in the
// app: this file is also read from outside the Nuxt alias — nuxt.config.ts
// takes the app name and description for the web app manifest, and the service
// worker takes the offline page's words — and neither resolves `~`.
import type { ApiErrorKind } from '../api/errors'
import type { DontProofMode, GoalEventKind, GoalStatus, KudosKind, ProofSource, QuotaPeriod } from '../api/types'

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
    friends: 'Freunde',
    dos: 'Dos',
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
    signInHeading: 'Anmelden',
    signInIntro: 'Qdos hilft dir, deine Vorsätze zu halten — weil deine Freunde sehen, ob du es tust.',
    signUpHeading: 'Konto erstellen',
    signUpIntro: 'Ein Name, eine E-Mail-Adresse, ein Passwort — mehr braucht es nicht.',
    name: 'Name',
    namePlaceholder: 'Wie sollen dich deine Freunde sehen?',
    email: 'E-Mail-Adresse',
    emailPlaceholder: 'du@beispiel.de',
    password: 'Passwort',
    passwordPlaceholder: 'Mindestens 10 Zeichen',
    passwordHint: (minimum: number) => `Mindestens ${minimum} Zeichen. Länger ist besser als komplizierter.`,
    showPassword: 'Passwort anzeigen',
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
    forgotPassword: 'Passwort vergessen?',
    forgotHeading: 'Passwort vergessen',
    forgotIntro: 'Gib die Adresse deines Kontos ein. Wir schicken dir einen Link, mit dem du ein neues Passwort setzt.',
    forgotSubmit: 'Link anfordern',
    forgotSentHeading: 'Schau in dein Postfach',
    forgotSentIntro: 'Falls es ein Konto mit dieser Adresse gibt, ist jetzt eine E-Mail mit einem Link unterwegs. Er ist eine Stunde lang gültig.',
    forgotSentHint: 'Nichts angekommen? Schau im Spam-Ordner nach, oder fordere in ein paar Minuten einen neuen Link an.',
    forgotAgain: 'Neuen Link anfordern',
    rememberedIt: 'Doch wieder eingefallen?',
    recoveryUnavailable: 'In dieser Umgebung werden keine E-Mails verschickt. Ein vergessenes Passwort lässt sich hier deshalb noch nicht zurücksetzen.',
    resetHeading: 'Neues Passwort',
    resetIntro: 'Wähl ein neues Passwort. Danach bist du auf allen Geräten abgemeldet und meldest dich mit dem neuen wieder an.',
    newPassword: 'Neues Passwort',
    resetSubmit: 'Passwort speichern',
    resetInvalidHeading: 'Dieser Link gilt nicht mehr',
    resetInvalid: 'Er ist abgelaufen oder wurde schon benutzt. Fordere einfach einen neuen an.',
    resetDoneHeading: 'Passwort geändert',
    resetDoneIntro: 'Du bist jetzt auf allen Geräten abgemeldet. Melde dich mit deinem neuen Passwort wieder an — im Browser wie in der App.',
  },

  home: {
    greeting: (hour: number): string => (hour < 11 ? 'Guten Morgen' : hour < 18 ? 'Hallo' : 'Guten Abend'),
    streakDays: (days: number): string => (days === 1 ? '1 Tag in Folge' : `${days} Tage in Folge`),
    noStreak: 'Noch keine Serie',
    todayHeading: 'Heute',
    nextUpHeading: 'Als Nächstes',
    openProfile: 'Profil öffnen',
    feedHeading: 'Aktivität deiner Freunde',
    noTasks: 'Für heute steht nichts an.',
    noTasksHint: 'Leg unter Dos etwas an, dann taucht es hier auf.',
    noGoals: 'Noch keine Dos & Don’ts.',
    noFeed: 'Noch nichts von deinen Freunden.',
    noFeedHint: 'Sobald jemand etwas schafft, steht es hier.',
  },

  goals: {
    heading: 'Dos & Don’ts',
    new: 'Neu',
    tabDos: 'Dos',
    tabDonts: 'Don’ts',
    sectionMine: 'Deine',
    sectionMineHint: 'Du lieferst den Beweis',
    sectionFriends: 'Von Freunden',
    sectionFriendsHint: 'Du prüfst',
    group: 'GRUPPE',
    detailHeading: 'Details',
    recordProof: 'Erledigt',
    recordedProof: 'Eingetragen',
    streak: 'Streak',
    streakDays: (days: number) => (days === 1 ? '1 Fenster' : `${days} Fenster`),
    reminder: 'Erinnerung',
    noReminder: 'Keine',
    cheer: 'Anfeuern',
    balance: 'Bilanz',
    balanceValue: (done: number, missed: number) => `${done} · ${missed}`,
    balanceCaption: 'geschafft · verpasst',
    historyHeading: 'Verlauf',
    noHistory: 'Noch kein abgeschlossener Zeitraum.',
    noHistoryHint: 'Sobald das erste Fenster vorbei ist, steht es hier.',
    noDos: 'Noch keine Dos',
    noDosHint: 'Leg mit dem Plus oben dein erstes Do an — deine Freunde sehen, ob du dranbleibst.',
    noDonts: 'Noch keine Don’ts',
    noDontsHint: 'Leg mit dem Plus oben dein erstes Don’t an — deine Freunde sehen, ob du durchhältst.',
    notFound: 'Nicht gefunden',
    notFoundHint: 'Wir konnten das nicht finden. Vielleicht wurde es entfernt.',
    overdue: 'Überfällig',
    archive: 'Archiv',
    openChat: 'Zum Chat',
    moreActions: 'Weitere Aktionen',
    teamMine: 'Wer dich prüft',
    teamTheirs: 'Dabei',
    owner: 'Von',
    proofOwedBy: (name: string) => `Noch kein Beweis von ${name}`,
    reminderEdit: 'Erinnerung ändern',
    reminderSubtitle: 'Wann möchtest du daran erinnert werden?',
    reminderTime: 'Uhrzeit',
    reminderSave: 'Speichern',
    reminderRemove: 'Keine Erinnerung',
  },

  /**
   * Die Pause — der Ausweg für Krankheit und Urlaub.
   *
   * Der Ton ist bewusst nüchtern: Wer aussetzt, hat nichts falsch gemacht. Die
   * Knappheit steht daneben, nicht als Drohung, sondern als Zahl.
   */
  pause: {
    open: 'Aussetzen',
    heading: 'Pause einlegen',
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
    heading: 'Beenden',
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
    subtitle: 'Beendete Dos & Don’ts',
    note: 'Hier passiert nichts mehr. Verlauf und Beweisbilder bleiben, bis du sie endgültig löschst.',
    empty: 'Noch nichts beendet',
    emptyHint: 'Was du beendest, landet hier — mit Verlauf und allen Beweisbildern.',
    closedOn: (day: string) => `Beendet am ${day}`,
    completedOn: (day: string) => `Abgeschlossen am ${day}`,
    delete: 'Endgültig löschen',
    deleteHeading: 'Endgültig löschen?',
    deleteBody: 'Verlauf, Nachrichten und alle Beweisbilder werden entfernt — für dich und für alle Beteiligten. Das lässt sich nicht rückgängig machen.',
    deleteConfirm: 'Endgültig löschen',
  },

  create: {
    heading: 'Neues Do oder Don’t',
    subtitle: 'Wozu verpflichtest du dich?',
    titleLabel: 'Titel',
    titlePlaceholder: 'z. B. dreimal die Woche laufen',
    descriptionLabel: 'Was zählt?',
    descriptionPlaceholder: 'z. B. mindestens 5 km, ohne Pause',
    descriptionHelp: 'Freiwillig. Deine Prüfer lesen es, bevor sie über einen Beweis abstimmen.',
    scheduleLabel: 'Fälligkeit',
    iconLabel: 'Symbol',
    intervalLabel: 'Alle wie viele Tage?',
    targetDateLabel: 'Bis zu welchem Tag?',
    weekdaysLabel: 'An welchen Tagen?',
    timesLabel: 'Wie oft?',
    periodLabel: 'Pro',
    dueOnLabel: 'Bis wann?',
    reminderLabel: 'Tägliche Erinnerung',
    friendsLabel: 'Wer prüft dich?',
    friendsHint: 'Mindestens eine Person. Sie sieht deine Beweise und stimmt ab — im Chat dazu.',
    friendsChosen: (count: number) => (count === 1 ? '1 ausgewählt' : `${count} ausgewählt`),
    submit: 'Anlegen',
    open: 'Neues Do oder Don’t anlegen',
    next: 'Weiter',
    stepOf: (step: number, total: number) => `Schritt ${step} von ${total}`,
    steps: ['Was', 'Rhythmus', 'Prüfer', 'Übersicht'],
    templatesLabel: 'Oder nimm eine Vorlage',
    templates: {
      run: '3× pro Woche laufen',
      read: 'Jeden Tag lesen',
      water: '2 L Wasser am Tag',
      early: 'Vor 7 Uhr aufstehen',
      gym: '2× pro Woche ins Gym',
      tidy: 'Jeden Sonntag aufräumen',
    },
    friendsSearch: 'Freunde durchsuchen',
    friendsNoMatch: 'Niemand mit diesem Namen.',
    reminderTimeLabel: 'Uhrzeit der Erinnerung',
    reviewTitle: 'Was',
    reviewDescription: 'Was zählt',
    reviewSchedule: 'Rhythmus',
    reviewFriends: 'Prüfer',
    reviewReminder: 'Erinnerung',
    reviewNoReminder: 'Keine',
    firstWindowToday: 'Dein erstes Fenster endet heute um Mitternacht.',
    firstWindowTomorrow: 'Dein erstes Fenster endet morgen um Mitternacht.',
    firstWindowOn: (day: string) => `Dein erstes Fenster endet am ${day} um Mitternacht.`,
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
    onceBy: (day: string) => `Einmalig · bis ${day}`,
    everyDay: 'Jeden Tag',
    everyNDays: (days: number) => (days === 1 ? 'Jeden Tag' : days === 7 ? 'Jede Woche' : `Alle ${days} Tage`),
    timesPer: (times: number, period: string) => `${times}× pro ${period}`,
    period: {
      Day: 'Tag',
      Week: 'Woche',
      Month: 'Monat',
    } satisfies Record<QuotaPeriod, string>,
    kind: {
      Interval: 'Regelmäßig',
      Weekdays: 'Wochentage',
      Times: 'X-mal',
      Once: 'Einmalig',
      Limit: 'Limit',
    },
    weekdayShort: ['Mo', 'Di', 'Mi', 'Do', 'Fr', 'Sa', 'So'],
    explainOnce: 'Einmal erledigen, bis zum gewählten Tag.',
    explainDaily: 'Jeden Tag ein Nachweis, bis Mitternacht.',
    explainInterval: 'Im gewählten Abstand ein Nachweis, jeweils bis Mitternacht.',
    explainWeekdays: 'An den gewählten Tagen ein Nachweis, jeweils bis Mitternacht.',
    explainTimes: (times: number, period: string) =>
      `${times} Nachweise pro ${period} — wann genau, ist dir überlassen.`,
  },

  /**
   * Ein Don't: ein Limit pro Zeitraum, Nutzungen, die man selbst einträgt, und
   * ein ehrlicher Rückfall (docs/adr/0037-donts.md).
   *
   * Der Ton ist nüchtern wie bei der Pause: Wer einen Rückfall einträgt, ist
   * ehrlich gewesen, und die App bedankt sich nicht dafür und schimpft nicht.
   */
  dont: {
    kindLabel: 'Was nimmst du dir vor?',
    kindDo: 'Ich will …',
    kindDont: 'Ich will nicht mehr …',
    titlePlaceholder: 'z. B. kein Alkohol unter der Woche',
    descriptionPlaceholder: 'z. B. ein Glas zum Essen am Wochenende zählt nicht',
    limitLabel: 'Wie oft höchstens?',
    limitNever: 'Nie',
    periodLabel: 'Pro',
    limit: (limit: number, period: string) => (limit === 0 ? 'Nie' : `Höchstens ${limit}× pro ${period}`),
    explainLimit: (limit: number, period: string) => (limit === 0
      ? `Kein einziges Mal. Jeder ${period} ohne ist durchgehalten.`
      : `Bis zu ${limit}× pro ${period} ist in Ordnung. Das ${limit + 1}. Mal ist ein Rückfall.`),
    limitStep: 'Limit',
    proofModeLabel: 'Wie prüfen dich deine Freunde?',
    proofMode: {
      SpotCheck: 'Stichprobe',
      ProofAtEnd: 'Beweis am Ende',
      HonestOnly: 'Nur ehrlich',
    } satisfies Record<DontProofMode, string>,
    proofModeHint: {
      SpotCheck: 'Zu einem zufälligen Moment am Tag ein Live-Foto, drei Minuten Zeit.',
      ProofAtEnd: 'Vor Ende des Zeitraums ein Foto oder Screenshot, z. B. deine Bildschirmzeit.',
      HonestOnly: 'Nur dein Wort und die Rückfall-Regel. Es gibt nichts abzustimmen.',
    } satisfies Record<DontProofMode, string>,
    templatesLabel: 'Oder nimm eine Vorlage',
    templates: {
      alcohol: 'Kein Alkohol',
      socialMedia: 'Kein Social Media nach 22 Uhr',
      smoking: 'Nicht rauchen',
      fastFood: 'Höchstens 2× Fast Food pro Woche',
      sugar: 'Kein Zucker',
    },
    used: (uses: number, limit: number) => (limit === 0
      ? (uses === 0 ? 'Bisher durchgehalten' : `${uses}× eingetragen`)
      : `${uses} von ${limit} genutzt`),
    run: (days: number) => (days < 0 ? 'Ab morgen' : days === 0 ? 'Ab heute' : days === 1 ? 'Seit 1 Tag' : `Seit ${days} Tagen`),
    runLabel: 'Ohne Rückfall',
    log: 'Eintragen',
    logFor: (title: string) => `Nutzung eintragen: ${title}`,
    logWithPhoto: 'Mit Foto',
    logWithPhotoFor: (title: string) => `Nutzung mit Foto eintragen: ${title}`,
    relapseHeading: 'Als Rückfall eintragen?',
    relapseBody: 'Damit ist dein Limit überschritten. Das zählt als Rückfall, nicht als verpasst: Deine Serie hält — erst der dritte Rückfall in Folge beendet sie.',
    relapseConfirm: 'Eintragen',
  },

  /**
   * Die Stichprobe (docs/adr/0038-spot-checks.md): drei Minuten für ein
   * Live-Foto von dem, was man gerade macht. Kurz und ohne Drohung — die Uhr
   * läuft auch so.
   */
  spotCheck: {
    heading: 'Stichprobe',
    prompt: 'Zeig deinen Freunden live, was du gerade machst.',
    left: (minutes: number, seconds: number) => `noch ${minutes}:${String(seconds).padStart(2, '0')}`,
    answer: 'Live-Foto aufnehmen',
    answerFor: (title: string) => `Stichprobe beantworten: ${title}`,
    over: 'Die Zeit ist um.',
  },

  /** One window: what it still wants, and what became of it. */
  window: {
    remaining: (remaining: number, required: number) => `Noch ${remaining} von ${required}`,
    dueToday: 'Heute fällig',
    dueBy: (day: string) => `Bis ${day}`,
    on: (day: string) => `Am ${day}`,
    from: (day: string) => `Ab ${day}`,
    done: 'Geschafft',
    kept: 'Durchgehalten',
    missed: 'Verpasst',
    relapse: 'Rückfall',
    open: 'Offen',
    paused: 'Ausgesetzt',
    leftMinutes: (minutes: number) => `noch ${minutes} Min.`,
    leftHours: (hours: number) => `noch ${hours} Std.`,
    leftDays: (days: number) => (days === 1 ? 'noch 1 Tag' : `noch ${days} Tage`),
    endsNow: 'läuft gerade ab',
    reminderAt: (time: string) => `Erinnerung um ${time}`,
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
    sharedGoal: 'Gemeinsam',
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
    sendPhoto: 'Foto senden',
    photo: 'Foto',
    photoGone: 'Foto nicht mehr verfügbar',
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
    mute: 'Stummschalten',
    unmute: 'Wieder benachrichtigen',
    muted: 'Stummgeschaltet',

    // The list, in three: what you have to deliver, what you are asked to
    // check, and conversations that are about no goal at all.
    sectionMine: 'Deine Dos & Don’ts',
    sectionMineHint: 'Du lieferst den Beweis',
    sectionFriends: 'Von Freunden',
    sectionFriendsHint: 'Du prüfst',
    sectionConversations: 'Unterhaltungen',
    awaitingVote: 'Wartet auf dein Urteil',
    openGoal: 'Details ansehen',

    /**
     * What happened to a goal, as its conversation says it between the
     * messages. Flat on purpose — a missed window is stated, never commented
     * on, and nothing here can be used to rub it in.
     */
    event: {
      created: (name: string | null) => (name ? `${name} hat es angelegt.` : 'Du hast es angelegt.'),
      windowDone: (streak: number) => `Geschafft · Streak ${streak}`,
      windowKept: (streak: number) => `Durchgehalten · Streak ${streak}`,
      windowRelapse: (streak: number) => (streak > 0 ? `Rückfall · Streak hält bei ${streak}` : 'Rückfall'),
      useLogged: (name: string | null, uses: number, limit: number | null) => {
        const count = limit === null ? `${uses}.` : uses <= limit ? `${uses} von ${limit}` : 'über dem Limit'
        return name ? `${name} hat eingetragen · ${count}` : `Du hast eingetragen · ${count}`
      },
      windowMissed: (confirmed: number, required: number) => (required > 1
        ? `Verpasst · ${confirmed} von ${required} geliefert`
        : 'Verpasst'),
      // No full stop of its own: the day is written "3.8." already.
      pauseStarted: (name: string | null, day: string) => (name ? `${name} setzt aus bis ${day}` : `Du setzt aus bis ${day}`),
      pauseEnded: 'Die Pause ist vorbei.',
      pauseOverturned: 'Die Pause wurde durch Einspruch aufgehoben.',
      completed: 'Abgeschlossen.',
      stopped: (name: string | null) => (name ? `${name} hat es beendet.` : 'Du hast es beendet.'),
    },

    /** The same, as the one line a row in the list has room for. */
    lastEvent: {
      Created: 'Angelegt',
      ProofDelivered: 'Neuer Beweis',
      WindowDone: 'Geschafft',
      WindowMissed: 'Verpasst',
      PauseStarted: 'Ausgesetzt',
      PauseEnded: 'Pause vorbei',
      PauseOverturned: 'Pause aufgehoben',
      Completed: 'Abgeschlossen',
      Stopped: 'Beendet',
      UseLogged: 'Eingetragen',
      WindowRelapse: 'Rückfall',
    } satisfies Record<GoalEventKind, string>,

    openInfo: 'Info zur Unterhaltung',
    infoMembers: 'Mitglieder',
    toGoal: 'Details ansehen',
    dayToday: 'Heute',
    dayYesterday: 'Gestern',
    onDay: (text: string, day: string) => `${text} · ${day}`,
    dayRange: (from: string, to: string) => `${from}–${to}`,
    missedRun: (count: number, range: string) => `${range} · ${count} Fenster verpasst`,
    // The streak a run ends on is at least its length; that is the default.
    keptRun: (count: number, range: string, streak = count) => `${range} · ${count} Fenster geschafft · Streak ${streak}`,
    heldRun: (count: number, range: string, streak = count) => `${range} · ${count}-mal durchgehalten · Streak ${streak}`,
    nextStep: (label: string, left: string) => `${label} · ${left}`,
  },

  friends: {
    heading: 'Freunde',

    // The screen and its tab are "Freunde": requests, suggestions and the
    // people you know are most of it, and finding somebody is the box on top.
    pageHeading: 'Freunde',
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
    goals: 'Abgeschlossen',
    activity: 'Letzte Aktivität',
    noActivity: 'Noch nichts passiert.',
    noActivityHint: 'Liefer einen Beweis, den deine Freunde glauben — er steht dann hier.',
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
    chooseScreenshot: 'Screenshot auswählen',
    labelledAs: 'Deine Freunde sehen:',
    preview: 'Vorschau',
    retake: 'Noch einmal',
    use: 'Verwenden',
    uploading: 'Wird hochgeladen …',
    cameraBlocked: 'Kein Zugriff auf die Kamera. Du kannst stattdessen ein Bild auswählen.',
    cameraRequired: 'Für eine Stichprobe braucht q2 die Kamera. Erlaube den Zugriff in den Einstellungen deines Geräts.',
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
    /**
     * Which way the photograph came in, on every proof (#61). "Live" says the
     * app's camera was used — not that the server could prove it was taken
     * just now, which nobody can.
     */
    source: {
      Live: 'Live',
      Screenshot: 'Screenshot',
      Unknown: 'Ohne Angabe',
    } satisfies Record<ProofSource, string>,
    confirmedByNobody: 'Noch niemand hat bestätigt.',
    confirmedBy: (names: string) => `Bestätigt von ${names}`,
    doubtCount: (count: number) => (count === 1 ? '1 Zweifel' : `${count} Zweifel`),
    doubtAnonymous: 'Wer zweifelt, bleibt anonym.',
    yourVerdict: 'Dein Urteil',
    votedConfirm: 'Du hast bestätigt.',
    votedDoubt: 'Du hast angezweifelt.',
    ownProof: 'Dein eigener Beweis — abstimmen dürfen die anderen.',
    changeToConfirm: 'Doch bestätigen',
    changeToDoubt: 'Doch anzweifeln',
    changeHint: 'Änderbar, bis abgestimmt ist.',
    enlarge: 'Foto vergrößern',
    refusedClosed: 'Dieses Fenster nimmt gerade keinen Beweis an.',
    refusedOffline: 'Keine Verbindung. Dein Foto ist noch da — versuch es gleich noch einmal.',
    refusedFailed: 'Der Beweis konnte nicht abgegeben werden. Versuch es noch einmal.',
  },

  /**
   * Your own photographs, on your profile. Only you see this collection, and
   * it says so — each photograph was shown to its goal's friends, but the
   * collection of them is nobody's but yours.
   */
  proofGallery: {
    heading: 'Deine Beweisfotos',
    subtitle: 'Nur du siehst diese Sammlung.',
    showAll: 'Alle ansehen',
    count: (count: number) => (count === 1 ? '1 Foto' : `${count} Fotos`),
    empty: 'Noch keine Beweisfotos.',
    emptyHint: 'Sobald du einen Beweis lieferst, landet das Foto hier.',
    months: ['Januar', 'Februar', 'März', 'April', 'Mai', 'Juni', 'Juli', 'August', 'September', 'Oktober', 'November', 'Dezember'],
    open: (what: string) => `${what} — groß ansehen`,
    deliveredOn: (date: string) => `Geliefert am ${date}`,
    openGoal: 'Details ansehen',
    unavailable: 'Foto nicht verfügbar',
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
    swipeHint: 'Rechts wischen bestätigt, links zweifelt an.',
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
    enlarge: (prompt: string) => `Beitrag zu „${prompt}“ vergrößern`,
  },

  /** The full-screen view any photograph opens into. */
  viewer: {
    label: 'Foto',
    close: 'Foto schließen',
    avatar: (name: string) => `Profilbild von ${name} vergrößern`,
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
    orSearch: 'Ist schon jemand hier? Dann such oben nach dem Namen.',
    meanwhile: 'Bis dahin: Bei der Challenge des Tages kannst du auch allein mitmachen.',
    open: 'Freunde einladen',
    sheetBody: 'Wer über deinen Link kommt, ist sofort mit dir befreundet — auch ohne Konto.',
    sheetHint: 'Ist der Link irgendwo gelandet, wo er nicht hin soll? In den Einstellungen kannst du ihn ersetzen.',
    settingsHeading: 'Einladungslink',
    notFoundHint: 'Nicht dabei? Schick deinen Link.',
    sendLink: 'Link schicken',
  },

  /**
   * Die Seite, auf der ein Einladungslink landet. Der Name steht in der Karte,
   * nicht in der Überschrift — dort kann er aus Session Replay herausgehalten
   * werden. Die Seite bietet genau einen nächsten Schritt an.
   */
  join: {
    title: 'Einladung',
    heading: 'Du bist eingeladen',
    intro: 'Qdos hilft, Vorsätze zu halten, weil Freunde zusehen. Wer über diesen Link kommt, ist sofort mit der Person befreundet, die ihn geschickt hat.',
    invites: 'lädt dich zu q2 ein',
    createAccount: 'Konto erstellen',
    haveAccount: 'Ich habe schon ein Konto',
    accept: 'Freundschaft annehmen',
    alreadyFriends: 'Ihr seid schon befreundet.',
    ownLink: 'Das ist dein eigener Link. Schick ihn jemandem, der noch fehlt.',
    invalidHeading: 'Dieser Link gilt nicht mehr',
    invalidIntro: 'Vielleicht wurde er inzwischen ersetzt. Frag nach einem neuen — oder leg einfach so los.',
    registerHint: 'Nach der Registrierung seid ihr befreundet.',
    previewTitle: 'Du bist zu q2 eingeladen',
  },

  /**
   * The first-run introduction (docs/adr/0039-first-run-onboarding.md).
   *
   * Said once, so it may say more than any other screen: what Qdos is, before
   * anybody is asked for anything. Still in the app's voice — plain, in the
   * second person, and honest that friends will see a miss. The sample lines
   * are what the illustrations show; they are words like any other.
   */
  onboarding: {
    title: 'Willkommen bei Qdos',
    progress: (step: number, total: number) => `Schritt ${step} von ${total}`,
    back: 'Zurück',
    skip: 'Überspringen',
    later: 'Später',
    next: 'Weiter',
    signOut: 'Nicht du? Abmelden',
    language: 'Sprache',
    welcome: {
      label: 'Was Qdos ist',
      slideOf: (slide: number, total: number) => `Folie ${slide} von ${total}`,
      start: 'Los geht’s',
      promise: {
        eyebrow: 'Qdos',
        heading: 'Versprich es dir — vor deinen Freunden.',
        body: 'Ob jeden Abend lesen oder dreimal die Woche laufen: Du sagst, was du vorhast. Deine Freunde sehen, ob es passiert.',
      },
      proof: {
        eyebrow: 'Der Beweis',
        heading: 'Ein Foto statt eines Hakens.',
        body: 'Selbst abhaken gibt es hier nicht. Du lieferst ein Foto, deine Freunde bestätigen es — oder zweifeln. Im Chat zu deinem Do.',
      },
      streak: {
        eyebrow: 'Der Streak',
        heading: 'Jedes gehaltene Fenster zählt.',
        body: 'Jede Frist, die du hältst, verlängert deine Serie. Verpasst ist verpasst — und deine Freunde sehen beides.',
      },
      sample: {
        read: 'Jeden Abend lesen',
        run: 'Dreimal die Woche laufen',
        phone: 'Kein Handy nach 22 Uhr',
        daily: 'Täglich',
        weekly: '3× pro Woche',
        dont: 'Don’t · höchstens 0× am Tag',
        initials: ['LM', 'JK', 'SR'],
        proofTitle: 'Jeden Abend lesen',
        proofMeta: 'Heute, 21:14 · Live',
        confirm: 'Bestätigen',
        doubt: 'Zweifeln',
        confirmed: '2 von 3 haben bestätigt',
        streakValue: '12',
        streakLabel: 'Tage in Folge',
      },
    },
    profile: {
      eyebrow: 'Dein Profil',
      heading: 'So sehen dich deine Freunde',
      intro: 'Ein Foto macht dich erkennbar, dein Kürzel auffindbar.',
      addPhoto: 'Foto aufnehmen',
      changePhoto: 'Anderes Foto',
      photoHint: 'Freiwillig — du kannst es jederzeit im Profil ändern.',
      name: 'Name',
      handle: 'Kürzel',
      handleHint: 'Nur jetzt änderbar. Danach bleibt es, wie es ist — Freunde finden dich darüber.',
      handleChecking: 'Wird geprüft …',
      handleAvailable: 'Frei',
      handleCurrent: 'Dein Kürzel',
      handleTaken: 'Das hat schon jemand.',
      handleInvalid: '3 bis 39 Zeichen: Buchstaben, Ziffern, Punkt, Bindestrich oder Unterstrich — am Anfang und Ende ein Buchstabe oder eine Ziffer.',
      saveFailed: 'Das hat nicht geklappt. Versuch es noch einmal.',
    },
    appearance: {
      eyebrow: 'Darstellung',
      heading: 'Mach es zu deinem',
      intro: 'Hell oder dunkel, und eine Farbe. Sie markiert immer das, was du gerade tun kannst — und sonst nichts.',
      theme: 'Hell oder dunkel',
      preview: 'Vorschau',
      previewTitle: 'Jeden Abend lesen',
      previewMeta: 'Heute · noch 4 Std.',
      previewAction: 'Foto',
    },
    firstDo: {
      eyebrow: 'Dein erstes Do',
      heading: 'Was nimmst du dir vor?',
      introWaiting: 'Noch prüft dich niemand. Dein Do wartet, bis dein erster Freund da ist — dann startet es von selbst.',
      introWithFriends: 'Wähl, wer dich prüft. Sobald du es anlegst, läuft dein erstes Fenster.',
      ideas: 'Oder nimm eine Idee',
      rhythm: 'Wie oft?',
      checkers: 'Wer prüft dich?',
      checkersHint: 'Sie sehen deine Fotos und stimmen ab.',
      submitWaiting: 'Vormerken',
      submitNow: 'Anlegen',
      waitingSaved: 'Vorgemerkt. Es startet mit deinem ersten Freund.',
      created: 'Angelegt. Dein erstes Fenster läuft.',
      change: 'Ändern',
      runningHeading: 'Deine Dos laufen schon',
      runningIntro: (count: number) => (count === 1
        ? 'Du hast schon ein laufendes Do oder Don’t. Neue legst du jederzeit mit dem Plus unter „Dos“ an.'
        : `Du hast schon ${count} laufende Dos & Don’ts. Neue legst du jederzeit mit dem Plus unter „Dos“ an.`),
    },
    invite: {
      eyebrow: 'Freunde',
      headingAlone: 'Wer soll zusehen?',
      introAlone: 'Schick deinen Link an jemanden, der dich kennt. Wer darüber kommt, ist sofort dein Freund — und prüft dein Do.',
      headingFriends: 'Mehr Augen, mehr Halt',
      introFriends: (names: string) => `Du bist schon mit ${names} befreundet. Mit jedem weiteren Freund wird es schwerer, sich herauszureden.`,
      and: 'und',
      others: (count: number) => (count === 1 ? 'einer weiteren Person' : `${count} weiteren`),
      share: 'Link teilen',
      copy: 'Kopieren',
      sent: 'Geteilt. Wer darüber kommt, ist sofort dein Freund.',
      leave: 'Erstmal ohne Freunde',
      leaveTitle: 'Ohne Freunde loslegen?',
      leaveBody: (title: string) => `Dein Do „${title}“ verfällt dann. Sobald jemand dabei ist, legst du jederzeit ein neues an.`,
      leaveConfirm: 'Loslegen',
      leaveFailed: 'Das hat nicht geklappt. Versuch es noch einmal.',
    },
    notifications: {
      eyebrow: 'Mitteilungen',
      heading: 'Damit nichts durchrutscht',
      intro: 'Wir melden uns, wenn ein Fenster bald endet, ein Freund dein Foto prüft oder deine Stimme gebraucht wird. Zwischen 22 und 7 Uhr ist Ruhe.',
      allow: 'Mitteilungen erlauben',
      on: 'Mitteilungen sind an.',
      blocked: 'Dein Browser hat Mitteilungen abgelehnt. Das lässt sich nur in seinen Einstellungen ändern.',
      sampleApp: 'Qdos',
      sampleTime: 'jetzt',
      sampleBody: 'Noch 3 Stunden für „Jeden Abend lesen“.',
    },
    done: {
      eyebrow: 'Geschafft',
      heading: 'Du bist dabei.',
      intro: 'Das hast du eingerichtet. Alles lässt sich später ändern — bis auf dein Kürzel.',
      photo: 'Profilfoto',
      photoSet: 'Gesetzt',
      photoMissing: 'Noch keins',
      handle: 'Kürzel',
      firstDo: 'Erstes Do',
      firstDoRunning: 'Läuft',
      firstDoWaiting: 'Wartet auf deinen ersten Freund',
      firstDoNone: 'Noch keins',
      friends: 'Freunde',
      friendsCount: (count: number) => (count === 1 ? '1 Freund' : `${count} Freunde`),
      friendsNone: 'Noch keine — dein Link wartet',
      notifications: 'Mitteilungen',
      notificationsOn: 'An',
      notificationsOff: 'Aus',
      start: 'Zur App',
      failed: 'Das hat nicht geklappt. Versuch es noch einmal.',
    },
  },

  /**
   * The short tour over the real tab bar, once, right after the introduction.
   * One sentence per tab: what it is for, not how it works.
   */
  tour: {
    label: 'Kurze Tour',
    stepOf: (step: number, total: number) => `${step} von ${total}`,
    next: 'Weiter',
    done: 'Fertig',
    skip: 'Tour beenden',
    home: { title: 'Start', body: 'Was als Nächstes fällig ist, mit der Kamera dazu. Darunter dein Streak und die Fotos, die auf deine Stimme warten.' },
    search: { title: 'Freunde', body: 'Leute finden, Anfragen beantworten und deinen Einladungslink teilen.' },
    dos: { title: 'Dos & Don’ts', body: 'Alles, was du dir vorgenommen hast. Mit dem Plus oben legst du Neues an.' },
    chats: { title: 'Chats', body: 'Jedes Do hat seinen eigenen Chat. Dort kommen deine Fotos an und werden geprüft.' },
    profile: { title: 'Profil', body: 'Dein Streak, deine Beweisfotos und die Einstellungen.' },
  },

  /**
   * A Do that waits for its owner's first friend (docs/adr/0039). Grey, never
   * accented, except for the one thing it lets somebody do now: send the link.
   */
  draft: {
    waiting: 'Wartet auf deinen ersten Freund',
    waitingHint: 'Startet von selbst, sobald jemand über deinen Link kommt.',
    share: 'Link teilen',
    discard: 'Verwerfen',
    discardTitle: 'Vorgemerktes Do verwerfen?',
    discardBody: 'Es startet dann nicht, wenn dein erster Freund kommt. Du kannst jederzeit ein neues vormerken.',
    discardConfirm: 'Verwerfen',
    createHint: 'Noch prüft dich niemand. Merk dein Do schon vor — es startet, sobald jemand über deinen Link dazukommt.',
    createHasDraft: 'Du hast schon ein Do vorgemerkt. Es startet, sobald dein erster Freund da ist.',
    homeHeading: 'Dein erster Schritt',
  },

  /** Das Konto löschen — der einzige Weg, der nicht zurückführt. */
  deleteAccount: {
    open: 'Konto löschen',
    heading: 'Konto endgültig löschen',
    body: 'Deine Dos & Don’ts, deine Fenster, deine Bilder, deine Direktchats und dein Streak werden gelöscht. In Gruppen verschwinden deine Nachrichten, der Rest bleibt bei den anderen. Das lässt sich nicht rückgängig machen.',
    password: 'Passwort zur Bestätigung',
    passwordHint: 'Wir fragen noch einmal, weil das hier nicht zurückzunehmen ist.',
    confirm: 'Endgültig löschen',
    wrongPassword: 'Das Passwort stimmt nicht.',
    failed: 'Das Konto konnte nicht gelöscht werden.',
  },

  /**
   * What a push says when it cannot say anything more specific: a payload this
   * version of the app does not understand, or none at all. Everything else a
   * notification says is in `notify`.
   */
  push: {
    generic: 'Es gibt etwas Neues.',
  },

  /**
   * The bell, and everything a notification says.
   *
   * One wording for both ways a notification arrives: the same sentence is a
   * line in the bell and the body on a lock screen, composed from a kind, a
   * name and a subject by `notificationText` in app/utils/display.ts. The
   * server never sends a sentence, which is what lets it arrive in the language
   * the person chose — and the service worker imports this file for exactly
   * that.
   *
   * A sentence that names somebody is written without the name, the way the
   * feed writes it: the bell sets the name in bold in front, and several people
   * reacting to one thing need "haben" where one needs "hat".
   *
   * Flat, like everything about somebody else's doing: who, what, and nothing
   * more. A verdict and a lifted pause never have a name, because doubt and
   * objection are anonymous.
   */
  notify: {
    heading: 'Mitteilungen',
    open: 'Mitteilungen öffnen',
    openWithCount: (count: number) => (count === 1 ? 'Mitteilungen öffnen, 1 neue' : `Mitteilungen öffnen, ${count} neue`),
    fresh: 'Neu',
    earlier: 'Früher',
    new: 'Neu',
    dismiss: 'Löschen',
    clearAll: 'Alle löschen',
    clearHeading: 'Alle Mitteilungen löschen?',
    clearBody: 'Alles, was du hier gerade siehst, wird endgültig gelöscht — auch auf deinen anderen Geräten.',
    clearConfirm: 'Alle löschen',
    empty: 'Noch nichts für dich.',
    emptyHint: 'Wenn jemand deine Anfrage annimmt, auf deinen Beweis reagiert oder dich als Prüfer einlädt, steht es hier.',
    someone: 'Jemand',
    others: (name: string, others: number) => (others === 1
      ? `${name} und eine weitere Person`
      : `${name} und ${others} weitere`),
    titles: {
      friends: 'Freunde',
      vote: 'Glaubst du das?',
      result: 'Dein Beweis',
      reaction: 'Kudos',
      goals: 'Dos & Don’ts',
      risk: 'Wird knapp',
      spotCheck: 'Stichprobe',
    },
    messageInGroup: (name: string, group: string) => `${name} in ${group}`,
    messageFallback: 'Neue Nachricht',
    friendRequest: 'möchte mit dir befreundet sein.',
    friendshipStarted: 'und du seid jetzt befreundet.',
    proofAwaitingVote: (goal: string) => `hat einen Beweis für „${goal}“ geliefert. Glaubst du es?`,
    proofConfirmed: (goal: string) => `Dein Beweis für „${goal}“ wurde bestätigt.`,
    proofRefused: (goal: string) => `Dein Beweis für „${goal}“ wurde nicht anerkannt.`,
    proofRefusedRetry: (goal: string) => `Dein Beweis für „${goal}“ wurde nicht anerkannt. Du hast noch einen Versuch.`,
    reactedToProof: (people: number, goal: string) => (people === 1
      ? `hat auf deinen Beweis für „${goal}“ reagiert.`
      : `haben auf deinen Beweis für „${goal}“ reagiert.`),
    reactedToMessage: (people: number): string => (people === 1
      ? 'hat auf deine Nachricht reagiert.'
      : 'haben auf deine Nachricht reagiert.'),
    reactedToChallenge: (people: number): string => (people === 1
      ? 'hat auf deinen Challenge-Beitrag reagiert.'
      : 'haben auf deinen Challenge-Beitrag reagiert.'),
    gaveKudos: (people: number, subject: string) =>
      `${people === 1 ? 'hat' : 'haben'} dir Kudos ${subject ? `für „${subject}“ ` : ''}gegeben.`,
    goalInvitation: (goal: string) => `hat dich zu „${goal}“ eingeladen. Du stimmst über die Beweise mit ab.`,
    goalPaused: (goal: string, days: number) => (days === 1
      ? `setzt „${goal}“ für einen Tag aus.`
      : `setzt „${goal}“ für ${days} Tage aus.`),
    pauseLifted: (goal: string) => `Einsprüche haben die Pause für „${goal}“ beendet. Das Fenster läuft wieder.`,
    friendAtRisk: (goal: string, missing: number) => (missing === 1
      ? `droht „${goal}“ zu verpassen — ein Nachweis fehlt noch.`
      : `droht „${goal}“ zu verpassen — es fehlen noch ${missing} Nachweise.`),
    spotCheckDue: (goal: string) => `„${goal}“: Zeig in den nächsten drei Minuten live, was du gerade machst.`,
  },

  /**
   * The notification screen, under Profil → Einstellungen: one switch per thing
   * somebody can decide to be interrupted for, grouped the way a person thinks
   * about them rather than the way the server counts them.
   */
  notificationSettings: {
    heading: 'Benachrichtigungen',
    device: 'Dieses Gerät',
    deviceNote: 'Erlaubnis und Empfang gelten nur für diesen Browser. Die Schalter darunter gelten für jedes Gerät, auf dem du Benachrichtigungen eingeschaltet hast.',
    friends: 'Freunde',
    goals: 'Dos & Don’ts',
    encouragement: 'Rückmeldungen',
    challenge: 'Challenge',
    messages: 'Nachrichten',
    friendships: 'Freundschaftsanfragen',
    votesDue: 'Ein Beweis wartet auf dein Urteil',
    proofResults: 'Urteil über deine Beweise',
    goalUpdates: 'Einladungen & Pausen',
    friendsAtRisk: 'Wird knapp bei Freunden',
    spotChecks: 'Stichproben bei deinen Don’ts',
    reactions: 'Kudos & Reaktionen',
    dailyChallenge: 'Challenge des Tages',
    switchesNote: 'Die Schalter entscheiden, was dich unterbrechen darf: ein Push auf dem Handy oder ein Hinweis oben in der offenen App. In der Glocke steht trotzdem alles.',
  },

  activityOverview: {
    heading: 'Aktivität',
    empty: 'Noch nichts passiert.',
    emptyHint: 'Wenn deine Freunde etwas liefern, steht es hier.',
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
    relapsed: (count: number) => (count === 1 ? '1 Rückfall' : `${count} Rückfälle`),
    clean: 'Noch nichts verpasst.',
    /** What a profile says when the two of them share nothing. */
    nothingShared: 'Ihr habt noch nichts gemeinsam.',
    nothingSharedHint: 'Die Bilanz zeigt nur Dos & Don’ts, auf die ihr beide eingeladen seid.',
    sharedGoals: (count: number) => (count === 1 ? '1 gemeinsames Do oder Don’t' : `${count} gemeinsame Dos & Don’ts`),
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
    moreActions: 'Weitere Aktionen',
    removeFriend: 'Als Freund entfernen',
    reportOrBlock: 'Melden oder blockieren',
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
    accent: 'Akzentfarbe',
    accentNote: 'Gilt auf diesem Gerät. Passt sich an Hell und Dunkel an.',
    accents: { iris: 'Iris', sage: 'Salbei', rose: 'Rosé', ochre: 'Ocker' },
    language: 'Sprache',
    languageGerman: 'Deutsch',
    languageEnglish: 'English',
    notifications: 'Benachrichtigungen',
    notificationsNote: 'Welche davon dich unterbrechen dürfen, und wann es still bleibt.',
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
    taskCompleted: (subject: string) => `hat bei „${subject}“ geliefert`,
    streakReached: (days: number) => `hat einen ${days}-Tage-Streak erreicht`,
    goalProgress: (subject: string, percent: number) => `ist bei „${subject}“ auf ${percent}\u00A0%`,
    goalCreated: (subject: string) => `hat sich etwas vorgenommen: ${subject}`,
    /** Named after the line, so five buttons on one screen are five different buttons (#103). */
    giveKudos: (line: string) => `Kudos geben: ${line}`,
    takeBackKudos: (line: string) => `Kudos zurücknehmen: ${line}`,
    openPerson: (name: string) => `Profil von ${name} öffnen`,
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
    rateLimited: 'Das waren gerade zu viele Anfragen. Warte ein paar Minuten und versuch es dann erneut.',
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
    body: 'Qdos braucht eine Verbindung, um deine Dos & Don’ts zu laden. Sobald du wieder Empfang hast, geht es weiter.',
    retry: 'Erneut versuchen',
  },

  /*
   * An icon name rather than an emoji, like everywhere else in the interface.
   * Every one of these has to be in the bundled icon list in nuxt.config.ts —
   * the scanner only sees literal strings, and these are the literals.
   */
  toast: {
    groupLeft: { icon: 'i-lucide-log-out', text: 'Gruppe verlassen' },
    reported: { icon: 'i-lucide-flag', text: 'Danke — wir sehen es uns an' },
    blocked: { icon: 'i-lucide-shield', text: 'Person blockiert' },
    inviteCopied: { icon: 'i-lucide-link', text: 'Link kopiert' },
    befriended: { icon: 'i-lucide-user-check', text: 'Ihr seid jetzt befreundet' },
    inviteReplaced: { icon: 'i-lucide-link', text: 'Neuer Link — der alte gilt nicht mehr' },
    goalDeleted: { icon: 'i-lucide-trash-2', text: 'Endgültig gelöscht' },
    feedbackUnavailable: { icon: 'i-lucide-circle-alert', text: 'Feedback lässt sich gerade nicht öffnen' },
    proofCounted: { icon: 'i-lucide-check', text: 'Beweis gezählt' },
    proofWaiting: { icon: 'i-lucide-hourglass', text: 'Beweis abgegeben — deine Freunde prüfen ihn' },
    useLogged: { icon: 'i-lucide-check', text: 'Eingetragen' },
    useUndone: { icon: 'i-lucide-undo-2', text: 'Zurückgenommen' },
    undoTooLate: { icon: 'i-lucide-clock', text: 'Zu spät — der Eintrag bleibt' },
    undo: 'Rückgängig',
    spotCheckAnswered: { icon: 'i-lucide-hourglass', text: 'Stichprobe abgegeben — deine Freunde prüfen sie' },
    relapseLogged: { icon: 'i-lucide-rotate-ccw', text: 'Als Rückfall eingetragen' },
    draftSaved: { icon: 'i-lucide-hourglass', text: 'Vorgemerkt — startet mit deinem ersten Freund' },
    draftDiscarded: { icon: 'i-lucide-trash-2', text: 'Verworfen' },
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
    friends: 'Friends',
    dos: 'Dos',
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
    signInHeading: 'Sign in',
    signInIntro: 'Qdos helps you keep what you set out to do — because your friends see whether you do.',
    signUpHeading: 'Create an account',
    signUpIntro: 'A name, an email address, a password — that is all it takes.',
    name: 'Name',
    namePlaceholder: 'How should your friends see you?',
    email: 'Email address',
    emailPlaceholder: 'you@example.com',
    password: 'Password',
    passwordPlaceholder: 'At least 10 characters',
    passwordHint: (minimum: number) => `At least ${minimum} characters. Longer beats complicated.`,
    showPassword: 'Show password',
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
    forgotPassword: 'Forgot your password?',
    forgotHeading: 'Forgot your password',
    forgotIntro: 'Enter the address of your account. We will send you a link to set a new password.',
    forgotSubmit: 'Send the link',
    forgotSentHeading: 'Check your inbox',
    forgotSentIntro: 'If there is an account with this address, an email with a link is on its way. The link works for one hour.',
    forgotSentHint: 'Nothing arrived? Check your spam folder, or ask for a new link in a few minutes.',
    forgotAgain: 'Ask for a new link',
    rememberedIt: 'Remembered it after all?',
    recoveryUnavailable: 'This environment sends no email, so a forgotten password cannot be reset here yet.',
    resetHeading: 'New password',
    resetIntro: 'Choose a new password. Afterwards you are signed out on every device and sign in again with the new one.',
    newPassword: 'New password',
    resetSubmit: 'Save the password',
    resetInvalidHeading: 'This link no longer works',
    resetInvalid: 'It has expired or has already been used. Just ask for a new one.',
    resetDoneHeading: 'Password changed',
    resetDoneIntro: 'You are now signed out on every device. Sign in again with your new password — in the browser and in the app.',
  },

  home: {
    greeting: (hour: number) => (hour < 11 ? 'Good morning' : hour < 18 ? 'Hello' : 'Good evening'),
    streakDays: (days: number) => (days === 1 ? '1 day in a row' : `${days} days in a row`),
    noStreak: 'No streak yet',
    todayHeading: 'Today',
    nextUpHeading: 'Up next',
    openProfile: 'Open profile',
    feedHeading: 'What your friends did',
    noTasks: 'Nothing on today.',
    noTasksHint: 'Add something under Dos and it will show up here.',
    noGoals: 'No Dos & Don’ts yet.',
    noFeed: 'Nothing from your friends yet.',
    noFeedHint: 'As soon as somebody gets something done, it appears here.',
  },

  goals: {
    heading: 'Dos & Don’ts',
    new: 'New',
    tabDos: 'Dos',
    tabDonts: 'Don’ts',
    sectionMine: 'Yours',
    sectionMineHint: 'You deliver the proof',
    sectionFriends: 'From friends',
    sectionFriendsHint: 'You check',
    group: 'GROUP',
    detailHeading: 'Details',
    recordProof: 'Done',
    recordedProof: 'Logged',
    streak: 'Streak',
    streakDays: (days: number) => (days === 1 ? '1 window' : `${days} windows`),
    reminder: 'Reminder',
    noReminder: 'None',
    cheer: 'Cheer on',
    balance: 'Balance',
    balanceValue: (done: number, missed: number) => `${done} · ${missed}`,
    balanceCaption: 'kept · missed',
    historyHeading: 'History',
    noHistory: 'No closed window yet.',
    noHistoryHint: 'Once the first one is over, it shows up here.',
    noDos: 'No Dos yet',
    noDosHint: 'Add your first Do with the plus at the top — your friends will see whether you keep at it.',
    noDonts: 'No Don’ts yet',
    noDontsHint: 'Add your first Don’t with the plus at the top — your friends will see whether you hold out.',
    notFound: 'Not found',
    notFoundHint: 'We could not find that. It may have been removed.',
    overdue: 'Overdue',
    archive: 'Archive',
    openChat: 'Open chat',
    moreActions: 'More actions',
    teamMine: 'Who checks you',
    teamTheirs: 'On it',
    owner: 'By',
    proofOwedBy: (name: string) => `No proof from ${name} yet`,
    reminderEdit: 'Change reminder',
    reminderSubtitle: 'When would you like to be reminded of it?',
    reminderTime: 'Time',
    reminderSave: 'Save',
    reminderRemove: 'No reminder',
  },

  pause: {
    open: 'Set aside',
    heading: 'Take a pause',
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
    heading: 'Stop',
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
    subtitle: 'Dos & Don’ts that have stopped',
    note: 'Nothing happens here any more. History and photographs stay until you delete them for good.',
    empty: 'Nothing stopped yet',
    emptyHint: 'Whatever you stop lands here — with its history and every photograph.',
    closedOn: (day: string) => `Stopped on ${day}`,
    completedOn: (day: string) => `Completed on ${day}`,
    delete: 'Delete for good',
    deleteHeading: 'Delete for good?',
    deleteBody: 'History, messages and every photograph are removed — for you and for everybody on it. This cannot be undone.',
    deleteConfirm: 'Delete for good',
  },

  create: {
    heading: 'New Do or Don’t',
    subtitle: 'What are you committing to?',
    titleLabel: 'Title',
    titlePlaceholder: 'e.g. run three times a week',
    descriptionLabel: 'What counts?',
    descriptionPlaceholder: 'e.g. at least 5 km, without a break',
    descriptionHelp: 'Optional. Your checkers read it before they vote on a proof.',
    scheduleLabel: 'Due',
    iconLabel: 'Icon',
    intervalLabel: 'Every how many days?',
    targetDateLabel: 'By which day?',
    weekdaysLabel: 'On which days?',
    timesLabel: 'How often?',
    periodLabel: 'Per',
    dueOnLabel: 'By when?',
    reminderLabel: 'Daily reminder',
    friendsLabel: 'Who checks you?',
    friendsHint: 'At least one person. They see your proof and vote on it — in its chat.',
    friendsChosen: (count: number) => `${count} selected`,
    submit: 'Create',
    open: 'Add a Do or Don’t',
    next: 'Next',
    stepOf: (step: number, total: number) => `Step ${step} of ${total}`,
    steps: ['What', 'Rhythm', 'Checkers', 'Summary'],
    templatesLabel: 'Or start from an idea',
    templates: {
      run: 'Run 3× a week',
      read: 'Read every day',
      water: 'Drink 2 L of water a day',
      early: 'Get up before 7',
      gym: 'Gym 2× a week',
      tidy: 'Tidy up every Sunday',
    },
    friendsSearch: 'Search friends',
    friendsNoMatch: 'Nobody by that name.',
    reminderTimeLabel: 'Reminder time',
    reviewTitle: 'What',
    reviewDescription: 'What counts',
    reviewSchedule: 'Rhythm',
    reviewFriends: 'Checkers',
    reviewReminder: 'Reminder',
    reviewNoReminder: 'None',
    firstWindowToday: 'Your first window ends tonight at midnight.',
    firstWindowTomorrow: 'Your first window ends tomorrow at midnight.',
    firstWindowOn: (day: string) => `Your first window ends on ${day} at midnight.`,
  },

  schedule: {
    once: 'Once',
    onceBy: (day: string) => `Once · by ${day}`,
    everyDay: 'Every day',
    everyNDays: (days: number) => (days === 1 ? 'Every day' : days === 7 ? 'Every week' : `Every ${days} days`),
    timesPer: (times: number, period: string) => `${times}× per ${period}`,
    period: {
      Day: 'day',
      Week: 'week',
      Month: 'month',
    },
    kind: {
      Interval: 'Regularly',
      Weekdays: 'Weekdays',
      Times: 'X times',
      Once: 'Once',
      Limit: 'Limit',
    },
    weekdayShort: ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'],
    explainOnce: 'Do it once, by the chosen day.',
    explainDaily: 'One proof every day, by midnight.',
    explainInterval: 'One proof at the chosen interval, each by midnight.',
    explainWeekdays: 'One proof on each chosen day, by midnight.',
    explainTimes: (times: number, period: string) =>
      `${times} proofs per ${period} — when exactly is up to you.`,
  },

  dont: {
    kindLabel: 'What are you taking on?',
    kindDo: 'I want to …',
    kindDont: 'I want to stop …',
    titlePlaceholder: 'e.g. no alcohol on weekdays',
    descriptionPlaceholder: 'e.g. a glass with dinner at the weekend does not count',
    limitLabel: 'How often at most?',
    limitNever: 'Never',
    periodLabel: 'Per',
    limit: (limit: number, period: string) => (limit === 0 ? 'Never' : `At most ${limit}× per ${period}`),
    explainLimit: (limit: number, period: string) => (limit === 0
      ? `Not once. Every ${period} without it is kept.`
      : `Up to ${limit}× per ${period} is fine. Time ${limit + 1} is a relapse.`),
    limitStep: 'Limit',
    proofModeLabel: 'How do your friends check you?',
    proofMode: {
      SpotCheck: 'Spot check',
      ProofAtEnd: 'Proof at the end',
      HonestOnly: 'Honest only',
    },
    proofModeHint: {
      SpotCheck: 'A live photo at a random moment of the day, with three minutes to take it.',
      ProofAtEnd: 'A photo or screenshot before the period ends, such as your screen time.',
      HonestOnly: 'Only your word and the relapse rule. There is nothing to vote on.',
    },
    templatesLabel: 'Or start from an idea',
    templates: {
      alcohol: 'No alcohol',
      socialMedia: 'No social media after 10 pm',
      smoking: 'No smoking',
      fastFood: 'Fast food at most twice a week',
      sugar: 'No sugar',
    },
    used: (uses: number, limit: number) => (limit === 0
      ? (uses === 0 ? 'Kept so far' : `logged ${uses}×`)
      : `${uses} of ${limit} used`),
    run: (days: number) => (days < 0 ? 'From tomorrow' : days === 0 ? 'From today' : days === 1 ? 'For 1 day' : `For ${days} days`),
    runLabel: 'Without a relapse',
    log: 'Log',
    logFor: (title: string) => `Log a use: ${title}`,
    logWithPhoto: 'With a photo',
    logWithPhotoFor: (title: string) => `Log a use with a photo: ${title}`,
    relapseHeading: 'Log this as a relapse?',
    relapseBody: 'This goes over your limit. It counts as a relapse, not as missed: your streak holds — only the third relapse in a row ends it.',
    relapseConfirm: 'Log it',
  },

  spotCheck: {
    heading: 'Spot check',
    prompt: 'Show your friends, live, what you are doing right now.',
    left: (minutes: number, seconds: number) => `${minutes}:${String(seconds).padStart(2, '0')} left`,
    answer: 'Take a live photo',
    answerFor: (title: string) => `Answer the spot check: ${title}`,
    over: 'Time is up.',
  },

  window: {
    remaining: (remaining: number, required: number) => `${remaining} of ${required} left`,
    dueToday: 'Due today',
    dueBy: (day: string) => `By ${day}`,
    on: (day: string) => `On ${day}`,
    from: (day: string) => `From ${day}`,
    done: 'Kept',
    kept: 'Held',
    missed: 'Missed',
    relapse: 'Relapse',
    open: 'Open',
    paused: 'Set aside',
    leftMinutes: (minutes: number) => `${minutes} min left`,
    leftHours: (hours: number) => `${hours} h left`,
    leftDays: (days: number) => (days === 1 ? '1 day left' : `${days} days left`),
    endsNow: 'ending now',
    reminderAt: (time: string) => `Reminder at ${time}`,
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
    sharedGoal: 'Together',
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
    sendPhoto: 'Send a photo',
    photo: 'Photo',
    photoGone: 'Photo no longer available',
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
    mute: 'Mute',
    unmute: 'Unmute',
    muted: 'Muted',

    sectionMine: 'Your Dos & Don’ts',
    sectionMineHint: 'You deliver the proof',
    sectionFriends: 'From friends',
    sectionFriendsHint: 'You check',
    sectionConversations: 'Conversations',
    awaitingVote: 'Waiting for your verdict',
    openGoal: 'View details',

    event: {
      created: (name: string | null) => (name ? `${name} created it.` : 'You created it.'),
      windowDone: (streak: number) => `Done · streak ${streak}`,
      windowKept: (streak: number) => `Held · streak ${streak}`,
      windowRelapse: (streak: number) => (streak > 0 ? `Relapse · streak holds at ${streak}` : 'Relapse'),
      useLogged: (name: string | null, uses: number, limit: number | null) => {
        const count = limit === null ? `#${uses}` : uses <= limit ? `${uses} of ${limit}` : 'over the limit'
        return name ? `${name} logged a use · ${count}` : `You logged a use · ${count}`
      },
      windowMissed: (confirmed: number, required: number) => (required > 1
        ? `Missed · ${confirmed} of ${required} delivered`
        : 'Missed'),
      pauseStarted: (name: string | null, day: string) => (name ? `${name} is pausing until ${day}` : `You are pausing until ${day}`),
      pauseEnded: 'The pause is over.',
      pauseOverturned: 'The pause was lifted by objection.',
      completed: 'Completed.',
      stopped: (name: string | null) => (name ? `${name} stopped it.` : 'You stopped it.'),
    },

    lastEvent: {
      Created: 'Created',
      ProofDelivered: 'New proof',
      WindowDone: 'Done',
      WindowMissed: 'Missed',
      PauseStarted: 'Paused',
      PauseEnded: 'Pause over',
      PauseOverturned: 'Pause lifted',
      Completed: 'Completed',
      Stopped: 'Stopped',
      UseLogged: 'Logged',
      WindowRelapse: 'Relapse',
    } satisfies Record<GoalEventKind, string>,

    openInfo: 'Conversation info',
    infoMembers: 'Members',
    toGoal: 'View details',
    dayToday: 'Today',
    dayYesterday: 'Yesterday',
    onDay: (text: string, day: string) => `${text} · ${day}`,
    dayRange: (from: string, to: string) => `${from}–${to}`,
    missedRun: (count: number, range: string) => `${range} · ${count} windows missed`,
    keptRun: (count: number, range: string, streak = count) => `${range} · ${count} windows done · streak ${streak}`,
    heldRun: (count: number, range: string, streak = count) => `${range} · held ${count} times · streak ${streak}`,
    nextStep: (label: string, left: string) => `${label} · ${left}`,
  },

  friends: {
    heading: 'Friends',
    pageHeading: 'Friends',
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
    goals: 'Completed',
    activity: 'Recent activity',
    noActivity: 'Nothing has happened yet.',
    noActivityHint: 'Deliver a proof your friends believe, and it shows up here.',
    openSettings: 'Open settings',
  },

  photo: {
    heading: 'Take a photo',
    cameraHint: 'Point at it and press the shutter.',
    fileHint: 'Pick a picture from your device.',
    shutter: 'Shutter',
    switchCamera: 'Switch camera',
    chooseFile: 'Choose a picture',
    chooseScreenshot: 'Choose a screenshot',
    labelledAs: 'Your friends will see:',
    preview: 'Preview',
    retake: 'Again',
    use: 'Use it',
    uploading: 'Uploading …',
    cameraBlocked: 'No access to the camera. You can pick a picture instead.',
    cameraRequired: 'A spot check needs the camera. Allow access in your device\'s settings.',
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
    source: {
      Live: 'Live',
      Screenshot: 'Screenshot',
      Unknown: 'Not recorded',
    },
    confirmedByNobody: 'Nobody has confirmed it yet.',
    confirmedBy: (names: string) => `Confirmed by ${names}`,
    doubtCount: (count: number) => (count === 1 ? '1 doubt' : `${count} doubts`),
    doubtAnonymous: 'Doubting stays anonymous.',
    yourVerdict: 'Your verdict',
    votedConfirm: 'You confirmed it.',
    votedDoubt: 'You doubted it.',
    ownProof: 'Your own proof — the others do the voting.',
    changeToConfirm: 'Confirm instead',
    changeToDoubt: 'Doubt instead',
    changeHint: 'You can change it until the vote closes.',
    enlarge: 'Enlarge photo',
    refusedClosed: 'This window is not taking a proof right now.',
    refusedOffline: 'No connection. Your photo is still here — try again in a moment.',
    refusedFailed: 'The proof could not be delivered. Please try again.',
  },

  proofGallery: {
    heading: 'Your proof photos',
    subtitle: 'Only you can see this collection.',
    showAll: 'Show all',
    count: (count: number) => (count === 1 ? '1 photo' : `${count} photos`),
    empty: 'No proof photos yet.',
    emptyHint: 'As soon as you deliver a proof, the photo lands here.',
    months: ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December'],
    open: (what: string) => `${what} — view full size`,
    deliveredOn: (date: string) => `Delivered on ${date}`,
    openGoal: 'View details',
    unavailable: 'Photo unavailable',
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
    swipeHint: 'Swipe right to confirm, left to doubt.',
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
    enlarge: (prompt: string) => `Enlarge contribution to “${prompt}”`,
  },

  viewer: {
    label: 'Photo',
    close: 'Close photo',
    avatar: (name: string) => `Enlarge ${name}'s profile picture`,
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
    orSearch: 'Somebody already here? Then search above for their name.',
    meanwhile: 'Until then: the challenge of the day is something you can do on your own.',
    open: 'Invite friends',
    sheetBody: 'Whoever comes through your link is your friend straight away — even without an account yet.',
    sheetHint: 'Has the link ended up somewhere it should not be? You can replace it in the settings.',
    settingsHeading: 'Invite link',
    notFoundHint: 'Not here? Send them your link.',
    sendLink: 'Send link',
  },

  join: {
    title: 'Invitation',
    heading: 'You are invited',
    intro: 'Qdos helps you keep what you set out to do, because friends are watching. Whoever comes through this link is friends straight away with the person who sent it.',
    invites: 'invites you to q2',
    createAccount: 'Create an account',
    haveAccount: 'I already have an account',
    accept: 'Accept friendship',
    alreadyFriends: 'You are already friends.',
    ownLink: 'This is your own link. Send it to somebody who is still missing.',
    invalidHeading: 'This link no longer works',
    invalidIntro: 'It may have been replaced since. Ask for a new one — or just get started.',
    registerHint: 'Once you have signed up, you are friends.',
    previewTitle: 'You are invited to q2',
  },

  onboarding: {
    title: 'Welcome to Qdos',
    progress: (step: number, total: number) => `Step ${step} of ${total}`,
    back: 'Back',
    skip: 'Skip',
    later: 'Later',
    next: 'Next',
    signOut: 'Not you? Sign out',
    language: 'Language',
    welcome: {
      label: 'What Qdos is',
      slideOf: (slide: number, total: number) => `Slide ${slide} of ${total}`,
      start: 'Let’s go',
      promise: {
        eyebrow: 'Qdos',
        heading: 'Promise it — in front of your friends.',
        body: 'Reading every evening or running three times a week: you say what you are going to do. Your friends see whether it happens.',
      },
      proof: {
        eyebrow: 'The proof',
        heading: 'A photo, not a tick.',
        body: 'Nobody ticks their own box here. You deliver a photo, and your friends confirm it — or doubt it. In the chat of your Do.',
      },
      streak: {
        eyebrow: 'The streak',
        heading: 'Every window kept counts.',
        body: 'Every deadline you keep extends your run. Missed is missed — and your friends see both.',
      },
      sample: {
        read: 'Read every evening',
        run: 'Run three times a week',
        phone: 'No phone after 10 pm',
        daily: 'Every day',
        weekly: '3× a week',
        dont: 'Don’t · at most 0× a day',
        initials: ['LM', 'JK', 'SR'],
        proofTitle: 'Read every evening',
        proofMeta: 'Today, 9:14 pm · Live',
        confirm: 'Confirm',
        doubt: 'Doubt',
        confirmed: '2 of 3 have confirmed',
        streakValue: '12',
        streakLabel: 'days in a row',
      },
    },
    profile: {
      eyebrow: 'Your profile',
      heading: 'How your friends see you',
      intro: 'A photo makes you recognisable, your handle findable.',
      addPhoto: 'Take a photo',
      changePhoto: 'Another photo',
      photoHint: 'Optional — you can change it on your profile any time.',
      name: 'Name',
      handle: 'Handle',
      handleHint: 'Only now can it change. After that it stays — it is how friends find you.',
      handleChecking: 'Checking …',
      handleAvailable: 'Available',
      handleCurrent: 'Your handle',
      handleTaken: 'Somebody already has that.',
      handleInvalid: '3 to 39 characters: letters, digits, dots, hyphens or underscores — starting and ending with a letter or a digit.',
      saveFailed: 'That did not work. Try again.',
    },
    appearance: {
      eyebrow: 'Appearance',
      heading: 'Make it yours',
      intro: 'Light or dark, and one colour. It always marks what you can do right now — and nothing else.',
      theme: 'Light or dark',
      preview: 'Preview',
      previewTitle: 'Read every evening',
      previewMeta: 'Today · 4 h left',
      previewAction: 'Photo',
    },
    firstDo: {
      eyebrow: 'Your first Do',
      heading: 'What are you setting out to do?',
      introWaiting: 'Nobody checks you yet. Your Do waits until your first friend arrives — then it starts by itself.',
      introWithFriends: 'Choose who checks you. The moment you create it, your first window runs.',
      ideas: 'Or take an idea',
      rhythm: 'How often?',
      checkers: 'Who checks you?',
      checkersHint: 'They see your photos and vote on them.',
      submitWaiting: 'Keep it waiting',
      submitNow: 'Create',
      waitingSaved: 'Saved. It starts with your first friend.',
      created: 'Created. Your first window is running.',
      change: 'Change',
      runningHeading: 'Your Dos are already running',
      runningIntro: (count: number) => (count === 1
        ? 'You already have a running Do or Don’t. Make new ones any time with the plus under “Dos”.'
        : `You already have ${count} running Dos & Don’ts. Make new ones any time with the plus under “Dos”.`),
    },
    invite: {
      eyebrow: 'Friends',
      headingAlone: 'Who should be watching?',
      introAlone: 'Send your link to somebody who knows you. Whoever comes through it is your friend straight away — and checks your Do.',
      headingFriends: 'More eyes, more hold',
      introFriends: (names: string) => `You are already friends with ${names}. Every friend more makes it harder to talk your way out.`,
      and: 'and',
      others: (count: number) => (count === 1 ? 'one other' : `${count} others`),
      share: 'Share link',
      copy: 'Copy',
      sent: 'Shared. Whoever comes through it is your friend straight away.',
      leave: 'Carry on without friends',
      leaveTitle: 'Start without friends?',
      leaveBody: (title: string) => `Your Do “${title}” lapses then. As soon as somebody is there, you can make a new one any time.`,
      leaveConfirm: 'Start',
      leaveFailed: 'That did not work. Try again.',
    },
    notifications: {
      eyebrow: 'Notifications',
      heading: 'So nothing slips through',
      intro: 'We get in touch when a window is about to end, a friend checks your photo or your vote is needed. Between 10 pm and 7 am it stays quiet.',
      allow: 'Allow notifications',
      on: 'Notifications are on.',
      blocked: 'Your browser refused notifications. Only its own settings can change that.',
      sampleApp: 'Qdos',
      sampleTime: 'now',
      sampleBody: '3 hours left for “Read every evening”.',
    },
    done: {
      eyebrow: 'Done',
      heading: 'You are in.',
      intro: 'This is what you set up. All of it can change later — except your handle.',
      photo: 'Profile photo',
      photoSet: 'Set',
      photoMissing: 'None yet',
      handle: 'Handle',
      firstDo: 'First Do',
      firstDoRunning: 'Running',
      firstDoWaiting: 'Waiting for your first friend',
      firstDoNone: 'None yet',
      friends: 'Friends',
      friendsCount: (count: number) => (count === 1 ? '1 friend' : `${count} friends`),
      friendsNone: 'None yet — your link is waiting',
      notifications: 'Notifications',
      notificationsOn: 'On',
      notificationsOff: 'Off',
      start: 'Open the app',
      failed: 'That did not work. Try again.',
    },
  },

  tour: {
    label: 'Short tour',
    stepOf: (step: number, total: number) => `${step} of ${total}`,
    next: 'Next',
    done: 'Done',
    skip: 'End tour',
    home: { title: 'Home', body: 'What is due next, with the camera for it. Below that your streak and the photos waiting for your vote.' },
    search: { title: 'Friends', body: 'Find people, answer requests and share your invite link.' },
    dos: { title: 'Dos & Don’ts', body: 'Everything you set out to do. The plus at the top makes something new.' },
    chats: { title: 'Chats', body: 'Every Do has a chat of its own. That is where your photos arrive and are checked.' },
    profile: { title: 'Profile', body: 'Your streak, your proof photos and the settings.' },
  },

  draft: {
    waiting: 'Waiting for your first friend',
    waitingHint: 'Starts by itself as soon as somebody comes through your link.',
    share: 'Share link',
    discard: 'Discard',
    discardTitle: 'Discard the waiting Do?',
    discardBody: 'It will not start when your first friend arrives. You can keep a new one waiting any time.',
    discardConfirm: 'Discard',
    createHint: 'Nobody checks you yet. Keep your Do waiting — it starts as soon as somebody comes through your link.',
    createHasDraft: 'You already have a Do waiting. It starts as soon as your first friend arrives.',
    homeHeading: 'Your first step',
  },

  deleteAccount: {
    open: 'Delete account',
    heading: 'Delete your account for good',
    body: 'Your Dos & Don’ts, your windows, your pictures, your direct chats and your streak will be deleted. In groups your messages disappear and the rest stays with the others. This cannot be undone.',
    password: 'Password to confirm',
    passwordHint: 'We ask again because this cannot be taken back.',
    confirm: 'Delete for good',
    wrongPassword: 'That password is not correct.',
    failed: 'The account could not be deleted.',
  },

  push: {
    generic: 'Something new happened.',
  },

  notify: {
    heading: 'Notifications',
    open: 'Open notifications',
    openWithCount: (count: number) => (count === 1 ? 'Open notifications, 1 new' : `Open notifications, ${count} new`),
    fresh: 'New',
    earlier: 'Earlier',
    new: 'New',
    dismiss: 'Delete',
    clearAll: 'Delete all',
    clearHeading: 'Delete all notifications?',
    clearBody: 'Everything you can see here is deleted for good — on your other devices too.',
    clearConfirm: 'Delete all',
    empty: 'Nothing for you yet.',
    emptyHint: 'When somebody accepts your request, reacts to your proof or invites you to check them, it turns up here.',
    someone: 'Somebody',
    others: (name: string, others: number) => (others === 1
      ? `${name} and one other`
      : `${name} and ${others} others`),
    titles: {
      friends: 'Friends',
      vote: 'Do you believe it?',
      result: 'Your proof',
      reaction: 'Kudos',
      goals: 'Dos & Don’ts',
      risk: 'Getting tight',
      spotCheck: 'Spot check',
    },
    messageInGroup: (name: string, group: string) => `${name} in ${group}`,
    messageFallback: 'New message',
    friendRequest: 'wants to be friends with you.',
    friendshipStarted: 'and you are friends now.',
    proofAwaitingVote: (goal: string) => `has delivered a proof for “${goal}”. Do you believe it?`,
    proofConfirmed: (goal: string) => `Your proof for “${goal}” was confirmed.`,
    proofRefused: (goal: string) => `Your proof for “${goal}” was not accepted.`,
    proofRefusedRetry: (goal: string) => `Your proof for “${goal}” was not accepted. You have one more try.`,
    reactedToProof: (people: number, goal: string) => (people === 1
      ? `has reacted to your proof for “${goal}”.`
      : `have reacted to your proof for “${goal}”.`),
    reactedToMessage: (people: number): string => (people === 1
      ? 'has reacted to your message.'
      : 'have reacted to your message.'),
    reactedToChallenge: (people: number): string => (people === 1
      ? 'has reacted to your challenge contribution.'
      : 'have reacted to your challenge contribution.'),
    gaveKudos: (people: number, subject: string) =>
      `${people === 1 ? 'has' : 'have'} given you kudos${subject ? ` for “${subject}”` : ''}.`,
    goalInvitation: (goal: string) => `has invited you onto “${goal}”. You get a vote on its proofs.`,
    goalPaused: (goal: string, days: number) => (days === 1
      ? `is setting “${goal}” aside for a day.`
      : `is setting “${goal}” aside for ${days} days.`),
    pauseLifted: (goal: string) => `Objections have ended the pause on “${goal}”. The window is running again.`,
    friendAtRisk: (goal: string, missing: number) => (missing === 1
      ? `is about to miss “${goal}” — one proof still missing.`
      : `is about to miss “${goal}” — ${missing} proofs still missing.`),
    spotCheckDue: (goal: string) => `“${goal}”: show, live, what you are doing — within three minutes.`,
  },

  notificationSettings: {
    heading: 'Notifications',
    device: 'This device',
    deviceNote: 'Permission and delivery apply to this browser only. The switches below apply to every device you have turned notifications on for.',
    friends: 'Friends',
    goals: 'Dos & Don’ts',
    encouragement: 'Encouragement',
    challenge: 'Challenge',
    messages: 'Messages',
    friendships: 'Friend requests',
    votesDue: 'A proof is waiting for your verdict',
    proofResults: 'Verdicts on your proofs',
    goalUpdates: 'Invitations & pauses',
    friendsAtRisk: 'Friends running out of time',
    spotChecks: 'Spot checks on your Don’ts',
    reactions: 'Kudos & reactions',
    dailyChallenge: 'Challenge of the day',
    switchesNote: 'The switches decide what may interrupt you: a push on your phone, or a banner at the top of the open app. The bell keeps everything either way.',
  },

  activityOverview: {
    heading: 'Activity',
    empty: 'Nothing has happened yet.',
    emptyHint: 'When your friends deliver something, it turns up here.',
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
    relapsed: (count: number) => (count === 1 ? '1 relapse' : `${count} relapses`),
    clean: 'Nothing missed yet.',
    nothingShared: 'You have nothing together yet.',
    nothingSharedHint: 'A record only covers Dos & Don’ts you are both on.',
    sharedGoals: (count: number) => (count === 1 ? '1 shared Do or Don’t' : `${count} shared Dos & Don’ts`),
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
    moreActions: 'More actions',
    removeFriend: 'Remove friend',
    reportOrBlock: 'Report or block',
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
    accent: 'Accent colour',
    accentNote: 'Applies on this device. Adapts to light and dark mode.',
    accents: { iris: 'Iris', sage: 'Sage', rose: 'Rose', ochre: 'Ochre' },
    language: 'Language',
    languageGerman: 'Deutsch',
    languageEnglish: 'English',
    notifications: 'Notifications',
    notificationsNote: 'Which of them may interrupt you, and when it stays quiet.',
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
    taskCompleted: (subject: string) => `delivered for “${subject}”`,
    streakReached: (days: number) => `reached a ${days}-day streak`,
    goalProgress: (subject: string, percent: number) => `is at ${percent}% on “${subject}”`,
    goalCreated: (subject: string) => `made a new commitment: ${subject}`,
    giveKudos: (line: string) => `Give kudos: ${line}`,
    takeBackKudos: (line: string) => `Take kudos back: ${line}`,
    openPerson: (name: string) => `Open ${name}'s profile`,
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
    rateLimited: 'That was too many requests in a short while. Wait a few minutes and try again.',
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
    body: 'Qdos needs a connection to load your Dos & Don’ts. As soon as you have signal again, you can pick up where you left off.',
    retry: 'Try again',
  },

  toast: {
    groupLeft: { icon: 'i-lucide-log-out', text: 'Left the group' },
    reported: { icon: 'i-lucide-flag', text: 'Thank you — we will look at it' },
    blocked: { icon: 'i-lucide-shield', text: 'Person blocked' },
    inviteCopied: { icon: 'i-lucide-link', text: 'Link copied' },
    befriended: { icon: 'i-lucide-user-check', text: 'You are friends now' },
    inviteReplaced: { icon: 'i-lucide-link', text: 'New link — the old one no longer works' },
    goalDeleted: { icon: 'i-lucide-trash-2', text: 'Deleted for good' },
    feedbackUnavailable: { icon: 'i-lucide-circle-alert', text: 'Feedback cannot be opened right now' },
    proofCounted: { icon: 'i-lucide-check', text: 'Proof counted' },
    proofWaiting: { icon: 'i-lucide-hourglass', text: 'Proof delivered — your friends are checking it' },
    useLogged: { icon: 'i-lucide-check', text: 'Logged' },
    useUndone: { icon: 'i-lucide-undo-2', text: 'Taken back' },
    undoTooLate: { icon: 'i-lucide-clock', text: 'Too late — the use stays' },
    undo: 'Undo',
    spotCheckAnswered: { icon: 'i-lucide-hourglass', text: 'Spot check delivered — your friends are checking it' },
    relapseLogged: { icon: 'i-lucide-rotate-ccw', text: 'Logged as a relapse' },
    draftSaved: { icon: 'i-lucide-hourglass', text: 'Saved — it starts with your first friend' },
    draftDiscarded: { icon: 'i-lucide-trash-2', text: 'Discarded' },
  },
}

export const messages = { de, en }

/** Maps the API's language preference onto the dictionaries above. */
export const languageKeys = { German: 'de', English: 'en' } as const

/** The error copy for a failure kind. Kept next to the kinds it covers. */
export function errorMessage(t: Messages, kind: ApiErrorKind): string {
  return t.errors[kind]
}
