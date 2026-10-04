import assert from 'node:assert/strict'
import test from 'node:test'
import { adjacentMines, createMinesweeper, revealMineCell, toggleFlag } from '../src/pages/games/minesweeperEngine.ts'

test('every opening has ten mines and a safe empty neighborhood', () => {
  for (let index = 0; index < 64; index++) {
    const initial = createMinesweeper()
    const game = revealMineCell(initial, index, () => 0.37)
    assert.equal(game.mines.filter(Boolean).length, 10)
    assert.equal(game.mines[index], false)
    assert.equal(adjacentMines(game.mines, index), 0)
    assert.equal(game.revealed[index], true)
    assert.equal(game.revealed.some((open, i) => open && game.mines[i]), false)
    assert.equal(initial.mines, null)
  }
})

test('flags block reveals, can be removed and cannot exceed the mine count', () => {
  let game = createMinesweeper()
  for (let i = 0; i < 10; i++) game = toggleFlag(game, i)
  assert.equal(toggleFlag(game, 10), game)
  assert.equal(revealMineCell(game, 0), game)
  game = toggleFlag(game, 0)
  assert.equal(revealMineCell(game, 0).revealed[0], true)
})

test('opening a mine loses; opening every safe cell wins and freezes input', () => {
  const initial = revealMineCell(createMinesweeper(), 27, () => 0.5)
  const mine = initial.mines.findIndex(Boolean)
  const lost = revealMineCell(initial, mine)
  assert.equal(lost.phase, 'lost')
  assert.equal(lost.detonated, mine)
  assert.equal(toggleFlag(lost, 1), lost)
  let won = initial
  initial.mines.forEach((isMine, index) => { if (!isMine) won = revealMineCell(won, index) })
  assert.equal(won.phase, 'won')
  assert.equal(won.revealed.filter(Boolean).length, 54)
  assert.equal(revealMineCell(won, mine), won)
})

test('neighbor counts do not wrap across row edges', () => {
  const mines = Array(64).fill(false)
  mines[7] = true
  assert.equal(adjacentMines(mines, 8), 0)
  assert.equal(adjacentMines(mines, 6), 1)
  assert.equal(adjacentMines(mines, 15), 1)
})
