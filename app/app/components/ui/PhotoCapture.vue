<script setup lang="ts">
import { hapticTap } from '~/utils/haptics'
import { isApiError } from '~/api/errors'
import type { Image, ImagePurpose, MediaKind } from '~/api/types'
import { mayTryBothAtOnce, openFrontBeside, streamIsLive } from '~/utils/bothCameras'
import { frameReady, grabFrame } from '~/utils/cameraFrame'
import type { Captured } from '~/utils/captured'
import { type ClipFormat, type ClipRecording, clipFormat, maxClipSeconds, openMicrophone, recordClip, stopStream } from '~/utils/clipRecording'
import { downscaleForUpload, uploadSizes } from '~/utils/images'
import { type CameraFacing, rememberFacing, rememberedFacing } from '~/utils/cameraFacing'
import { type PhotoOrigin, proofSourceOf } from '~/utils/proofSource'

/**
 * Taking a picture and handing back the stored one.
 *
 * This is what the migration plan called `ProofCamera`, named for what it does
 * rather than for any one thing it is used for: a profile picture, the
 * photograph a goal's friends vote on and a contribution to the daily
 * challenge all go through it, unchanged.
 *
 * It offers two routes, and both are needed:
 *
 * **The camera**, through `getUserMedia`. For a proof this is the point — a
 * picture you can pick out of your gallery is not evidence of anything — and
 * for an avatar it is simply the fastest way to get one.
 *
 * **The file picker**, for everything else: a desktop browser with no camera,
 * a phone where permission was refused once and is now remembered, a picture
 * that already exists. The plan calls it the fallback; in practice it is the
 * route that always works, so nothing here ever leaves somebody stuck behind a
 * permission prompt.
 *
 * What is *not* here is any decision about what the picture is for. The purpose
 * comes in as a prop and goes straight to the server, which is what decides who
 * may ever see it — and what happens to the stored picture next is the
 * caller's `handIn`, which this only waits for.
 *
 * **It is a screen of its own, not a sheet.** A full-screen `UModal`: the
 * viewfinder takes what the phone has, and the controls sit where a thumb
 * reaches them, the way every camera on a phone is laid out. It is still an
 * overlay rather than a route on purpose — the caller's `handIn` and the
 * screen underneath, with its scroll position and whatever row asked for the
 * picture, stay exactly where they were.
 *
 * **Open it from a screen, never from inside a sheet.** It used to be a
 * `UDrawer`, and two drawers open at once deadlock (vaul drives both from one
 * set of body styles). The callers therefore close their own sheet before
 * opening this and bring it back afterwards — see `pages/profile/index.vue` — and
 * that stays the rule: a camera on top of a half-hidden form is not a layout
 * anybody wants on a phone either.
 *
 * **It opens with the camera used last on this device**, the rear one until
 * somebody switches (`utils/cameraFacing.ts`).
 *
 * **It says which route a picture took** — the shutter or the picker — to
 * `handIn` and with `uploaded`, and leaves what that means to the caller. A
 * proof turns it into "Live" or "Screenshot" for its friends (#61); with
 * `labelsSource` the picker is offered as a screenshot and the preview shows
 * the label the friends will see, so nobody is surprised by it afterwards.
 *
 * **It records a clip and uses both cameras for everything but a profile
 * picture** (`fullCamera`; #124 and #125 for a proof, #152 for a challenge
 * contribution, a chat photograph and a logged use;
 * docs/adr/0060-video-and-both-cameras.md, docs/adr/0062-the-full-camera-everywhere.md).
 * A profile picture is one photograph from one camera: a picture-in-picture
 * cannot be an avatar, and an avatar does not move. A clip is up to
 * `maxClipSeconds` long, in the one format every q2 client can play, with a
 * still taken as it starts — the server never decodes video. Both cameras is
 * on whenever the phone has two, every time the screen opens, and can be
 * switched off for one capture: the rear camera shows what was done, the front
 * camera who did it. Where both can run at once they are both on screen and
 * one press takes both; where they cannot, which is most phones in a browser,
 * the rear picture is taken first and the screen says "Jetzt du" before the
 * front one, a moment later. The front camera is mirrored while live and never
 * in what is kept. Neither a clip nor both cameras can come from the picker.
 *
 * **Which goes first, and where the corner sits, are the person's** (#151,
 * docs/adr/0061-both-cameras-in-step.md). A tap on the corner swaps the order
 * before anything is taken: where both cameras run at once the two
 * viewfinders trade places, and where they run in turn the front camera opens
 * first and the rear one follows with "Jetzt hinten". A drag moves the corner
 * to the other top corner, out of the way of whatever it covers. Neither is
 * remembered, and neither changes what is kept: the rear camera is always the
 * main part, and every viewer starts with it large.
 *
 * **Where both run at once, a clip films both sides together**: two
 * recordings started in the same moment, each with the sound and a still of
 * its own, which every viewer plays as one (`ProofMediaFrame`). Where they
 * run in turn, a clip gets the front picture before or after it.
 */
const props = withDefaults(defineProps<{
  purpose: ImagePurpose
  /** The longest edge the upload may have. Defaults to the purpose's own size. */
  maxEdge?: number
  /**
   * What is handed over while the screen is still open: the stored picture or
   * clip, the route it took, a clip's still and the front camera's picture or
   * clip with its still. Resolving to a sentence means it was refused: the screen
   * stays, showing the picture and the sentence, and "Verwenden" tries the
   * hand-in again without uploading a second time. Without it, the screen
   * closes once the upload is done — which is all an avatar needs.
   *
   * This exists because a proof's hand-in is the step that can fail, and it
   * used to run after the camera had closed: a refused photograph was simply
   * gone, with nothing on screen to say so.
   */
  handIn?: (captured: Captured) => Promise<string | null>
  /**
   * For a proof or a challenge contribution: the picker is "Screenshot" rather
   * than "Bild auswählen", and the preview shows the label friends will see on
   * the photograph.
   */
  labelsSource?: boolean
  /**
   * The camera and nothing else: no picker, no file input. For a spot check,
   * which asks what somebody is doing right now — a picture that already
   * existed would answer a different question (docs/adr/0038-spot-checks.md).
   */
  liveOnly?: boolean
}>(), {
  maxEdge: undefined,
  handIn: undefined,
  labelsSource: false,
  liveOnly: false,
})

/**
 * Once everything is up: the main picture and its route, for callers that
 * need nothing else (an avatar), and everything that was taken with it.
 */
const emit = defineEmits<{ uploaded: [image: Image, origin: PhotoOrigin, captured: Captured] }>()

/** A clip as well as a photograph, and both cameras — everything but a profile picture. */
const fullCamera = computed(() => props.purpose !== 'Avatar')

const open = defineModel<boolean>('open', { required: true })

const t = useMessages()
const api = useQ2Api()
const { report } = useErrorReporter()

const video = useTemplateRef<HTMLVideoElement>('video')
const frontVideo = useTemplateRef<HTMLVideoElement>('frontVideo')
const fileInput = useTemplateRef<HTMLInputElement>('fileInput')

/**
 * The camera is live; a clip is recording; the second camera is being taken
 * after the first; something is waiting to be confirmed; or it is going up.
 */
const stage = ref<'camera' | 'recording' | 'second' | 'preview' | 'uploading'>('camera')
const message = ref<string | null>(null)
const facing = ref<CameraFacing>('environment')

/** Which camera the large viewfinder shows right now, for mirroring it. */
const showing = ref<CameraFacing>('environment')

/** Only offered when there is a second camera to switch to. */
const canSwitch = ref(false)

/** A photograph or a clip. Every opening starts with a photograph. */
const kind = ref<MediaKind>('Photo')

/** Both cameras. On at every opening, wherever there is a second camera. */
const bothWanted = ref(true)

/**
 * The front camera first: large where both run at once, opened first where
 * they run in turn. Off at every opening — the rear camera comes first until
 * somebody taps the corner.
 */
const frontFirst = ref(false)

/** Where the corner sits, dragged out of the way of what it covers. */
const corner = useInsetCorner()

/** Which camera the second part comes from, while it is being taken. */
const secondFacing = ref<CameraFacing>('user')

/** The first of two pictures taken in turn, shown in the corner while the second is taken. */
const firstShot = ref<string | null>(null)

/** The format this browser records clips in; null means no video mode at all. */
const format = shallowRef<ClipFormat | null>(null)

const videoOffered = computed(() => fullCamera.value && format.value !== null)
const bothOffered = computed(() => fullCamera.value && canSwitch.value)
const both = computed(() => bothOffered.value && bothWanted.value)

/** What was taken, kept until "Verwenden" or "Noch einmal". */
interface Pending {
  kind: MediaKind
  /** The photograph or the clip. */
  main: Blob
  /** A clip's still. */
  poster: Blob | null
  /** The front camera's picture, or its clip where both filmed at once. */
  inset: Blob | null
  insetKind: MediaKind
  /** A front clip's still. */
  insetPoster: Blob | null
}

function photo(main: Blob, inset: Blob | null): Pending {
  return { kind: 'Photo', main, poster: null, inset, insetKind: 'Photo', insetPoster: null }
}

const pending = shallowRef<Pending | null>(null)
const previewUrl = ref<string | null>(null)
const posterUrl = ref<string | null>(null)
const insetUrl = ref<string | null>(null)
const insetPosterUrl = ref<string | null>(null)
const origin = ref<PhotoOrigin>('camera')

/**
 * The stored copies of `pending`, each once it is up. Kept while a refused
 * hand-in is on screen, so trying again reuses them rather than uploading the
 * same picture twice; dropped with `pending`, because they are copies of it.
 */
const uploaded = shallowRef<{ main?: Image, poster?: Image | null, inset?: Image | null, insetPoster?: Image | null }>({})

const stream = shallowRef<MediaStream | null>(null)
/** The front camera, while it runs beside the rear one. */
const frontStream = shallowRef<MediaStream | null>(null)

/** Both cameras, taken one after the other because they cannot run at once. */
const inTurn = computed(() => both.value && !frontStream.value)

/** Where both run at once and the front camera was tapped large. */
const frontLarge = computed(() => frontFirst.value && !!frontStream.value)

/** The corner's place: along the top, wherever it was dragged. */
const cornerGeometry = computed(() => [
  'absolute top-2.5 aspect-[3/4] w-[28%] max-w-32 rounded-(--q2-radius-sm)',
  corner.side.value === 'end' ? 'end-2.5' : 'start-2.5',
])

/** A live camera in the corner. */
const liveCorner = computed(() => [...cornerGeometry.value, 'z-[1] overflow-hidden bg-black shadow-lg ring-2 ring-black/30'])
const liveCornerStyle = computed(() => corner.style.value)

/**
 * The corner is there whenever both cameras are — until the first of two
 * pictures taken in turn is in, which then waits there instead.
 */
const cornerShown = computed(() => !previewUrl.value && cameraReady.value && both.value && !firstShot.value
  && (stage.value === 'camera' || stage.value === 'recording'))

/**
 * In turn, the order is only changed before anything is taken: once a clip is
 * recording, the camera that goes first is already open.
 */
const cornerPressable = computed(() => !!frontStream.value || stage.value === 'camera')

const cornerLabel = computed(() => {
  if (frontStream.value) return frontLarge.value ? t.value.photo.rearLarge : t.value.photo.frontLarge
  return frontFirst.value ? t.value.photo.rearFirst : t.value.photo.frontFirst
})
const microphone = shallowRef<MediaStream | null>(null)
const cameraReady = ref(false)

/** The clip being recorded, and for how long. */
const recording = shallowRef<ClipRecording | null>(null)
const elapsedMs = ref(0)

/** Seconds left before the front picture is taken, while "Jetzt du" is on screen. */
const countdown = ref<number | null>(null)

/** A clip will be recorded without sound: the microphone was refused or there is none. */
const soundless = ref(false)

const edge = computed(() => props.maxEdge ?? (props.purpose === 'Avatar' ? uploadSizes.avatar : uploadSizes.proof))

const heading = computed(() => (kind.value === 'Video' && videoOffered.value ? t.value.photo.videoHeading : t.value.photo.heading))

const hint = computed(() => {
  if (!cameraReady.value) return props.liveOnly ? t.value.photo.cameraRequired : t.value.photo.fileHint
  return kind.value === 'Video' ? t.value.photo.recordHint(maxClipSeconds) : t.value.photo.cameraHint
})

const modeOptions = computed(() => [
  { value: 'Photo' as const, label: t.value.photo.photoMode, icon: 'i-lucide-camera' },
  { value: 'Video' as const, label: t.value.photo.videoMode, icon: 'i-lucide-video' },
])

const shutterLabel = computed(() => {
  if (kind.value !== 'Video') return t.value.photo.shutter
  return recording.value ? t.value.photo.stopRecording : t.value.photo.record
})

/*
 * Both directions. Opening starts the camera; closing stops it and throws the
 * pending picture away — a photograph left in memory after the screen is gone is
 * the sort of thing that turns up in the next person's session.
 */
watch(open, async (isOpen) => {
  if (isOpen) {
    reset()
    facing.value = rememberedFacing()
    format.value = fullCamera.value ? clipFormat() : null
    await startCamera()
  }
  else {
    stopCamera()
    clearPending()
    showFirst(null)
  }
})

// A route change or a hot reload closes the screen without a `false` ever
// arriving, so the release has to happen here as well.
onBeforeUnmount(() => {
  stopCamera()
  clearPending()
  showFirst(null)
})

/** The microphone is open only while a clip may be recorded. */
watch(kind, async (now) => {
  if (now === 'Video' && cameraReady.value && stage.value === 'camera') await openSound()
  else releaseSound()
})

/**
 * The camera this opening shows large: with both cameras the rear one — or
 * the front one, tapped to go first, where the two are taken in turn — else
 * the one used last. Where both may run at once the rear camera is always the
 * one opened, and the front one opens beside it.
 */
function wantedFacing(): CameraFacing {
  if (!fullCamera.value || !bothWanted.value) return facing.value
  return frontFirst.value && !mayTryBothAtOnce() ? 'user' : 'environment'
}

async function startCamera() {
  // No `mediaDevices` at all in a plain HTTP context, in a component test, or
  // in a browser that has none. That is not an error to report — it is the
  // file picker's cue.
  if (!import.meta.client || !navigator.mediaDevices?.getUserMedia) {
    cameraReady.value = false
    return
  }

  const opening = wantedFacing()

  try {
    const opened = await navigator.mediaDevices.getUserMedia({
      video: { facingMode: opening },
      audio: false,
    })

    // Closed while the permission prompt was up: a stream nobody can see is a
    // recording light that stays on.
    if (!open.value) {
      stopStream(opened)
      return
    }

    stopCamera()
    stream.value = opened
    showing.value = opening
    cameraReady.value = true
    canSwitch.value = await hasSeveralCameras()

    await show(opened)

    if (both.value && opening === 'environment') await startFrontBeside()
    if (kind.value === 'Video') await openSound()
  }
  catch {
    // A refused permission is the person's answer, not a defect: no Sentry
    // issue, just the other route — or, where there is none, why not.
    cameraReady.value = false
    message.value = props.liveOnly ? t.value.photo.cameraRequired : t.value.photo.cameraBlocked
  }
}

/** Puts a stream in the large viewfinder. */
async function show(opened: MediaStream) {
  await nextTick()

  if (video.value) {
    video.value.srcObject = opened
    await video.value.play().catch(() => {
      // Autoplay refused. The stream is still live and the frame the shutter
      // grabs is still current, so this is not worth interrupting anybody for.
    })
  }
}

/**
 * Tries to run the front camera beside the rear one. A phone that cannot is
 * remembered (`utils/bothCameras.ts`) and takes the two pictures in turn
 * instead; one that muted the rear camera on the way gets it back.
 */
async function startFrontBeside() {
  const main = stream.value
  if (!main || !navigator.mediaDevices?.enumerateDevices) return

  const front = await openFrontBeside(main, navigator.mediaDevices)

  if (!open.value || stream.value !== main) {
    stopStream(front)
    return
  }

  if (front) {
    frontStream.value = front
    await nextTick()

    if (frontVideo.value) {
      frontVideo.value.srcObject = front
      await frontVideo.value.play().catch(() => {})
    }

    return
  }

  if (!streamIsLive(main)) await startCamera()
}

async function openSound() {
  if (microphone.value || !format.value?.withSound) {
    soundless.value = !format.value?.withSound
    return
  }

  const opened = await openMicrophone()

  // Closed, or back to photographs, while the prompt was up.
  if (!open.value || kind.value !== 'Video') {
    stopStream(opened)
    return
  }

  microphone.value = opened
  soundless.value = opened === null
}

function releaseSound() {
  stopStream(microphone.value)
  microphone.value = null
}

function stopCamera() {
  recording.value?.discard()
  recording.value = null

  stopStream(stream.value)
  stream.value = null
  stopStream(frontStream.value)
  frontStream.value = null
  releaseSound()

  if (video.value) video.value.srcObject = null
  if (frontVideo.value) frontVideo.value.srcObject = null
}

/**
 * Device labels and counts are only reported once permission is granted, which
 * is why this runs after the stream is open. A browser that cannot say keeps
 * the button: offering a switch that changes nothing is cheaper than hiding
 * the selfie camera from somebody who has one.
 */
async function hasSeveralCameras(): Promise<boolean> {
  try {
    const devices = await navigator.mediaDevices.enumerateDevices?.()

    return devices ? devices.filter(device => device.kind === 'videoinput').length > 1 : true
  }
  catch {
    return true
  }
}

async function switchCamera() {
  facing.value = facing.value === 'user' ? 'environment' : 'user'
  rememberFacing(facing.value)

  // Released before the other one is asked for: plenty of Android phones
  // refuse to open a second camera while the first is still held.
  stopCamera()
  await startCamera()
}

/**
 * Both cameras on or off, for this capture only. Off keeps whatever the
 * viewfinder shows; on goes back to the rear camera, with the front one beside
 * it or after it.
 */
async function toggleBoth() {
  bothWanted.value = !bothWanted.value
  frontFirst.value = false
  corner.reset()

  if (!bothWanted.value) {
    stopStream(frontStream.value)
    frontStream.value = null
    facing.value = showing.value
    return
  }

  stopCamera()
  await startCamera()
}

/**
 * The corner's tap: the other camera first. Where both run at once only the
 * viewfinders trade places; where they run in turn the camera that goes first
 * is opened. A drag that ended on the corner is not a tap.
 */
async function swapOrder() {
  if (corner.tookClick()) return

  frontFirst.value = !frontFirst.value

  if (frontStream.value) return

  stopCamera()
  await startCamera()
}

/** Shows the first of two pictures in the corner while the second is taken. */
function showFirst(taken: Blob | null) {
  if (firstShot.value) URL.revokeObjectURL(firstShot.value)
  firstShot.value = taken ? URL.createObjectURL(taken) : null
}

/**
 * The rear picture and, with both cameras, the front one: from the camera
 * beside it, or — where only one can run — by switching to it.
 */
async function shoot() {
  const first = await grabFrame(video.value)

  if (!first) return

  hapticTap()

  // In turn, front first: the rear picture follows, and is still the main one.
  if (inTurn.value && frontFirst.value) {
    showFirst(first)
    const rear = await takeSecond('environment')

    if (open.value) hold(rear ? photo(rear, first) : photo(first, null), 'camera')
    return
  }

  if (inTurn.value) showFirst(first)

  const inset = both.value ? await takeFront() : null

  if (open.value) hold(photo(first, inset), 'camera')
}

/**
 * Starts a clip, or stops the one running.
 *
 * In turn and front first, the front picture is taken before the clip: the
 * rear camera opens after "Jetzt hinten" and starts recording on its own, so
 * one press is still all it takes.
 */
async function toggleRecording() {
  if (recording.value) {
    recording.value.stop()
    return
  }

  const chosen = format.value

  if (!chosen || !stream.value) return

  let frontStill: Blob | null = null

  if (inTurn.value && frontFirst.value) {
    frontStill = await grabFrame(video.value)

    if (!frontStill) return

    hapticTap()
    showFirst(frontStill)

    if (!await switchForSecond('environment')) {
      if (open.value) hold(photo(frontStill, null), 'camera')
      return
    }

    await openSound()
  }

  await recordFromHere(chosen, frontStill)
}

/**
 * Records what the camera shows — and, where both run at once, what the front
 * camera shows, as a clip of its own started in the same moment with the same
 * sound (#151).
 *
 * Each clip's still is taken as it starts: it is what friends see before they
 * press play, and what every grid shows. A camera reopened a moment ago may
 * not have a frame yet, so it is waited for — a clip without its still would
 * be refused.
 */
async function recordFromHere(chosen: ClipFormat, frontStill: Blob | null) {
  const camera = stream.value

  if (!camera) return

  await frameReady(video.value)
  let poster = await grabFrame(video.value)

  const front = frontStream.value
  let frontPoster = front ? await grabFrame(frontVideo.value) : null

  const sound = chosen.withSound ? microphone.value?.getAudioTracks() ?? [] : []

  // The front clip gets its own copy of the sound, so each recorder owns what
  // it records, and whichever clip is large can be the one that is heard.
  const frontSound = front ? sound.map(track => track.clone()) : []

  hapticTap()
  elapsedMs.value = 0
  stage.value = 'recording'

  const clip = recordClip(new MediaStream([...camera.getVideoTracks(), ...sound]), chosen, {
    limitMs: maxClipSeconds * 1000,
    onTick: (elapsed) => {
      elapsedMs.value = elapsed
    },
  })
  const frontClip = front
    ? recordClip(new MediaStream([...front.getVideoTracks(), ...frontSound]), chosen, { limitMs: maxClipSeconds * 1000 })
    : null

  // One recording to the shutter: it starts and stops both together.
  const pair: ClipRecording = {
    done: clip.done,
    stop: () => {
      clip.stop()
      frontClip?.stop()
    },
    discard: () => {
      clip.discard()
      frontClip?.discard()
    },
  }
  recording.value = pair

  const [main, frontMain] = await Promise.all([clip.done, frontClip?.done ?? Promise.resolve(null)])
  frontSound.forEach(track => track.stop())

  // Thrown away: the screen closed while it was recording.
  if (recording.value !== pair) return

  recording.value = null

  // Still none: the last frame, while the cameras that recorded it are open.
  poster ??= await grabFrame(video.value)
  if (frontMain) frontPoster ??= await grabFrame(frontVideo.value)

  if (!main || !poster) {
    stage.value = 'camera'
    message.value = t.value.photo.recordFailed
    return
  }

  hapticTap()

  let taken: Pending = { kind: 'Video', main, poster, inset: frontStill, insetKind: 'Photo', insetPoster: null }

  if (!frontStill && frontMain && frontPoster) {
    taken = { ...taken, inset: frontMain, insetKind: 'Video', insetPoster: frontPoster }
  }
  else if (!frontStill && both.value) {
    // In turn, rear first — or a front clip that failed beside a good rear
    // one: the front picture after it, as before #151.
    if (inTurn.value) showFirst(poster)
    taken = { ...taken, inset: await takeFront() }
  }

  if (open.value) hold(taken, 'camera')
}

/**
 * The front camera's picture: at once from the camera beside the rear one, or
 * by switching to it and saying so first.
 */
async function takeFront(): Promise<Blob | null> {
  if (frontStream.value) return grabFrame(frontVideo.value)

  return takeSecond('user')
}

/** The second of two pictures taken in turn, from `to`, after its countdown. */
async function takeSecond(to: CameraFacing): Promise<Blob | null> {
  if (!await switchForSecond(to)) return null

  hapticTap()

  return grabFrame(video.value)
}

/**
 * Opens `to` in the viewfinder for the second part, and counts down: "Jetzt
 * du" before the front camera, "Jetzt hinten" before the rear one. False when
 * that camera would not open, or the screen closed meanwhile.
 */
async function switchForSecond(to: CameraFacing): Promise<boolean> {
  stage.value = 'second'
  secondFacing.value = to

  // Released before the other camera is asked for, for the same reason as
  // `switchCamera`.
  stopStream(stream.value)
  stream.value = null
  releaseSound()

  let opened: MediaStream

  try {
    opened = await navigator.mediaDevices.getUserMedia({ video: { facingMode: to }, audio: false })
  }
  catch {
    message.value = to === 'user' ? t.value.photo.frontUnavailable : t.value.photo.rearUnavailable
    return false
  }

  if (!open.value) {
    stopStream(opened)
    return false
  }

  stream.value = opened
  showing.value = to
  await show(opened)
  await frameReady(video.value)

  // A moment to look up, or to turn the phone round: the second picture
  // should catch nobody mid-blink, nor off guard.
  for (let left = 2; left > 0; left--) {
    countdown.value = left
    await new Promise(resolve => setTimeout(resolve, 600))
    if (!open.value) return false
  }

  countdown.value = null

  return true
}

/** The shutter: a photograph, or the start and end of a clip. */
async function press() {
  if (kind.value === 'Video') await toggleRecording()
  else await shoot()
}

function onFilePicked(event: Event) {
  const [file] = (event.target as HTMLInputElement).files ?? []

  if (file) hold(photo(file, null), 'library')

  // Cleared so picking the same file twice in a row fires `change` again.
  if (fileInput.value) fileInput.value.value = ''
}

function hold(taken: Pending, from: PhotoOrigin) {
  // A camera that would not open is worth saying under the one picture that
  // was taken; nothing else from before the shutter is.
  const unavailable = [t.value.photo.frontUnavailable, t.value.photo.rearUnavailable]
  const note = message.value && unavailable.includes(message.value) ? message.value : null

  clearPending()
  showFirst(null)
  pending.value = taken
  origin.value = from
  previewUrl.value = URL.createObjectURL(taken.main)
  posterUrl.value = taken.poster ? URL.createObjectURL(taken.poster) : null
  insetUrl.value = taken.inset ? URL.createObjectURL(taken.inset) : null
  insetPosterUrl.value = taken.insetPoster ? URL.createObjectURL(taken.insetPoster) : null
  stage.value = 'preview'
  countdown.value = null
  message.value = note
  stopCamera()
}

function clearPending() {
  for (const url of [previewUrl.value, posterUrl.value, insetUrl.value, insetPosterUrl.value]) {
    if (url) URL.revokeObjectURL(url)
  }

  previewUrl.value = null
  posterUrl.value = null
  insetUrl.value = null
  insetPosterUrl.value = null
  pending.value = null
  uploaded.value = {}
}

async function retake() {
  clearPending()
  stage.value = 'camera'
  message.value = null
  await startCamera()
}

/** Uploads one still picture, shrunk to the purpose's size. */
async function uploadPicture(blob: Blob): Promise<Image> {
  return api.images.upload(await downscaleForUpload(blob, edge.value), props.purpose)
}

async function confirm() {
  const taken = pending.value

  if (!taken) return

  stage.value = 'uploading'
  message.value = t.value.photo.uploading

  // A clip goes up as it was recorded: shrinking it would mean re-encoding it
  // on the phone, and it was recorded at a modest size to begin with. Which
  // kind of upload was under way says which sentence a refusal gets.
  let step = 'Photo' as MediaKind
  const upload = (blob: Blob, as: MediaKind) => {
    step = as
    return as === 'Video' ? api.images.upload(blob, props.purpose) : uploadPicture(blob)
  }

  try {
    const main = uploaded.value.main ?? await upload(taken.main, taken.kind)
    uploaded.value = { ...uploaded.value, main }

    const poster = uploaded.value.poster ?? (taken.poster ? await upload(taken.poster, 'Photo') : null)
    uploaded.value = { ...uploaded.value, poster }

    const inset = uploaded.value.inset ?? (taken.inset ? await upload(taken.inset, taken.insetKind) : null)
    uploaded.value = { ...uploaded.value, inset }

    const insetPoster = uploaded.value.insetPoster ?? (taken.insetPoster ? await upload(taken.insetPoster, 'Photo') : null)
    uploaded.value = { ...uploaded.value, insetPoster }

    const captured: Captured = { image: main, origin: origin.value, poster, inset, insetPoster }
    const refused = props.handIn ? await props.handIn(captured) : null

    if (refused) {
      stage.value = 'preview'
      message.value = refused
      return
    }

    // Cleared before closing: the screen stays mounted while it animates out,
    // and "Wird hochgeladen …" left standing under a finished upload reads as
    // though it were stuck.
    message.value = null
    emit('uploaded', main, origin.value, captured)
    open.value = false
  }
  catch (caught) {
    stage.value = 'preview'

    // A file the browser could not decode never reached the network, so it is
    // not an API failure, is nobody's defect, and gets its own sentence rather
    // than a Sentry issue.
    if (!isApiError(caught)) {
      message.value = t.value.photo.unreadable
      return
    }

    const failure = report(caught, { feature: 'images', action: 'upload' })
    const clipRefused = step === 'Video'

    if (failure.status === 413) message.value = clipRefused ? t.value.photo.videoTooLarge : t.value.photo.tooLarge
    else if (failure.status === 400 && clipRefused) message.value = t.value.photo.videoRefused
    else message.value = t.value.photo.failed
  }
}

function reset() {
  stage.value = 'camera'
  message.value = null
  cameraReady.value = false
  kind.value = 'Photo'
  bothWanted.value = true
  frontFirst.value = false
  corner.reset()
  showFirst(null)
  countdown.value = null
  soundless.value = false
}
</script>

<template>
  <!--
    `title` and `description` still reach the dialog: with the `content` slot
    Nuxt UI renders them visually hidden, which is what a screen reader
    announces when the screen opens. The visible heading below is the same
    words and is therefore hidden from it.
  -->
  <UModal
    v-model:open="open"
    fullscreen
    :title="heading"
    :description="hint"
    :ui="{ content: 'bg-(--ui-bg) divide-y-0' }"
  >
    <template #content>
      <div
        class="mx-auto flex size-full max-w-[430px] flex-col pt-[max(0.5rem,env(safe-area-inset-top))] pb-(--q2-safe-bottom)"
        data-testid="photo-screen"
      >
        <div class="flex shrink-0 items-center gap-2 px-2">
          <UButton
            icon="i-lucide-x"
            color="neutral"
            variant="ghost"
            size="xl"
            class="size-11 justify-center"
            :aria-label="t.common.close"
            data-testid="photo-close"
            @click="open = false"
          />

          <p
            class="flex-1 text-center text-[17px] font-bold text-(--ui-text-highlighted)"
            aria-hidden="true"
          >
            {{ heading }}
          </p>

          <!--
            Both cameras, where there are two. A switch rather than
            an action — it says what the shutter will do — so it is grey either
            way and says which by being pressed.
          -->
          <UButton
            v-if="bothOffered && (stage === 'camera')"
            color="neutral"
            :variant="bothWanted ? 'soft' : 'ghost'"
            size="xl"
            icon="i-lucide-picture-in-picture-2"
            class="size-11 justify-center"
            :aria-label="t.photo.bothCameras"
            :aria-pressed="bothWanted"
            data-testid="photo-both"
            @click="toggleBoth"
          />

          <!-- Balances the close button, so the heading sits in the middle. -->
          <span
            v-else
            class="size-11"
            aria-hidden="true"
          />
        </div>

        <!--
          `data-q2-block` on the frame, not on the picture: Session Replay
          records every session, and somebody's face is the most personal thing
          this app will ever hold. Blocking the container covers the live
          cameras, the preview — a clip and the front picture too — and
          whatever sits between them.
        -->
        <div
          class="relative mx-3 mt-2 min-h-0 flex-1 overflow-hidden rounded-(--q2-radius-lg) bg-(--ui-bg-elevated)"
          data-testid="photo-frame"
          data-q2-block
        >
          <!--
            `contain`, not `cover`: this is exactly what will be sent, and a
            preview that crops would be showing a different picture.
          -->
          <ProofMediaFrame
            v-if="previewUrl && pending"
            :kind="pending.kind"
            :src="previewUrl"
            :poster-src="posterUrl"
            :inset-kind="pending.insetKind"
            :inset-src="insetUrl"
            :inset-poster-src="insetPosterUrl"
            fit="contain"
            :alt="t.photo.preview"
            data-testid="photo-preview"
          />

          <!-- What the friends will read on it, before it is sent. -->
          <p
            v-if="previewUrl && labelsSource"
            class="absolute start-2.5 bottom-2.5 flex items-center gap-1.5 text-[11px] font-semibold text-white [text-shadow:0_1px_3px_rgb(0_0_0/0.9)]"
            data-testid="photo-source-note"
          >
            {{ t.photo.labelledAs }}
            <ProofSourceBadge :source="proofSourceOf(origin)" />
          </p>

          <!--
            `playsinline` keeps iOS from taking the video full screen the moment
            it plays, which would replace this screen with a player. `muted` is
            what makes autoplay permitted at all. The front camera is mirrored
            the way every phone shows it; the picture taken is not.

            Each camera keeps its own element, and where both run at once they
            only trade places: a stream moved to another element would stall
            for a moment, and a clip being recorded must not.
          -->
          <div
            v-show="!previewUrl && cameraReady"
            :class="frontLarge ? liveCorner : 'absolute inset-0'"
            :style="frontLarge ? liveCornerStyle : undefined"
          >
            <video
              ref="video"
              class="size-full object-cover"
              :class="{ '-scale-x-100': showing === 'user' }"
              playsinline
              muted
              autoplay
              data-testid="photo-camera"
            />
          </div>

          <!-- The front camera beside the rear one, where both can run at once. -->
          <div
            v-show="!previewUrl && cameraReady && frontStream && stage !== 'second'"
            :class="frontLarge ? 'absolute inset-0' : liveCorner"
            :style="frontLarge ? undefined : liveCornerStyle"
          >
            <video
              ref="frontVideo"
              class="size-full -scale-x-100 bg-black object-cover"
              playsinline
              muted
              autoplay
              data-testid="photo-front-camera"
            />
          </div>

          <!--
            The corner, pressed: where both run at once a tap trades the two
            places, and where they run in turn it says which comes second and a
            tap puts the other one first. A drag moves it to the other top
            corner. While the second picture is taken, the first waits here.
          -->
          <button
            v-if="cornerShown"
            type="button"
            class="z-[2] flex touch-pan-y flex-col items-center justify-center gap-1 text-center text-[11px] font-bold text-white focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--q2-accent-solid)"
            :class="[cornerGeometry, frontStream ? '' : 'bg-black/45 ring-2 ring-white/20 backdrop-blur-sm']"
            :style="corner.style.value"
            :aria-label="cornerLabel"
            aria-keyshortcuts="ArrowLeft ArrowRight"
            :disabled="!cornerPressable"
            :data-side="corner.side.value"
            :data-testid="frontStream ? 'photo-corner' : 'photo-front-after'"
            @pointerdown="corner.onPointerdown($event)"
            @pointermove="corner.onPointermove($event)"
            @pointerup="corner.onPointerup($event)"
            @pointercancel="corner.onPointerup($event)"
            @keydown="corner.onKeydown($event)"
            @click.stop="swapOrder"
          >
            <template v-if="!frontStream">
              <UIcon
                :name="frontFirst ? 'i-lucide-mountain' : 'i-lucide-user-round'"
                class="size-5"
                aria-hidden="true"
              />
              {{ frontFirst ? t.photo.bothCamerasAfterRear : t.photo.bothCamerasAfter }}
            </template>
          </button>

          <img
            v-if="!previewUrl && firstShot && (stage === 'second' || stage === 'recording')"
            :src="firstShot"
            alt=""
            class="z-[1] overflow-hidden bg-black object-cover shadow-lg ring-2 ring-black/30"
            :class="cornerGeometry"
            :style="corner.style.value"
            data-testid="photo-first-shot"
          >

          <!-- The second picture is next: say which, and count it down. -->
          <div
            v-if="stage === 'second'"
            class="pointer-events-none absolute inset-0 flex flex-col items-center justify-center gap-1 bg-black/25 text-white"
            role="status"
            data-testid="photo-now-you"
          >
            <span class="text-[28px] font-extrabold [text-shadow:0_2px_8px_rgb(0_0_0/0.6)]">{{ secondFacing === 'user' ? t.photo.nowYou : t.photo.nowRear }}</span>
            <span
              v-if="countdown"
              class="text-[44px] leading-none font-extrabold tabular-nums [text-shadow:0_2px_8px_rgb(0_0_0/0.6)]"
              aria-hidden="true"
            >{{ countdown }}</span>
          </div>

          <!-- How long it has been recording, against how long it may. -->
          <span
            v-if="stage === 'recording'"
            class="absolute top-2.5 left-1/2 flex -translate-x-1/2 items-center gap-1.5 rounded-full bg-black/60 px-3 py-1 text-[13px] font-bold text-white tabular-nums"
            role="timer"
            data-testid="photo-recording-time"
          >
            <span
              class="size-2 animate-pulse rounded-full bg-white"
              aria-hidden="true"
            />
            {{ t.photo.recording(Math.floor(elapsedMs / 1000), maxClipSeconds) }}
          </span>

          <div
            v-if="!previewUrl && !cameraReady"
            class="flex size-full items-center justify-center"
          >
            <UIcon
              name="i-lucide-camera-off"
              class="size-10 text-(--ui-text-muted)"
              aria-hidden="true"
            />
          </div>
        </div>

        <p
          v-if="message"
          class="shrink-0 px-5 pt-3 text-center text-[13px] font-semibold text-(--ui-text-muted)"
          role="status"
          data-testid="photo-message"
        >
          {{ message }}
        </p>

        <p
          v-else-if="stage === 'camera'"
          class="shrink-0 px-5 pt-3 text-center text-[13px] text-(--ui-text-muted)"
          aria-hidden="true"
        >
          {{ kind === 'Video' && soundless ? `${hint} ${t.photo.soundless}` : hint }}
        </p>

        <!--
          Always present, never visible. The buttons below are what people
          press; this is the element the browser needs in order to open a
          picker at all.
        -->
        <input
          v-if="!liveOnly"
          ref="fileInput"
          type="file"
          accept="image/*"
          class="sr-only"
          data-testid="photo-file"
          @change="onFilePicked"
        >

        <!--
          Photograph or clip, above the shutter where a phone's camera puts it.
          Grey: which one is chosen is a state, not something to do.
        -->
        <div
          v-if="videoOffered && cameraReady && stage === 'camera'"
          class="mx-auto w-full max-w-60 shrink-0 pt-3"
          data-testid="photo-modes"
        >
          <AppSegmented
            v-model="kind"
            :options="modeOptions"
            :label="t.photo.modes"
          />
        </div>

        <!--
          A camera's layout: the gallery under the left thumb, the shutter in
          the middle, the other camera on the right. The shutter is the one
          thing on this screen that is done now, so it carries the accent.
        -->
        <div
          v-if="(stage === 'camera' || stage === 'recording') && cameraReady"
          class="grid shrink-0 grid-cols-3 items-center px-6 pt-4 pb-2"
        >
          <!-- No picker for a clip: a clip is only ever live. -->
          <span
            v-if="liveOnly || kind === 'Video'"
            aria-hidden="true"
          />
          <div
            v-else
            class="flex flex-col items-center gap-1 justify-self-start"
          >
            <UButton
              color="neutral"
              variant="soft"
              :icon="labelsSource ? 'i-lucide-smartphone' : 'i-lucide-image'"
              class="size-14 justify-center rounded-full"
              :ui="{ leadingIcon: 'size-6' }"
              :aria-label="labelsSource ? t.photo.chooseScreenshot : t.photo.chooseFile"
              data-testid="photo-choose"
              @click="fileInput?.click()"
            />
            <!-- For a proof the two routes are both real choices, so the
                 second one is named rather than left to an icon. -->
            <span
              v-if="labelsSource"
              class="text-[11px] font-semibold text-(--ui-text-muted)"
              aria-hidden="true"
            >{{ t.proof.source.Screenshot }}</span>
          </div>

          <!--
            For a clip the shutter starts and stops it. Recording, the inner
            disc becomes a square, the way every phone says "press to stop" —
            still the accent, because stopping is the thing to do now.
          -->
          <button
            type="button"
            class="q2-press flex size-[76px] items-center justify-center justify-self-center rounded-full p-1.5 ring-4 ring-(--ui-border-accented) focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-(--q2-accent-solid)"
            :aria-label="shutterLabel"
            :aria-pressed="kind === 'Video' ? stage === 'recording' : undefined"
            data-testid="photo-shutter"
            @click="press"
          >
            <span
              class="block bg-(--q2-accent-solid) transition-all duration-150"
              :class="stage === 'recording' ? 'size-7 rounded-(--q2-radius-sm)' : 'size-full rounded-full'"
            />
          </button>

          <UButton
            v-if="canSwitch && !both && stage === 'camera'"
            color="neutral"
            variant="soft"
            icon="i-lucide-switch-camera"
            class="size-14 justify-self-end justify-center rounded-full"
            :ui="{ leadingIcon: 'size-6' }"
            :aria-label="t.photo.switchCamera"
            data-testid="photo-switch"
            @click="switchCamera"
          />
        </div>

        <div
          v-else-if="stage === 'camera' && !liveOnly"
          class="shrink-0 px-4 pt-4 pb-2"
        >
          <UButton
            block
            size="xl"
            :icon="labelsSource ? 'i-lucide-smartphone' : 'i-lucide-image'"
            :label="labelsSource ? t.photo.chooseScreenshot : t.photo.chooseFile"
            data-testid="photo-choose"
            @click="fileInput?.click()"
          />
        </div>

        <!-- Holds the controls' height while the front picture is taken, so nothing jumps. -->
        <div
          v-else-if="stage === 'second'"
          class="h-[100px] shrink-0"
          aria-hidden="true"
        />

        <div
          v-else-if="stage === 'preview' || stage === 'uploading'"
          class="grid shrink-0 grid-cols-2 gap-2 px-4 pt-4 pb-2"
        >
          <UButton
            block
            size="xl"
            color="neutral"
            variant="outline"
            icon="i-lucide-rotate-ccw"
            :label="t.photo.retake"
            :disabled="stage === 'uploading'"
            data-testid="photo-retake"
            @click="retake"
          />

          <UButton
            block
            size="xl"
            icon="i-lucide-check"
            :label="t.photo.use"
            :loading="stage === 'uploading'"
            data-testid="photo-confirm"
            @click="confirm"
          />
        </div>
      </div>
    </template>
  </UModal>
</template>
