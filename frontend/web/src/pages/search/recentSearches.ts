export function normalizeRecentSearches(value: unknown): string[] {
  if (!Array.isArray(value)) return []
  const seen = new Set<string>()
  const queries: string[] = []
  for (const item of value) {
    if (typeof item !== 'string') continue
    const query = item.trim()
    const key = query.toLocaleLowerCase('vi')
    if (!query || query.length > 100 || seen.has(key)) continue
    seen.add(key)
    queries.push(query)
    if (queries.length === 10) break
  }
  return queries
}

export function addRecentSearch(current: readonly string[], query: string) {
  return normalizeRecentSearches([query, ...current])
}

export function readRecentSearches(userId: string): string[] {
  try { return normalizeRecentSearches(JSON.parse(localStorage.getItem(`fookbase.search.recent.${userId}`) ?? '[]')) }
  catch { return [] }
}

export function writeRecentSearches(userId: string, queries: readonly string[]) {
  try {
    const key = `fookbase.search.recent.${userId}`
    const normalized = normalizeRecentSearches(queries)
    if (normalized.length) localStorage.setItem(key, JSON.stringify(normalized))
    else localStorage.removeItem(key)
  } catch { /* History storage must never prevent searching. */ }
}
