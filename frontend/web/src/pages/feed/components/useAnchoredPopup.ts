import { useLayoutEffect, useState, type RefObject } from 'react'

// Both post menus live in portals so cards, dialogs and image containers cannot clip them.
export function useAnchoredPopup(
  open: boolean,
  anchor: RefObject<HTMLButtonElement | null>,
  popup: RefObject<HTMLDivElement | null>,
  placement: 'above' | 'below',
  onOpenChange: (open: boolean) => void,
) {
  const [position, setPosition] = useState<{ left: number; top: number } | null>(null)
  useLayoutEffect(() => {
    if (!open) return
    let frame = 0
    const measure = () => {
      const button = anchor.current
      const menu = popup.current
      if (!button || !menu) return
      const rect = button.getBoundingClientRect()
      if (rect.bottom <= 0 || rect.top >= window.innerHeight || rect.right <= 0 || rect.left >= window.innerWidth) {
        onOpenChange(false)
        return
      }
      const width = menu.offsetWidth
      const height = menu.offsetHeight
      const left = placement === 'above' ? rect.left + (rect.width - width) / 2 : rect.right - width
      const top = placement === 'above' ? rect.top - height - 8 : rect.bottom + 6
      setPosition({
        left: Math.max(8, Math.min(left, window.innerWidth - width - 8)),
        top: Math.max(8, Math.min(top, window.innerHeight - height - 8)),
      })
    }
    const schedule = () => {
      window.cancelAnimationFrame(frame)
      frame = window.requestAnimationFrame(measure)
    }
    measure()
    window.addEventListener('resize', schedule)
    window.addEventListener('scroll', schedule, true)
    return () => {
      window.cancelAnimationFrame(frame)
      window.removeEventListener('resize', schedule)
      window.removeEventListener('scroll', schedule, true)
    }
  }, [open, anchor, popup, placement, onOpenChange])
  return position
}
