// The SVG and collision checks use the same coordinates on desktop and mobile.
export const JUMPING_WORLD = { width: 720, height: 400, groundY: 316, playerX: 124, radius: 18 } as const
const STEP = 1 / 120
const GRAVITY = 1900
const LIFT = 760

export interface JumpingObstacle { id: number; x: number; width: number; height: number; scored: boolean }
export interface JumpingState {
  phase: 'ready' | 'playing' | 'paused' | 'over'
  height: number
  velocity: number
  score: number
  elapsed: number
  distance: number
  remainder: number
  seed: number
  nextId: number
  obstacles: JumpingObstacle[]
}

export function createJumping(seed = Math.floor(Math.random() * 0x1_0000_0000)): JumpingState {
  return { phase: 'ready', height: 0, velocity: 0, score: 0, elapsed: 0, distance: 0,
    remainder: 0, seed: seed >>> 0, nextId: 1, obstacles: [] }
}

function addObstacle(state: JumpingState, x: number) {
  const random = () => {
    state.seed = (Math.imul(state.seed, 1664525) + 1013904223) >>> 0
    return state.seed / 0x1_0000_0000
  }
  state.obstacles.push({ id: state.nextId++, x, width: 26 + Math.floor(random() * 25),
    height: 30 + Math.floor(random() * 31), scored: false })
  return x + 360 + Math.floor(random() * 140)
}

export function startJumping(state: JumpingState): JumpingState {
  if (state.phase === 'paused') return { ...state, phase: 'playing', remainder: 0 }
  if (state.phase !== 'ready') return state
  const next = { ...state, phase: 'playing' as const, obstacles: [] as JumpingObstacle[] }
  let x = JUMPING_WORLD.width + 100
  for (let index = 0; index < 3; index++) x = addObstacle(next, x)
  return next
}

export function jump(state: JumpingState): JumpingState {
  if (state.phase !== 'playing' || state.height > 0) return state
  return { ...state, velocity: LIFT }
}

export function advanceJumping(state: JumpingState, seconds: number): JumpingState {
  if (state.phase !== 'playing' || !Number.isFinite(seconds) || seconds <= 0) return state
  const next = { ...state, obstacles: state.obstacles.map(block => ({ ...block })) }
  next.remainder += Math.min(seconds, 0.1)
  while (next.remainder + 1e-9 >= STEP && next.phase === 'playing') {
    next.remainder = Math.max(0, next.remainder - STEP)
    next.elapsed += STEP
    next.velocity -= GRAVITY * STEP
    next.height = Math.max(0, next.height + next.velocity * STEP)
    if (next.height === 0) next.velocity = 0
    const movement = Math.min(370, 250 + next.elapsed * 2) * STEP
    next.distance += movement
    for (const block of next.obstacles) block.x -= movement

    const y = JUMPING_WORLD.groundY - JUMPING_WORLD.radius - next.height
    const hit = next.obstacles.some(block => {
      const dx = JUMPING_WORLD.playerX - Math.max(block.x, Math.min(JUMPING_WORLD.playerX, block.x + block.width))
      const dy = y - Math.max(JUMPING_WORLD.groundY - block.height, Math.min(y, JUMPING_WORLD.groundY))
      return dx * dx + dy * dy <= JUMPING_WORLD.radius ** 2
    })
    if (hit) { next.phase = 'over'; next.remainder = 0; break }

    for (const block of next.obstacles) {
      if (!block.scored && block.x + block.width < JUMPING_WORLD.playerX - JUMPING_WORLD.radius) {
        block.scored = true
        next.score++
      }
    }
    next.obstacles = next.obstacles.filter(block => block.x + block.width > 0)
    const last = next.obstacles.at(-1)
    if (last && last.x < JUMPING_WORLD.width) {
      // Reserve enough space for a complete jump, even at the maximum running speed.
      next.seed = (Math.imul(next.seed, 1664525) + 1013904223) >>> 0
      addObstacle(next, last.x + 360 + Math.floor(next.seed / 0x1_0000_0000 * 140))
    }
  }
  return next
}
