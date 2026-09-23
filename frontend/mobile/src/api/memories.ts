import { apiRequest } from './client'
import type { Post } from './posts'

export interface MemoryYear {
  year: number
  yearsAgo: number
  items: Post[]
}

export interface MemoriesToday {
  date: string
  years: MemoryYear[]
}

export const memoriesApi = {
  getToday: () => apiRequest<MemoriesToday>('/api/memories/today'),
}
