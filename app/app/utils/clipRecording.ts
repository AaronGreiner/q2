/**
 * Recording a proof clip in the browser (#124).
 *
 * The server does not transcode (docs/adr/0060-video-and-both-cameras.md), so
 * what this records is what friends will play — on an iPhone, an Android phone
 * and a desktop alike. That only holds for one format: H.264 in an MP4, with
 * AAC for the sound. A browser that cannot record it gets no video mode at all,
 * rather than producing a WebM that some iPhones will not play.
 */

/** The longest clip the camera records, in seconds. The server's `StoredImage.MaxVideoSeconds`. */
export const maxClipSeconds = 10

/** Picture and sound bitrates: about 3.5 MB for the longest clip, well inside the server's 12. */
const videoBitsPerSecond = 2_500_000
const audioBitsPerSecond = 128_000

/** A format `MediaRecorder` can write, and whether it can carry sound. */
export interface ClipFormat {
  mimeType: string
  withSound: boolean
}

/**
 * In order of preference. H.264 with AAC first; H.264 alone where a browser
 * has no AAC encoder (Chromium on Linux), which records without sound rather
 * than not at all; and plain `video/mp4` last, which is how Safari is asked —
 * it writes H.264 and AAC unless told otherwise.
 */
const formats: readonly ClipFormat[] = [
  { mimeType: 'video/mp4;codecs=avc1.42E01E,mp4a.40.2', withSound: true },
  { mimeType: 'video/mp4;codecs=avc1.42E01E', withSound: false },
  { mimeType: 'video/mp4;codecs=avc1', withSound: false },
  { mimeType: 'video/mp4', withSound: true },
]

/**
 * The format this browser records proof clips in, or null when it records
 * none that every q2 client can play.
 */
export function clipFormat(
  isTypeSupported: ((type: string) => boolean) | undefined = recorderTypeCheck(),
): ClipFormat | null {
  if (!isTypeSupported) return null

  return formats.find(format => safely(() => isTypeSupported(format.mimeType))) ?? null
}

function recorderTypeCheck(): ((type: string) => boolean) | undefined {
  const recorder = globalThis.MediaRecorder

  return typeof recorder?.isTypeSupported === 'function' ? type => recorder.isTypeSupported(type) : undefined
}

function safely(check: () => boolean): boolean {
  try {
    return check()
  }
  catch {
    return false
  }
}

/**
 * Asks for the microphone, and gives up after `timeoutMs`.
 *
 * Sound is optional: a refused microphone records a silent clip rather than no
 * clip. The timeout is there because a permission request can also simply
 * never answer — a headless browser's fake microphone does, and so can a
 * prompt somebody walked away from — and the camera must not wait on it. A
 * stream that arrives after giving up is stopped at once, so no microphone
 * stays open that nobody is using.
 */
export async function openMicrophone(
  devices: Pick<MediaDevices, 'getUserMedia'> | undefined = globalThis.navigator?.mediaDevices,
  timeoutMs = 4000,
): Promise<MediaStream | null> {
  if (!devices?.getUserMedia) return null

  let gaveUp = false

  const asked = devices.getUserMedia({ audio: true, video: false }).then(
    (stream) => {
      if (gaveUp) stopStream(stream)
      return gaveUp ? null : stream
    },
    () => null,
  )

  const timeout = new Promise<null>(resolve => setTimeout(() => {
    gaveUp = true
    resolve(null)
  }, timeoutMs))

  return Promise.race([asked, timeout])
}

/** A clip being recorded. */
export interface ClipRecording {
  /**
   * The finished clip once the recorder has stopped — asked to, or at the
   * limit — or null when it was thrown away.
   */
  done: Promise<Blob | null>
  /** Stops and keeps what was recorded. */
  stop: () => void
  /** Stops and throws it away: the screen closed while recording. */
  discard: () => void
}

export interface RecordOptions {
  /** Stops on its own after this long. */
  limitMs: number
  /** Called a few times a second with how long it has been recording. */
  onTick?: (elapsedMs: number) => void
  /** For tests: the clock. */
  now?: () => number
}

/**
 * Records `stream` in `format` until stopped or until the limit.
 *
 * The data is asked for every second rather than only at the end, so a
 * recorder that is stopped by the page going away has still handed over what
 * it had. The clip is labelled `video/mp4` without codecs, which is the media
 * type the upload is sent as and the only one the server accepts.
 */
export function recordClip(stream: MediaStream, format: ClipFormat, options: RecordOptions): ClipRecording {
  const now = options.now ?? (() => performance.now())
  const chunks: Blob[] = []
  let discarded = false

  const recorder = new MediaRecorder(stream, {
    mimeType: format.mimeType,
    videoBitsPerSecond,
    ...(format.withSound ? { audioBitsPerSecond } : {}),
  })

  let finish: (clip: Blob | null) => void = () => {}
  const done = new Promise<Blob | null>((resolve) => {
    finish = (clip) => {
      clearInterval(ticker)
      resolve(clip)
    }
  })

  recorder.ondataavailable = (event) => {
    if (event.data.size > 0) chunks.push(event.data)
  }

  recorder.onstop = () => finish(discarded || chunks.length === 0 ? null : new Blob(chunks, { type: 'video/mp4' }))
  recorder.onerror = () => finish(null)

  const startedAt = now()

  const ticker = setInterval(() => {
    const elapsed = now() - startedAt
    options.onTick?.(Math.min(elapsed, options.limitMs))

    if (elapsed >= options.limitMs) stop()
  }, 100)

  function stop() {
    if (recorder.state !== 'inactive') recorder.stop()
  }

  try {
    recorder.start(1000)
  }
  catch {
    // A recorder that will not start is no clip, not a stuck screen.
    finish(null)
  }

  return {
    done,
    stop,
    discard: () => {
      discarded = true
      stop()
    },
  }
}

/** Stops every track of a stream: a track left running is a recording light that stays on. */
export function stopStream(stream: MediaStream | null | undefined): void {
  stream?.getTracks().forEach(track => track.stop())
}
