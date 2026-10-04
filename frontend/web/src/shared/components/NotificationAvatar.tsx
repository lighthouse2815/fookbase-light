import type { AppNotification } from '../../api/notifications'
import { getNotificationPresentation, type NotificationPresentation } from '../notificationPresentation'

export function NotificationIcon({ icon }: { icon: NotificationPresentation['icon'] }) {
  const paths: Record<NotificationPresentation['icon'], React.ReactNode> = {
    bell: <><path d="M18 8a6 6 0 0 0-12 0c0 7-3 7-3 9h18c0-2-3-2-3-9" /><path d="M10 21h4" /></>,
    shield: <><path d="m12 3 8 3v6c0 5-8 9-8 9s-8-4-8-9V6l8-3Z" /><path d="M12 8v5m0 3h.01" /></>,
    calendar: <><rect x="3" y="5" width="18" height="16" rx="3" /><path d="M7 3v4m10-4v4M3 11h18m-13 4h3m2 3h3" /></>,
    friends: <><circle cx="9" cy="8" r="3" /><path d="M3 21v-2a6 6 0 0 1 12 0v2m2-16a3 3 0 0 1 0 6m4 10v-2a6 6 0 0 0-3-5" /></>,
    groups: <><circle cx="12" cy="8" r="3" /><path d="M6 21v-2a6 6 0 0 1 12 0v2M5 5a3 3 0 0 0 0 6m14-6a3 3 0 0 1 0 6M2 20v-2a5 5 0 0 1 2-4m18 6v-2a5 5 0 0 0-2-4" /></>,
    heart: <path d="M20.8 4.6a5.5 5.5 0 0 0-7.8 0L12 5.7l-1.1-1.1a5.5 5.5 0 0 0-7.8 7.8L12 21l8.8-8.6a5.5 5.5 0 0 0 0-7.8Z" />,
    comment: <path d="M21 11a9 9 0 0 1-9 9c-1.5 0-3-.4-4.3-1L3 21l1.5-4.7A9 9 0 1 1 21 11Z" />,
    share: <><path d="m14 3 7 7-7 7v-4c-6 0-9 2-11 6 0-9 4-12 11-12V3Z" /></>,
    page: <><path d="M5 21V3m0 1c6-4 8 4 15 0v11c-7 4-9-4-15 0" /></>,
    story: <><circle cx="12" cy="12" r="9" strokeDasharray="4 3" /><path d="m10 8 6 4-6 4V8Z" /></>,
  }
  return <svg viewBox="0 0 24 24" aria-hidden="true" className="h-full w-full fill-none stroke-current" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">{paths[icon]}</svg>
}

export default function NotificationAvatar({ notification, compact = false }: { notification: AppNotification; compact?: boolean }) {
  const presentation = getNotificationPresentation(notification)
  return <span aria-hidden="true" className={`relative flex shrink-0 items-center justify-center rounded-full ${compact ? 'h-10 w-10 text-xs' : 'h-14 w-14 text-sm'} ${presentation.isSystem ? 'bg-primary/10 text-primary' : 'bg-primary font-bold text-white'}`}>
    {presentation.actor ? presentation.actor.slice(0, 2).toLocaleUpperCase('vi-VN') : <span className={compact ? 'h-5 w-5' : 'h-7 w-7'}><NotificationIcon icon={presentation.icon} /></span>}
    {!presentation.isSystem && !compact && <span className={`absolute -bottom-0.5 -right-0.5 grid h-6 w-6 place-items-center rounded-full border-2 border-surface p-1 text-white ${presentation.badge.className}`}><NotificationIcon icon={presentation.icon} /></span>}
  </span>
}
