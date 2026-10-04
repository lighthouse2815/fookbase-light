import assert from 'node:assert/strict'
import test from 'node:test'
import { postDraftKey, readPostDraft, savePostDraft } from '../src/pages/feed/components/postDraft.ts'

function storage() {
  const values = new Map()
  globalThis.sessionStorage = {
    getItem: (key) => values.get(key) ?? null,
    setItem: (key, value) => values.set(key, value),
    removeItem: (key) => values.delete(key),
  }
  return values
}

test('drafts restore text, audience and background only in the same account and posting context', () => {
  storage()
  const key = postDraftKey('user-1', '/groups/group-1')
  const draft = { content: 'Bản nháp riêng', privacy: 'friends', textBackground: 'blue', hasAttachments: true }
  savePostDraft(key, draft)
  assert.deepEqual(readPostDraft(key), draft)
  assert.equal(readPostDraft(postDraftKey('user-2', '/groups/group-1')), null)
  assert.equal(readPostDraft(postDraftKey('user-1', '/feed')), null)
  assert.equal(readPostDraft(postDraftKey('user-1', '/groups/group-2')), null)
  assert.notEqual(postDraftKey('a.b', '/feed'), postDraftKey('a', 'b./feed'))
})

test('attachment-only drafts survive and a completed or discarded draft is removed', () => {
  const values = storage()
  const key = postDraftKey('user', '/feed')
  const empty = { content: '', privacy: 'public', textBackground: null, hasAttachments: false }
  savePostDraft(key, { ...empty, hasAttachments: true })
  assert.equal(readPostDraft(key).hasAttachments, true)
  savePostDraft(key, empty)
  assert.equal(values.has(key), false)
})

test('corrupt storage and unsupported privacy cannot become publishable drafts', () => {
  const values = storage()
  const key = postDraftKey('user', '/feed')
  for (const invalid of ['{', 'null', '[]', '{"content":"private","privacy":"invalid"}', '{"privacy":"public"}']) {
    values.set(key, invalid)
    assert.equal(readPostDraft(key), null)
  }
  values.set(key, JSON.stringify({ content: 'text', privacy: 'onlyMe', textBackground: {}, hasAttachments: 'yes' }))
  assert.deepEqual(readPostDraft(key), { content: 'text', privacy: 'onlyMe', textBackground: null, hasAttachments: false })
})

test('disabled browser storage does not break composing', () => {
  globalThis.sessionStorage = {
    getItem: () => { throw new Error('disabled') },
    setItem: () => { throw new Error('disabled') },
    removeItem: () => { throw new Error('disabled') },
  }
  assert.equal(readPostDraft('key'), null)
  assert.doesNotThrow(() => savePostDraft('key', { content: 'text', privacy: 'public', textBackground: null, hasAttachments: false }))
})
