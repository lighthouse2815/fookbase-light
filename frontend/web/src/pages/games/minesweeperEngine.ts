export const MINE_SIZE = 8
export const MINE_COUNT = 10
export interface MineState {
  mines: boolean[] | null
  revealed: boolean[]
  flags: boolean[]
  phase: 'ready' | 'playing' | 'won' | 'lost'
  detonated: number | null
}
const neighbors = (index: number) => Array.from({ length: 64 }, (_, cell) => cell).filter((cell) => cell !== index
  && Math.abs(cell % MINE_SIZE - index % MINE_SIZE) <= 1 && Math.abs(Math.floor(cell / MINE_SIZE) - Math.floor(index / MINE_SIZE)) <= 1)
export const adjacentMines = (mines: boolean[], index: number) => neighbors(index).filter((cell) => mines[cell]).length
export const createMinesweeper = (): MineState => ({ mines: null, revealed: Array<boolean>(64).fill(false), flags: Array<boolean>(64).fill(false), phase: 'ready', detonated: null })
const playable = (state: MineState, index: number) => Number.isInteger(index) && index >= 0 && index < 64 && !state.revealed[index] && state.phase !== 'won' && state.phase !== 'lost'

export function toggleFlag(state: MineState, index: number): MineState {
  if (!playable(state, index) || (!state.flags[index] && state.flags.filter(Boolean).length === MINE_COUNT)) return state
  return { ...state, flags: state.flags.with(index, !state.flags[index]) }
}

export function revealMineCell(state: MineState, index: number, random = Math.random): MineState {
  if (!playable(state, index) || state.flags[index]) return state
  let mines = state.mines
  if (!mines) {
    const safe = new Set([index, ...neighbors(index)])
    const candidates = Array.from({ length: 64 }, (_, cell) => cell).filter((cell) => !safe.has(cell))
    for (let i = candidates.length - 1; i > 0; i--) {
      const j = Math.floor(random() * (i + 1))
      ;[candidates[i], candidates[j]] = [candidates[j], candidates[i]]
    }
    mines = Array<boolean>(64).fill(false)
    candidates.slice(0, MINE_COUNT).forEach((cell) => { mines![cell] = true })
  }
  if (mines[index]) return { ...state, mines, phase: 'lost', detonated: index }
  const revealed = [...state.revealed]
  const pending = [index]
  while (pending.length) {
    const cell = pending.pop()!
    if (revealed[cell] || state.flags[cell] || mines[cell]) continue
    revealed[cell] = true
    if (adjacentMines(mines, cell) === 0) pending.push(...neighbors(cell))
  }
  return { ...state, mines, revealed, phase: revealed.filter(Boolean).length === 64 - MINE_COUNT ? 'won' : 'playing' }
}
