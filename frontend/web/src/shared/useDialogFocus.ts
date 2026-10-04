import { useEffect, useRef, type RefObject } from 'react'

const dialogs: HTMLElement[] = []
let originalOverflow = ''
const focusableSelector = 'button:not([disabled]), [href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'
const focusableElements = (dialog: HTMLElement) => Array.from(dialog.querySelectorAll<HTMLElement>(focusableSelector))
  .filter((element) => element.getClientRects().length > 0 && !element.closest('[inert]'))

// Share AppDialog's focus behavior with the existing full-size post/photo dialogs.
export function useDialogFocus(open: boolean, ref: RefObject<HTMLElement | null>, onClose: () => void) {
  const onCloseRef = useRef(onClose)
  useEffect(() => { onCloseRef.current = onClose })
  useEffect(() => {
    const dialog = ref.current
    if (!open || !dialog) return
    const previousFocus = document.activeElement instanceof HTMLElement ? document.activeElement : null
    if (dialogs.length === 0) {
      originalOverflow = document.body.style.overflow
      document.body.style.overflow = 'hidden'
    }
    dialogs.push(dialog)
    const focusable = focusableElements(dialog)
    const preferred = dialog.querySelector<HTMLElement>('[data-dialog-initial-focus]')
    const initial = preferred && focusable.includes(preferred) ? preferred : focusable[0] ?? dialog
    initial.focus({ preventScroll: true })

    const keyDown = (event: KeyboardEvent) => {
      if (dialogs.at(-1) !== dialog || event.defaultPrevented) return
      if (event.key === 'Escape') {
        event.preventDefault()
        event.stopPropagation()
        onCloseRef.current()
        return
      }
      if (event.key !== 'Tab') return
      const focusable = focusableElements(dialog)
      if (focusable.length === 0) {
        event.preventDefault()
        dialog.focus()
        return
      }
      const first = focusable[0] ?? dialog
      const last = focusable.at(-1) ?? dialog
      if (!dialog.contains(document.activeElement) || document.activeElement === dialog) {
        event.preventDefault()
        ;(event.shiftKey ? last : first).focus()
      } else if (event.shiftKey && document.activeElement === first) {
        event.preventDefault()
        last.focus()
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault()
        first.focus()
      }
    }
    document.addEventListener('keydown', keyDown)
    return () => {
      const index = dialogs.indexOf(dialog)
      if (index !== -1) dialogs.splice(index, 1)
      if (dialogs.length === 0) document.body.style.overflow = originalOverflow
      document.removeEventListener('keydown', keyDown)
      const top = dialogs.at(-1)
      if (previousFocus?.isConnected && (!top || top.contains(previousFocus))) previousFocus.focus({ preventScroll: true })
      else if (top) (focusableElements(top)[0] ?? top).focus({ preventScroll: true })
    }
  }, [open, ref])
}
