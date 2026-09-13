import { apiRequest } from './client'
import type { Post } from './posts'

export interface MemoryYear {
  year: number
  yearsAgo: number
  items: Post[]
}

export interface MemoryToday {
  date: string
  years: MemoryYear[]
}

export const memoriesApi = {
  getToday: () => apiRequest<MemoryToday>('/api/memories/today'),
}
