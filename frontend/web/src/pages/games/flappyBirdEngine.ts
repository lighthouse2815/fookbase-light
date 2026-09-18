// Drawing and collision detection share this fixed coordinate system at every screen size.
export const WORLD = {
  width: 360, height: 540, groundY: 464,
  birdX: 90, radius: 13, pipeWidth: 60, pipeLip: 4, pipeCapHeight: 22, gap: 150, pipeSpacing: 210,
} as const

const STEP = 1 / 120
const GRAVITY = 1500
const LIFT = -400
const SPEED = 125

export interface FlappyPipe { id: number; x: number; gapCenter: number; scored: boolean }
export interface FlappyState {
  phase: 'ready' | 'playing' | 'paused' | 'over'
  birdY: number
  velocity: number
  pipes: FlappyPipe[]
  score: number
  distance: number
  remainder: number
  nextPipeId: number
  seed: number | null
}

export const createGame = (seed: number | null = null): FlappyState => ({
  phase: 'ready', birdY: 230, velocity: 0, pipes: [], score: 0,
  distance: 0, remainder: 0, nextPipeId: 1, seed,
})

export function flap(state: FlappyState): FlappyState {
  if (state.phase !== 'ready' && state.phase !== 'playing') return state
  return {
    ...state, phase: 'playing', velocity: LIFT,
    pipes: state.phase === 'ready'
      ? [{ id: state.nextPipeId, x: WORLD.width + 32, gapCenter: 230, scored: false }]
      : state.pipes,
    nextPipeId: state.phase === 'ready' ? state.nextPipeId + 1 : state.nextPipeId,
  }
}

export function advanceGame(state: FlappyState, elapsed: number, random = Math.random): FlappyState {
  if (state.phase !== 'playing') return state
  const next = { ...state, pipes: state.pipes.map(pipe => ({ ...pipe })) }
  next.remainder += Math.max(0, Math.min(elapsed, 0.1))

  // Fixed substeps avoid frame-rate-dependent motion and tunnelling through pipe edges.
  while (next.remainder + 1e-9 >= STEP && next.phase === 'playing') {
    next.remainder = Math.max(0, next.remainder - STEP)
    next.velocity = Math.min(next.velocity + GRAVITY * STEP, 620)
    next.birdY += next.velocity * STEP
    next.distance += SPEED * STEP
    for (const pipe of next.pipes) pipe.x -= SPEED * STEP

    const last = next.pipes.at(-1)
    if (last && last.x <= WORLD.width - WORLD.pipeSpacing) {
      next.pipes.push({
        id: next.nextPipeId++, x: last.x + WORLD.pipeSpacing,
        gapCenter: Math.max(145, Math.min(319, last.gapCenter + (next.seed === null ? random() : seededRandom(next.seed, next.nextPipeId) * 2 - 1) * 70)),
        scored: false,
      })
    }

    const hitsPipe = next.pipes.some(pipe => {
      const left = pipe.x - WORLD.pipeLip
      const right = pipe.x + WORLD.pipeWidth + WORLD.pipeLip
      const top = pipe.gapCenter - WORLD.gap / 2
      const bottom = pipe.gapCenter + WORLD.gap / 2
      const hitsRect = (x1: number, x2: number, y1: number, y2: number) => {
        const dx = WORLD.birdX - Math.max(x1, Math.min(WORLD.birdX, x2))
        const dy = next.birdY - Math.max(y1, Math.min(next.birdY, y2))
        return dx * dx + dy * dy <= WORLD.radius ** 2
      }
      return hitsRect(pipe.x, pipe.x + WORLD.pipeWidth, 0, top)
        || hitsRect(left, right, top - WORLD.pipeCapHeight, top)
        || hitsRect(pipe.x, pipe.x + WORLD.pipeWidth, bottom, WORLD.groundY)
        || hitsRect(left, right, bottom, bottom + WORLD.pipeCapHeight)
    })
    if (hitsPipe || next.birdY - WORLD.radius <= 0 || next.birdY + WORLD.radius >= WORLD.groundY) {
      next.phase = 'over'
      next.birdY = Math.max(WORLD.radius, Math.min(WORLD.groundY - WORLD.radius, next.birdY))
      next.remainder = 0
      break
    }
    for (const pipe of next.pipes) {
      if (!pipe.scored && pipe.x + WORLD.pipeWidth + WORLD.pipeLip < WORLD.birdX - WORLD.radius) {
        pipe.scored = true
        next.score++
      }
    }
    next.pipes = next.pipes.filter(pipe => pipe.x + WORLD.pipeWidth + WORLD.pipeLip > 0)
  }
  return next
}

function seededRandom(seed: number, value: number) {
  let hash = (seed ^ Math.imul(value, 0x9e3779b9)) >>> 0
  hash = Math.imul(hash ^ (hash >>> 16), 0x85ebca6b) >>> 0
  hash = Math.imul(hash ^ (hash >>> 13), 0xc2b2ae35) >>> 0
  return ((hash ^ (hash >>> 16)) >>> 0) / 0x1_0000_0000
}
