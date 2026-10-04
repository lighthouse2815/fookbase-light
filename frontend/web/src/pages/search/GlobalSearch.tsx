import { useEffect, useId, useRef, useState, type KeyboardEvent } from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { searchApi } from '../../api/search'
import { resolveProfileImageUrl } from '../../api/users'
import { usePreferences } from '../../preferences'
import HighlightedText from './HighlightedText'
import { flattenSearchSuggestions, getNextSearchIndex, type SearchSuggestion } from './searchPresentation'
import { addRecentSearch, readRecentSearches, writeRecentSearches } from './recentSearches'

interface SuggestionsState {
  query: string
  status: 'idle' | 'loading' | 'ready' | 'error'
  items: SearchSuggestion[]
}

export default function GlobalSearch({ onOpen, userId }: { onOpen: () => void; userId: string }) {
  const location = useLocation()
  const navigate = useNavigate()
  const { t } = usePreferences()
  const [query, setQuery] = useState('')
  const [isOpen, setIsOpen] = useState(false)
  const [activeIndex, setActiveIndex] = useState(-1)
  const [retry, setRetry] = useState(0)
  const [response, setResponse] = useState<SuggestionsState>({ query: '', status: 'idle', items: [] })
  const [locationKey, setLocationKey] = useState(location.key)
  const [recent, setRecent] = useState(() => readRecentSearches(userId))
  const inputRef = useRef<HTMLInputElement>(null)
  const wrapperRef = useRef<HTMLDivElement>(null)
  const generationRef = useRef(0)
  const listId = useId()
  const trimmed = query.trim()
  const eligible = trimmed.length >= 2
  const ready = response.query === trimmed && response.status === 'ready'
  const failed = response.query === trimmed && response.status === 'error'
  const loading = eligible && !ready && !failed
  const items = ready ? response.items : []
  const count = eligible ? items.length + 1 : 0
  const optionId = (index: number) => `${listId}-${index}`

  if (locationKey !== location.key) {
    setLocationKey(location.key)
    setQuery(location.pathname === '/search' ? new URLSearchParams(location.search).get('q') ?? '' : '')
    setIsOpen(false)
    setActiveIndex(-1)
  }

  const close = () => { setIsOpen(false); setActiveIndex(-1) }
  const updateRecent = (next: string[]) => { setRecent(next); writeRecentSearches(userId, next) }
  const search = (value = trimmed) => {
    const submitted = value.trim()
    if (submitted.length >= 2 && submitted.length <= 100) updateRecent(addRecentSearch(recent, submitted))
    close()
    navigate(submitted ? `/search?q=${encodeURIComponent(submitted)}` : '/search')
  }
  const select = (index: number) => {
    const item = items[index]
    if (item) { close(); navigate(item.destination) }
    else search()
  }

  useEffect(() => {
    if (!isOpen || !eligible) return
    const generation = ++generationRef.current
    const controller = new AbortController()
    const timer = window.setTimeout(() => {
      setResponse({ query: trimmed, status: 'loading', items: [] })
      void searchApi.suggestions(trimmed, { signal: controller.signal }).then((data) => {
        if (controller.signal.aborted || generation !== generationRef.current) return
        setResponse({ query: trimmed, status: 'ready', items: flattenSearchSuggestions(data) })
        setActiveIndex(-1)
      }).catch(() => {
        if (controller.signal.aborted || generation !== generationRef.current) return
        setResponse({ query: trimmed, status: 'error', items: [] })
        setActiveIndex(-1)
      })
    }, 300)
    return () => { controller.abort(); window.clearTimeout(timer); generationRef.current += 1 }
  }, [trimmed, isOpen, eligible, retry])

  useEffect(() => {
    if (!isOpen) return
    const outside = (event: PointerEvent) => {
      if (!wrapperRef.current?.contains(event.target as Node)) { setIsOpen(false); setActiveIndex(-1) }
    }
    document.addEventListener('pointerdown', outside, true)
    return () => document.removeEventListener('pointerdown', outside, true)
  }, [isOpen])

  useEffect(() => {
    if (isOpen && activeIndex >= 0) document.getElementById(`${listId}-${activeIndex}`)?.scrollIntoView({ block: 'nearest' })
  }, [isOpen, activeIndex, listId])

  const keyDown = (event: KeyboardEvent<HTMLInputElement>) => {
    if (event.nativeEvent.isComposing) { if (event.key === 'Enter') event.preventDefault(); return }
    if (event.altKey || event.ctrlKey || event.metaKey) return
    if (event.key === 'Escape' && isOpen) { event.preventDefault(); event.stopPropagation(); close() }
    else if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
      if (!eligible) return
      event.preventDefault()
      setIsOpen(true)
      setActiveIndex(getNextSearchIndex(isOpen ? activeIndex : -1, count, event.key === 'ArrowDown' ? 1 : -1))
    } else if (event.key === 'Enter' && isOpen && activeIndex >= 0) { event.preventDefault(); select(activeIndex) }
  }

  return <div ref={wrapperRef} className="relative min-w-0 flex-1 max-sm:hidden" onBlur={(event) => { if (!event.currentTarget.contains(event.relatedTarget)) close() }}>
    <form role="search" onSubmit={(event) => { event.preventDefault(); search() }} className="relative">
      <span aria-hidden="true" className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-text-light">⌕</span>
      <input ref={inputRef} type="search" role="combobox" aria-autocomplete="list" aria-label={t('searchFookbase')} aria-expanded={isOpen && eligible} aria-controls={isOpen && eligible ? listId : undefined} aria-activedescendant={isOpen && activeIndex >= 0 ? optionId(activeIndex) : undefined} autoComplete="off" maxLength={100} value={query} onChange={(event) => {
        setQuery(event.target.value); setActiveIndex(-1); setResponse({ query: '', status: 'idle', items: [] }); setIsOpen(true)
      }} onFocus={() => { onOpen(); setRecent(readRecentSearches(userId)); setIsOpen(true) }} onKeyDown={keyDown} placeholder={t('searchFookbase')} className="h-10 w-full rounded-full border-0 bg-surface-2 py-2 pl-9 pr-11 text-[13px] text-text outline-none placeholder:text-text-light focus:ring-2 focus:ring-primary [&::-webkit-search-cancel-button]:hidden" />
      {query && <button type="button" aria-label="Xóa nội dung tìm kiếm" onClick={() => { setQuery(''); setActiveIndex(-1); setResponse({ query: '', status: 'idle', items: [] }); setIsOpen(true); inputRef.current?.focus() }} className="absolute right-0 top-0 grid h-10 w-10 place-items-center rounded-full border-0 bg-transparent text-lg text-text-muted hover:bg-surface-3 focus-visible:ring-2 focus-visible:ring-primary">×</button>}
    </form>
    {isOpen && <div className="absolute left-0 top-12 z-[60] w-[max(100%,20rem)] max-w-[calc(100vw-1rem)] overflow-hidden rounded-2xl border border-border bg-surface shadow-xl">
      {loading && <div role="status" aria-label="Đang tải gợi ý" className="space-y-2 p-3"><span className="sr-only">Đang tải gợi ý…</span>{Array.from({ length: 4 }, (_, index) => <div key={index} aria-hidden="true" className="flex items-center gap-3 motion-safe:animate-pulse"><span className="h-10 w-10 shrink-0 rounded-full bg-surface-2" /><span className="flex-1 space-y-2"><span className="block h-3 w-3/4 rounded bg-surface-2" /><span className="block h-2.5 w-1/2 rounded bg-surface-2" /></span></div>)}</div>}
      {failed && <div role="status" className="p-4 text-sm text-text-muted"><p>Không thể tải gợi ý</p><button type="button" aria-label="Thử lại gợi ý" onClick={() => { setResponse({ query: '', status: 'idle', items: [] }); setRetry((value) => value + 1) }} className="mt-2 min-h-11 rounded-lg border-0 bg-surface-2 px-3 font-semibold text-primary hover:bg-surface-3">Thử lại</button></div>}
      {eligible && ready && items.length === 0 && <p role="status" className="px-4 py-4 text-sm text-text-muted">Không tìm thấy gợi ý cho “{trimmed}”</p>}
      {trimmed.length === 1 && <p className="px-4 py-4 text-sm text-text-muted">Nhập ít nhất 2 ký tự để tìm kiếm.</p>}
      {!trimmed && <section aria-label="Tìm kiếm gần đây" className="p-2">
        <div className="flex items-center justify-between gap-2 px-2"><h2 className="text-sm font-semibold text-text">Tìm kiếm gần đây</h2>{recent.length > 0 && <button type="button" onClick={() => { updateRecent([]); inputRef.current?.focus() }} className="min-h-11 shrink-0 rounded-lg border-0 bg-transparent px-2 text-xs font-semibold text-primary hover:bg-surface-2 focus-visible:ring-2 focus-visible:ring-primary">Xóa tất cả</button>}</div>
        {recent.length === 0 && <p className="px-2 py-3 text-sm text-text-muted">Chưa có tìm kiếm gần đây.</p>}
        {recent.map((value) => <div key={value} className="flex items-center rounded-xl hover:bg-surface-2"><button type="button" aria-label={`Tìm lại “${value}”`} onClick={() => search(value)} className="flex min-h-12 min-w-0 flex-1 items-center gap-3 rounded-xl border-0 bg-transparent px-2 py-2 text-left text-sm text-text focus-visible:ring-2 focus-visible:ring-primary"><span aria-hidden="true" className="grid h-9 w-9 shrink-0 place-items-center rounded-full bg-surface-2 text-text-muted">◷</span><span className="truncate">{value}</span></button><button type="button" aria-label={`Xóa tìm kiếm “${value}”`} onClick={() => { updateRecent(recent.filter((item) => item !== value)); inputRef.current?.focus() }} className="grid h-11 w-11 shrink-0 place-items-center rounded-full border-0 bg-transparent text-lg text-text-muted hover:bg-surface-3 focus-visible:ring-2 focus-visible:ring-primary">×</button></div>)}
      </section>}
      {eligible && <div id={listId} role="listbox" aria-label="Gợi ý tìm kiếm" className="max-h-[min(26rem,calc(100dvh-8rem))] overflow-y-auto overscroll-contain p-2">
        {items.map((item, index) => <div key={item.key} role="presentation">
          {items[index - 1]?.category !== item.category && <p aria-hidden="true" className="px-2 pb-1 pt-2 text-xs font-semibold text-text-muted">{item.category}</p>}
          <Link id={optionId(index)} role="option" aria-selected={activeIndex === index} tabIndex={-1} to={item.destination} onMouseMove={() => setActiveIndex(index)} onMouseDown={(event) => event.preventDefault()} onClick={close} className={`flex min-h-14 items-center gap-3 rounded-xl px-2 py-2 text-text no-underline hover:bg-surface-2 ${activeIndex === index ? 'bg-surface-2 ring-1 ring-primary/40' : ''}`}>
            <span className="grid h-10 w-10 shrink-0 place-items-center overflow-hidden rounded-full bg-primary/15 text-xs font-semibold text-primary">{item.avatarUrl ? <img src={resolveProfileImageUrl(item.avatarUrl)} alt="" loading="lazy" decoding="async" className="h-full w-full object-cover" /> : item.label.slice(0, 2).toUpperCase()}</span>
            <span className="min-w-0 flex-1"><span className="block truncate text-sm font-semibold"><HighlightedText text={item.label} query={trimmed} /></span><span className="block truncate text-xs text-text-muted"><HighlightedText text={item.secondary} query={trimmed} /></span></span>
          </Link>
        </div>)}
        <button id={optionId(items.length)} role="option" aria-selected={activeIndex === items.length} tabIndex={-1} type="button" onMouseMove={() => setActiveIndex(items.length)} onMouseDown={(event) => event.preventDefault()} onClick={() => search()} className={`mt-1 min-h-11 w-full rounded-xl border-0 px-3 py-2 text-left text-sm font-semibold text-primary hover:bg-surface-2 ${activeIndex === items.length ? 'bg-surface-2 ring-1 ring-primary/40' : 'bg-transparent'}`}>Xem tất cả kết quả cho “{trimmed}”</button>
      </div>}
    </div>}
  </div>
}
