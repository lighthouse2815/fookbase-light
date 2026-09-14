export type ReactionType = 'like' | 'love' | 'haha' | 'wow' | 'sad' | 'angry'

export const reactionChoices: ReadonlyArray<{ type: ReactionType; icon: string; label: string; color: string }> = [
  { type: 'like', icon: '👍', label: 'Thích', color: 'text-[#1877f2]' },
  { type: 'love', icon: '❤️', label: 'Yêu thích', color: 'text-[#f33e58]' },
  { type: 'haha', icon: '😆', label: 'Haha', color: 'text-[#f7b125]' },
  { type: 'wow', icon: '😮', label: 'Wow', color: 'text-[#f7b125]' },
  { type: 'sad', icon: '😢', label: 'Buồn', color: 'text-[#f7b125]' },
  { type: 'angry', icon: '😡', label: 'Phẫn nộ', color: 'text-[#e9710f]' },
]
