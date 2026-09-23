export const GRAVITY_FLIP_DURATION = 40

export interface GravityGate {
  id: number
  x: number
  gap: number
  size: number
}

export interface GravityFlipState {
  phase: 'ready' | 'playing' | 'paused' | 'over'
  gravity: -1 | 1
  playerY: number
  velocity: number
  gates: GravityGate[]
  score: number
  elapsed: number
  nextId: number
  flips: number
  event: 'flip' | 'score' | 'hit' | null
}

const clamp = (value: number, min: number, max: number) => Math.max(min, Math.min(max, value))

function createGate(id: number, random: () => number, x = 1.14): GravityGate {
  return { id, x, gap: 22 + random() * 56, size: Math.max(18, 29 - random() * 8) }
}

export function createGravityFlip(random = Math.random): GravityFlipState {
  const gates = Array.from({ length: 4 }, (_, index) => createGate(index, random, 1.1 + index * 0.42))
  return { phase: 'ready', gravity: 1, playerY: 50, velocity: 0, gates, score: 0, elapsed: 0, nextId: gates.length, flips: 0, event: null }
}

export function flipGravity(state: GravityFlipState): GravityFlipState {
  if (state.phase !== 'playing') return state
  return { ...state, gravity: state.gravity === 1 ? -1 : 1, velocity: -state.velocity * 0.65, flips: state.flips + 1, event: 'flip' }
}

export function startGravityFlip(state: GravityFlipState): GravityFlipState {
  return state.phase === 'ready' || state.phase === 'paused' ? { ...state, phase: 'playing', event: null } : state
}

export function advanceGravityFlip(state: GravityFlipState, seconds: number, random = Math.random): GravityFlipState {
  if (state.phase !== 'playing' || !Number.isFinite(seconds) || seconds <= 0) return state
  const delta = Math.min(seconds, 0.1)
  const elapsed = Math.min(GRAVITY_FLIP_DURATION, state.elapsed + delta)
  const speed = 0.33 + elapsed / 130
  const velocity = clamp(state.velocity + state.gravity * 92 * delta, -110, 110)
  const playerY = clamp(state.playerY + velocity * delta, 7, 93)
  let score = state.score
  let event: GravityFlipState['event'] = null
  let crashed = playerY <= 7 || playerY >= 93
  const gates: GravityGate[] = []
  for (const gate of state.gates) {
    const x = gate.x - speed * delta
    if (x <= 0.28 && x >= 0.18) {
      const inGap = Math.abs(playerY - gate.gap) <= gate.size / 2
      if (!inGap) crashed = true
    }
    if (x <= 0.02) score += 10
    else gates.push({ ...gate, x })
  }
  let nextId = state.nextId
  while (gates.length < 4) gates.push(createGate(nextId++, random, 1.15 + random() * 0.35))
  if (crashed) event = 'hit'
  else if (gates.length < state.gates.length) event = 'score'
  return { ...state, phase: crashed || elapsed >= GRAVITY_FLIP_DURATION ? 'over' : 'playing', playerY, velocity, gates, score, elapsed, nextId, event }
}
