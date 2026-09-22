import { useCallback, useEffect, useRef, useState } from 'react'
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
import { getGroupHeaderCapabilities } from './groupHeaderCapabilities'

const managerRoles = new Set(['owner', 'admin', 'moderator'])

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
  const [isInviteDialogOpen, setIsInviteDialogOpen] = useState(false)
  const [isHeaderMenuOpen, setIsHeaderMenuOpen] = useState(false)
  const [activeSection, setActiveSection] = useState<'discussion' | 'about' | 'members'>('discussion')
  const [shareStatus, setShareStatus] = useState<string | null>(null)
  const [coverReadUrl, setCoverReadUrl] = useState<{ version: string; url: string } | null>(null)
  const coverInputRef = useRef<HTMLInputElement>(null)

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

  const coverVersion = group?.coverUrl
    ? `${group.id}:${group.updatedAtUtc ?? group.createdAtUtc}`
    : null
  const coverGroupId = group?.id

  useEffect(() => {
    if (!coverGroupId || !coverVersion) return

    let disposed = false
    void groupsApi.getCoverAccess(coverGroupId)
      .then((access) => { if (!disposed) setCoverReadUrl({ version: coverVersion, url: access.url }) })
      .catch(() => {})
    return () => { disposed = true }
  }, [coverGroupId, coverVersion])

  const currentCoverReadUrl = coverReadUrl?.version === coverVersion ? coverReadUrl.url : null

  if (!groupId) return null
  const isMember = group?.viewerRole !== null && group?.viewerRole !== undefined
  const { canInvite, canManageSettings, canModerate } = getGroupHeaderCapabilities(group?.viewerRole ?? null)
  const canManage = canModerate
  const canManageRules = canManageSettings
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

  const createPost = async (
    content: string,
    files: readonly File[],
    onUploadProgress: (progress: number) => void,
    _privacy: 'public' | 'friends' | 'onlyMe',
    textBackground: string | null,
  ) => {
    const mediaIds = await mediaApi.uploadFiles(files, onUploadProgress)
    const post = await groupsApi.createPost(groupId, content, mediaIds, textBackground)
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
      setIsInviteDialogOpen(false)
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
    if (!file || !group || !canManageSettings) return
    try {
      const coverMediaId = await mediaApi.uploadFile(file)
      const updated = await groupsApi.update(group.id, {
        name: group.name,
        description: group.description ?? '',
        privacy: group.privacy,
        coverMediaId,
      })
      setGroup(updated)
      const postPage = await groupsApi.getPosts(group.id)
      setPosts(postPage.items)
      setPostsCursor(postPage.nextCursor)
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : 'Không thể cập nhật ảnh bìa nhóm.')
    }
  }

  const removeCover = async () => {
    if (!group || !canManageSettings) return
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

  const navigateToSection = (section: 'discussion' | 'about' | 'members') => {
    setActiveSection(section)
    document.getElementById(`group-${section}`)?.scrollIntoView({ behavior: 'smooth', block: 'start' })
  }

  const shareGroup = async () => {
    const groupUrl = window.location.href
    try {
      if (!navigator.clipboard) throw new Error('Clipboard is unavailable.')
      await navigator.clipboard.writeText(groupUrl)
      setShareStatus('Đã sao chép liên kết nhóm.')
    } catch {
      window.prompt('Sao chép liên kết nhóm', groupUrl)
      setShareStatus(null)
    }
  }

  if (isLoading) return <main className="mx-auto w-full max-w-4xl px-3 py-5"><div className="h-60 animate-pulse rounded-xl bg-surface-2" /></main>
  if (!group) return <main className="mx-auto w-full max-w-3xl px-3 py-10"><Link to="/groups" className="text-primary">← Quay lại Nhóm</Link><p className="mt-4 rounded-xl border border-border bg-surface p-5 text-text-muted">{error ?? 'Nhóm này không còn khả dụng.'}</p></main>

  return (
    <main className="mx-auto min-h-screen w-full max-w-6xl px-3 py-4 sm:px-5">
      <Link to="/groups" className="text-sm font-semibold text-primary no-underline">← Nhóm</Link>
      <section className="mt-3 overflow-hidden rounded-2xl border border-border bg-surface shadow-sm">
        <div className="relative isolate">
          {group.coverUrl ? (
            currentCoverReadUrl ? <img src={currentCoverReadUrl} alt={`Ảnh bìa của ${group.name}`} className="h-56 w-full object-cover sm:h-72 lg:h-80" /> : <div className="flex h-56 items-center justify-center bg-surface-2 text-sm text-text-muted sm:h-72 lg:h-80">Đang tải ảnh bìa…</div>
          ) : (
            <div className="flex h-56 items-center justify-center bg-linear-to-br from-primary/70 via-[#6655bf] to-[#262b50] text-7xl sm:h-72 lg:h-80" aria-label="Nhóm chưa có ảnh bìa">👥</div>
          )}
          <div className="pointer-events-none absolute inset-x-0 bottom-0 h-24 bg-linear-to-t from-black/45 to-transparent" />
          {canManageSettings && <>
            <input ref={coverInputRef} type="file" accept="image/jpeg,image/png,image/webp" className="hidden" onChange={(event) => { const file = event.target.files?.[0]; event.currentTarget.value = ''; void changeCover(file) }} />
            <button type="button" onClick={() => coverInputRef.current?.click()} className="absolute bottom-3 right-3 inline-flex items-center gap-2 rounded-lg border border-white/25 bg-black/55 px-3 py-2 text-sm font-semibold text-white shadow-lg backdrop-blur-sm transition hover:bg-black/70">🖼️ Đổi ảnh bìa</button>
          </>}
        </div>

        <div className="flex flex-col gap-4 px-5 pb-4 pt-5 lg:flex-row lg:items-end lg:justify-between">
          <div className="min-w-0">
            <h1 className="font-heading text-3xl font-bold tracking-tight text-text sm:text-4xl">{group.name}</h1>
            <p className="mt-1 text-sm font-medium text-text-muted">{group.privacy === 'public' ? '🌐 Nhóm công khai' : '🔒 Nhóm riêng tư'} · {group.memberCount} thành viên{group.viewerRole ? ` · ${roleLabels[group.viewerRole]}` : ''}</p>
            <div className="mt-3 flex items-center" aria-label="Một số thành viên trong nhóm">
              {members.slice(0, 9).map((member, index) => <span key={member.userId} className={`-ml-1.5 grid h-8 w-8 shrink-0 place-items-center overflow-hidden rounded-full border-2 border-surface bg-primary text-[10px] font-bold text-white ${index === 0 ? 'ml-0' : ''}`} title={member.displayName ?? member.username ?? 'Thành viên'}>{member.avatarUrl ? <img src={resolveProfileImageUrl(member.avatarUrl)} alt="" className="h-full w-full object-cover" /> : (member.displayName ?? 'TV').slice(0, 2).toUpperCase()}</span>)}
              {group.memberCount > members.length && <span className="ml-2 text-xs font-semibold text-text-muted">và nhiều người khác</span>}
            </div>
          </div>

          <div className="relative flex flex-wrap items-center gap-2">
            {canInvite && <button type="button" onClick={() => { setInvitee(null); setIsInviteDialogOpen(true) }} className="rounded-lg border-0 bg-primary px-3.5 py-2 text-sm font-semibold text-white shadow-sm transition hover:brightness-110">＋ Mời</button>}
            <button type="button" onClick={() => void shareGroup()} className="rounded-lg border border-border bg-surface-2 px-3.5 py-2 text-sm font-semibold text-text transition hover:bg-surface-3">↗ Chia sẻ</button>
            {isMember ? (
              <span className="inline-flex rounded-lg border border-border bg-surface-2 px-3.5 py-2 text-sm font-semibold text-text">✓ {isOwner ? 'Đang quản lý' : 'Đã tham gia'}</span>
            ) : (
              <button type="button" disabled={isJoining || requested} onClick={() => void joinOrLeave()} className="rounded-lg border-0 bg-primary px-3.5 py-2 text-sm font-semibold text-white transition hover:brightness-110 disabled:cursor-not-allowed disabled:opacity-60">{requested ? 'Đã gửi yêu cầu' : isJoining ? 'Đang xử lý…' : group.privacy === 'private' ? 'Xin tham gia' : 'Tham gia nhóm'}</button>
            )}
            {isMember && <div className="relative">
              <button type="button" aria-label="Tùy chọn nhóm" aria-expanded={isHeaderMenuOpen} onClick={() => setIsHeaderMenuOpen((current) => !current)} className="grid h-9 w-9 place-items-center rounded-lg border border-border bg-surface-2 text-lg font-bold text-text transition hover:bg-surface-3">⌄</button>
              {isHeaderMenuOpen && <div className="absolute right-0 top-11 z-20 min-w-48 rounded-xl border border-border bg-surface p-1.5 shadow-2xl">
                {canManageSettings && <button type="button" onClick={() => { setIsHeaderMenuOpen(false); document.getElementById('group-settings')?.scrollIntoView({ behavior: 'smooth', block: 'start' }) }} className="flex w-full rounded-lg px-3 py-2 text-left text-sm font-semibold text-text hover:bg-surface-2">⚙ Quản lý nhóm</button>}
                {canManageSettings && group.coverUrl && <button type="button" onClick={() => { setIsHeaderMenuOpen(false); void removeCover() }} className="flex w-full rounded-lg px-3 py-2 text-left text-sm font-semibold text-[#ff8a9b] hover:bg-surface-2">Gỡ ảnh bìa</button>}
                {!isOwner && <button type="button" disabled={isJoining} onClick={() => { setIsHeaderMenuOpen(false); void joinOrLeave() }} className="flex w-full rounded-lg px-3 py-2 text-left text-sm font-semibold text-[#ff8a9b] hover:bg-surface-2 disabled:opacity-60">Rời nhóm</button>}
              </div>}
            </div>}
            {shareStatus && <p className="absolute right-0 top-full mt-2 whitespace-nowrap text-xs font-semibold text-primary" role="status">{shareStatus}</p>}
          </div>
        </div>

        <nav aria-label="Điều hướng nhóm" className="flex items-center gap-1 overflow-x-auto border-t border-border px-3 sm:px-5">
          <button type="button" onClick={() => navigateToSection('discussion')} className={`shrink-0 border-x-0 border-b-2 border-t-0 px-3 py-3 text-sm font-semibold transition ${activeSection === 'discussion' ? 'border-primary text-primary' : 'border-transparent text-text-muted hover:bg-surface-2 hover:text-text'}`}>Thảo luận</button>
          <button type="button" onClick={() => navigateToSection('about')} className={`shrink-0 border-x-0 border-b-2 border-t-0 px-3 py-3 text-sm font-semibold transition ${activeSection === 'about' ? 'border-primary text-primary' : 'border-transparent text-text-muted hover:bg-surface-2 hover:text-text'}`}>Giới thiệu</button>
          <button type="button" onClick={() => navigateToSection('members')} className={`shrink-0 border-x-0 border-b-2 border-t-0 px-3 py-3 text-sm font-semibold transition ${activeSection === 'members' ? 'border-primary text-primary' : 'border-transparent text-text-muted hover:bg-surface-2 hover:text-text'}`}>Thành viên</button>
        </nav>
      </section>

      {error && <div className="mt-4 rounded-lg border border-[#e41e3f]/40 bg-[#e41e3f]/10 p-3 text-sm text-[#ff8a9b]">{error}</div>}
      <div className="mt-5 grid gap-5 lg:grid-cols-[minmax(0,1fr)_20rem]">
        <section id="group-discussion" className="flex min-w-0 scroll-mt-5 flex-col gap-4">
          {isMember && <NewPostBox onPost={createPost} />}
          {!isMember && <p className="rounded-xl border border-border bg-surface p-4 text-sm text-text-muted">Tham gia nhóm để đăng bài, bình luận và bày tỏ cảm xúc.</p>}
          {posts.map((post) => <div key={post.id} className="relative"><LivePostCard post={post} author={toAuthor(post)} currentUserId={session!.user.id} onPostUpdated={(updated) => setPosts((current) => current.map((item) => item.id === updated.id ? updated : item))} onPostDeleted={(postId) => setPosts((current) => current.filter((postItem) => postItem.id !== postId))} />{canModerate && post.authorUserId !== session!.user.id && <button type="button" onClick={() => void removeGroupPost(post.id)} className="absolute right-3 top-3 rounded-md border border-border bg-surface px-2 py-1 text-xs text-text-muted cursor-pointer">Gỡ bài viết</button>}</div>)}
          {posts.length === 0 && <p className="rounded-xl border border-border bg-surface p-5 text-sm text-text-muted">Chưa có bài viết nào trong nhóm.</p>}
          <PaginationControls hasMore={postsCursor !== null} isLoading={false} error={null} label="Xem thêm bài viết" onLoadMore={() => void loadMorePosts()} />
        </section>

        <aside className="flex flex-col gap-4">
          <section id="group-about" className="scroll-mt-5 rounded-xl border border-border bg-surface p-4">
            <h2 className="font-heading text-lg font-bold text-text">Giới thiệu</h2>
            <p className="mt-2 whitespace-pre-wrap text-sm leading-6 text-text-muted">{group.description || 'Nhóm chưa có phần giới thiệu.'}</p>
            <p className="mt-3 text-xs font-medium text-text-light">{group.privacy === 'public' ? 'Mọi người có thể xem nhóm này.' : 'Chỉ thành viên được duyệt mới có thể xem nội dung.'}</p>
          </section>
          <section className="rounded-xl border border-border bg-surface p-4"><h2 className="font-heading font-bold text-text">Quy tắc</h2>{rules.length === 0 ? <p className="mt-2 text-sm text-text-muted">Chưa có quy tắc nào.</p> : <ol className="mt-3 flex list-decimal flex-col gap-3 pl-5 text-sm text-text">{rules.map((rule) => <li key={rule.id}><p className="font-semibold">{rule.title}</p>{rule.description && <p className="text-text-muted">{rule.description}</p>}{canManageRules && <button type="button" onClick={() => void removeRule(rule.id)} className="mt-1 border-0 bg-transparent text-xs text-[#ff8a9b] cursor-pointer">Xóa</button>}</li>)}</ol>}{canManageRules && <form onSubmit={createRule} className="mt-4 flex flex-col gap-2"><input value={newRuleTitle} onChange={(event) => setNewRuleTitle(event.target.value)} placeholder="Tên quy tắc mới" className="rounded-md border border-border bg-surface-2 px-2 py-1.5 text-sm text-text" /><input value={newRuleDescription} onChange={(event) => setNewRuleDescription(event.target.value)} placeholder="Mô tả" className="rounded-md border border-border bg-surface-2 px-2 py-1.5 text-sm text-text" /><button className="rounded-md border-0 bg-surface-2 px-3 py-1.5 text-sm font-semibold text-primary cursor-pointer">Thêm quy tắc</button></form>}</section>
          <div id="group-members" className="scroll-mt-5">
          <section className="rounded-xl border border-border bg-surface p-4"><h2 className="font-heading font-bold text-text">Thành viên</h2><div className="mt-3 flex flex-col gap-2">{members.map((member) => <div key={member.userId} className="flex items-center justify-between gap-2 text-sm"><div className="flex min-w-0 items-center gap-2"><span className="grid h-7 w-7 shrink-0 place-items-center overflow-hidden rounded-full bg-primary text-[10px] font-bold text-white">{member.avatarUrl ? <img src={resolveProfileImageUrl(member.avatarUrl)} alt="" loading="lazy" className="h-full w-full object-cover" /> : (member.displayName ?? 'TV').slice(0, 2).toUpperCase()}</span><span className="truncate text-text">{member.userId === session!.user.id ? 'Bạn' : member.displayName ?? 'Thành viên'}</span></div><span className="text-xs text-text-muted">{roleLabels[member.role]}</span>{isOwner && member.userId !== session!.user.id && <select value={member.role} onChange={(event) => void changeRole(member.userId, event.target.value as GroupMember['role'])} className="max-w-22 rounded border border-border bg-surface-2 text-xs text-text"><option value="member">Thành viên</option><option value="moderator">Kiểm duyệt viên</option><option value="admin">Quản trị viên</option><option value="owner">Chủ nhóm</option></select>}{canManage && member.userId !== session!.user.id && member.role !== 'owner' && <button type="button" onClick={() => void removeMember(member.userId)} className="border-0 bg-transparent text-xs text-[#ff8a9b] cursor-pointer">Xóa</button>}</div>)}</div></section>
          </div>
          {canManage && <section className="rounded-xl border border-border bg-surface p-4"><h2 className="font-heading font-bold text-text">Yêu cầu đang chờ</h2>{requests.length === 0 ? <p className="mt-2 text-sm text-text-muted">Chưa có yêu cầu nào.</p> : <div className="mt-3 flex flex-col gap-2">{requests.map((request) => <div key={request.id} className="text-sm text-text"><p className="truncate">Người dùng</p><div className="mt-1 flex gap-2"><button type="button" onClick={() => void respondToRequest(request.id, true)} className="border-0 bg-transparent text-xs font-semibold text-primary cursor-pointer">Chấp nhận</button><button type="button" onClick={() => void respondToRequest(request.id, false)} className="border-0 bg-transparent text-xs text-[#ff8a9b] cursor-pointer">Từ chối</button></div></div>)}</div>}</section>}
          {canManageSettings && <section id="group-settings" className="scroll-mt-5 rounded-xl border border-border bg-surface p-4"><h2 className="font-heading text-lg font-bold text-text">Cài đặt nhóm</h2><form onSubmit={saveDetails} className="mt-3 flex flex-col gap-2"><input name="name" aria-label="Tên nhóm" defaultValue={group.name} required className="rounded-md border border-border bg-surface-2 px-2 py-1.5 text-sm text-text" /><textarea name="description" aria-label="Mô tả nhóm" defaultValue={group.description ?? ''} className="rounded-md border border-border bg-surface-2 px-2 py-1.5 text-sm text-text" /><select name="privacy" aria-label="Quyền riêng tư nhóm" defaultValue={group.privacy} className="rounded-md border border-border bg-surface-2 px-2 py-1.5 text-sm text-text"><option value="public">Công khai</option><option value="private">Riêng tư</option></select><button disabled={isSavingDetails} className="rounded-md border-0 bg-surface-2 px-3 py-1.5 text-sm font-semibold text-primary cursor-pointer disabled:opacity-60">{isSavingDetails ? 'Đang lưu…' : 'Lưu thay đổi'}</button></form></section>}
          {isOwner && <button type="button" onClick={() => setIsGroupPendingDeletion(true)} className="rounded-lg border border-[#e41e3f]/40 bg-transparent px-4 py-2 text-sm font-semibold text-[#ff8a9b] cursor-pointer">Xóa nhóm</button>}
        </aside>
      </div>
      {isInviteDialogOpen && <AppDialog title="Mời vào nhóm" onClose={() => { setInvitee(null); setIsInviteDialogOpen(false) }}><p className="mt-2 text-sm text-text-muted">Chọn người bạn muốn mời tham gia {group.name}.</p><form onSubmit={invite} className="mt-4 grid gap-3">{invitee ? <div className="flex items-center justify-between rounded-lg bg-surface-2 px-3 py-2 text-sm text-text"><span className="truncate font-semibold">{invitee.displayName}</span><button type="button" onClick={() => setInvitee(null)} className="border-0 bg-transparent text-xs font-semibold text-primary">Đổi</button></div> : <UserSearchPicker excludedUserIds={members.map((member) => member.userId)} onSelect={setInvitee} />}<div className="flex justify-end gap-2"><button type="button" onClick={() => { setInvitee(null); setIsInviteDialogOpen(false) }} className="rounded-lg border-0 bg-surface-2 px-4 py-2 text-sm font-semibold text-text hover:bg-surface-3">Hủy</button><button disabled={!invitee} className="rounded-lg border-0 bg-primary px-4 py-2 text-sm font-semibold text-white hover:brightness-110 disabled:cursor-not-allowed disabled:opacity-60">Gửi lời mời</button></div></form></AppDialog>}
      {isGroupPendingDeletion && <AppDialog title="Xóa nhóm?" onClose={() => setIsGroupPendingDeletion(false)}><p className="mt-3 text-sm text-text-muted">Bạn có chắc muốn xóa nhóm này?</p><div className="mt-5 flex justify-end gap-2"><button type="button" onClick={() => setIsGroupPendingDeletion(false)} className="rounded-lg border-0 bg-surface-2 px-4 py-2 text-sm font-semibold text-text hover:bg-surface-3">Hủy</button><button type="button" onClick={() => { setIsGroupPendingDeletion(false); void groupsApi.delete(groupId).then(() => navigate('/groups')).catch((requestError: unknown) => setError(requestError instanceof ApiError ? requestError.message : 'Không thể xóa nhóm.')) }} className="rounded-lg border-0 bg-[#e41e3f] px-4 py-2 text-sm font-semibold text-white hover:brightness-110">Xóa</button></div></AppDialog>}
    </main>
  )
}
