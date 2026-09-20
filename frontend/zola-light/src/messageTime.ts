const clock = new Intl.DateTimeFormat('vi-VN', { hour: '2-digit', minute: '2-digit', hourCycle: 'h23' })
const fullDate = new Intl.DateTimeFormat('vi-VN', { weekday: 'long', day: '2-digit', month: '2-digit', year: 'numeric' })

export function sameMessageDay(left: string, right: string) {
  return new Date(left).toDateString() === new Date(right).toDateString()
}

export function messageClock(value: string) {
  return clock.format(new Date(value))
}

export function messageDay(value: string, now = new Date()) {
  const date = new Date(value)
  if (date.toDateString() === now.toDateString()) return 'Hôm nay'
  const yesterday = new Date(now)
  yesterday.setDate(yesterday.getDate() - 1)
  if (date.toDateString() === yesterday.toDateString()) return 'Hôm qua'
  return fullDate.format(date)
}

export function conversationTime(value: string, now = new Date()) {
  const day = messageDay(value, now)
  if (day === 'Hôm nay') return messageClock(value)
  if (day === 'Hôm qua') return day
  return new Intl.DateTimeFormat('vi-VN', {
    day: '2-digit', month: '2-digit',
    ...(new Date(value).getFullYear() !== now.getFullYear() ? { year: 'numeric' as const } : {}),
  }).format(new Date(value))
}

export function messageFullTime(value: string) {
  return `${messageClock(value)} · ${fullDate.format(new Date(value))}`
}
