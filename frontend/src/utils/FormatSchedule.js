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
