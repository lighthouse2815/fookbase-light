import type { SearchSuggestions, SearchType } from '../../api/search'
import { publicProfileHandle } from '../../shared/publicProfileHandle.ts'

type DestinationType = Exclude<SearchType, 'all'> | 'events'
export function getSearchDestination(type: DestinationType, identifier: string) {
  const id = encodeURIComponent(identifier)
  if (type === 'people') return `/profile/${id}`
  if (type === 'reels') return `/reels?reel=${id}`
  return `/${type}/${id}`
}

export interface SearchSuggestion {
  key: string
  label: string
  secondary: string
  avatarUrl: string | null
  destination: string
  category: string
}

export function flattenSearchSuggestions(data: SearchSuggestions): SearchSuggestion[] {
  const seen = new Set<string>()
  const rows: SearchSuggestion[] = []
  const add = (row: SearchSuggestion) => {
    if (!seen.has(row.key)) { seen.add(row.key); rows.push(row) }
  }
  for (const person of data.people) {
    const handle = publicProfileHandle(person.username)
    add({ key: `people:${person.userId}`, label: person.displayName, secondary: handle ? `@${handle}` : 'Trang cá nhân', avatarUrl: person.avatarUrl, destination: getSearchDestination('people', person.userId), category: 'Mọi người' })
  }
  for (const group of data.groups) add({ key: `groups:${group.groupId}`, label: group.name, secondary: `${group.memberCount} thành viên`, avatarUrl: group.coverUrl, destination: getSearchDestination('groups', group.groupId), category: 'Nhóm' })
  for (const page of data.pages) add({ key: `pages:${page.pageId}`, label: page.name, secondary: `@${page.username}`, avatarUrl: page.avatarUrl, destination: getSearchDestination('pages', page.username), category: 'Trang' })
  return rows
}

export function getNextSearchIndex(index: number, count: number, direction: -1 | 1) {
  if (count < 1) return -1
  if (index < 0) return direction > 0 ? 0 : count - 1
  return Math.max(0, Math.min(count - 1, index + direction))
}

const fold = (text: string) => text.normalize('NFD').replace(/\p{M}/gu, '').replace(/[đĐ]/g, 'd').toLocaleLowerCase('vi')

export function getHighlightParts(text: string, query: string): Array<{ text: string; match: boolean }> {
  const needle = fold(query.trim())
  if (!needle) return [{ text, match: false }]
  let normalized = ''
  const positions: Array<{ start: number; end: number }> = []
  for (const { segment, index } of new Intl.Segmenter('vi', { granularity: 'grapheme' }).segment(text)) {
    const value = fold(segment)
    normalized += value
    for (let i = 0; i < value.length; i++) positions.push({ start: index, end: index + segment.length })
  }
  const parts: Array<{ text: string; match: boolean }> = []
  let searchFrom = 0
  let textFrom = 0
  for (let found = normalized.indexOf(needle); found >= 0; found = normalized.indexOf(needle, searchFrom)) {
    const start = positions[found].start
    const end = positions[found + needle.length - 1].end
    if (start >= textFrom) {
      if (start > textFrom) parts.push({ text: text.slice(textFrom, start), match: false })
      parts.push({ text: text.slice(start, end), match: true })
      textFrom = end
    }
    searchFrom = found + needle.length
  }
  if (textFrom < text.length || parts.length === 0) parts.push({ text: text.slice(textFrom), match: false })
  return parts
}
