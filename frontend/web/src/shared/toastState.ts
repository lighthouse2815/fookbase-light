import type { AppNotification } from '../api/notifications'

export interface ToastMessage {
  id: string
  message: string
  tone: 'success' | 'error' | 'info'
  notification?: { item: AppNotification; onOpen: () => void }
}

let messages: readonly ToastMessage[] = []
const listeners = new Set<() => void>()
const timers = new Map<string, ReturnType<typeof setTimeout>>()
const notify = () => listeners.forEach((listener) => listener())

export const toastState = {
  getSnapshot: () => messages,
  subscribe: (listener: () => void) => {
    listeners.add(listener)
    return () => { listeners.delete(listener) }
  },
}

export function dismissToast(id: string) {
  clearTimeout(timers.get(id))
  timers.delete(id)
  messages = messages.filter((toast) => toast.id !== id)
  notify()
}

export function showToast(message: string, tone: ToastMessage['tone'] = 'error', id = `${tone}:${message}`, notification?: ToastMessage['notification']) {
  clearTimeout(timers.get(id))
  messages = [...messages.filter((toast) => toast.id !== id), { id, message, tone, notification }]
  while (messages.length > 3) dismissToast(messages[0].id)
  timers.set(id, setTimeout(() => dismissToast(id), 5_000))
  notify()
}
