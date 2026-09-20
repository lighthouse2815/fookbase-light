import assert from 'node:assert/strict'
import { test } from 'node:test'
import { conversationTime, messageClock, messageDay, messageFullTime, sameMessageDay } from '../src/messageTime.ts'

test('groups UTC timestamps by the viewer local calendar day', () => {
  assert.equal(sameMessageDay('2026-09-20T16:59:00Z', '2026-09-20T17:01:00Z'), false)
  assert.equal(sameMessageDay('2026-09-20T17:01:00Z', '2026-09-21T10:00:00Z'), true)
  assert.equal(messageClock('2026-09-20T17:01:00Z'), '00:01')
})

test('labels today and yesterday across year boundaries', () => {
  const now = new Date('2027-01-01T08:00:00+07:00')
  assert.equal(messageDay('2027-01-01T01:00:00+07:00', now), 'Hôm nay')
  assert.equal(messageDay('2026-12-31T23:59:00+07:00', now), 'Hôm qua')
  assert.equal(conversationTime('2027-01-01T01:00:00+07:00', now), '01:00')
  assert.equal(conversationTime('2026-12-31T23:59:00+07:00', now), 'Hôm qua')
  assert.equal(conversationTime('2026-12-30T12:00:00+07:00', now), '30/12/2026')
})

test('older timestamps include enough date context to distinguish years', () => {
  const now = new Date('2026-09-21T08:00:00+07:00')
  assert.match(conversationTime('2026-09-18T12:00:00+07:00', now), /^18[/-]09$/)
  assert.equal(conversationTime('2025-09-18T12:00:00+07:00', now), '18/09/2025')
  assert.match(messageFullTime('2025-09-18T12:00:00+07:00'), /12:00.*18\/09\/2025/)
})
