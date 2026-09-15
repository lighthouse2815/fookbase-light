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
import AppDialog from '../../shared/components/AppDialog'
import UserSearchPicker from '../../shared/components/UserSearchPicker'
import LivePostCard from '../feed/components/LivePostCard'
import NewPostBox from '../feed/components/NewPostBox'

const managerRoles = new Set(['owner', 'admin', 'moderator'])
const rulesManagerRoles = new Set(['owner', 'admin'])

const roleLabels: Record<GroupMember['role'], string> = {
  owner: 'Chủ nhóm',
  admin: 'Quản trị viên',
  moderator: 'Kiểm duyệt viên',
  member: 'Thành viên',
}

function toAuthor(post: Post): UserProfile {
  const authorUserId = post.authorUserId!
  return {
    userId: authorUserId,
    username: 'member',
    displayName: 'Thành viên nhóm',
    avatarUrl: null,
    bio: null,
    coverUrl: null,
    dateOfBirth: null,
    currentCity: null,
    createdAt: post.createdAtUtc,
    updatedAt: post.updatedAtUtc ?? post.createdAtUtc,
    followerCount: 0,
    followingCount: 0,
    isFollowing: null,
    isFollowedBy: null,
    friendshipState: null,
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
  const [invitee, setInvitee] = useState<UserProfile | null>(null)
  const [isSavingDetails, setIsSavingDetails] = useState(false)
  const [isGroupPendingDeletion, setIsGroupPendingDeletion] = useState(false)

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
      setError(requestError instanceof ApiError ? requestError.message : 'Không thể tải nhóm này.')
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
      setError(requestError instanceof ApiError ? requestError.message : 'Không thể thay đổi trạng thái thành viên.')
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
      setError(requestError instanceof ApiError ? requestError.message : 'Không thể tải thêm bài viết.')
    }
  }

  const respondToRequest = async (requestId: string, approve: boolean) => {
    try {
      if (approve) await groupsApi.approveJoinRequest(groupId, requestId)
      else await groupsApi.declineJoinRequest(groupId, requestId)
      setRequests((current) => current.filter((request) => request.id !== requestId))
      await load()
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : 'Không thể cập nhật yêu cầu tham gia.')
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
      setError(requestError instanceof ApiError ? requestError.message : 'Không thể tạo quy tắc.')
    }
  }

  const removeRule = async (ruleId: string) => {
    try {
      await groupsApi.deleteRule(groupId, ruleId)
      setRules((current) => current.filter((rule) => rule.id !== ruleId))
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : 'Không thể xóa quy tắc.')
    }
  }

  const changeRole = async (userId: string, role: GroupMember['role']) => {
    try {
      const changed = await groupsApi.changeMemberRole(groupId, userId, role)
      setMembers((current) => current.map((member) => member.userId === userId ? changed : member))
      await load()
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : 'Không thể thay đổi vai trò này.')
    }
  }

  const removeMember = async (userId: string) => {
    try {
      await groupsApi.removeMember(groupId, userId)
      setMembers((current) => current.filter((member) => member.userId !== userId))
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : 'Không thể xóa thành viên này.')
    }
  }

  const invite = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (!invitee) return
    try {
      await groupsApi.invite(groupId, invitee.userId)
      setInvitee(null)
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : 'Không thể gửi lời mời.')
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
      setError(requestError instanceof ApiError ? requestError.message : 'Không thể cập nhật thông tin nhóm.')
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
      setError(requestError instanceof ApiError ? requestError.message : 'Không thể cập nhật ảnh bìa nhóm.')
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
      setError(requestError instanceof ApiError ? requestError.message : 'Không thể gỡ ảnh bìa nhóm.')
    }
  }

  const removeGroupPost = async (postId: string) => {
    try {
      await groupsApi.removePost(groupId, postId)
      setPosts((current) => current.filter((post) => post.id !== postId))
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : 'Không thể gỡ bài viết khỏi nhóm.')
    }
  }

  if (isLoading) return <main className="mx-auto w-full max-w-4xl px-3 py-5"><div className="h-60 animate-pulse rounded-xl bg-surface-2" /></main>
  if (!group) return <main className="mx-auto w-full max-w-3xl px-3 py-10"><Link to="/groups" className="text-primary">← Quay lại Nhóm</Link><p className="mt-4 rounded-xl border border-border bg-surface p-5 text-text-muted">{error ?? 'Nhóm này không còn khả dụng.'}</p></main>

  return (
    <main className="mx-auto min-h-screen w-full max-w-6xl px-3 py-5 sm:px-5">
      <Link to="/groups" className="text-sm font-semibold text-primary no-underline">← Nhóm</Link>
      <section className="mt-3 overflow-hidden rounded-2xl border border-border bg-surface">
        {group.coverUrl ? <img src={resolveProfileImageUrl(group.coverUrl)} alt="" className="h-52 w-full object-cover sm:h-72" /> : <div className="flex h-52 items-center justify-center bg-primary/15 text-6xl sm:h-72">👥</div>}
        <div className="flex flex-wrap items-end justify-between gap-4 p-5">
          <div><p className="text-xs font-bold uppercase tracking-widest text-primary">Nhóm {group.privacy === 'public' ? 'công khai' : 'riêng tư'}</p><h1 className="font-heading text-3xl font-bold text-text">{group.name}</h1><p className="mt-2 max-w-2xl text-sm text-text-muted">{group.description || 'Chưa có mô tả.'}</p><p className="mt-3 text-sm text-text-muted">{group.memberCount} thành viên {group.viewerRole ? `· Vai trò: ${roleLabels[group.viewerRole]}` : ''}</p></div>
          <div className="flex gap-2">{isMember ? <button type="button" disabled={isJoining || group.viewerRole === 'owner'} onClick={() => void joinOrLeave()} className="rounded-lg border border-border bg-surface-2 px-4 py-2 text-sm font-semibold text-text cursor-pointer disabled:opacity-50">{group.viewerRole === 'owner' ? 'Chủ nhóm' : isJoining ? 'Đang xử lý…' : 'Rời nhóm'}</button> : <button type="button" disabled={isJoining || requested} onClick={() => void joinOrLeave()} className="rounded-lg border-0 bg-primary px-4 py-2 text-sm font-semibold text-white cursor-pointer disabled:opacity-60">{requested ? 'Đã gửi yêu cầu' : isJoining ? 'Đang xử lý…' : group.privacy === 'private' ? 'Xin tham gia' : 'Tham gia nhóm'}</button>}</div>
        </div>
      </section>

      {error && <div className="mt-4 rounded-lg border border-[#e41e3f]/40 bg-[#e41e3f]/10 p-3 text-sm text-[#ff8a9b]">{error}</div>}
      <div className="mt-5 grid gap-5 lg:grid-cols-[minmax(0,1fr)_20rem]">
        <section className="flex min-w-0 flex-col gap-4">
          {isMember && <NewPostBox onPost={createPost} />}
          {!isMember && <p className="rounded-xl border border-border bg-surface p-4 text-sm text-text-muted">Tham gia nhóm để đăng bài, bình luận và bày tỏ cảm xúc.</p>}
          {posts.map((post) => <div key={post.id} className="relative"><LivePostCard post={post} author={toAuthor(post)} currentUserId={session!.user.id} onPostUpdated={(updated) => setPosts((current) => current.map((item) => item.id === updated.id ? updated : item))} onPostDeleted={(postId) => setPosts((current) => current.filter((postItem) => postItem.id !== postId))} />{canManage && post.authorUserId !== session!.user.id && <button type="button" onClick={() => void removeGroupPost(post.id)} className="absolute right-3 top-3 rounded-md border border-border bg-surface px-2 py-1 text-xs text-text-muted cursor-pointer">Gỡ bài viết</button>}</div>)}
          {posts.length === 0 && <p className="rounded-xl border border-border bg-surface p-5 text-sm text-text-muted">Chưa có bài viết nào trong nhóm.</p>}
          <PaginationControls hasMore={postsCursor !== null} isLoading={false} error={null} label="Xem thêm bài viết" onLoadMore={() => void loadMorePosts()} />
        </section>

        <aside className="flex flex-col gap-4">
          <section className="rounded-xl border border-border bg-surface p-4"><h2 className="font-heading font-bold text-text">Quy tắc</h2>{rules.length === 0 ? <p className="mt-2 text-sm text-text-muted">Chưa có quy tắc nào.</p> : <ol className="mt-3 flex list-decimal flex-col gap-3 pl-5 text-sm text-text">{rules.map((rule) => <li key={rule.id}><p className="font-semibold">{rule.title}</p>{rule.description && <p className="text-text-muted">{rule.description}</p>}{canManageRules && <button type="button" onClick={() => void removeRule(rule.id)} className="mt-1 border-0 bg-transparent text-xs text-[#ff8a9b] cursor-pointer">Xóa</button>}</li>)}</ol>}{canManageRules && <form onSubmit={createRule} className="mt-4 flex flex-col gap-2"><input value={newRuleTitle} onChange={(event) => setNewRuleTitle(event.target.value)} placeholder="Tên quy tắc mới" className="rounded-md border border-border bg-surface-2 px-2 py-1.5 text-sm text-text" /><input value={newRuleDescription} onChange={(event) => setNewRuleDescription(event.target.value)} placeholder="Mô tả" className="rounded-md border border-border bg-surface-2 px-2 py-1.5 text-sm text-text" /><button className="rounded-md border-0 bg-surface-2 px-3 py-1.5 text-sm font-semibold text-primary cursor-pointer">Thêm quy tắc</button></form>}</section>
          <section className="rounded-xl border border-border bg-surface p-4"><h2 className="font-heading font-bold text-text">Thành viên</h2><div className="mt-3 flex flex-col gap-2">{members.map((member) => <div key={member.userId} className="flex items-center justify-between gap-2 text-sm"><div className="flex min-w-0 items-center gap-2"><span className="grid h-7 w-7 shrink-0 place-items-center overflow-hidden rounded-full bg-primary text-[10px] font-bold text-white">{member.avatarUrl ? <img src={resolveProfileImageUrl(member.avatarUrl)} alt="" loading="lazy" className="h-full w-full object-cover" /> : (member.displayName ?? 'TV').slice(0, 2).toUpperCase()}</span><span className="truncate text-text">{member.userId === session!.user.id ? 'Bạn' : member.displayName ?? 'Thành viên'}</span></div><span className="text-xs text-text-muted">{roleLabels[member.role]}</span>{isOwner && member.userId !== session!.user.id && <select value={member.role} onChange={(event) => void changeRole(member.userId, event.target.value as GroupMember['role'])} className="max-w-22 rounded border border-border bg-surface-2 text-xs text-text"><option value="member">Thành viên</option><option value="moderator">Kiểm duyệt viên</option><option value="admin">Quản trị viên</option><option value="owner">Chủ nhóm</option></select>}{canManage && member.userId !== session!.user.id && member.role !== 'owner' && <button type="button" onClick={() => void removeMember(member.userId)} className="border-0 bg-transparent text-xs text-[#ff8a9b] cursor-pointer">Xóa</button>}</div>)}</div></section>
          {canManage && <section className="rounded-xl border border-border bg-surface p-4"><h2 className="font-heading font-bold text-text">Yêu cầu đang chờ</h2>{requests.length === 0 ? <p className="mt-2 text-sm text-text-muted">Chưa có yêu cầu nào.</p> : <div className="mt-3 flex flex-col gap-2">{requests.map((request) => <div key={request.id} className="text-sm text-text"><p className="truncate">Người dùng</p><div className="mt-1 flex gap-2"><button type="button" onClick={() => void respondToRequest(request.id, true)} className="border-0 bg-transparent text-xs font-semibold text-primary cursor-pointer">Chấp nhận</button><button type="button" onClick={() => void respondToRequest(request.id, false)} className="border-0 bg-transparent text-xs text-[#ff8a9b] cursor-pointer">Từ chối</button></div></div>)}</div>}<form onSubmit={invite} className="mt-4 grid gap-2">{invitee ? <div className="flex items-center justify-between rounded bg-surface-2 px-2 py-1.5 text-sm text-text"><span>{invitee.displayName}</span><button type="button" onClick={() => setInvitee(null)} className="border-0 bg-transparent text-xs text-primary">Đổi</button></div> : <UserSearchPicker excludedUserIds={members.map((member) => member.userId)} onSelect={setInvitee} />}<button disabled={!invitee} className="rounded-md border-0 bg-surface-2 px-2 py-1.5 text-xs font-semibold text-primary cursor-pointer disabled:opacity-50">Mời vào nhóm</button></form></section>}
          {canManage && <section className="rounded-xl border border-border bg-surface p-4"><h2 className="font-heading font-bold text-text">Cài đặt nhóm</h2><form onSubmit={saveDetails} className="mt-3 flex flex-col gap-2"><input name="name" defaultValue={group.name} required className="rounded-md border border-border bg-surface-2 px-2 py-1.5 text-sm text-text" /><textarea name="description" defaultValue={group.description ?? ''} className="rounded-md border border-border bg-surface-2 px-2 py-1.5 text-sm text-text" /><select name="privacy" defaultValue={group.privacy} className="rounded-md border border-border bg-surface-2 px-2 py-1.5 text-sm text-text"><option value="public">Công khai</option><option value="private">Riêng tư</option></select><button disabled={isSavingDetails} className="rounded-md border-0 bg-surface-2 px-3 py-1.5 text-sm font-semibold text-primary cursor-pointer">Lưu thay đổi</button></form><label className="mt-3 block cursor-pointer text-sm font-semibold text-primary">Đổi ảnh bìa<input type="file" accept="image/jpeg,image/png,image/webp" className="hidden" onChange={(event) => void changeCover(event.target.files?.[0])} /></label>{group.coverUrl && <button type="button" onClick={() => void removeCover()} className="mt-2 border-0 bg-transparent text-xs text-[#ff8a9b] cursor-pointer">Gỡ ảnh bìa</button>}</section>}
          {isOwner && <button type="button" onClick={() => setIsGroupPendingDeletion(true)} className="rounded-lg border border-[#e41e3f]/40 bg-transparent px-4 py-2 text-sm font-semibold text-[#ff8a9b] cursor-pointer">Xóa nhóm</button>}
        </aside>
      </div>
      {isGroupPendingDeletion && <AppDialog title="Xóa nhóm?" onClose={() => setIsGroupPendingDeletion(false)}><p className="mt-3 text-sm text-text-muted">Bạn có chắc muốn xóa nhóm này?</p><div className="mt-5 flex justify-end gap-2"><button type="button" onClick={() => setIsGroupPendingDeletion(false)} className="rounded-lg border-0 bg-surface-2 px-4 py-2 text-sm font-semibold text-text hover:bg-surface-3">Hủy</button><button type="button" onClick={() => { setIsGroupPendingDeletion(false); void groupsApi.delete(groupId).then(() => navigate('/groups')).catch((requestError: unknown) => setError(requestError instanceof ApiError ? requestError.message : 'Không thể xóa nhóm.')) }} className="rounded-lg border-0 bg-[#e41e3f] px-4 py-2 text-sm font-semibold text-white hover:brightness-110">Xóa</button></div></AppDialog>}
    </main>
  )
}
