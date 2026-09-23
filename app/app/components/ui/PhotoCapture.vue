<script setup lang="ts">
import { hapticTap } from '~/utils/haptics'
import { isApiError } from '~/api/errors'
import type { Image, ImagePurpose } from '~/api/types'
import { downscaleForUpload, uploadSizes } from '~/utils/images'
import { type CameraFacing, rememberFacing, rememberedFacing } from '~/utils/cameraFacing'

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
 */
const props = withDefaults(defineProps<{
  purpose: ImagePurpose
  /** The longest edge the upload may have. Defaults to the purpose's own size. */
  maxEdge?: number
  /**
   * What the stored picture is handed to while the screen is still open.
   * Resolving to a sentence means it was refused: the screen stays, showing the
   * picture and the sentence, and "Verwenden" tries the hand-in again without
   * uploading a second time. Without it, the screen closes once the upload is
   * done — which is all an avatar needs.
   *
   * This exists because a proof's hand-in is the step that can fail, and it
   * used to run after the camera had closed: a refused photograph was simply
   * gone, with nothing on screen to say so.
   */
  handIn?: (image: Image) => Promise<string | null>
}>(), {
  maxEdge: undefined,
  handIn: undefined,
})

const emit = defineEmits<{ uploaded: [image: Image] }>()

const open = defineModel<boolean>('open', { required: true })

const t = useMessages()
const api = useQ2Api()
const { report } = useErrorReporter()

const video = useTemplateRef<HTMLVideoElement>('video')
const fileInput = useTemplateRef<HTMLInputElement>('fileInput')

/** The camera is live, a picture is waiting to be confirmed, or it is going up. */
const stage = ref<'camera' | 'preview' | 'uploading'>('camera')
const message = ref<string | null>(null)
const facing = ref<CameraFacing>('environment')

/** Only offered when there is a second camera to switch to. */
const canSwitch = ref(false)

/** The picture taken but not yet sent, and the object URL showing it. */
const pending = shallowRef<Blob | null>(null)
const previewUrl = ref<string | null>(null)

/**
 * The stored copy of `pending`, once it is up. Kept while a refused hand-in is
 * on screen, so trying again reuses it rather than uploading the same picture
 * twice; dropped with `pending`, because it is a copy of that one picture.
 */
const uploaded = shallowRef<Image | null>(null)

const stream = shallowRef<MediaStream | null>(null)
const cameraReady = ref(false)

const edge = computed(() => props.maxEdge ?? (props.purpose === 'Avatar' ? uploadSizes.avatar : uploadSizes.proof))

/*
 * Both directions. Opening starts the camera; closing stops it and throws the
 * pending picture away — a photograph left in memory after the screen is gone is
 * the sort of thing that turns up in the next person's session.
 */
watch(open, async (isOpen) => {
  if (isOpen) {
    reset()
    facing.value = rememberedFacing()
    await startCamera()
  }
  else {
    stopCamera()
    clearPending()
  }
})

// A route change or a hot reload closes the screen without a `false` ever
// arriving, so the release has to happen here as well.
onBeforeUnmount(() => {
  stopCamera()
  clearPending()
})

async function startCamera() {
  // No `mediaDevices` at all in a plain HTTP context, in a component test, or
  // in a browser that has none. That is not an error to report — it is the
  // file picker's cue.
  if (!import.meta.client || !navigator.mediaDevices?.getUserMedia) {
    cameraReady.value = false
    return
  }

  try {
    const opened = await navigator.mediaDevices.getUserMedia({
      video: { facingMode: facing.value },
      audio: false,
    })

    // Closed while the permission prompt was up: a stream nobody can see is a
    // recording light that stays on.
    if (!open.value) {
      opened.getTracks().forEach(track => track.stop())
      return
    }

    stopCamera()
    stream.value = opened
    cameraReady.value = true
    canSwitch.value = await hasSeveralCameras()

    await nextTick()

    if (video.value) {
      video.value.srcObject = opened
      await video.value.play().catch(() => {
        // Autoplay refused. The stream is still live and the frame the shutter
        // grabs is still current, so this is not worth interrupting anybody for.
      })
    }
  }
  catch {
    // A refused permission is the person's answer, not a defect: no Sentry
    // issue, just the other route.
    cameraReady.value = false
    message.value = t.value.photo.cameraBlocked
  }
}

function stopCamera() {
  stream.value?.getTracks().forEach(track => track.stop())
  stream.value = null

  if (video.value) {
    video.value.srcObject = null
  }
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

/** Grabs the current frame at the camera's own resolution. */
function shoot() {
  const element = video.value

  if (!element || !element.videoWidth) return

  hapticTap()

  const canvas = document.createElement('canvas')
  canvas.width = element.videoWidth
  canvas.height = element.videoHeight
  canvas.getContext('2d')?.drawImage(element, 0, 0)

  canvas.toBlob((blob) => {
    if (blob) hold(blob)
  }, 'image/jpeg', 0.92)
}

function onFilePicked(event: Event) {
  const [file] = (event.target as HTMLInputElement).files ?? []

  if (file) hold(file)

  // Cleared so picking the same file twice in a row fires `change` again.
  if (fileInput.value) fileInput.value.value = ''
}

function hold(blob: Blob) {
  clearPending()
  pending.value = blob
  previewUrl.value = URL.createObjectURL(blob)
  stage.value = 'preview'
  message.value = null
  stopCamera()
}

function clearPending() {
  if (previewUrl.value) URL.revokeObjectURL(previewUrl.value)
  previewUrl.value = null
  pending.value = null
  uploaded.value = null
}

async function retake() {
  clearPending()
  stage.value = 'camera'
  await startCamera()
}

async function confirm() {
  const blob = pending.value

  if (!blob) return

  stage.value = 'uploading'
  message.value = t.value.photo.uploading

  try {
    const image = uploaded.value
      ?? await api.images.upload(await downscaleForUpload(blob, edge.value), props.purpose)

    uploaded.value = image

    const refused = props.handIn ? await props.handIn(image) : null

    if (refused) {
      stage.value = 'preview'
      message.value = refused
      return
    }

    // Cleared before closing: the screen stays mounted while it animates out,
    // and "Wird hochgeladen …" left standing under a finished upload reads as
    // though it were stuck.
    message.value = null
    emit('uploaded', image)
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
    message.value = failure.status === 413 ? t.value.photo.tooLarge : t.value.photo.failed
  }
}

function reset() {
  stage.value = 'camera'
  message.value = null
  cameraReady.value = false
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
    :title="t.photo.heading"
    :description="cameraReady ? t.photo.cameraHint : t.photo.fileHint"
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
            {{ t.photo.heading }}
          </p>

          <!-- Balances the close button, so the heading sits in the middle. -->
          <span
            class="size-11"
            aria-hidden="true"
          />
        </div>

        <!--
          `data-q2-block` on the frame, not on the picture: Session Replay
          records every session, and somebody's face is the most personal thing
          this app will ever hold. Blocking the container covers the live
          camera, the preview and whatever sits between them.
        -->
        <div
          class="relative mx-3 mt-2 min-h-0 flex-1 overflow-hidden rounded-(--q2-radius-lg) bg-(--ui-bg-elevated)"
          data-q2-block
        >
          <!--
            `contain`, not `cover`: this is exactly what will be sent, and a
            preview that crops would be showing a different picture.
          -->
          <img
            v-if="previewUrl"
            :src="previewUrl"
            :alt="t.photo.preview"
            class="size-full object-contain"
            data-testid="photo-preview"
          >

          <!--
            `playsinline` keeps iOS from taking the video full screen the moment
            it plays, which would replace this screen with a player. `muted` is
            what makes autoplay permitted at all. The front camera is mirrored
            the way every phone shows it; the picture taken is not.
          -->
          <video
            v-show="!previewUrl && cameraReady"
            ref="video"
            class="size-full object-cover"
            :class="{ '-scale-x-100': facing === 'user' }"
            playsinline
            muted
            autoplay
            data-testid="photo-camera"
          />

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
          {{ cameraReady ? t.photo.cameraHint : t.photo.fileHint }}
        </p>

        <!--
          Always present, never visible. The buttons below are what people
          press; this is the element the browser needs in order to open a
          picker at all.
        -->
        <input
          ref="fileInput"
          type="file"
          accept="image/*"
          class="sr-only"
          data-testid="photo-file"
          @change="onFilePicked"
        >

        <!--
          A camera's layout: the gallery under the left thumb, the shutter in
          the middle, the other camera on the right. The shutter is the one
          thing on this screen that is done now, so it carries the accent.
        -->
        <div
          v-if="stage === 'camera' && cameraReady"
          class="grid shrink-0 grid-cols-3 items-center px-6 pt-4 pb-2"
        >
          <UButton
            color="neutral"
            variant="soft"
            icon="i-lucide-image"
            class="size-14 justify-self-start justify-center rounded-full"
            :ui="{ leadingIcon: 'size-6' }"
            :aria-label="t.photo.chooseFile"
            data-testid="photo-choose"
            @click="fileInput?.click()"
          />

          <button
            type="button"
            class="q2-press size-[76px] justify-self-center rounded-full p-1.5 ring-4 ring-(--ui-border-accented) focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-(--q2-accent-solid)"
            :aria-label="t.photo.shutter"
            data-testid="photo-shutter"
            @click="shoot"
          >
            <span class="block size-full rounded-full bg-(--q2-accent-solid)" />
          </button>

          <UButton
            v-if="canSwitch"
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
          v-else-if="stage === 'camera'"
          class="shrink-0 px-4 pt-4 pb-2"
        >
          <UButton
            block
            size="xl"
            icon="i-lucide-image"
            :label="t.photo.chooseFile"
            data-testid="photo-choose"
            @click="fileInput?.click()"
          />
        </div>

        <div
          v-else
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
