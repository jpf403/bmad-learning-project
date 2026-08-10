const MONTH_NAMES = [
  'January',
  'February',
  'March',
  'April',
  'May',
  'June',
  'July',
  'August',
  'September',
  'October',
  'November',
  'December',
]

export function formatTimeLabel(time) {
  const [hourStr, minuteStr] = time.split(':')
  const hour = Number(hourStr)
  const minute = Number(minuteStr)

  const period = hour >= 12 ? 'PM' : 'AM'
  const displayHour = hour % 12 === 0 ? 12 : hour % 12
  const displayMinute = String(minute).padStart(2, '0')

  return `${displayHour}:${displayMinute} ${period}`
}

export function formatDateLabel(date) {
  const [, month, day] = date.split('-').map(Number)
  return `${MONTH_NAMES[month - 1]} ${day}`
}

export function addDays(date, delta) {
  const [year, month, day] = date.split('-').map(Number)
  const result = new Date(year, month - 1, day)
  result.setDate(result.getDate() + delta)
  const y = result.getFullYear()
  const m = String(result.getMonth() + 1).padStart(2, '0')
  const d = String(result.getDate()).padStart(2, '0')
  return `${y}-${m}-${d}`
}

export function isWeekend(date) {
  const [year, month, day] = date.split('-').map(Number)
  const dayOfWeek = new Date(year, month - 1, day).getDay()
  return dayOfWeek === 0 || dayOfWeek === 6
}

// Steps one weekday at a time in the given direction (+1/-1), skipping over
// Saturday/Sunday entirely -- e.g. Friday + 1 lands on Monday, not Saturday.
export function addWeekdays(date, delta) {
  if (delta === 0) {
    return date
  }
  let result = addDays(date, delta)
  // A week has 7 days, so at most 7 applications of any non-zero `delta`
  // are ever needed to leave a weekend -- caps the loop for any `delta`
  // whose repeated application would otherwise never land on a weekday
  // (e.g. delta = 7 from a Saturday), not just the delta === 0 case above.
  for (let i = 0; isWeekend(result) && i < 7; i++) {
    result = addDays(result, delta)
  }
  return result
}

export function formatDateHeader(date) {
  const [year, month, day] = date.split('-').map(Number)
  const weekday = new Date(year, month - 1, day).toLocaleDateString('en-US', {
    weekday: 'long',
  })
  return `${weekday}, ${formatDateLabel(date)}`
}
