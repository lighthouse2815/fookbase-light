import assert from 'node:assert/strict'
import test from 'node:test'
import { getSearchDestination, flattenSearchSuggestions, getNextSearchIndex, getHighlightParts } from '../src/pages/search/searchPresentation.ts'

test('every search result uses its existing entity route, and Posts always use Post detail', () => {
  const cases = [['people', 'user-1', '/profile/user-1'], ['groups', 'group-1', '/groups/group-1'], ['pages', 'page.name', '/pages/page.name'], ['posts', 'post-1', '/posts/post-1'], ['reels', 'reel-1', '/reels?reel=reel-1'], ['events', 'event-1', '/events/event-1']]
  for (const [type, id, destination] of cases) assert.equal(getSearchDestination(type, id), destination)
  assert.equal(getSearchDestination('pages', 'a/b'), '/pages/a%2Fb')
})

test('autocomplete deduplicates by type and ID without merging entities with equal names', () => {
  const people = [{ userId: 'one', displayName: 'Same', username: 'same', avatarUrl: null }, { userId: 'one', displayName: 'Duplicate', username: 'duplicate' }, { userId: 'two', displayName: 'Same', username: 'same', avatarUrl: null }]
  const rows = flattenSearchSuggestions({ people, groups: [{ groupId: 'one', name: 'Same', coverUrl: null }], pages: [{ pageId: 'one', name: 'Same', username: 'same.page', avatarUrl: null }] })
  assert.deepEqual(rows.map((row) => row.key), ['people:one', 'people:two', 'groups:one', 'pages:one'])
  assert.deepEqual(rows.map((row) => row.destination), ['/profile/one', '/profile/two', '/groups/one', '/pages/same.page'])
})

test('suggestion navigation starts at the first/last option and stays bounded', () => {
  assert.equal(getNextSearchIndex(-1, 3, 1), 0)
  assert.equal(getNextSearchIndex(-1, 3, -1), 2)
  assert.equal(getNextSearchIndex(0, 3, 1), 1)
  assert.equal(getNextSearchIndex(1, 3, -1), 0)
  assert.equal(getNextSearchIndex(2, 3, 1), 2)
  assert.equal(getNextSearchIndex(0, 3, -1), 0)
  assert.equal(getNextSearchIndex(1, 0, 1), -1)
})

test('highlight matches Vietnamese diacritics and case while retaining original text', () => {
  assert.deepEqual(getHighlightParts('Trần Đăng Khoa', ' dang '), [{ text: 'Trần ', match: false }, { text: 'Đăng', match: true }, { text: ' Khoa', match: false }])
  assert.deepEqual(getHighlightParts('ĐĂNG và đăng', 'Đăng'), [{ text: 'ĐĂNG', match: true }, { text: ' và ', match: false }, { text: 'đăng', match: true }])
})

test('highlight preserves complete combining graphemes and emoji boundaries', () => {
  const text = '🙂 Đa\u0306ng 🙂'
  const parts = getHighlightParts(text, 'dang')
  assert.equal(parts.map((part) => part.text).join(''), text)
  assert.deepEqual(parts.filter((part) => part.match), [{ text: 'Đa\u0306ng', match: true }])
  assert.deepEqual(getHighlightParts('🙂hello🙂', '🙂'), [{ text: '🙂', match: true }, { text: 'hello', match: false }, { text: '🙂', match: true }])
})

test('highlight treats query as literal substring and returns safe plain text parts', () => {
  const text = '<img src=x onerror=alert(1)> a+b [x]'
  const parts = getHighlightParts(text, 'a+b')
  assert.deepEqual(parts.filter((part) => part.match), [{ text: 'a+b', match: true }])
  assert.equal(parts.map((part) => part.text).join(''), text)
  assert.deepEqual(getHighlightParts('abc', ''), [{ text: 'abc', match: false }])
  assert.deepEqual(getHighlightParts('abc', 'xy'), [{ text: 'abc', match: false }])
})
