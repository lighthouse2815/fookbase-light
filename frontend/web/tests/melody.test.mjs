import test from 'node:test'
import assert from 'node:assert/strict'
import { answerMelody, createMelody, finishMelodyPlayback, MELODY_ROUNDS, nextMelody } from '../src/pages/games/melodyEngine.ts'

test('playback blocks input and each completed round preserves the previous melody', () => {
  let state = nextMelody(createMelody(), () => 0.3)
  assert.deepEqual(state.sequence, [1])
  assert.equal(answerMelody(state, 1), state)
  state = answerMelody(finishMelodyPlayback(state), 1)
  assert.equal(state.completed, 1)
  assert.equal(state.phase, 'between')
  state = nextMelody(state, () => 0.8)
  assert.deepEqual(state.sequence, [1, 3])
  state = answerMelody(finishMelodyPlayback(state), 1)
  assert.equal(state.phase, 'input')
  assert.equal(state.cursor, 1)
  assert.equal(answerMelody(state, 3).phase, 'between')
})

test('a wrong note ends the round without awarding unearned progress', () => {
  const state = finishMelodyPlayback(nextMelody(createMelody(), () => 0))
  const failed = answerMelody(state, 2)
  assert.equal(failed.phase, 'over')
  assert.equal(failed.completed, 0)
  assert.equal(answerMelody(failed, 0), failed)
  assert.equal(nextMelody(failed), failed)
  for (const invalid of [-1, 4, 1.5, NaN]) assert.equal(answerMelody(state, invalid), state)
})

test('all twelve rounds can be won, and a new game clears the sequence', () => {
  let state = createMelody()
  for (let round = 1; round <= MELODY_ROUNDS; round++) {
    state = finishMelodyPlayback(nextMelody(state, () => (round % 4) / 4))
    for (const note of state.sequence) state = answerMelody(state, note)
    assert.equal(state.completed, round)
  }
  assert.equal(state.phase, 'won')
  assert.equal(nextMelody(state), state)
  assert.equal(finishMelodyPlayback(state), state)
  assert.deepEqual(createMelody().sequence, [])
})
