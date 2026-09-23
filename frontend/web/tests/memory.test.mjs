import assert from 'node:assert/strict'
import test from 'node:test'
import { createMemoryGame, flipCard } from '../src/pages/games/memoryEngine.ts'

test('a fresh deck has exactly eight pairs and clears the previous round', () => {
  const game = createMemoryGame()
  assert.deepEqual([...game.cards].sort((a, b) => a - b), Array.from({ length: 16 }, (_, i) => Math.floor(i / 2)))
  assert.equal(game.moves, 0)
  assert.deepEqual(game.opened, [])
  assert.deepEqual(game.matched, [])
})

test('a mismatch counts one turn and blocks a third card until hidden', () => {
  const initial = { cards: [0, 1, 0, 1], opened: [], matched: [], moves: 0 }
  const first = flipCard(initial, 0)
  assert.equal(first.moves, 0)
  assert.equal(flipCard(first, 0), first)
  const second = flipCard(first, 1)
  assert.equal(second.moves, 1)
  assert.deepEqual(second.opened, [0, 1])
  assert.equal(flipCard(second, 2), second)
  assert.deepEqual(initial.opened, [])
})

test('matched cards stay revealed and every pair can be completed once', () => {
  let game = { cards: [0, 1, 0, 1], opened: [], matched: [], moves: 0 }
  game = flipCard(flipCard(game, 0), 2)
  assert.deepEqual(game.matched, [0, 2])
  assert.deepEqual(game.opened, [])
  assert.equal(flipCard(game, 0), game)
  game = flipCard(flipCard(game, 1), 3)
  assert.equal(game.matched.length, 4)
  assert.equal(game.moves, 2)
})
