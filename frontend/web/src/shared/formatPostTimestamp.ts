const vietnameseWeekdays = [
  'Chủ Nhật', 'Thứ Hai', 'Thứ Ba', 'Thứ Tư', 'Thứ Năm', 'Thứ Sáu', 'Thứ Bảy',
]

function pad(value: number) {
  return String(value).padStart(2, '0')
}

function timeOfDay(value: Date) {
  return `${pad(value.getHours())}:${pad(value.getMinutes())}`
}

function startOfDay(value: Date) {
  const result = new Date(value)
  result.setHours(0, 0, 0, 0)
  return result
}

function startOfWeek(value: Date) {
  const result = startOfDay(value)
  const daysSinceMonday = (result.getDay() + 6) % 7
  result.setDate(result.getDate() - daysSinceMonday)
  return result
}

function formatDate(value: Date, includeYear: boolean) {
  const dayAndMonth = `${value.getDate()} tháng ${value.getMonth() + 1}`
  return `${dayAndMonth}${includeYear ? `, ${value.getFullYear()}` : ''} lúc ${timeOfDay(value)}`
}

export interface PostTimestamp {
  compact: string
  absolute: string
}

export function formatPostTimestamp(value: string, now = new Date()): PostTimestamp {
  const timestamp = new Date(value)
  if (Number.isNaN(timestamp.getTime())) {
    return { compact: '—', absolute: 'Không có thời gian bài viết' }
  }

  const elapsedMinutes = Math.max(0, Math.floor((now.getTime() - timestamp.getTime()) / 60_000))
  const absoluteDate = new Intl.DateTimeFormat('vi-VN', {
    weekday: 'long',
    day: 'numeric',
    month: 'long',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
    hourCycle: 'h23',
  }).format(timestamp)
  const absolute = absoluteDate.charAt(0).toLocaleUpperCase('vi-VN') + absoluteDate.slice(1)

  if (elapsedMinutes < 1) return { compact: 'Vừa xong', absolute }
  if (elapsedMinutes < 60) return { compact: `${elapsedMinutes} phút`, absolute }
  if (elapsedMinutes < 24 * 60) return { compact: `${Math.floor(elapsedMinutes / 60)} giờ`, absolute }

  const today = startOfDay(now)
  const yesterday = new Date(today)
  yesterday.setDate(yesterday.getDate() - 1)
  if (timestamp >= yesterday && timestamp < today) {
    return { compact: `Hôm qua lúc ${timeOfDay(timestamp)}`, absolute }
  }

  if (timestamp >= startOfWeek(now) && timestamp < yesterday) {
    return { compact: `${vietnameseWeekdays[timestamp.getDay()]} lúc ${timeOfDay(timestamp)}`, absolute }
  }

  return { compact: formatDate(timestamp, timestamp.getFullYear() !== now.getFullYear()), absolute }
}
