/**
 * Tiny cross-platform process helpers used by every root script.
 *
 * Deliberately dependency-free: commands are spawned with an argv array and
 * never through a shell, so there is no quoting/escaping difference between
 * macOS, Linux and Windows.
 */

export interface Step {
  /** Short name shown in the log and in failure messages. */
  label: string
  cmd: string
  args: string[]
  /** Repo-relative working directory. Defaults to the repository root. */
  cwd?: string
  env?: Record<string, string | undefined>
}

const RESET = '\u001B[0m'
const BOLD = '\u001B[1m'
const DIM = '\u001B[2m'
const RED = '\u001B[31m'
const GREEN = '\u001B[32m'
const CYAN = '\u001B[36m'

export function heading(text: string): void {
  console.log(`\n${BOLD}${CYAN}▶ ${text}${RESET}`)
}

export function info(text: string): void {
  console.log(`${DIM}${text}${RESET}`)
}

export function success(text: string): void {
  console.log(`${GREEN}✔ ${text}${RESET}`)
}

export function failure(text: string): void {
  console.error(`${RED}✖ ${text}${RESET}`)
}

function describe(step: Step): string {
  const where = step.cwd ? ` ${DIM}(cwd: ${step.cwd})${RESET}` : ''
  return `${step.cmd} ${step.args.join(' ')}${where}`
}

/** Runs a single step, streaming its output. Resolves with the exit code. */
export async function run(step: Step, options: { quiet?: boolean } = {}): Promise<number> {
  if (!options.quiet) {
    heading(`${step.label}`)
    info(`  ${describe(step)}`)
  }

  const child = Bun.spawn([step.cmd, ...step.args], {
    cwd: step.cwd,
    env: { ...process.env, ...step.env },
    stdio: ['inherit', 'inherit', 'inherit'],
  })

  return await child.exited
}

/**
 * Runs steps one after another and stops at the first failure.
 * Returns the exit code of the failing step, or 0 when everything passed.
 */
export async function runSequence(steps: Step[]): Promise<number> {
  const started = Date.now()

  for (const step of steps) {
    const code = await run(step)
    if (code !== 0) {
      failure(`"${step.label}" failed with exit code ${code}`)
      failure(`  ${step.cmd} ${step.args.join(' ')}`)
      return code
    }
  }

  const seconds = ((Date.now() - started) / 1000).toFixed(1)
  success(`${steps.length} step(s) passed in ${seconds}s`)
  return 0
}

/**
 * Runs steps concurrently and keeps them alive until one exits or the user
 * presses Ctrl-C. Used for `dev` style tasks where every process is long-lived.
 */
export async function runConcurrently(steps: Step[]): Promise<number> {
  const children = steps.map((step) => {
    heading(step.label)
    info(`  ${describe(step)}`)
    return {
      step,
      child: Bun.spawn([step.cmd, ...step.args], {
        cwd: step.cwd,
        env: { ...process.env, ...step.env },
        stdio: ['inherit', 'inherit', 'inherit'],
      }),
    }
  })

  let shuttingDown = false
  const stopAll = () => {
    if (shuttingDown) return
    shuttingDown = true
    for (const { child } of children) {
      try {
        child.kill()
      }
      catch {
        // The process may already be gone — nothing to do.
      }
    }
  }

  process.on('SIGINT', stopAll)
  process.on('SIGTERM', stopAll)

  const first = await Promise.race(
    children.map(async ({ step, child }) => ({ step, code: await child.exited })),
  )

  stopAll()
  await Promise.allSettled(children.map(({ child }) => child.exited))

  if (first.code !== 0 && !shuttingDown) {
    failure(`"${first.step.label}" exited with code ${first.code}`)
  }
  return first.code
}
