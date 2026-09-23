export interface MemoryState {
  cards: number[]
  opened: number[]
  matched: number[]
  moves: number
}

export function createMemoryGame(): MemoryState {
  const cards = Array.from({ length: 16 }, (_, index) => index % 8)
  for (let index = cards.length - 1; index > 0; index--) {
    const other = Math.floor(Math.random() * (index + 1))
    ;[cards[index], cards[other]] = [cards[other], cards[index]]
  }
  return { cards, opened: [], matched: [], moves: 0 }
}

export function flipCard(state: MemoryState, index: number): MemoryState {
  if (!Number.isInteger(index) || index < 0 || index >= state.cards.length
    || state.opened.length === 2 || state.opened.includes(index) || state.matched.includes(index)) return state
  const opened = [...state.opened, index]
  if (opened.length === 1) return { ...state, opened }
  const moves = state.moves + 1
  return state.cards[opened[0]] === state.cards[index]
    ? { ...state, opened: [], matched: [...state.matched, ...opened], moves }
    : { ...state, opened, moves }
}
