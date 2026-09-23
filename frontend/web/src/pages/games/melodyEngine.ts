export const MELODY_ROUNDS = 12
export interface MelodyState {
  phase: 'ready' | 'listening' | 'input' | 'between' | 'over' | 'won'
  sequence: number[]
  cursor: number
  completed: number
}

export const createMelody = (): MelodyState => ({ phase: 'ready', sequence: [], cursor: 0, completed: 0 })

export function nextMelody(state: MelodyState, random = Math.random): MelodyState {
  if (state.phase !== 'ready' && state.phase !== 'between') return state
  return { ...state, phase: 'listening', cursor: 0, sequence: [...state.sequence, Math.floor(random() * 4)] }
}

export function finishMelodyPlayback(state: MelodyState): MelodyState {
  return state.phase === 'listening' ? { ...state, phase: 'input', cursor: 0 } : state
}

export function answerMelody(state: MelodyState, note: number): MelodyState {
  if (state.phase !== 'input' || !Number.isInteger(note) || note < 0 || note > 3) return state
  if (state.sequence[state.cursor] !== note) return { ...state, phase: 'over' }
  const cursor = state.cursor + 1
  if (cursor < state.sequence.length) return { ...state, cursor }
  const completed = state.completed + 1
  return { ...state, cursor, completed, phase: completed === MELODY_ROUNDS ? 'won' : 'between' }
}
