import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import Calendar from './Calendar'

// Wednesday, July 15, 2026, fixed at noon local time to avoid any
// date-boundary flakiness from timezone rounding.
const FIXED_TODAY = new Date(2026, 6, 15, 12, 0, 0)

describe('Calendar', () => {
  beforeEach(() => {
    vi.useFakeTimers({ shouldAdvanceTime: true })
    vi.setSystemTime(FIXED_TODAY)
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('disables a known past date', async () => {
    const user = userEvent.setup()
    render(<Calendar value="" onChange={() => {}} />)
    await user.click(screen.getByRole('button', { name: 'Select a date' }))

    const pastDay = document.querySelector('[data-day="2026-07-10"]')
    expect(pastDay).toHaveAttribute('data-disabled', 'true')
    expect(pastDay.querySelector('button')).toBeDisabled()
  })

  it('disables a known weekend date', async () => {
    const user = userEvent.setup()
    render(<Calendar value="" onChange={() => {}} />)
    await user.click(screen.getByRole('button', { name: 'Select a date' }))

    // Saturday, July 18, 2026.
    const weekendDay = document.querySelector('[data-day="2026-07-18"]')
    expect(weekendDay).toHaveAttribute('data-disabled', 'true')
    expect(weekendDay.querySelector('button')).toBeDisabled()
  })

  it('disables a date more than 30 days out', async () => {
    const user = userEvent.setup()
    render(<Calendar value="" onChange={() => {}} />)
    await user.click(screen.getByRole('button', { name: 'Select a date' }))
    await user.click(
      screen.getByRole('button', { name: 'Go to the Next Month' }),
    )

    // August 20, 2026 is 36 days after the fixed "today" (July 15, 2026).
    const farFutureDay = document.querySelector('[data-day="2026-08-20"]')
    expect(farFutureDay).toHaveAttribute('data-disabled', 'true')
    expect(farFutureDay.querySelector('button')).toBeDisabled()
  })

  it('applies the selected styling class to the selected day', async () => {
    const user = userEvent.setup()
    render(<Calendar value="2026-07-16" onChange={() => {}} />)
    await user.click(screen.getByRole('button', { name: 'July 16, 2026' }))

    const day = document.querySelector('[data-day="2026-07-16"]')
    expect(day).toHaveClass('rdp-selected')
  })

  it('applies the today styling class to today when not selected', async () => {
    const user = userEvent.setup()
    render(<Calendar value="" onChange={() => {}} />)
    await user.click(screen.getByRole('button', { name: 'Select a date' }))

    const today = document.querySelector('[data-day="2026-07-15"]')
    expect(today).toHaveClass('rdp-today')
    expect(today).not.toHaveClass('rdp-selected')
  })
})
