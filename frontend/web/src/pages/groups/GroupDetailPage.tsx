import { useCallback, useEffect, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { ApiError } from '../../api/client'
import { groupsApi } from '../../api/groups'
import type { Group, GroupJoinRequest, GroupMember, GroupRule } from '../../api/groups'
import { mediaApi } from '../../api/media'
import type { Post } from '../../api/posts'
import type { UserProfile } from '../../api/users'
import { resolveProfileImageUrl } from '../../api/users'
import { useAuth } from '../../auth/useAuth'
import PaginationControls from '../../shared/components/PaginationControls'
import LivePostCard from '../feed/components/LivePostCard'
import NewPostBox from '../feed/components/NewPostBox'

const managerRoles = new Set(['owner', 'admin', 'moderator'])
const rulesManagerRoles = new Set(['owner', 'admin'])

function toAuthor(post: Post): UserProfile {
  const authorUserId = post.authorUserId!
  const suffix = authorUserId.slice(0, 8)
  return {
    userId: authorUserId,
    username: 'member_' + suffix,
    displayName: 'Group member ' + suffix,
    avatarUrl: null,
    bio: null,
    coverUrl: null,
    dateOfBirth: null,
    currentCity: null,
    createdAt: post.createdAtUtc,
    updatedAt: post.updatedAtUtc ?? post.createdAtUtc,
  }
}

export default function GroupDetailPage() {
  const { groupId } = useParams()
  const { session } = useAuth()
  const navigate = useNavigate()
  const [group, setGroup] = useState<Group | null>(null)
  const [members, setMembers] = useState<GroupMember[]>([])
  const [rules, setRules] = useState<GroupRule[]>([])
  const [posts, setPosts] = useState<Post[]>([])
  const [postsCursor, setPostsCursor] = useState<string | null>(null)
  const [requests, setRequests] = useState<GroupJoinRequest[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [isJoining, setIsJoining] = useState(false)
  const [requested, setRequested] = useState(false)
  const [newRuleTitle, setNewRuleTitle] = useState('')
  const [newRuleDescription, setNewRuleDescription] = useState('')
  const [inviteeId, setInviteeId] = useState('')
  const [isSavingDetails, setIsSavingDetails] = useState(false)

  const load = useCallback(async () => {
    if (!groupId) return
    setIsLoading(true)
    setError(null)
    try {
      const [nextGroup, memberPage, nextRules, postPage] = await Promise.all([
        groupsApi.get(groupId),
        groupsApi.getMembers(groupId),
        groupsApi.getRules(groupId),
        groupsApi.getPosts(groupId),
      ])
      setGroup(nextGroup)
      setMembers(memberPage.items)
      setRules(nextRules)
      setPosts(postPage.items)
      setPostsCursor(postPage.nextCursor)
      if (nextGroup.viewerRole && managerRoles.has(nextGroup.viewerRole)) {
        const requestPage = await groupsApi.getJoinRequests(groupId)
        setRequests(requestPage.items)
      } else {
        setRequests([])
      }
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : 'Unable to load this group.')
      setGroup(null)
    } finally {
      setIsLoading(false)
    }
  }, [groupId])

  useEffect(() => {
    const timeoutId = window.setTimeout(() => { void load() }, 0)
    return () => window.clearTimeout(timeoutId)
  }, [load])

  if (!groupId) return null
  const isMember = group?.viewerRole !== null && group?.viewerRole !== undefined
  const canManage = group?.viewerRole !== null && group?.viewerRole !== undefined && managerRoles.has(group.viewerRole)
  const canManageRules = group?.viewerRole !== null && group?.viewerRole !== undefined && rulesManagerRoles.has(group.viewerRole)
  const isOwner = group?.viewerRole === 'owner'

  const joinOrLeave = async () => {
    if (!group) return
    setIsJoining(true)
    setError(null)
    try {
      if (isMember) {
        await groupsApi.leave(group.id)
        await load()
      } else {
        const request = await groupsApi.join(group.id)
        if (request) setRequested(true)
        else await load()
      }
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : 'Unable to change membership.')
    } finally {
      setIsJoining(false)
    }
  }

  const createPost = async (content: string, files: readonly File[], onUploadProgress: (progress: number) => void) => {
    const mediaIds = await mediaApi.uploadFiles(files, onUploadProgress)
    const post = await groupsApi.createPost(groupId, content, mediaIds)
    setPosts((current) => [post, ...current])
  }

  const loadMorePosts = async () => {
    if (!postsCursor) return
    try {
      const page = await groupsApi.getPosts(groupId, postsCursor)
      setPosts((current) => [...current, ...page.items.filter((post) => !current.some((item) => item.id === post.id))])
      setPostsCursor(page.nextCursor)
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : 'Unable to load more posts.')
    }
  }

  const respondToRequest = async (requestId: string, approve: boolean) => {
    try {
      if (approve) await groupsApi.approveJoinRequest(groupId, requestId)
      else await groupsApi.declineJoinRequest(groupId, requestId)
      setRequests((current) => current.filter((request) => request.id !== requestId))
      await load()
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : 'Unable to update the join request.')
    }
  }

  const createRule = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (!newRuleTitle.trim()) return
    try {
      const rule = await groupsApi.createRule(groupId, newRuleTitle, newRuleDescription, rules.length)
      setRules((current) => [...current, rule].sort((first, second) => first.sortOrder - second.sortOrder))
      setNewRuleTitle('')
      setNewRuleDescription('')
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : 'Unable to create the rule.')
    }
  }

  const removeRule = async (ruleId: string) => {
    try {
      await groupsApi.deleteRule(groupId, ruleId)
      setRules((current) => current.filter((rule) => rule.id !== ruleId))
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : 'Unable to delete the rule.')
    }
  }

  const changeRole = async (userId: string, role: GroupMember['role']) => {
    try {
      const changed = await groupsApi.changeMemberRole(groupId, userId, role)
      setMembers((current) => current.map((member) => member.userId === userId ? changed : member))
      await load()
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : 'Unable to change this role.')
    }
  }

  const removeMember = async (userId: string) => {
    try {
      await groupsApi.removeMember(groupId, userId)
      setMembers((current) => current.filter((member) => member.userId !== userId))
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : 'Unable to remove this member.')
    }
  }

  const invite = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (!inviteeId.trim()) return
    try {
      await groupsApi.invite(groupId, inviteeId.trim())
      setInviteeId('')
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : 'Unable to send the invite.')
    }
  }

  const saveDetails = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (!group) return
    setIsSavingDetails(true)
    try {
      const form = new FormData(event.currentTarget)
      const updated = await groupsApi.update(group.id, {
        name: String(form.get('name') ?? ''),
        description: String(form.get('description') ?? ''),
        privacy: String(form.get('privacy')) as 'public' | 'private',
      })
      setGroup(updated)
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : 'Unable to update group details.')
    } finally {
      setIsSavingDetails(false)
    }
  }

  const changeCover = async (file: File | undefined) => {
    if (!file || !group) return
    try {
      const coverMediaId = await mediaApi.uploadFile(file)
      const updated = await groupsApi.update(group.id, {
        name: group.name,
        description: group.description ?? '',
        privacy: group.privacy,
        coverMediaId,
      })
      setGroup(updated)
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : 'Unable to update the group cover.')
    }
  }

  const removeCover = async () => {
    if (!group) return
    try {
      const updated = await groupsApi.update(group.id, {
        name: group.name,
        description: group.description ?? '',
        privacy: group.privacy,
        removeCover: true,
      })
      setGroup(updated)
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : 'Unable to remove the group cover.')
    }
  }

  const removeGroupPost = async (postId: string) => {
    try {
      await groupsApi.removePost(groupId, postId)
      setPosts((current) => current.filter((post) => post.id !== postId))
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : 'Unable to remove the group post.')
    }
  }

  if (isLoading) return <main className="mx-auto w-full max-w-4xl px-3 py-5"><div className="h-60 animate-pulse rounded-xl bg-surface-2" /></main>
  if (!group) return <main className="mx-auto w-full max-w-3xl px-3 py-10"><Link to="/groups" className="text-primary">← Back to groups</Link><p className="mt-4 rounded-xl border border-border bg-surface p-5 text-text-muted">{error ?? 'This group is unavailable.'}</p></main>

  return (
    <main className="mx-auto min-h-screen w-full max-w-6xl px-3 py-5 sm:px-5">
      <Link to="/groups" className="text-sm font-semibold text-primary no-underline">← Groups</Link>
      <section className="mt-3 overflow-hidden rounded-2xl border border-border bg-surface">
        {group.coverUrl ? <img src={resolveProfileImageUrl(group.coverUrl)} alt="" className="h-52 w-full object-cover sm:h-72" /> : <div className="flex h-52 items-center justify-center bg-primary/15 text-6xl sm:h-72">👥</div>}
        <div className="flex flex-wrap items-end justify-between gap-4 p-5">
          <div><p className="text-xs font-bold uppercase tracking-widest text-primary">{group.privacy} group</p><h1 className="font-heading text-3xl font-bold text-text">{group.name}</h1><p className="mt-2 max-w-2xl text-sm text-text-muted">{group.description || 'No description yet.'}</p><p className="mt-3 text-sm text-text-muted">{group.memberCount} members {group.viewerRole ? '· Your role: ' + group.viewerRole : ''}</p></div>
          <div className="flex gap-2">{isMember ? <button type="button" disabled={isJoining || group.viewerRole === 'owner'} onClick={() => void joinOrLeave()} className="rounded-lg border border-border bg-surface-2 px-4 py-2 text-sm font-semibold text-text cursor-pointer disabled:opacity-50">{group.viewerRole === 'owner' ? 'Owner' : isJoining ? 'Working…' : 'Leave group'}</button> : <button type="button" disabled={isJoining || requested} onClick={() => void joinOrLeave()} className="rounded-lg border-0 bg-primary px-4 py-2 text-sm font-semibold text-white cursor-pointer disabled:opacity-60">{requested ? 'Request sent' : isJoining ? 'Working…' : group.privacy === 'private' ? 'Request to join' : 'Join group'}</button>}</div>
        </div>
      </section>

      {error && <div className="mt-4 rounded-lg border border-[#e41e3f]/40 bg-[#e41e3f]/10 p-3 text-sm text-[#ff8a9b]">{error}</div>}
      <div className="mt-5 grid gap-5 lg:grid-cols-[minmax(0,1fr)_20rem]">
        <section className="flex min-w-0 flex-col gap-4">
          {isMember && <NewPostBox onPost={createPost} />}
          {!isMember && <p className="rounded-xl border border-border bg-surface p-4 text-sm text-text-muted">Join this group to post, comment and react.</p>}
          {posts.map((post) => <div key={post.id} className="relative"><LivePostCard post={post} author={toAuthor(post)} currentUserId={session!.user.id} onPostUpdated={(updated) => setPosts((current) => current.map((item) => item.id === updated.id ? updated : item))} onPostDeleted={(postId) => setPosts((current) => current.filter((postItem) => postItem.id !== postId))} />{canManage && post.authorUserId !== session!.user.id && <button type="button" onClick={() => void removeGroupPost(post.id)} className="absolute right-3 top-3 rounded-md border border-border bg-surface px-2 py-1 text-xs text-text-muted cursor-pointer">Remove post</button>}</div>)}
          {posts.length === 0 && <p className="rounded-xl border border-border bg-surface p-5 text-sm text-text-muted">No group posts yet.</p>}
          <PaginationControls hasMore={postsCursor !== null} isLoading={false} error={null} label="Load more posts" onLoadMore={() => void loadMorePosts()} />
        </section>

        <aside className="flex flex-col gap-4">
          <section className="rounded-xl border border-border bg-surface p-4"><h2 className="font-heading font-bold text-text">Rules</h2>{rules.length === 0 ? <p className="mt-2 text-sm text-text-muted">No rules yet.</p> : <ol className="mt-3 flex list-decimal flex-col gap-3 pl-5 text-sm text-text">{rules.map((rule) => <li key={rule.id}><p className="font-semibold">{rule.title}</p>{rule.description && <p className="text-text-muted">{rule.description}</p>}{canManageRules && <button type="button" onClick={() => void removeRule(rule.id)} className="mt-1 border-0 bg-transparent text-xs text-[#ff8a9b] cursor-pointer">Delete</button>}</li>)}</ol>}{canManageRules && <form onSubmit={createRule} className="mt-4 flex flex-col gap-2"><input value={newRuleTitle} onChange={(event) => setNewRuleTitle(event.target.value)} placeholder="New rule title" className="rounded-md border border-border bg-surface-2 px-2 py-1.5 text-sm text-text" /><input value={newRuleDescription} onChange={(event) => setNewRuleDescription(event.target.value)} placeholder="Description" className="rounded-md border border-border bg-surface-2 px-2 py-1.5 text-sm text-text" /><button className="rounded-md border-0 bg-surface-2 px-3 py-1.5 text-sm font-semibold text-primary cursor-pointer">Add rule</button></form>}</section>
          <section className="rounded-xl border border-border bg-surface p-4"><h2 className="font-heading font-bold text-text">Members</h2><div className="mt-3 flex flex-col gap-2">{members.map((member) => <div key={member.userId} className="flex items-center justify-between gap-2 text-sm"><span className="truncate text-text">Member {member.userId.slice(0, 8)}</span><span className="text-xs text-text-muted">{member.role}</span>{isOwner && member.userId !== session!.user.id && <select value={member.role} onChange={(event) => void changeRole(member.userId, event.target.value as GroupMember['role'])} className="max-w-22 rounded border border-border bg-surface-2 text-xs text-text"><option value="member">Member</option><option value="moderator">Moderator</option><option value="admin">Admin</option><option value="owner">Owner</option></select>}{canManage && member.userId !== session!.user.id && member.role !== 'owner' && <button type="button" onClick={() => void removeMember(member.userId)} className="border-0 bg-transparent text-xs text-[#ff8a9b] cursor-pointer">Remove</button>}</div>)}</div></section>
          {canManage && <section className="rounded-xl border border-border bg-surface p-4"><h2 className="font-heading font-bold text-text">Pending requests</h2>{requests.length === 0 ? <p className="mt-2 text-sm text-text-muted">No pending requests.</p> : <div className="mt-3 flex flex-col gap-2">{requests.map((request) => <div key={request.id} className="text-sm text-text"><p className="truncate">User {request.requesterUserId.slice(0, 8)}</p><div className="mt-1 flex gap-2"><button type="button" onClick={() => void respondToRequest(request.id, true)} className="border-0 bg-transparent text-xs font-semibold text-primary cursor-pointer">Approve</button><button type="button" onClick={() => void respondToRequest(request.id, false)} className="border-0 bg-transparent text-xs text-[#ff8a9b] cursor-pointer">Decline</button></div></div>)}</div>}<form onSubmit={invite} className="mt-4 flex gap-2"><input value={inviteeId} onChange={(event) => setInviteeId(event.target.value)} placeholder="User ID to invite" className="min-w-0 flex-1 rounded-md border border-border bg-surface-2 px-2 py-1.5 text-xs text-text" /><button className="rounded-md border-0 bg-surface-2 px-2 text-xs font-semibold text-primary cursor-pointer">Invite</button></form></section>}
          {canManage && <section className="rounded-xl border border-border bg-surface p-4"><h2 className="font-heading font-bold text-text">Group settings</h2><form onSubmit={saveDetails} className="mt-3 flex flex-col gap-2"><input name="name" defaultValue={group.name} required className="rounded-md border border-border bg-surface-2 px-2 py-1.5 text-sm text-text" /><textarea name="description" defaultValue={group.description ?? ''} className="rounded-md border border-border bg-surface-2 px-2 py-1.5 text-sm text-text" /><select name="privacy" defaultValue={group.privacy} className="rounded-md border border-border bg-surface-2 px-2 py-1.5 text-sm text-text"><option value="public">Public</option><option value="private">Private</option></select><button disabled={isSavingDetails} className="rounded-md border-0 bg-surface-2 px-3 py-1.5 text-sm font-semibold text-primary cursor-pointer">Save details</button></form><label className="mt-3 block cursor-pointer text-sm font-semibold text-primary">Change cover<input type="file" accept="image/jpeg,image/png,image/webp" className="hidden" onChange={(event) => void changeCover(event.target.files?.[0])} /></label>{group.coverUrl && <button type="button" onClick={() => void removeCover()} className="mt-2 border-0 bg-transparent text-xs text-[#ff8a9b] cursor-pointer">Remove cover</button>}</section>}
          {isOwner && <button type="button" onClick={() => { if (window.confirm('Delete this group?')) void groupsApi.delete(groupId).then(() => navigate('/groups')).catch((requestError: unknown) => setError(requestError instanceof ApiError ? requestError.message : 'Unable to delete group.')) }} className="rounded-lg border border-[#e41e3f]/40 bg-transparent px-4 py-2 text-sm font-semibold text-[#ff8a9b] cursor-pointer">Delete group</button>}
        </aside>
      </div>
    </main>
  )
}
