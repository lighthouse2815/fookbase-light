export const MOLE_ROUND_MS = 30_000
export interface MoleState {
  phase: 'ready' | 'playing' | 'paused' | 'over'
  remaining: number
  target: number
  targetRemaining: number
  lastHit: number | null
  score: number
  misses: number
}
export const createMoleGame = (): MoleState => ({ phase: 'ready', remaining: MOLE_ROUND_MS, target: -1, targetRemaining: 0, lastHit: null, score: 0, misses: 0 })

export function advanceMole(state: MoleState, elapsed: number, random = Math.random): MoleState {
  if (state.phase !== 'playing' || !Number.isFinite(elapsed) || elapsed < 0) return state
  const remaining = Math.max(0, state.remaining - elapsed)
  if (!remaining) return { ...state, remaining, target: -1, lastHit: null, phase: 'over' }
  const targetRemaining = state.targetRemaining - elapsed
  if (targetRemaining > 0) return { ...state, remaining, targetRemaining }
  const choices = Array.from({ length: 9 }, (_, index) => index).filter((index) => index !== state.target)
  return { ...state, remaining, target: choices[Math.floor(random() * choices.length)], targetRemaining: Math.max(450, 1000 - state.score * 2), lastHit: null }
}

export function hitMole(state: MoleState, index: number): MoleState {
  if (state.phase !== 'playing' || state.remaining <= 0 || !Number.isInteger(index) || index < 0 || index > 8) return state
  if (index !== state.target) return { ...state, misses: state.misses + 1 }
  return { ...state, score: state.score + 10, target: -1, targetRemaining: 180, lastHit: index }
}
