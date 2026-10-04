import { useEffect, useId, useLayoutEffect, useRef, useState, type KeyboardEvent, type PointerEvent } from 'react'
import { createPortal } from 'react-dom'
import { reactionChoices, type ReactionType } from './reactionChoices'
import { useAnchoredPopup } from './useAnchoredPopup'
import './postInteractions.css'

interface PostReactionPickerProps {
  viewerReaction: string | null
  onToggleDefault: () => void
  onSelect: (type: ReactionType) => void
  animationVersion?: number
  disabled?: boolean
  compact?: boolean
}

export default function PostReactionPicker({ viewerReaction, onToggleDefault, onSelect, animationVersion = 0, disabled = false, compact = false }: PostReactionPickerProps) {
  const [open, setOpen] = useState(false)
  const triggerRef = useRef<HTMLButtonElement>(null)
  const popupRef = useRef<HTMLDivElement>(null)
  const pressTimer = useRef<ReturnType<typeof setTimeout> | null>(null)
  const closeTimer = useRef<ReturnType<typeof setTimeout> | null>(null)
  const pressOrigin = useRef<{ x: number; y: number } | null>(null)
  const skipClick = useRef(false)
  const focusOnOpen = useRef(false)
  const popupId = useId()
  const position = useAnchoredPopup(open, triggerRef, popupRef, 'above')
  const selected = reactionChoices.find(({ type }) => type === viewerReaction)

  const clearPress = () => { clearTimeout(pressTimer.current ?? undefined); pressTimer.current = null }
  const keepOpen = () => { clearTimeout(closeTimer.current ?? undefined) }
  const close = (restoreFocus = false) => {
    keepOpen()
    clearPress()
    setOpen(false)
    if (restoreFocus) triggerRef.current?.focus()
  }
  const show = (focus = false) => {
    if (disabled) return
    keepOpen()
    focusOnOpen.current = focus
    setOpen(true)
    if (focus && open) {
      const selectedButton = popupRef.current?.querySelector<HTMLButtonElement>('[aria-checked="true"]')
        ?? popupRef.current?.querySelector<HTMLButtonElement>('button')
      selectedButton?.focus()
    }
  }
  const leave = () => {
    keepOpen()
    closeTimer.current = setTimeout(() => {
      if (!popupRef.current?.contains(document.activeElement)) setOpen(false)
    }, 140)
  }

  useEffect(() => () => {
    clearTimeout(pressTimer.current ?? undefined)
    clearTimeout(closeTimer.current ?? undefined)
  }, [])

  useLayoutEffect(() => {
    if (open && position && focusOnOpen.current) {
      const buttons = popupRef.current?.querySelectorAll<HTMLButtonElement>('button')
      const index = Math.max(0, reactionChoices.findIndex(({ type }) => type === viewerReaction))
      buttons?.[index]?.focus()
      focusOnOpen.current = false
    }
  }, [open, position, viewerReaction])

  useEffect(() => {
    if (!open) return
    const outside = (event: globalThis.PointerEvent) => {
      const target = event.target as Node
      if (!triggerRef.current?.contains(target) && !popupRef.current?.contains(target)) setOpen(false)
    }
    const escape = (event: globalThis.KeyboardEvent) => {
      if (event.key !== 'Escape') return
      event.preventDefault()
      event.stopImmediatePropagation()
      setOpen(false)
      triggerRef.current?.focus()
    }
    document.addEventListener('pointerdown', outside)
    document.addEventListener('keydown', escape, true)
    return () => {
      document.removeEventListener('pointerdown', outside)
      document.removeEventListener('keydown', escape, true)
    }
  }, [open])

  const onPointerDown = (event: PointerEvent<HTMLButtonElement>) => {
    if (event.pointerType !== 'touch' || disabled) return
    clearPress()
    skipClick.current = false
    pressOrigin.current = { x: event.clientX, y: event.clientY }
    pressTimer.current = setTimeout(() => {
      skipClick.current = true
      show()
    }, 450)
  }

  const navigate = (event: KeyboardEvent<HTMLDivElement>) => {
    const buttons = Array.from(popupRef.current?.querySelectorAll<HTMLButtonElement>('button') ?? [])
    const index = buttons.indexOf(document.activeElement as HTMLButtonElement)
    let next = index
    if (event.key === 'ArrowRight' || event.key === 'ArrowDown') next = (index + 1) % buttons.length
    else if (event.key === 'ArrowLeft' || event.key === 'ArrowUp') next = (index - 1 + buttons.length) % buttons.length
    else if (event.key === 'Home') next = 0
    else if (event.key === 'End') next = buttons.length - 1
    else if (event.key === 'Tab') { close(true); return }
    else return
    event.preventDefault()
    buttons[next]?.focus()
  }

  return <div className={compact ? 'inline-flex' : 'min-w-0'}>
    <button
      ref={triggerRef}
      type="button"
      disabled={disabled}
      data-post-reaction
      aria-label={selected ? `Bỏ cảm xúc ${selected.label}` : 'Thích'}
      aria-pressed={Boolean(viewerReaction)}
      aria-haspopup="menu"
      aria-expanded={open}
      aria-controls={open ? popupId : undefined}
      onPointerEnter={(event) => { if (event.pointerType === 'mouse') show() }}
      onPointerLeave={(event) => { clearPress(); if (event.pointerType === 'mouse') leave() }}
      onPointerDown={onPointerDown}
      onPointerMove={(event) => {
        const origin = pressOrigin.current
        if (origin && Math.hypot(event.clientX - origin.x, event.clientY - origin.y) > 10) clearPress()
      }}
      onPointerUp={clearPress}
      onPointerCancel={() => { clearPress(); skipClick.current = false; pressOrigin.current = null }}
      onContextMenu={(event) => { if (skipClick.current) event.preventDefault() }}
      onKeyDown={(event) => {
        if (event.key === 'ArrowDown' || event.key === 'ArrowUp') { event.preventDefault(); show(true) }
      }}
      onClick={() => {
        if (skipClick.current) { skipClick.current = false; return }
        if (compact && !selected) { show(true); return }
        close()
        onToggleDefault()
      }}
      className={`post-action-focus flex items-center justify-center gap-2 rounded-lg border-0 text-sm font-semibold transition-colors disabled:cursor-wait disabled:opacity-60 ${compact ? 'bg-transparent p-0 text-xs' : 'w-full py-2'} ${selected ? `${selected.color} ${compact ? '' : 'bg-primary/10'}` : 'text-text-muted hover:bg-surface-2'}`}
    >
      {!compact && <span key={animationVersion} className={`inline-flex h-5 w-5 items-center justify-center text-[19px] leading-none ${animationVersion > 0 ? 'post-reaction-pulse' : ''}`} aria-hidden="true">
        {selected?.icon ?? <svg viewBox="0 0 24 24" className="h-5 w-5 fill-none stroke-current" strokeWidth="1.9" strokeLinecap="round" strokeLinejoin="round"><path d="M7.1 21H4.4a1.4 1.4 0 0 1-1.4-1.4v-7.2A1.4 1.4 0 0 1 4.4 11H7l2.2-6.2a2.05 2.05 0 0 1 4 .7l-.3 4.5h4.3a2.8 2.8 0 0 1 2.7 3.5l-1.2 5a3.2 3.2 0 0 1-3.1 2.5H7.1V11" /></svg>}
      </span>}
      {compact && selected ? `${selected.icon} ` : ''}{selected?.label ?? 'Thích'}
    </button>
    {open && createPortal(<div
      ref={popupRef}
      id={popupId}
      role="menu"
      aria-label={compact ? 'Chọn cảm xúc cho bình luận' : 'Chọn cảm xúc'}
      className="post-popup post-reaction-picker fixed z-[85] flex max-w-[calc(100vw-1rem)] items-center gap-0.5 rounded-full border border-border bg-surface p-1.5 shadow-xl"
      style={{ ...position, visibility: position ? 'visible' : 'hidden' }}
      onPointerEnter={keepOpen}
      onPointerLeave={(event) => { if (event.pointerType === 'mouse') leave() }}
      onKeyDown={navigate}
      onBlur={(event) => {
        if (!event.currentTarget.contains(event.relatedTarget as Node) && event.relatedTarget !== triggerRef.current) close()
      }}
    >
      {reactionChoices.map(({ type, icon, label }) => <button
        key={type}
        type="button"
        role="menuitemradio"
        aria-checked={viewerReaction === type}
        aria-label={label}
        data-label={label}
        className="post-reaction-choice post-action-focus relative grid h-10 w-10 shrink-0 place-items-center rounded-full border-0 bg-transparent p-0 text-[26px] leading-none hover:bg-surface-2"
        onClick={() => { close(true); onSelect(type) }}
      ><span aria-hidden="true">{icon}</span><span className="post-reaction-tooltip" aria-hidden="true">{label}</span></button>)}
    </div>, document.body)}
  </div>
}
