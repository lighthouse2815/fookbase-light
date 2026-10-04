import { useSyncExternalStore } from 'react'
import { createPortal } from 'react-dom'
import { dismissToast, toastState } from '../toastState'
import { getNotificationPresentation } from '../notificationPresentation'
import NotificationAvatar from './NotificationAvatar'

export default function ToastViewport() {
  const messages = useSyncExternalStore(toastState.subscribe, toastState.getSnapshot)
  return createPortal(
    <div className="pointer-events-none fixed bottom-5 right-4 z-[100] flex max-w-[calc(100vw-2rem)] flex-col gap-2" aria-live="polite" aria-relevant="additions text" aria-atomic="false">
      {messages.map(({ id, message, tone, notification }) => {
        const presentation = notification ? getNotificationPresentation(notification.item) : null
        return <div key={id} role="status" className="pointer-events-auto flex w-80 max-w-full items-start gap-3 rounded-xl border border-border bg-surface px-4 py-3 text-sm text-text shadow-xl motion-safe:animate-[slide-up_180ms_ease-out]">
        {notification ? <a href={presentation!.destination} onClick={(event) => {
          if (event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return
          event.preventDefault()
          dismissToast(id)
          notification.onOpen()
        }} className="flex min-w-0 flex-1 items-center gap-3 rounded-lg text-left text-text no-underline focus-visible:outline-2 focus-visible:outline-primary">
          <NotificationAvatar notification={notification.item} compact />
          <span className="min-w-0 [overflow-wrap:anywhere]">{presentation!.actor ? <><strong className="font-semibold">{presentation!.actor}</strong> {presentation!.message}</> : message}</span>
        </a> : <><span className={`mt-0.5 shrink-0 font-bold ${tone === 'error' ? 'text-danger' : 'text-primary'}`} aria-hidden="true">{tone === 'error' ? '!' : '✓'}</span><p className="min-w-0 flex-1">{message}</p></>}
        <button type="button" onClick={() => dismissToast(id)} aria-label="Đóng thông báo" className="shrink-0 rounded px-1 text-lg leading-5 text-text-muted hover:bg-surface-2 focus-visible:outline-2 focus-visible:outline-primary">×</button>
      </div>
      })}
    </div>, document.body,
  )
}
