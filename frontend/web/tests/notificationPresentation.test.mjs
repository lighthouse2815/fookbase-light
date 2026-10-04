import assert from 'node:assert/strict'
import { test } from 'node:test'
import { getNotificationPresentation } from '../src/shared/notificationPresentation.ts'

function notification(overrides = {}) {
  return {
    id: 'notification-id', recipientUserId: 'recipient-id', actorUserId: 'actor-id',
    actorUsername: 'minh', actorDisplayName: 'Minh', type: 'PostReaction', entityType: 'Post',
    entityId: 'post-id', parentEntityId: null, isRead: false, createdAtUtc: '2026-10-04T08:00:00Z',
    readAtUtc: null, ...overrides,
  }
}

test('all current notification types use existing destinations', () => {
  const cases = [
    [{ type: 'FriendRequestReceived' }, '/profile/actor-id'],
    [{ type: 'FriendRequestAccepted' }, '/profile/actor-id'],
    [{ type: 'UserFollowed' }, '/profile/actor-id'],
    [{ type: 'PostReaction' }, '/posts/post-id'],
    [{ type: 'PostComment' }, '/posts/post-id'],
    [{ type: 'PostShared' }, '/posts/post-id'],
    [{ type: 'PostMention' }, '/posts/post-id'],
    [{ type: 'CommentReaction', entityType: 'Comment', entityId: 'comment-id', parentEntityId: 'parent-post' }, '/posts/parent-post#comment-comment-id'],
    [{ type: 'CommentMention', entityType: 'Comment', entityId: 'comment-id', parentEntityId: 'parent-post' }, '/posts/parent-post#comment-comment-id'],
    [{ type: 'GroupInvite', entityType: 'GroupInvite', entityId: 'invite-id' }, '/groups'],
    [{ type: 'GroupJoinApproved', entityType: 'GroupJoinRequest', entityId: 'request-id' }, '/groups'],
    [{ type: 'PageRoleInvite', entityType: 'PageRoleInvitation', entityId: 'invite-id' }, '/pages'],
    [{ type: 'StoryReaction', entityType: 'Story' }, '/stories/post-id'],
    [{ type: 'EventInvite', entityType: 'Event', entityId: 'invite-id', parentEntityId: 'event-id' }, '/events/event-id'],
    [{ type: 'EventUpdated', entityType: 'Event', entityId: 'event-id' }, '/events/event-id'],
    [{ type: 'EventCancelled', entityType: 'Event', entityId: 'event-id' }, '/events/event-id'],
    [{ type: 'AccountWarning', entityType: null, entityId: null }, '/settings/security'],
  ]
  for (const [input, destination] of cases) {
    assert.equal(getNotificationPresentation(notification(input)).destination, destination, input.type)
  }
})

test('comments and event invitations never use their own IDs as parent IDs', () => {
  assert.equal(getNotificationPresentation(notification({
    type: 'PostComment', entityType: 'Comment', entityId: 'comment-id', parentEntityId: 'post-id',
  })).destination, '/posts/post-id#comment-comment-id')
  assert.equal(getNotificationPresentation(notification({
    type: 'CommentReaction', entityType: 'Comment', entityId: 'comment-id',
  })).destination, '/notifications')
  assert.equal(getNotificationPresentation(notification({
    type: 'EventInvite', entityType: 'Event', entityId: 'invite-id',
  })).destination, '/events')
  assert.equal(getNotificationPresentation(notification({
    type: 'EventInvite', parentEntityId: '00000000-0000-0000-0000-000000000000',
  })).destination, '/events')
  assert.equal(getNotificationPresentation(notification({
    type: 'CommentMention', entityId: null, parentEntityId: 'post-id',
  })).destination, '/posts/post-id')
})

test('group and page invitations select their existing acceptance flow before opening private content', () => {
  assert.equal(getNotificationPresentation(notification({
    type: 'GroupInvite', entityType: 'Group', entityId: 'group-id',
  })).destination, '/groups/group-id')
  assert.equal(getNotificationPresentation(notification({
    type: 'GroupInvite', entityType: 'GroupInvite', entityId: 'invite-id', parentEntityId: 'group-id',
  })).destination, '/groups?invite=invite-id&group=group-id')
  assert.equal(getNotificationPresentation(notification({
    type: 'GroupJoinApproved', entityType: 'GroupJoinRequest', entityId: 'request-id', parentEntityId: 'group-id',
  })).destination, '/groups/group-id')
  assert.equal(getNotificationPresentation(notification({
    type: 'PageRoleInvite', entityType: 'PageRoleInvitation', entityId: 'invite-id',
    parentEntityId: 'page-id', pageUsername: 'trang của minh',
  })).destination, '/pages?invite=invite-id&pageId=page-id&page=trang%20c%E1%BB%A7a%20minh')
  assert.equal(getNotificationPresentation(notification({
    type: 'PageRoleInvite', entityType: 'PageRoleInvitation', entityId: 'invite-id',
    parentEntityId: 'page-id', pageUsername: null,
  })).destination, '/pages?invite=invite-id&pageId=page-id')
})

test('old or incomplete DTOs keep safe fallbacks and Story IDs open the existing viewer route', () => {
  for (const [type, entityType, destination] of [
    ['GroupInvite', 'GroupInvite', '/groups'],
    ['GroupJoinApproved', 'GroupJoinRequest', '/groups'],
    ['PageRoleInvite', 'PageRoleInvitation', '/pages'],
  ]) {
    assert.equal(getNotificationPresentation(notification({ type, entityType, parentEntityId: null })).destination, destination)
  }
  assert.equal(getNotificationPresentation(notification({ type: 'StoryReaction', entityType: 'Story', entityId: 'story-id' })).destination, '/stories/story-id')
  assert.equal(getNotificationPresentation(notification({ type: 'StoryReaction', entityType: 'Story', entityId: null })).destination, '/feed')
})

test('system notifications never expose a moderator or fake actor', () => {
  for (const type of ['AccountWarning', 'EventUpdated', 'EventCancelled']) {
    const presentation = getNotificationPresentation(notification({ type }))
    assert.equal(presentation.actor, null)
    assert.equal(presentation.isSystem, true)
    assert.doesNotMatch(presentation.text, /Minh|minh|Người dùng/)
    assert.equal(presentation.icon, type === 'AccountWarning' ? 'shield' : 'calendar')
  }
})

test('missing actor data uses a complete sentence and is not surfaced as a realtime toast', () => {
  for (const overrides of [
    { actorUserId: null },
    { actorDisplayName: null, actorUsername: null },
    { actorDisplayName: ' ', actorUsername: '' },
    { actorDisplayName: 'efcb6a5c-3d4f-4a4f-8fe6-6b3372db231c', actorUsername: null },
  ]) {
    const presentation = getNotificationPresentation(notification(overrides))
    assert.equal(presentation.actor, null)
    assert.equal(presentation.isSystem, true)
    assert.equal(presentation.canToast, false)
    assert.equal(presentation.text, 'Bài viết của bạn có cảm xúc mới.')
  }
})

test('real actors use display names or usernames without extra profile fetches', () => {
  assert.equal(getNotificationPresentation(notification()).text, 'Minh đã bày tỏ cảm xúc về bài viết của bạn.')
  assert.equal(getNotificationPresentation(notification({ actorDisplayName: ' ', actorUsername: ' minh ' })).actor, 'minh')
  assert.equal(getNotificationPresentation(notification()).canToast, true)
  assert.equal(getNotificationPresentation(notification({
    type: 'AccountWarning', actorUserId: null, actorDisplayName: null, actorUsername: null,
  })).canToast, true)
})
