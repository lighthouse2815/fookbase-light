import { useId, useRef, type ReactNode } from 'react'
import { createPortal } from 'react-dom'
import { useDialogFocus } from '../useDialogFocus'

interface AppDialogProps {
  title: string
  children: ReactNode
  onClose: () => void
  describedBy?: string
  className?: string
}

export default function AppDialog({ title, children, onClose, describedBy, className = 'max-w-md' }: AppDialogProps) {
  const dialogRef = useRef<HTMLElement>(null)
  const titleId = useId()
  useDialogFocus(true, dialogRef, onClose)

  return createPortal(
    <div className="fixed inset-0 z-[70] grid place-items-center bg-black/70 p-4 backdrop-blur-[2px]" role="presentation" onMouseDown={onClose}>
      <section ref={dialogRef} tabIndex={-1} role="dialog" aria-modal="true" aria-labelledby={titleId} aria-describedby={describedBy} onMouseDown={(event) => event.stopPropagation()} className={`relative max-h-[calc(100dvh-2rem)] w-full overflow-y-auto rounded-2xl border border-border bg-surface p-5 shadow-2xl motion-safe:animate-[scale-in_180ms_ease-out] ${className}`}>
        <h2 id={titleId} className="font-heading text-xl font-bold text-text">{title}</h2>
        {children}
      </section>
    </div>,
    document.body,
  )
}
