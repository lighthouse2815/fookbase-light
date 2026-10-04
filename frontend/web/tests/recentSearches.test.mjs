import assert from 'node:assert/strict'
import test from 'node:test'
import { addRecentSearch, normalizeRecentSearches, readRecentSearches, writeRecentSearches } from '../src/pages/search/recentSearches.ts'

test('recent searches trim input, deduplicate case-insensitively and promote a repeated query', () => {
  assert.deepEqual(addRecentSearch(['react', 'Fookbase', 'dang'], ' fookBASE '), ['fookBASE', 'react', 'dang'])
  assert.deepEqual(addRecentSearch(['react'], '  '), ['react'])
})

test('recent history caps ten real queries and drops invalid stored values', () => {
  assert.deepEqual(addRecentSearch(Array.from({ length: 10 }, (_, index) => `q${index}`), 'new'), ['new', 'q0', 'q1', 'q2', 'q3', 'q4', 'q5', 'q6', 'q7', 'q8'])
  assert.deepEqual(normalizeRecentSearches(['react', null, {}, ' REACT ', '', 'a'.repeat(101), 'Đăng']), ['react', 'Đăng'])
  assert.deepEqual(normalizeRecentSearches({ queries: ['react'] }), [])
})

test('history reads malformed JSON and unavailable storage without crashing', () => {
  const original = Object.getOwnPropertyDescriptor(globalThis, 'localStorage')
  try {
    Object.defineProperty(globalThis, 'localStorage', { configurable: true, value: { getItem: () => '{broken' } })
    assert.deepEqual(readRecentSearches('viewer'), [])
    Object.defineProperty(globalThis, 'localStorage', { configurable: true, get: () => { throw new Error('Storage unavailable') } })
    assert.deepEqual(readRecentSearches('viewer'), [])
    assert.doesNotThrow(() => writeRecentSearches('viewer', ['react']))
  } finally {
    if (original) Object.defineProperty(globalThis, 'localStorage', original)
    else delete globalThis.localStorage
  }
})

test('history persists queries per viewer and removing/clearing does not affect another account', () => {
  const values = new Map()
  const original = Object.getOwnPropertyDescriptor(globalThis, 'localStorage')
  try {
    Object.defineProperty(globalThis, 'localStorage', { configurable: true, value: { getItem: (key) => values.get(key) ?? null, setItem: (key, value) => values.set(key, value), removeItem: (key) => values.delete(key) } })
    writeRecentSearches('viewer-a', ['react', 'dang'])
    writeRecentSearches('viewer-b', ['fookbase'])
    assert.deepEqual(readRecentSearches('viewer-a'), ['react', 'dang'])
    writeRecentSearches('viewer-a', ['react'])
    assert.deepEqual(readRecentSearches('viewer-a'), ['react'])
    writeRecentSearches('viewer-a', [])
    assert.deepEqual(readRecentSearches('viewer-a'), [])
    assert.deepEqual(readRecentSearches('viewer-b'), ['fookbase'])
    assert.equal(values.size, 1)
  } finally {
    if (original) Object.defineProperty(globalThis, 'localStorage', original)
    else delete globalThis.localStorage
  }
})

test('quota errors do not interrupt the explicit search action', () => {
  const original = Object.getOwnPropertyDescriptor(globalThis, 'localStorage')
  try {
    Object.defineProperty(globalThis, 'localStorage', { configurable: true, value: { setItem: () => { throw new Error('Quota exceeded') }, removeItem: () => { throw new Error('Storage unavailable') } } })
    assert.doesNotThrow(() => writeRecentSearches('viewer', ['fookbase']))
    assert.doesNotThrow(() => writeRecentSearches('viewer', []))
  } finally {
    if (original) Object.defineProperty(globalThis, 'localStorage', original)
    else delete globalThis.localStorage
  }
})
