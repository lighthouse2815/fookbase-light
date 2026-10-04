export const imageDurationMs = 5_000

export interface ImageStoryClock {
  elapsedMs: number
  startedAt: number | null
}

export function imageElapsedMs(clock: ImageStoryClock, now: number) {
  return Math.min(imageDurationMs, clock.elapsedMs + (clock.startedAt === null ? 0 : Math.max(0, now - clock.startedAt)))
}

export function pauseImageClock(clock: ImageStoryClock, now: number) {
  clock.elapsedMs = imageElapsedMs(clock, now)
  clock.startedAt = null
}
