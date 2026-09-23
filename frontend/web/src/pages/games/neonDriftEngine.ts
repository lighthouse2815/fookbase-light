export const NEON_DRIFT_DURATION = 45

export interface NeonObstacle {
  id: number
  lane: -1 | 0 | 1
  depth: number
  speed: number
  kind: 'gate' | 'orb'
}

export interface NeonDriftState {
  phase: 'ready' | 'playing' | 'paused' | 'over'
  lane: -1 | 0 | 1
  obstacles: NeonObstacle[]
  score: number
  lives: number
  elapsed: number
  nextId: number
  phaseTime: number
  event: 'dodge' | 'hit' | 'phase' | null
}

const clamp = (value: number, min: number, max: number) => Math.max(min, Math.min(max, value))
const lanes: Array<-1 | 0 | 1> = [-1, 0, 1]

function createObstacle(id: number, random: () => number, depth = -0.14): NeonObstacle {
  return { id, lane: lanes[Math.floor(random() * lanes.length)], depth, speed: 0.34 + random() * 0.13, kind: random() < 0.58 ? 'gate' : 'orb' }
}

export function createNeonDrift(random = Math.random): NeonDriftState {
  const obstacles = Array.from({ length: 5 }, (_, index) => createObstacle(index, random, -index * 0.24 - 0.12))
  return { phase: 'ready', lane: 0, obstacles, score: 0, lives: 3, elapsed: 0, nextId: obstacles.length, phaseTime: 0, event: null }
}

export function moveNeonDrift(state: NeonDriftState, direction: -1 | 1): NeonDriftState {
  if (state.phase !== 'playing') return state
  return { ...state, lane: clamp(state.lane + direction, -1, 1) as -1 | 0 | 1 }
}

export function phaseShift(state: NeonDriftState): NeonDriftState {
  if (state.phase !== 'playing' || state.phaseTime > 0) return state
  return { ...state, phaseTime: 1.25, event: 'phase' }
}

export function startNeonDrift(state: NeonDriftState): NeonDriftState {
  return state.phase === 'ready' || state.phase === 'paused' ? { ...state, phase: 'playing', event: null } : state
}

export function advanceNeonDrift(state: NeonDriftState, seconds: number, random = Math.random): NeonDriftState {
  if (state.phase !== 'playing' || !Number.isFinite(seconds) || seconds <= 0) return state
  const delta = Math.min(seconds, 0.1)
  const elapsed = Math.min(NEON_DRIFT_DURATION, state.elapsed + delta)
  const phaseTime = Math.max(0, state.phaseTime - delta)
  let score = state.score
  let lives = state.lives
  let event: NeonDriftState['event'] = null
  const obstacles: NeonObstacle[] = []
  for (const obstacle of state.obstacles) {
    const depth = obstacle.depth + obstacle.speed * delta * (1 + elapsed / 100)
    const atPlayer = depth >= 0.84 && depth <= 0.98 && obstacle.lane === state.lane
    if (atPlayer) {
      if (phaseTime > 0) { score += obstacle.kind === 'gate' ? 20 : 15; event = 'phase' }
      else { lives -= 1; event = 'hit' }
      continue
    }
    if (depth <= 1.12) obstacles.push({ ...obstacle, depth })
    else score += obstacle.kind === 'gate' ? 12 : 8
  }
  let nextId = state.nextId
  while (obstacles.length < 5) obstacles.push(createObstacle(nextId++, random))
  return { ...state, phase: lives <= 0 || elapsed >= NEON_DRIFT_DURATION ? 'over' : 'playing', obstacles, score, lives, elapsed, nextId, phaseTime, event: event ?? (obstacles.length !== state.obstacles.length ? 'dodge' : null) }
}
