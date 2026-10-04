import assert from 'node:assert/strict'
import test from 'node:test'
import { createSlidingPuzzle, isPuzzleSolved, puzzleNeighbors, slideTile } from '../src/pages/games/slidingPuzzleEngine.ts'

test('shuffled boards contain every tile once, remain solvable and are not already solved', () => {
  for (let seed = 1; seed <= 100; seed++) {
    let value = seed
    const game = createSlidingPuzzle(() => { value = (value * 1664525 + 1013904223) >>> 0; return value / 2 ** 32 })
    assert.deepEqual([...game.board].sort((a, b) => a - b), Array.from({ length: 16 }, (_, i) => i))
    const tiles = game.board.filter(Boolean)
    let inversions = 0
    tiles.forEach((tile, i) => { inversions += tiles.slice(i + 1).filter(other => tile > other).length })
    const emptyRowFromBottom = 4 - Math.floor(game.board.indexOf(0) / 4)
    assert.equal((inversions + emptyRowFromBottom) % 2, 1)
    assert.equal(isPuzzleSolved(game.board), false)
    assert.equal(game.moves, 0)
  }
})

test('only adjacent tiles move, with no wrapping between rows', () => {
  assert.deepEqual(puzzleNeighbors(3).sort((a, b) => a - b), [2, 7])
  assert.deepEqual(puzzleNeighbors(4).sort((a, b) => a - b), [0, 5, 8])
  const state = { board: [1, 2, 3, 0, 5, 6, 7, 4, 9, 10, 11, 8, 13, 14, 15, 12], moves: 0 }
  assert.equal(slideTile(state, 4), state)
  assert.equal(slideTile(state, 3), state)
  const moved = slideTile(state, 7)
  assert.equal(moved.board[3], 4)
  assert.equal(moved.board[7], 0)
  assert.equal(moved.moves, 1)
  assert.equal(state.board[3], 0)
})

test('the final slide wins and completed boards cannot be changed', () => {
  const state = { board: [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 0, 15], moves: 20 }
  const won = slideTile(state, 15)
  assert.equal(isPuzzleSolved(won.board), true)
  assert.equal(won.moves, 21)
  assert.equal(slideTile(won, 14), won)
})
