export type Direction = 'up' | 'down' | 'left' | 'right'
export interface Twenty48State { board: number[]; score: number }

function addTile(board: number[], random: () => number): number[] {
  const empty = board.flatMap((value, index) => value === 0 ? [index] : [])
  if (!empty.length) return board
  const next = [...board]
  next[empty[Math.floor(random() * empty.length)]] = random() < 0.9 ? 2 : 4
  return next
}

export function createTwenty48(random = Math.random): Twenty48State {
  return { board: addTile(addTile(Array<number>(16).fill(0), random), random), score: 0 }
}

export function canMove(board: number[]): boolean {
  return board.some((value, index) => value === 0
    || (index % 4 < 3 && value === board[index + 1])
    || (index < 12 && value === board[index + 4]))
}

export function moveTiles(state: Twenty48State, direction: Direction, random = Math.random): Twenty48State {
  const board = [...state.board]
  let score = state.score
  for (let line = 0; line < 4; line++) {
    const indices = Array.from({ length: 4 }, (_, offset) => {
      if (direction === 'left') return line * 4 + offset
      if (direction === 'right') return line * 4 + 3 - offset
      if (direction === 'up') return offset * 4 + line
      return (3 - offset) * 4 + line
    })
    const values = indices.map((index) => state.board[index]).filter(Boolean)
    const merged: number[] = []
    for (let index = 0; index < values.length; index++) {
      if (values[index] === values[index + 1]) {
        const value = values[index] * 2
        merged.push(value)
        score += value
        index++
      } else merged.push(values[index])
    }
    indices.forEach((index, offset) => { board[index] = merged[offset] ?? 0 })
  }
  if (board.every((value, index) => value === state.board[index])) return state
  return { board: addTile(board, random), score }
}
