import assert = require('node:assert/strict')
import test = require('node:test')
import { getGroupHeaderCapabilities } from '../src/pages/groups/groupHeaderCapabilities'

test('group header actions match each membership role', () => {
  assert.deepEqual(getGroupHeaderCapabilities(null), {
    canInvite: false,
    canManageSettings: false,
    canModerate: false,
  })
  assert.deepEqual(getGroupHeaderCapabilities('member'), {
    canInvite: true,
    canManageSettings: false,
    canModerate: false,
  })
  assert.deepEqual(getGroupHeaderCapabilities('moderator'), {
    canInvite: true,
    canManageSettings: false,
    canModerate: true,
  })
  assert.deepEqual(getGroupHeaderCapabilities('admin'), {
    canInvite: true,
    canManageSettings: true,
    canModerate: true,
  })
  assert.deepEqual(getGroupHeaderCapabilities('owner'), {
    canInvite: true,
    canManageSettings: true,
    canModerate: true,
  })
})
