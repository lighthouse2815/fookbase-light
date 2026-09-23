import assert from 'node:assert/strict'
import test from 'node:test'
import { canMove, createTwenty48, moveTiles } from '../src/pages/games/twenty48Engine.ts'

const stateWithRow = (row) => ({ board: [...row, ...Array(12).fill(0)], score: 0 })
const random = () => 0

test('a new game starts with two tiles and no score', () => {
  const game = createTwenty48(random)
  assert.equal(game.board.filter(Boolean).length, 2)
  assert.equal(game.score, 0)
})

test('each tile merges at most once per move, with score equal to merged values', () => {
  const cases = [
    [[2, 2, 2, 2], [4, 4, 2, 0], 8],
    [[2, 2, 4, 0], [4, 4, 2, 0], 4],
    [[4, 0, 4, 4], [8, 4, 2, 0], 8],
  ]
  for (const [row, expected, score] of cases) {
    const original = stateWithRow(row)
    const moved = moveTiles(original, 'left', random)
    assert.deepEqual(moved.board.slice(0, 4), expected)
    assert.equal(moved.score, score)
    assert.deepEqual(original.board.slice(0, 4), row)
  }
})

test('right, up and down respect the movement direction', () => {
  assert.deepEqual(moveTiles(stateWithRow([2, 2, 4, 0]), 'right', random).board.slice(0, 4), [2, 0, 4, 4])
  const state = { board: [2, 0, 0, 0, 2, 0, 0, 0, 4, 0, 0, 0, 0, 0, 0, 0], score: 10 }
  const up = moveTiles(state, 'up', random)
  assert.deepEqual([0, 4, 8, 12].map((i) => up.board[i]), [4, 4, 0, 0])
  const down = moveTiles(state, 'down', random)
  assert.deepEqual([0, 4, 8, 12].map((i) => down.board[i]), [2, 0, 4, 4])
  assert.equal(down.score, 14)
})

test('a blocked move does not spawn tiles or change the score', () => {
  const state = stateWithRow([2, 4, 8, 16])
  assert.equal(moveTiles(state, 'left', () => { throw new Error('Should not spawn') }), state)
})

test('a full board ends only when no horizontal or vertical merge remains', () => {
  const board = [2, 4, 2, 4, 4, 2, 4, 2, 2, 4, 2, 4, 4, 2, 4, 2]
  assert.equal(canMove(board), false)
  assert.equal(canMove(board.with(0, 0)), true)
  assert.equal(canMove(board.with(0, 4)), true)
  assert.equal(canMove(board.with(4, 2)), true)
  assert.equal(canMove([2, 4, 8, 16, 16, 8, 4, 2, 2, 4, 8, 16, 16, 8, 4, 2]), false)
})

test('reaching 2048 awards points and the board can continue playing', () => {
  const game = moveTiles(stateWithRow([1024, 1024, 0, 0]), 'left', random)
  assert.equal(game.board[0], 2048)
  assert.equal(game.score, 2048)
  assert.equal(canMove(game.board), true)
})
