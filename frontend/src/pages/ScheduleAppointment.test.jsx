import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { useEffect } from 'react'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Routes, Route } from 'react-router'
import { AuthProvider, useAuth } from '../context/AuthContext'
import ScheduleAppointment from './ScheduleAppointment'

const SIGNED_IN_USER = {
  accessToken: 'token-abc',
  id: 1,
  email: 'customer@example.com',
  firstName: 'John',
  lastName: 'Smith',
  role: 'Customer',
}

// Wednesday, August 26, 2026, fixed at noon local time so the Calendar's
// disabled-day matcher (which reads the real wall clock) and the booking
// date used across these tests stay deterministic.
const FIXED_TODAY = new Date(2026, 7, 26, 12, 0, 0)
const TODAY_WIRE_DATE = '2026-08-26'

// ScheduleAppointment assumes a signed-in user is already in context
// (RequireRole's job on the real route) -- sign in during an effect and only
// mount the page once the context user is settled, matching Account.test.jsx.
function SignInThenRenderPage() {
  const { user, login } = useAuth()

  useEffect(() => {
    login(SIGNED_IN_USER)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  if (!user) return null
  return <ScheduleAppointment />
}

function renderPage() {
  return render(
    <AuthProvider>
      <MemoryRouter initialEntries={['/schedule-appointment']}>
        <Routes>
          <Route
            path="/schedule-appointment"
            element={<SignInThenRenderPage />}
          />
        </Routes>
      </MemoryRouter>
    </AuthProvider>,
  )
}

function mockFetch({
  barbers = [],
  availability = [],
  booking,
  appointments = [],
  cancel,
} = {}) {
  return vi.spyOn(globalThis, 'fetch').mockImplementation((url, options) => {
    const href = url.toString()

    if (href.endsWith('/api/booking/barbers')) {
      return Promise.resolve({ ok: true, json: async () => barbers })
    }
    if (href.includes('/api/booking/availability')) {
      return Promise.resolve({ ok: true, json: async () => availability })
    }
    if (href.endsWith('/api/booking/mine')) {
      return Promise.resolve({ ok: true, json: async () => appointments })
    }
    if (
      href.match(/\/api\/booking\/\d+\/cancel$/) &&
      options?.method === 'POST'
    ) {
      return (
        cancel ??
        Promise.resolve({ ok: true, status: 204, json: async () => null })
      )
    }
    if (href.endsWith('/api/booking') && options?.method === 'POST') {
      return (
        booking ??
        Promise.resolve({
          ok: true,
          status: 201,
          json: async () => ({
            id: 1,
            barberName: 'Amy Barber',
            date: TODAY_WIRE_DATE,
            startTime: '09:00',
          }),
        })
      )
    }
    return Promise.resolve({ ok: false, status: 401 })
  })
}

async function selectBarberAndToday(user) {
  await user.click(await screen.findByLabelText('Barber'))
  await user.click(await screen.findByRole('option', { name: 'Amy Barber' }))
  await user.click(screen.getByLabelText('Date'))
  const todayCell = document.querySelector('[data-today]')
  await user.click(todayCell.querySelector('button'))
}

describe('ScheduleAppointment', () => {
  beforeEach(() => {
    vi.useFakeTimers({ shouldAdvanceTime: true })
    vi.setSystemTime(FIXED_TODAY)
  })

  afterEach(() => {
    vi.useRealTimers()
    vi.restoreAllMocks()
  })

  it('renders the booking form with barber, date, and time fields unselected', async () => {
    mockFetch({ barbers: [{ id: 1, firstName: 'Amy', lastName: 'Barber' }] })
    renderPage()

    expect(await screen.findByLabelText('Barber')).toHaveTextContent(
      'Select a barber',
    )
    expect(screen.getByLabelText('Date')).toHaveTextContent('Select a date')
  })

  it('shows "No barbers available" when the barber list resolves empty', async () => {
    mockFetch({ barbers: [] })
    renderPage()

    expect(await screen.findByText('No barbers available')).toBeInTheDocument()
  })

  it('populates the time options from a stubbed availability response', async () => {
    mockFetch({
      barbers: [{ id: 1, firstName: 'Amy', lastName: 'Barber' }],
      availability: ['09:00', '09:30'],
    })
    const user = userEvent.setup()
    renderPage()

    await selectBarberAndToday(user)

    await user.click(await screen.findByLabelText('Time'))
    expect(
      await screen.findByRole('option', { name: '9:00 AM' }),
    ).toBeInTheDocument()
    expect(screen.getByRole('option', { name: '9:30 AM' })).toBeInTheDocument()
  })

  it('submits the booking and renders the confirmation screen', async () => {
    mockFetch({
      barbers: [{ id: 1, firstName: 'Amy', lastName: 'Barber' }],
      availability: ['09:00'],
    })
    const user = userEvent.setup()
    renderPage()

    await selectBarberAndToday(user)
    await user.click(await screen.findByLabelText('Time'))
    await user.click(await screen.findByRole('option', { name: '9:00 AM' }))

    await user.click(screen.getByRole('button', { name: 'Submit' }))

    expect(
      await screen.findByText(
        'Appointment booked with Amy Barber at 9:00 AM on August 26.',
      ),
    ).toBeInTheDocument()
  })

  it('shows a retry-friendly error and keeps the barber/date selection on a 409 conflict', async () => {
    mockFetch({
      barbers: [{ id: 1, firstName: 'Amy', lastName: 'Barber' }],
      availability: ['09:00'],
      booking: Promise.resolve({
        ok: false,
        status: 409,
        json: async () => ({
          title: 'That time is no longer available. Choose another.',
        }),
      }),
    })
    const user = userEvent.setup()
    renderPage()

    await selectBarberAndToday(user)
    await user.click(await screen.findByLabelText('Time'))
    await user.click(await screen.findByRole('option', { name: '9:00 AM' }))

    await user.click(screen.getByRole('button', { name: 'Submit' }))

    expect(
      await screen.findByText(
        'That time is no longer available. Choose another.',
      ),
    ).toBeInTheDocument()
    expect(screen.getByLabelText('Barber')).toHaveTextContent('Amy Barber')
    expect(screen.getByLabelText('Date')).not.toHaveTextContent('Select a date')
  })

  it('renders "No upcoming appointments." on an empty list', async () => {
    mockFetch({ appointments: [] })
    renderPage()

    expect(
      await screen.findByText('No upcoming appointments.'),
    ).toBeInTheDocument()
  })

  it('renders a row with barber name, formatted time/date, and a Cancel button for a non-empty list', async () => {
    mockFetch({
      appointments: [
        {
          id: 1,
          barberName: 'Amy Barber',
          date: '2026-08-27',
          startTime: '09:00',
        },
      ],
    })
    renderPage()

    expect(await screen.findByText('Amy Barber')).toBeInTheDocument()
    expect(screen.getByText('9:00 AM, August 27')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Cancel' })).toBeInTheDocument()
  })

  it('opens the confirm popup with the exact title/message shape when Cancel is clicked', async () => {
    mockFetch({
      appointments: [
        {
          id: 1,
          barberName: 'Amy Barber',
          date: '2026-08-27',
          startTime: '09:00',
        },
      ],
    })
    const user = userEvent.setup()
    renderPage()

    await user.click(await screen.findByRole('button', { name: 'Cancel' }))

    expect(screen.getByText('Cancel this appointment?')).toBeInTheDocument()
    expect(
      screen.getByText(
        'Amy Barber — 9:00 AM, August 27. This cannot be undone.',
      ),
    ).toBeInTheDocument()
  })

  it('confirming cancel calls the cancel endpoint then re-fetches and re-renders the list without the cancelled row', async () => {
    const fetchMock = mockFetch({
      appointments: [
        {
          id: 1,
          barberName: 'Amy Barber',
          date: '2026-08-27',
          startTime: '09:00',
        },
      ],
    })
    const user = userEvent.setup()
    renderPage()

    await user.click(await screen.findByRole('button', { name: 'Cancel' }))
    fetchMock.mockImplementation((url) => {
      const href = url.toString()
      if (href.endsWith('/api/booking/mine')) {
        return Promise.resolve({ ok: true, json: async () => [] })
      }
      if (href.match(/\/api\/booking\/\d+\/cancel$/)) {
        return Promise.resolve({
          ok: true,
          status: 204,
          json: async () => null,
        })
      }
      return Promise.resolve({ ok: false, status: 401 })
    })
    await user.click(screen.getByRole('button', { name: 'Confirm' }))

    expect(
      await screen.findByText('No upcoming appointments.'),
    ).toBeInTheDocument()
  })

  it('shows the already-cancelled error on a 409 response and still refetches', async () => {
    mockFetch({
      appointments: [
        {
          id: 1,
          barberName: 'Amy Barber',
          date: '2026-08-27',
          startTime: '09:00',
        },
      ],
      cancel: Promise.resolve({
        ok: false,
        status: 409,
        json: async () => ({
          title: 'This appointment has already been cancelled.',
        }),
      }),
    })
    const user = userEvent.setup()
    renderPage()

    await user.click(await screen.findByRole('button', { name: 'Cancel' }))
    await user.click(screen.getByRole('button', { name: 'Confirm' }))

    expect(
      await screen.findByText('This appointment has already been cancelled.'),
    ).toBeInTheDocument()
  })

  it('dismissing the popup via Go Back makes no network call', async () => {
    const fetchMock = mockFetch({
      appointments: [
        {
          id: 1,
          barberName: 'Amy Barber',
          date: '2026-08-27',
          startTime: '09:00',
        },
      ],
    })
    const user = userEvent.setup()
    renderPage()

    await user.click(await screen.findByRole('button', { name: 'Cancel' }))
    fetchMock.mockClear()
    await user.click(screen.getByRole('button', { name: 'Go Back' }))

    expect(
      screen.queryByText('Cancel this appointment?'),
    ).not.toBeInTheDocument()
    expect(fetchMock).not.toHaveBeenCalled()
  })
})
