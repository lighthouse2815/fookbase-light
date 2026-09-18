import { useEffect, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { ApiError } from '../../api/client'
import { postsApi, type Post } from '../../api/posts'
import { useAuth } from '../../auth/useAuth'
import LivePostCard from '../feed/components/LivePostCard'
import { Mascot } from 'page-mascot'

export default function PostDetailPage() {
  const { postId = '' } = useParams()
  const navigate = useNavigate()
  const { session } = useAuth()
  const [post, setPost] = useState<Post | null>(null)
  const [error, setError] = useState<ApiError | null>(null)

  useEffect(() => {
    let active = true
    void postsApi.getById(postId).then((item) => { if (active) setPost(item) }).catch((reason: unknown) => {
      if (active) setError(reason instanceof ApiError ? reason : new ApiError('Không thể tải bài viết.', 0))
    })
    return () => { active = false }
  }, [postId])

  if (error?.status === 403 || error?.status === 404) return (
    <main className="mx-auto flex max-w-3xl flex-col items-center gap-3 p-10 text-center text-text-muted">
      <Mascot directions="/mascots/owl-directions.webp" reactions="/mascots/owl-reactions.webp" size={80} label="Post owl mascot" />
      <p>Nội dung này không còn khả dụng.</p>
    </main>
  )
  if (error) return (
    <main className="mx-auto flex max-w-3xl flex-col items-center gap-3 p-10 text-center text-[#ff8a9b]">
      <Mascot directions="/mascots/owl-directions.webp" reactions="/mascots/owl-reactions.webp" size={80} label="Post owl mascot" />
      <p>{error.message}</p>
    </main>
  )
  if (!post || !session) return (
    <main className="mx-auto flex max-w-3xl flex-col items-center gap-3 p-10 text-center text-text-muted">
      <Mascot directions="/mascots/owl-directions.webp" reactions="/mascots/owl-reactions.webp" size={80} label="Post owl mascot" />
      <p>Đang tải bài viết…</p>
    </main>
  )
  const match = /^#comment-([\da-f-]{36})$/i.exec(window.location.hash)
  return (
    <main className="mx-auto max-w-3xl p-3 sm:p-5">
      <div className="mb-3 flex items-center justify-between">
        <button type="button" onClick={() => navigate(-1)} className="flex items-center gap-1.5 rounded-lg border-0 bg-transparent px-2 py-1 text-sm font-semibold text-text-muted hover:text-text cursor-pointer">
          <span>←</span> Quay lại
        </button>
        <Mascot directions="/mascots/owl-directions.webp" reactions="/mascots/owl-reactions.webp" size={40} label="Post owl mascot" />
      </div>
      <LivePostCard post={post} currentUserId={session.user.id} initialCommentId={match?.[1]} onPostUpdated={setPost} onPostDeleted={() => navigate('/feed', { replace: true })} />
    </main>
  )
}
