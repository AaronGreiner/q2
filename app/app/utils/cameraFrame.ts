/**
 * One frame of a live camera, as a JPEG.
 *
 * At the camera's own resolution — the upload shrinks it afterwards — and
 * drawn as the camera delivers it. That matters for the front camera: the
 * screen shows it mirrored, the way every phone does, but the picture kept is
 * the scene as it was, so writing on a T-shirt reads the right way round for
 * the friends who look at it (#125).
 *
 * `createCanvas` is there for tests; a page never passes it.
 */
export function grabFrame(
  video: HTMLVideoElement | null | undefined,
  createCanvas: () => HTMLCanvasElement = () => document.createElement('canvas'),
): Promise<Blob | null> {
  if (!video?.videoWidth || !video.videoHeight) return Promise.resolve(null)

  const canvas = createCanvas()
  canvas.width = video.videoWidth
  canvas.height = video.videoHeight

  const context = canvas.getContext('2d')
  if (!context) return Promise.resolve(null)

  context.drawImage(video, 0, 0)

  return new Promise(resolve => canvas.toBlob(resolve, 'image/jpeg', 0.92))
}

/**
 * Waits until a `<video>` shows a frame — a camera that was just opened needs a
 * moment before there is anything to grab. Gives up after `timeoutMs`, and
 * then whatever grabs the frame finds none.
 */
export function frameReady(video: HTMLVideoElement | null | undefined, timeoutMs = 3000): Promise<void> {
  if (!video || video.videoWidth) return Promise.resolve()

  return new Promise((resolve) => {
    const finish = () => {
      clearTimeout(timer)
      video.removeEventListener('loadeddata', finish)
      resolve()
    }
    const timer = setTimeout(finish, timeoutMs)
    video.addEventListener('loadeddata', finish)
  })
}
