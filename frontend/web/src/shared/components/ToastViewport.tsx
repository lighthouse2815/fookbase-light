import { useSyncExternalStore } from 'react'
import { createPortal } from 'react-dom'
import { dismissToast, toastState } from '../toastState'

export default function ToastViewport() {
  const messages = useSyncExternalStore(toastState.subscribe, toastState.getSnapshot)
  return createPortal(
    <div className="pointer-events-none fixed bottom-5 right-4 z-[100] flex max-w-[calc(100vw-2rem)] flex-col gap-2" aria-live="polite" aria-relevant="additions text" aria-atomic="false">
      {messages.map(({ id, message, tone }) => <div key={id} role="status" className="pointer-events-auto flex w-80 max-w-full items-start gap-3 rounded-xl border border-border bg-surface px-4 py-3 text-sm text-text shadow-xl motion-safe:animate-[slide-up_180ms_ease-out]">
        <span className={`mt-0.5 shrink-0 font-bold ${tone === 'error' ? 'text-danger' : 'text-primary'}`} aria-hidden="true">{tone === 'error' ? '!' : '✓'}</span>
        <p className="min-w-0 flex-1">{message}</p>
        <button type="button" onClick={() => dismissToast(id)} aria-label="Đóng thông báo" className="shrink-0 rounded px-1 text-lg leading-5 text-text-muted hover:bg-surface-2 focus-visible:outline-2 focus-visible:outline-primary">×</button>
      </div>)}
    </div>, document.body,
  )
}
