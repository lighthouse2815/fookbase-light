export type GroupHeaderViewerRole = 'owner' | 'admin' | 'moderator' | 'member' | null

export function getGroupHeaderCapabilities(viewerRole: GroupHeaderViewerRole) {
  return {
    canInvite: viewerRole !== null,
    canManageSettings: viewerRole === 'owner' || viewerRole === 'admin',
    canModerate: viewerRole === 'owner' || viewerRole === 'admin' || viewerRole === 'moderator',
  }
}
