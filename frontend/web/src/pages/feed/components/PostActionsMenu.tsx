import { useEffect, useId, useLayoutEffect, useRef, useState, type KeyboardEvent, type ReactNode } from 'react'
import { createPortal } from 'react-dom'
import { useAnchoredPopup } from './useAnchoredPopup'

export default function PostActionsMenu({ label, icon, children }: { label: string; icon: ReactNode; children: ReactNode }) {
  const [open, setOpen] = useState(false)
  const triggerRef = useRef<HTMLButtonElement>(null)
  const popupRef = useRef<HTMLDivElement>(null)
  const focusOnOpen = useRef<'first' | 'last' | null>(null)
  const popupId = useId()
  const position = useAnchoredPopup(open, triggerRef, popupRef, 'below')

  useLayoutEffect(() => {
    if (!open || !position) return
    const buttons = popupRef.current?.querySelectorAll<HTMLButtonElement>('button:not([disabled])')
    if (focusOnOpen.current) {
      buttons?.[focusOnOpen.current === 'first' ? 0 : buttons.length - 1]?.focus()
      focusOnOpen.current = null
    }
  }, [open, position])

  useEffect(() => {
    if (!open) return
    const outside = (event: PointerEvent) => {
      const target = event.target as Node
      if (!popupRef.current?.contains(target) && !triggerRef.current?.contains(target)) setOpen(false)
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

  const navigate = (event: KeyboardEvent<HTMLDivElement>) => {
    const buttons = Array.from(popupRef.current?.querySelectorAll<HTMLButtonElement>('button:not([disabled])') ?? [])
    const index = buttons.indexOf(document.activeElement as HTMLButtonElement)
    let next = index
    if (event.key === 'ArrowDown') next = (index + 1) % buttons.length
    else if (event.key === 'ArrowUp') next = (index - 1 + buttons.length) % buttons.length
    else if (event.key === 'Home') next = 0
    else if (event.key === 'End') next = buttons.length - 1
    else if (event.key === 'Tab') { setOpen(false); triggerRef.current?.focus(); return }
    else return
    event.preventDefault()
    buttons[next]?.focus()
  }

  return <>
    <button ref={triggerRef} type="button" onClick={() => {
      focusOnOpen.current = 'first'
      setOpen((current) => !current)
    }} onKeyDown={(event) => {
      if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
        event.preventDefault()
        focusOnOpen.current = event.key === 'ArrowDown' ? 'first' : 'last'
        setOpen(true)
      }
    }} className="post-action-focus grid h-9 w-9 shrink-0 place-items-center rounded-full border-0 bg-transparent text-text-muted transition-colors hover:bg-surface-2 hover:text-text" aria-label={label} aria-haspopup="menu" aria-expanded={open} aria-controls={popupId}>{icon}</button>
    {createPortal(<div
      ref={popupRef}
      id={popupId}
      role="menu"
      aria-label={label}
      aria-hidden={!open}
      inert={!open}
      className={`post-popup post-actions-menu fixed z-[85] max-h-[calc(100dvh-1rem)] min-w-56 max-w-[calc(100vw-1rem)] overflow-y-auto rounded-xl border border-border bg-surface p-1.5 shadow-2xl ${open ? '' : 'post-popup-closed'}`}
      style={{ ...position, visibility: open && position ? 'visible' : 'hidden' }}
      onKeyDown={navigate}
      onClick={(event) => {
        if (popupRef.current?.contains(event.target as Node) && (event.target as HTMLElement).closest('button')) {
          // Focus the persistent trigger before a modal captures its return target.
          triggerRef.current?.focus()
          setOpen(false)
        }
      }}
      onBlur={(event) => {
        if (!event.currentTarget.contains(event.relatedTarget as Node) && event.relatedTarget !== triggerRef.current) setOpen(false)
      }}
    >{children}</div>, document.body)}
  </>
}
