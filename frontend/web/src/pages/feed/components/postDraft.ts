export type PostPrivacy = 'public' | 'friends' | 'onlyMe'

export interface PostDraft {
  content: string
  privacy: PostPrivacy
  textBackground: string | null
  hasAttachments: boolean
}

export function postDraftKey(userId: string, context: string) {
  return `fookbase.post-draft.${encodeURIComponent(userId).replaceAll('.', '%2E')}.${encodeURIComponent(context).replaceAll('.', '%2E')}`
}

export function readPostDraft(key: string): PostDraft | null {
  try {
    const draft: unknown = JSON.parse(sessionStorage.getItem(key) ?? 'null')
    if (!draft || typeof draft !== 'object') return null
    const value = draft as Partial<PostDraft>
    if (typeof value.content !== 'string' || !['public', 'friends', 'onlyMe'].includes(value.privacy ?? '')) return null
    return {
      content: value.content,
      privacy: value.privacy as PostPrivacy,
      textBackground: typeof value.textBackground === 'string' ? value.textBackground : null,
      hasAttachments: value.hasAttachments === true,
    }
  } catch {
    return null
  }
}

export function savePostDraft(key: string, draft: PostDraft) {
  try {
    if (draft.content || draft.hasAttachments || draft.textBackground || draft.privacy !== 'public') {
      sessionStorage.setItem(key, JSON.stringify(draft))
    } else {
      sessionStorage.removeItem(key)
    }
  } catch {
    // The composer remains usable when the browser disables storage.
  }
}
