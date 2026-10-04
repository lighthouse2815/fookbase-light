import { actorId, notification, notificationFixtures, viewerId } from './notificationsFixture.mjs'

export const groupId = '00000000-0000-0000-0000-000000000801'
export const groupInviteId = '00000000-0000-0000-0000-000000000811'
export const pageId = '00000000-0000-0000-0000-000000000901'
export const pageInviteId = '00000000-0000-0000-0000-000000000911'
export const pageUsername = 'trang-moi-doi-ten'

export async function invitationNotificationFixture(context, kind, options = {}) {
  const isGroup = kind === 'group'
  const resourceId = isGroup ? groupId : pageId
  const inviteId = isGroup ? groupInviteId : pageInviteId
  await notificationFixtures(context, { items: [notification(1, {
    type: isGroup ? 'GroupInvite' : 'PageRoleInvite', entityType: isGroup ? 'GroupInvite' : 'PageRoleInvitation',
    entityId: inviteId, parentEntityId: resourceId, pageUsername: isGroup ? null : 'ten-trang-cu',
  })] })
  const group = { id: groupId, name: 'Nhóm riêng tư được mời', description: null, privacy: 'private', ownerUserId: actorId, coverUrl: null, memberCount: 4, viewerRole: 'member', createdAtUtc: new Date().toISOString(), updatedAtUtc: null }
  const page = { id: pageId, name: 'Trang chưa xuất bản được mời', username: pageUsername, category: 'Community', bio: null, status: 'unpublished', avatarUrl: null, coverUrl: null, followerCount: 4, isFollowing: false, viewerRole: 'editor', createdAtUtc: new Date().toISOString(), updatedAtUtc: null }
  const invitation = {
    id: inviteId, [isGroup ? 'groupId' : 'pageId']: resourceId,
    inviterUserId: actorId, inviteeUserId: viewerId, role: 'editor', status: 'pending',
    createdAtUtc: new Date().toISOString(), respondedAtUtc: null, ...(isGroup ? { group } : {}),
  }
  const other = { ...invitation, id: '00000000-0000-0000-0000-000000000999' }
  const state = {
    kind, resourceId, inviteId, accepted: options.accepted ?? false, declined: false, missing: options.missing ?? false,
    detailStatus: options.detailStatus ?? 404, actionDelay: 450, failAction: false,
    invitationReads: [], detailReads: [], writes: [], responses: 0,
  }
  const empty = { items: [], nextCursor: null }
  await context.route('**/api/**', async (route) => {
    const request = route.request()
    const url = new URL(request.url())
    const path = url.pathname
    const invitationsPath = isGroup ? '/api/groups/invites/mine' : '/api/pages/invitations/mine'
    if (path === invitationsPath) {
      const cursor = url.searchParams.get('cursor')
      state.invitationReads.push(cursor)
      if (!cursor) return route.fulfill({ json: { items: [other], nextCursor: 'invitation-second-page' } })
      return route.fulfill({ json: { items: state.missing || state.accepted || state.declined ? [] : [invitation], nextCursor: null } })
    }
    const responsePrefix = isGroup ? `/api/groups/${groupId}/invites/${groupInviteId}/` : `/api/pages/invitations/${pageInviteId}/`
    if (path.startsWith(responsePrefix)) {
      const action = path.endsWith('/accept') ? 'accept' : 'decline'
      const fail = state.failAction
      state.writes.push({ action, path })
      await new Promise((resolve) => setTimeout(resolve, state.actionDelay))
      state.responses++
      if (fail) return route.fulfill({ status: 503, json: { detail: 'Không thể xử lý lời mời thử nghiệm.' } })
      state.accepted = action === 'accept'
      state.declined = action === 'decline'
      return route.fulfill({ json: { ...invitation, status: state.accepted ? 'accepted' : 'declined', respondedAtUtc: new Date().toISOString() } })
    }
    const detailPaths = isGroup ? [`/api/groups/${groupId}`] : [`/api/pages/${pageId}`, `/api/pages/${pageUsername}`, '/api/pages/ten-trang-cu']
    if (detailPaths.includes(path)) {
      state.detailReads.push({ path, accepted: state.accepted, invitationReads: [...state.invitationReads] })
      const status = path.endsWith('/ten-trang-cu') ? 404 : state.accepted ? 200 : state.detailStatus
      return route.fulfill(status === 200 ? { json: isGroup ? group : page } : { status, json: { detail: 'Nội dung này không còn khả dụng.' } })
    }
    if (path === `/api/groups/${groupId}/rules`) return route.fulfill({ json: [] })
    if (path.startsWith('/api/groups/') || path.startsWith('/api/pages/')) return route.fulfill({ json: empty })
    return route.fallback()
  })
  return state
}
