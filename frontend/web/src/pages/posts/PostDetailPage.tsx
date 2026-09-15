import { useEffect, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { ApiError } from '../../api/client'
import { postsApi, type Post } from '../../api/posts'
import { useAuth } from '../../auth/useAuth'
import LivePostCard from '../feed/components/LivePostCard'

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

  if (error?.status === 403 || error?.status === 404) return <main className="mx-auto max-w-3xl p-5 text-center text-text-muted">Nội dung này không còn khả dụng.</main>
  if (error) return <main className="mx-auto max-w-3xl p-5 text-center text-[#ff8a9b]">{error.message}</main>
  if (!post || !session) return <main className="p-5 text-text-muted">Đang tải bài viết…</main>
  const match = /^#comment-([\da-f-]{36})$/i.exec(window.location.hash)
  return <main className="mx-auto max-w-3xl p-3 sm:p-5"><LivePostCard post={post} currentUserId={session.user.id} initialCommentId={match?.[1]} onPostUpdated={setPost} onPostDeleted={() => navigate('/feed', { replace: true })} /></main>
}
