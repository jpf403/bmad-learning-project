import { describe, it, expect } from 'vitest'
import { formatTimeLabel, formatDateLabel } from './FormatSchedule'

describe('formatTimeLabel', () => {
  it('formats a morning time as AM', () => {
    expect(formatTimeLabel('09:00')).toBe('9:00 AM')
  })

  it('formats an afternoon time as PM', () => {
    expect(formatTimeLabel('13:30')).toBe('1:30 PM')
  })

  it('formats midnight as 12:00 AM', () => {
    expect(formatTimeLabel('00:00')).toBe('12:00 AM')
  })

  it('formats noon as 12:00 PM', () => {
    expect(formatTimeLabel('12:00')).toBe('12:00 PM')
  })
})

describe('formatDateLabel', () => {
  it('formats a wire date string as a month and day', () => {
    expect(formatDateLabel('2026-07-24')).toBe('July 24')
  })

  it('formats a single-digit day without a leading zero', () => {
    expect(formatDateLabel('2026-09-01')).toBe('September 1')
  })
})
