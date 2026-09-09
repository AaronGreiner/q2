<script setup lang="ts">
import { isApiError } from '~/api/errors'
import type { Image, ImagePurpose } from '~/api/types'
import { downscaleForUpload, uploadSizes } from '~/utils/images'

/**
 * Taking a picture and handing back the stored one.
 *
 * This is what the migration plan calls `ProofCamera`, named for what it does
 * rather than for the one thing it will be used for: stage 3 uses it for a
 * profile picture and stage 4 will use the same component, unchanged, for the
 * photograph a goal's friends vote on.
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
 * may ever see it.
 *
 * **Open it from a screen, never from inside another sheet.** Two `UDrawer`s
 * open at once deadlock: vaul drives both from one set of body styles, so the
 * second one's close transition never finishes — it sits at
 * `data-state="closed"` and stays on screen while the first is translated out
 * of the viewport, and neither can be reached again. vaul's own answer,
 * `nested`, does not apply here either: it needs a real `DrawerRoot` ancestor,
 * and a drawer portals its content, so a sheet rendered beside one is not
 * inside it. The caller therefore closes its own sheet before opening this and
 * brings it back afterwards — see `pages/profile.vue`.
 */
const props = withDefaults(defineProps<{
  purpose: ImagePurpose
  /** The longest edge the upload may have. Defaults to the purpose's own size. */
  maxEdge?: number
}>(), {
  maxEdge: undefined,
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
const facing = ref<'user' | 'environment'>('user')

/** The picture taken but not yet sent, and the object URL showing it. */
const pending = shallowRef<Blob | null>(null)
const previewUrl = ref<string | null>(null)

const stream = shallowRef<MediaStream | null>(null)
const cameraReady = ref(false)

const edge = computed(() => props.maxEdge ?? (props.purpose === 'Avatar' ? uploadSizes.avatar : uploadSizes.proof))

/*
 * Both directions. Opening starts the camera; closing stops it and throws the
 * pending picture away — a photograph left in memory after the sheet is gone is
 * the sort of thing that turns up in the next person's session.
 */
watch(open, async (isOpen) => {
  if (isOpen) {
    reset()
    await startCamera()
  }
  else {
    stopCamera()
    clearPending()
  }
})

// A route change or a hot reload closes the sheet without a `false` ever
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

    stopCamera()
    stream.value = opened
    cameraReady.value = true

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

async function switchCamera() {
  facing.value = facing.value === 'user' ? 'environment' : 'user'
  await startCamera()
}

/** Grabs the current frame at the camera's own resolution. */
function shoot() {
  const element = video.value

  if (!element || !element.videoWidth) return

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
    const uploaded = await api.images.upload(await downscaleForUpload(blob, edge.value), props.purpose)

    // Cleared before closing: the sheet stays mounted while it animates out,
    // and "Wird hochgeladen …" left standing under a finished upload reads as
    // though it were stuck.
    message.value = null
    emit('uploaded', uploaded)
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
  <UDrawer
    v-model:open="open"
    :title="t.photo.heading"
    :description="cameraReady ? t.photo.cameraHint : t.photo.fileHint"
    :ui="{ container: 'max-w-[430px] mx-auto' }"
  >
    <template #body>
      <div class="flex flex-col gap-4 pb-2">
        <!--
          `data-q2-block` on the frame, not on the picture: Session Replay
          records every session, and somebody's face is the most personal thing
          this app will ever hold. Blocking the container covers the live
          camera, the preview and whatever sits between them.
        -->
        <div
          class="relative aspect-square w-full overflow-hidden rounded-(--q2-radius-lg) bg-(--ui-bg-elevated)"
          data-q2-block
        >
          <img
            v-if="previewUrl"
            :src="previewUrl"
            :alt="t.photo.preview"
            class="size-full object-cover"
            data-testid="photo-preview"
          >

          <!--
            `playsinline` keeps iOS from taking the video full screen the moment
            it plays, which would replace the whole sheet with a player. `muted`
            is what makes autoplay permitted at all.
          -->
          <video
            v-show="!previewUrl && cameraReady"
            ref="video"
            class="size-full object-cover"
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
          class="text-[13px] font-semibold text-(--ui-text-muted)"
          role="status"
          data-testid="photo-message"
        >
          {{ message }}
        </p>

        <!--
          Always present, never visible. The two buttons below are what people
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

        <div
          v-if="stage === 'camera'"
          class="flex flex-col gap-2"
        >
          <UButton
            v-if="cameraReady"
            block
            size="xl"
            icon="i-lucide-camera"
            :label="t.photo.shutter"
            data-testid="photo-shutter"
            @click="shoot"
          />

          <UButton
            block
            size="xl"
            color="neutral"
            :variant="cameraReady ? 'outline' : 'solid'"
            icon="i-lucide-image"
            :label="t.photo.chooseFile"
            data-testid="photo-choose"
            @click="fileInput?.click()"
          />

          <UButton
            v-if="cameraReady"
            block
            size="lg"
            color="neutral"
            variant="ghost"
            icon="i-lucide-switch-camera"
            :label="t.photo.switchCamera"
            data-testid="photo-switch"
            @click="switchCamera"
          />
        </div>

        <div
          v-else
          class="flex flex-col gap-2"
        >
          <UButton
            block
            size="xl"
            icon="i-lucide-check"
            :label="t.photo.use"
            :loading="stage === 'uploading'"
            data-testid="photo-confirm"
            @click="confirm"
          />

          <UButton
            block
            size="lg"
            color="neutral"
            variant="outline"
            icon="i-lucide-rotate-ccw"
            :label="t.photo.retake"
            :disabled="stage === 'uploading'"
            data-testid="photo-retake"
            @click="retake"
          />
        </div>
      </div>
    </template>
  </UDrawer>
</template>
