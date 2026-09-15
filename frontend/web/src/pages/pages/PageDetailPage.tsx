import { useCallback, useEffect, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { ApiError } from '../../api/client'
import { pagesApi, type Page, type PageInvitation, type PageMember } from '../../api/pages'
import { mediaApi } from '../../api/media'
import { postsApi, type Post } from '../../api/posts'
import { resolveProfileImageUrl } from '../../api/users'
import type { UserProfile } from '../../api/users'
import { useAuth } from '../../auth/useAuth'
import PaginationControls from '../../shared/components/PaginationControls'
import AppDialog from '../../shared/components/AppDialog'
import UserSearchPicker from '../../shared/components/UserSearchPicker'
import LivePostCard from '../feed/components/LivePostCard'
import NewPostBox from '../feed/components/NewPostBox'

const postRoles = new Set(['owner', 'admin', 'editor'])
const memberRoles = new Set(['owner', 'admin'])

export default function PageDetailPage() {
  const { username } = useParams()
  const { session } = useAuth()
  const navigate = useNavigate()
  const [page, setPage] = useState<Page | null>(null)
  const [posts, setPosts] = useState<Post[]>([])
  const [postsCursor, setPostsCursor] = useState<string | null>(null)
  const [members, setMembers] = useState<PageMember[]>([])
  const [error, setError] = useState<string | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [isWorking, setIsWorking] = useState(false)
  const [inviteUser, setInviteUser] = useState<UserProfile | null>(null)
  const [inviteRole, setInviteRole] = useState<PageInvitation['role']>('editor')
  const [transferTarget, setTransferTarget] = useState<PageMember | null>(null)
  const [isPagePendingDeletion, setIsPagePendingDeletion] = useState(false)

  const load = useCallback(async () => {
    if (!username) return
    setIsLoading(true)
    setError(null)
    try {
      const nextPage = await pagesApi.get(username)
      const timeline = await pagesApi.posts(nextPage.id)
      setPage(nextPage)
      setPosts(timeline.items)
      setPostsCursor(timeline.nextCursor)
      if (nextPage.viewerRole) {
        const memberPage = await pagesApi.members(nextPage.id)
        setMembers(memberPage.items)
      } else {
        setMembers([])
      }
    } catch (requestError) {
      setPage(null)
      setError(requestError instanceof ApiError ? requestError.message : 'Trang này hiện không khả dụng.')
    } finally {
      setIsLoading(false)
    }
  }, [username])

  useEffect(() => {
    const timeoutId = window.setTimeout(() => { void load() }, 0)
    return () => window.clearTimeout(timeoutId)
  }, [load])

  if (!username) return null
  if (isLoading) return <main className="mx-auto w-full max-w-5xl px-3 py-8"><div className="h-64 animate-pulse rounded-2xl bg-surface-2" /></main>
  if (!page) return <main className="mx-auto w-full max-w-3xl px-3 py-10"><Link to="/pages" className="text-primary">← Trang</Link><p className="mt-4 rounded-xl border border-border bg-surface p-5 text-text-muted">{error ?? 'Trang này hiện không khả dụng.'}</p></main>

  const isOwner = page.viewerRole === 'owner'
  const canPost = page.viewerRole !== null && postRoles.has(page.viewerRole)
  const canManageMembers = page.viewerRole !== null && memberRoles.has(page.viewerRole)
  const canManageSettings = page.viewerRole === 'owner' || page.viewerRole === 'admin'

  const follow = async () => {
    setIsWorking(true)
    try {
      if (page.isFollowing) await pagesApi.unfollow(page.id)
      else await pagesApi.follow(page.id)
      setPage((current) => current ? { ...current, isFollowing: !current.isFollowing, followerCount: current.followerCount + (current.isFollowing ? -1 : 1) } : current)
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : 'Không thể cập nhật trạng thái theo dõi.')
    } finally { setIsWorking(false) }
  }

  const createPost = async (content: string, files: readonly File[], onUploadProgress: (progress: number) => void) => {
    const mediaIds = await mediaApi.uploadFiles(files, onUploadProgress)
    const post = await pagesApi.createPost(page.id, content, mediaIds)
    setPosts((current) => [post, ...current])
  }

  const loadMorePosts = async () => {
    if (!postsCursor) return
    try {
      const next = await pagesApi.posts(page.id, postsCursor)
      setPosts((current) => [...current, ...next.items.filter((post) => !current.some((item) => item.id === post.id))])
      setPostsCursor(next.nextCursor)
    } catch (requestError) { setError(requestError instanceof ApiError ? requestError.message : 'Không thể tải thêm bài viết.') }
  }

  const publish = async (nextPublished: boolean) => {
    try { setPage(nextPublished ? await pagesApi.publish(page.id) : await pagesApi.unpublish(page.id)) }
    catch (requestError) { setError(requestError instanceof ApiError ? requestError.message : 'Không thể thay đổi trạng thái Trang.') }
  }

  const saveDetails = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const form = new FormData(event.currentTarget)
    try {
      const updated = await pagesApi.update(page.id, { name: String(form.get('name') ?? ''), username: String(form.get('username') ?? ''), category: String(form.get('category') ?? ''), bio: String(form.get('bio') ?? '') || null })
      setPage(updated)
      if (updated.username !== username) navigate(`/pages/${updated.username}`, { replace: true })
    } catch (requestError) { setError(requestError instanceof ApiError ? requestError.message : 'Không thể lưu thông tin Trang.') }
  }

  const setImage = async (slot: 'avatarMediaId' | 'coverMediaId', file: File | undefined) => {
    if (!file) return
    try {
      const mediaId = await mediaApi.uploadFile(file)
      setPage(await pagesApi.setMedia(page.id, { [slot]: mediaId }))
    } catch (requestError) { setError(requestError instanceof ApiError ? requestError.message : 'Không thể cập nhật ảnh này.') }
  }

  const invite = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (!inviteUser) return
    try { await pagesApi.invite(page.id, inviteUser.userId, inviteRole); setInviteUser(null) }
    catch (requestError) { setError(requestError instanceof ApiError ? requestError.message : 'Không thể gửi lời mời vai trò này.') }
  }

  const changeRole = async (member: PageMember, role: PageInvitation['role']) => {
    try { const updated = await pagesApi.changeRole(page.id, member.userId, role); setMembers((current) => current.map((item) => item.userId === member.userId ? updated : item)) }
    catch (requestError) { setError(requestError instanceof ApiError ? requestError.message : 'Không thể thay đổi vai trò này.') }
  }

  const removeMember = async (member: PageMember) => {
    try { await pagesApi.removeMember(page.id, member.userId); setMembers((current) => current.filter((item) => item.userId !== member.userId)) }
    catch (requestError) { setError(requestError instanceof ApiError ? requestError.message : 'Không thể xóa người quản lý này.') }
  }

  const transfer = async () => {
    if (!transferTarget) return
    try { await pagesApi.transferOwnership(page.id, transferTarget.userId); await load(); setTransferTarget(null) }
    catch (requestError) { setError(requestError instanceof ApiError ? requestError.message : 'Không thể chuyển quyền sở hữu.') }
  }

  const removePost = async (postId: string) => {
    try { await postsApi.delete(postId); setPosts((current) => current.filter((post) => post.id !== postId)) }
    catch (requestError) { setError(requestError instanceof ApiError ? requestError.message : 'Không thể xóa bài viết của Trang.') }
  }

  return <main className="mx-auto min-h-screen w-full max-w-6xl px-3 py-5 sm:px-5">
    <Link to="/pages" className="text-sm font-semibold text-primary no-underline">← Trang</Link>
    <section className="mt-3 overflow-hidden rounded-2xl border border-border bg-surface">
      {page.coverUrl ? <img src={resolveProfileImageUrl(page.coverUrl)} alt="" loading="lazy" decoding="async" className="h-48 w-full object-cover sm:h-72" /> : <div className="h-48 bg-primary/15 sm:h-72" />}
      <div className="flex flex-wrap items-end justify-between gap-4 p-5"><div className="flex min-w-0 items-end gap-4"><div className="-mt-14 flex h-24 w-24 shrink-0 items-center justify-center overflow-hidden rounded-full border-4 border-surface bg-primary text-2xl font-bold text-white">{page.avatarUrl ? <img src={resolveProfileImageUrl(page.avatarUrl)} alt="" loading="lazy" decoding="async" className="h-full w-full object-cover" /> : page.name.slice(0, 2).toUpperCase()}</div><div><p className="text-xs font-bold uppercase tracking-widest text-primary">{page.category} · {page.status}</p><h1 className="font-heading text-3xl font-bold text-text">{page.name}</h1><p className="text-sm text-text-muted">@{page.username} · {page.followerCount} người theo dõi {page.viewerRole ? `· Vai trò của bạn: ${page.viewerRole}` : ''}</p></div></div><div className="flex flex-wrap gap-2">{isOwner && <button type="button" onClick={() => void publish(page.status !== 'published')} className="rounded-lg border border-border bg-surface-2 px-4 py-2 text-sm font-semibold text-text cursor-pointer">{page.status === 'published' ? 'Hủy xuất bản' : 'Xuất bản'}</button>}<button type="button" disabled={isWorking} onClick={() => void follow()} className="rounded-lg border-0 bg-primary px-4 py-2 text-sm font-semibold text-white cursor-pointer disabled:opacity-60">{page.isFollowing ? 'Đang theo dõi' : 'Theo dõi'}</button></div></div>
      {page.bio && <p className="px-5 pb-5 text-sm text-text-muted">{page.bio}</p>}
    </section>
    {error && <p className="mt-4 rounded-lg border border-[#e41e3f]/40 bg-[#e41e3f]/10 p-3 text-sm text-[#ff8a9b]">{error}</p>}
    <div className="mt-5 grid gap-5 lg:grid-cols-[minmax(0,1fr)_21rem]"><section className="flex min-w-0 flex-col gap-4">{canPost && <><p className="rounded-lg border border-primary/35 bg-primary/10 px-3 py-2 text-sm font-semibold text-primary">Đăng với tư cách Trang · chỉ hỗ trợ bài viết thường có ảnh hoặc video, không hỗ trợ Reels.</p><NewPostBox identityName={page.name} postingLabel="Đăng với tư cách Trang" onPost={createPost} /></>}{posts.map((post) => <div key={post.id} className="relative"><LivePostCard post={post} currentUserId={session!.user.id} onPostUpdated={(updated) => setPosts((current) => current.map((item) => item.id === updated.id ? updated : item))} onPostDeleted={(postId) => setPosts((current) => current.filter((item) => item.id !== postId))} />{canPost && <button type="button" onClick={() => void removePost(post.id)} className="absolute right-2 top-12 rounded border border-border bg-surface px-2 py-1 text-xs text-[#ff8a9b] cursor-pointer">Xóa bài viết của Trang</button>}</div>)}{posts.length === 0 && <p className="rounded-xl border border-border bg-surface p-5 text-sm text-text-muted">Trang chưa có bài viết.</p>}<PaginationControls hasMore={postsCursor !== null} isLoading={false} error={null} label="Tải thêm bài viết" onLoadMore={() => void loadMorePosts()} /></section>
      <aside className="flex flex-col gap-4"><section className="rounded-xl border border-border bg-surface p-4"><h2 className="font-heading text-lg font-bold text-text">Giới thiệu</h2><p className="mt-2 text-sm text-text-muted">{page.bio || 'Chưa có phần giới thiệu.'}</p><p className="mt-3 text-xs text-text-muted">Quyền quản lý và theo dõi là riêng biệt: người quản lý không tự động theo dõi Trang này.</p></section>
      {canManageMembers && <section className="rounded-xl border border-border bg-surface p-4"><h2 className="font-heading text-lg font-bold text-text">Người quản lý Trang</h2><div className="mt-3 flex flex-col gap-3">{members.map((member) => <div key={member.userId} className="rounded-lg bg-surface-2 p-2 text-sm"><p className="font-semibold text-text">{member.displayName}</p><p className="text-xs text-text-muted">@{member.username} · {member.role}</p>{member.role !== 'owner' && <div className="mt-2 flex flex-wrap gap-2"><select value={member.role} onChange={(event) => void changeRole(member, event.target.value as PageInvitation['role'])} className="rounded border border-border bg-surface px-1.5 py-1 text-xs text-text"><option value="editor">Biên tập viên</option><option value="moderator">Kiểm duyệt viên</option>{isOwner && <option value="admin">Quản trị viên</option>}</select><button type="button" onClick={() => void removeMember(member)} className="border-0 bg-transparent text-xs text-[#ff8a9b] cursor-pointer">Xóa</button>{isOwner && <button type="button" onClick={() => setTransferTarget(member)} className="border-0 bg-transparent text-xs text-primary cursor-pointer">Chuyển quyền sở hữu</button>}</div>}</div>)}</div><form onSubmit={invite} className="mt-4 grid gap-2">{inviteUser ? <div className="flex items-center justify-between rounded bg-surface-2 px-2 py-1.5 text-sm text-text"><span>{inviteUser.displayName}</span><button type="button" onClick={() => setInviteUser(null)} className="border-0 bg-transparent text-xs text-primary">Đổi</button></div> : <UserSearchPicker excludedUserIds={members.map((member) => member.userId)} onSelect={setInviteUser} />}<select value={inviteRole} onChange={(event) => setInviteRole(event.target.value as PageInvitation['role'])} className="rounded border border-border bg-surface-2 px-2 py-1.5 text-sm text-text">{isOwner && <option value="admin">Quản trị viên</option>}<option value="editor">Biên tập viên</option><option value="moderator">Kiểm duyệt viên</option></select><button disabled={!inviteUser} className="rounded-md border-0 bg-surface-2 px-3 py-2 text-sm font-semibold text-primary cursor-pointer disabled:opacity-50">Mời quản lý</button></form></section>}
      {canManageSettings && <section className="rounded-xl border border-border bg-surface p-4"><h2 className="font-heading text-lg font-bold text-text">Cài đặt Trang</h2><form onSubmit={saveDetails} className="mt-3 flex flex-col gap-2"><input name="name" aria-label="Tên Trang" defaultValue={page.name} required className="rounded border border-border bg-surface-2 px-2 py-1.5 text-sm text-text" /><input name="username" aria-label="Tên người dùng" defaultValue={page.username} required className="rounded border border-border bg-surface-2 px-2 py-1.5 text-sm text-text" /><input name="category" aria-label="Danh mục" defaultValue={page.category} required className="rounded border border-border bg-surface-2 px-2 py-1.5 text-sm text-text" /><textarea name="bio" aria-label="Giới thiệu" defaultValue={page.bio ?? ''} className="rounded border border-border bg-surface-2 px-2 py-1.5 text-sm text-text" /><button className="rounded-md border-0 bg-surface-2 px-3 py-2 text-sm font-semibold text-primary cursor-pointer">Lưu thông tin</button></form><label className="mt-3 block cursor-pointer text-sm font-semibold text-primary">Đổi ảnh đại diện<input type="file" accept="image/jpeg,image/png,image/webp" className="hidden" onChange={(event) => void setImage('avatarMediaId', event.target.files?.[0])} /></label><label className="mt-2 block cursor-pointer text-sm font-semibold text-primary">Đổi ảnh bìa<input type="file" accept="image/jpeg,image/png,image/webp" className="hidden" onChange={(event) => void setImage('coverMediaId', event.target.files?.[0])} /></label></section>}
      {isOwner && <button type="button" onClick={() => setIsPagePendingDeletion(true)} className="rounded-lg border border-[#e41e3f]/40 bg-transparent px-4 py-2 text-sm font-semibold text-[#ff8a9b] cursor-pointer">Xóa Trang</button>}</aside></div>
    {transferTarget && <AppDialog title="Chuyển quyền sở hữu Trang?" onClose={() => setTransferTarget(null)}><p className="mt-3 text-sm text-text-muted">{transferTarget.displayName} sẽ trở thành chủ sở hữu Trang này.</p><div className="mt-5 flex justify-end gap-2"><button type="button" onClick={() => setTransferTarget(null)} className="rounded-lg border-0 bg-surface-2 px-4 py-2 text-sm font-semibold text-text hover:bg-surface-3">Hủy</button><button type="button" onClick={() => void transfer()} className="rounded-lg border-0 bg-primary px-4 py-2 text-sm font-semibold text-white hover:brightness-110">Chuyển quyền</button></div></AppDialog>}
    {isPagePendingDeletion && <AppDialog title="Xóa Trang?" onClose={() => setIsPagePendingDeletion(false)}><p className="mt-3 text-sm text-text-muted">Trang này sẽ bị xóa khỏi Fookbase.</p><div className="mt-5 flex justify-end gap-2"><button type="button" onClick={() => setIsPagePendingDeletion(false)} className="rounded-lg border-0 bg-surface-2 px-4 py-2 text-sm font-semibold text-text hover:bg-surface-3">Hủy</button><button type="button" onClick={() => { setIsPagePendingDeletion(false); void pagesApi.delete(page.id).then(() => navigate('/pages')).catch((requestError: unknown) => setError(requestError instanceof ApiError ? requestError.message : 'Không thể xóa Trang này.')) }} className="rounded-lg border-0 bg-[#e41e3f] px-4 py-2 text-sm font-semibold text-white hover:brightness-110">Xóa</button></div></AppDialog>}
  </main>
}
