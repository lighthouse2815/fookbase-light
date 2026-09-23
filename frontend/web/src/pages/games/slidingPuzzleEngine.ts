export interface PuzzleState { board: number[]; moves: number }
export const isPuzzleSolved = (board: number[]) => board.every((value, index) => value === (index + 1) % 16)
export const puzzleNeighbors = (empty: number) => [empty - 4, empty + 4, empty - 1, empty + 1].filter((index) => index >= 0 && index < 16
  && Math.abs(index % 4 - empty % 4) + Math.abs(Math.floor(index / 4) - Math.floor(empty / 4)) === 1)

export function createSlidingPuzzle(random = Math.random): PuzzleState {
  const board = Array.from({ length: 16 }, (_, index) => (index + 1) % 16)
  let empty = 15
  let previous = -1
  // Shuffle through legal moves so every board remains solvable.
  for (let step = 0; step < 200; step++) {
    const choices = puzzleNeighbors(empty).filter((index) => index !== previous)
    const next = choices[Math.floor(random() * choices.length)]
    ;[board[empty], board[next]] = [board[next], board[empty]]
    previous = empty
    empty = next
  }
  if (isPuzzleSolved(board)) [board[14], board[15]] = [board[15], board[14]]
  return { board, moves: 0 }
}

export function slideTile(state: PuzzleState, index: number): PuzzleState {
  const empty = state.board.indexOf(0)
  if (isPuzzleSolved(state.board) || !puzzleNeighbors(empty).includes(index)) return state
  const board = [...state.board]
  ;[board[empty], board[index]] = [board[index], board[empty]]
  return { board, moves: state.moves + 1 }
}
