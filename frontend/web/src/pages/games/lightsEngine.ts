export const LIGHTS_SIZE = 5
const cellCount = LIGHTS_SIZE * LIGHTS_SIZE

export interface LightsState {
  phase: 'ready' | 'playing' | 'won'
  board: boolean[]
  moves: number
  level: number
}

function affectedCells(index: number) {
  const row = Math.floor(index / LIGHTS_SIZE)
  const column = index % LIGHTS_SIZE
  return [index, row > 0 ? index - LIGHTS_SIZE : -1, row < LIGHTS_SIZE - 1 ? index + LIGHTS_SIZE : -1, column > 0 ? index - 1 : -1, column < LIGHTS_SIZE - 1 ? index + 1 : -1].filter((cell) => cell >= 0)
}

export function toggleLights(board: boolean[], index: number): boolean[] {
  if (!Number.isInteger(index) || index < 0 || index >= cellCount) return board
  const next = [...board]
  for (const cell of affectedCells(index)) next[cell] = !next[cell]
  return next
}

export function isLightsSolved(board: boolean[]) {
  return board.length === cellCount && board.every((light) => !light)
}

export function createLights(random = Math.random, level = 1): LightsState {
  let board = Array<boolean>(cellCount).fill(false)
  const scrambleCount = Math.min(8 + level * 2, 22)
  for (let count = 0; count < scrambleCount; count++) board = toggleLights(board, Math.floor(random() * cellCount))
  if (isLightsSolved(board)) board = toggleLights(board, Math.floor(random() * cellCount))
  return { phase: 'ready', board, moves: 0, level }
}

export function startLights(state: LightsState): LightsState {
  return state.phase === 'ready' ? { ...state, phase: 'playing' } : state
}

export function pressLights(state: LightsState, index: number): LightsState {
  if (state.phase !== 'playing' || !Number.isInteger(index) || index < 0 || index >= cellCount) return state
  const board = toggleLights(state.board, index)
  return { ...state, board, moves: state.moves + 1, phase: isLightsSolved(board) ? 'won' : 'playing' }
}
