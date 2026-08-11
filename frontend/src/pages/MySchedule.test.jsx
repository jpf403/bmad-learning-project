import { describe, it, expect, vi, afterEach } from 'vitest'
import { StrictMode, useEffect } from 'react'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Routes, Route } from 'react-router'
import { AuthProvider, useAuth } from '../context/AuthContext'
import MySchedule from './MySchedule'

const SIGNED_IN_BARBER = {
  accessToken: 'token-abc',
  id: 1,
  email: 'barber@example.com',
  firstName: 'John',
  lastName: 'Smith',
  role: 'Barber',
}

const SIGNED_IN_ADMIN = {
  ...SIGNED_IN_BARBER,
  email: 'admin@example.com',
  role: 'Admin',
}

const FIXED_SLOTS = [
  '09:00',
  '09:30',
  '10:00',
  '10:30',
  '11:00',
  '11:30',
  '12:00',
  '12:30',
  '13:00',
  '13:30',
  '14:00',
  '14:30',
  '15:00',
  '15:30',
  '16:00',
  '16:30',
]

function emptySchedule(date) {
  return {
    date,
    slots: FIXED_SLOTS.map((startTime) => ({ startTime, appointment: null })),
  }
}

function scheduleWithBooking(date, startTime, appointment) {
  return {
    date,
    slots: FIXED_SLOTS.map((slot) => ({
      startTime: slot,
      appointment: slot === startTime ? appointment : null,
    })),
  }
}

// SignedInThenRenderPage mirrors ScheduleAppointment.test.jsx's own pattern:
// MySchedule assumes a signed-in user is already in context (RequireRole's
// job on the real route).
function SignInThenRenderPage({ user }) {
  const { user: contextUser, login } = useAuth()

  useEffect(() => {
    login(user)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  if (!contextUser) return null
  return <MySchedule />
}

// Wrapped in StrictMode to match main.jsx's real production tree -- dev-only
// double-invoked effects have previously masked a stale isMountedRef bug that
// only StrictMode's mount/cleanup/mount cycle exposes.
function renderPage(user = SIGNED_IN_BARBER) {
  return render(
    <StrictMode>
      <AuthProvider>
        <MemoryRouter initialEntries={['/my-schedule']}>
          <Routes>
            <Route
              path="/my-schedule"
              element={<SignInThenRenderPage user={user} />}
            />
          </Routes>
        </MemoryRouter>
      </AuthProvider>
    </StrictMode>,
  )
}

function mockFetch({
  scheduleResponse,
  cancel,
  barbersResponse,
  barbersFail,
} = {}) {
  return vi.spyOn(globalThis, 'fetch').mockImplementation((url, options) => {
    const href = url.toString()

    if (href.includes('/api/booking/barbers')) {
      if (barbersFail) {
        return Promise.resolve({
          ok: false,
          status: 500,
          json: async () => null,
        })
      }
      return Promise.resolve({ ok: true, json: async () => barbersResponse })
    }
    if (href.includes('/api/booking/schedule')) {
      const date = new URL(href).searchParams.get('date')
      const body =
        typeof scheduleResponse === 'function'
          ? scheduleResponse(date)
          : scheduleResponse
      return Promise.resolve({ ok: true, json: async () => body })
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
    return Promise.resolve({ ok: false, status: 401 })
  })
}

describe('MySchedule', () => {
  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('renders "Loading…" during the initial fetch', () => {
    mockFetch({ scheduleResponse: emptySchedule('2026-08-24') })
    renderPage()

    expect(screen.getByText('Loading…')).toBeInTheDocument()
  })

  it('renders all 16 slots as "Available" when nothing is booked', async () => {
    mockFetch({ scheduleResponse: emptySchedule('2026-08-24') })
    renderPage()

    expect(await screen.findAllByText('Available')).toHaveLength(16)
  })

  it("renders a booked slot's customer name plus a Cancel button", async () => {
    mockFetch({
      scheduleResponse: scheduleWithBooking('2026-08-24', '10:00', {
        id: 5,
        customerId: 2,
        customerName: 'Jane Doe',
        barberId: 1,
        barberName: 'John Smith',
        date: '2026-08-24',
        startTime: '10:00',
        finished: false,
        cancelledAt: null,
      }),
    })
    renderPage()

    expect(await screen.findByText('Jane Doe')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Cancel' })).toBeInTheDocument()
    expect(screen.getAllByText('Available')).toHaveLength(15)
  })

  it('renders a "Finished" label instead of a Cancel button for a finished slot', async () => {
    mockFetch({
      scheduleResponse: scheduleWithBooking('2026-08-24', '10:00', {
        id: 5,
        customerId: 2,
        customerName: 'Jane Doe',
        barberId: 1,
        barberName: 'John Smith',
        date: '2026-08-24',
        startTime: '10:00',
        finished: true,
        cancelledAt: null,
      }),
    })
    renderPage()

    expect(await screen.findByText('Jane Doe')).toBeInTheDocument()
    expect(screen.getByText('Finished')).toBeInTheDocument()
    expect(
      screen.queryByRole('button', { name: 'Cancel' }),
    ).not.toBeInTheDocument()
  })

  it('clicking a date-nav arrow triggers a re-fetch with the adjacent date and updates the header', async () => {
    const fetchMock = mockFetch({
      scheduleResponse: (date) => emptySchedule(date ?? '2026-08-24'),
    })
    const user = userEvent.setup()
    renderPage()

    expect(await screen.findByText('Monday, August 24')).toBeInTheDocument()

    await user.click(screen.getByLabelText('Next day'))

    expect(await screen.findByText('Tuesday, August 25')).toBeInTheDocument()
    const lastCallUrl = fetchMock.mock.calls.at(-1)[0].toString()
    expect(lastCallUrl).toContain('date=2026-08-25')
  })

  it('clicking Next day from a Friday skips the weekend and lands on Monday', async () => {
    const fetchMock = mockFetch({
      scheduleResponse: (date) => emptySchedule(date ?? '2026-08-21'),
    })
    const user = userEvent.setup()
    renderPage()

    expect(await screen.findByText('Friday, August 21')).toBeInTheDocument()

    await user.click(screen.getByLabelText('Next day'))

    expect(await screen.findByText('Monday, August 24')).toBeInTheDocument()
    const lastCallUrl = fetchMock.mock.calls.at(-1)[0].toString()
    expect(lastCallUrl).toContain('date=2026-08-24')
  })

  it('clicking Previous day from a Monday skips the weekend and lands on Friday', async () => {
    const fetchMock = mockFetch({
      scheduleResponse: (date) => emptySchedule(date ?? '2026-08-24'),
    })
    const user = userEvent.setup()
    renderPage()

    expect(await screen.findByText('Monday, August 24')).toBeInTheDocument()

    await user.click(screen.getByLabelText('Previous day'))

    expect(await screen.findByText('Friday, August 21')).toBeInTheDocument()
    const lastCallUrl = fetchMock.mock.calls.at(-1)[0].toString()
    expect(lastCallUrl).toContain('date=2026-08-21')
  })

  it('renders the "Closed" message and no slot rows for a weekend-dated response', async () => {
    mockFetch({ scheduleResponse: emptySchedule('2026-08-29') })
    renderPage()

    expect(
      await screen.findByText('Closed — the shop is not open on weekends.'),
    ).toBeInTheDocument()
    expect(screen.queryByText('Available')).not.toBeInTheDocument()
    expect(
      screen.queryByRole('button', { name: 'Cancel' }),
    ).not.toBeInTheDocument()
  })

  it('opens the confirm popup with the exact title/message shape when Cancel is clicked', async () => {
    mockFetch({
      scheduleResponse: scheduleWithBooking('2026-08-24', '10:00', {
        id: 5,
        customerId: 2,
        customerName: 'Jane Doe',
        barberId: 1,
        barberName: 'John Smith',
        date: '2026-08-24',
        startTime: '10:00',
        finished: false,
        cancelledAt: null,
      }),
    })
    const user = userEvent.setup()
    renderPage()

    await user.click(await screen.findByRole('button', { name: 'Cancel' }))

    expect(screen.getByText('Cancel this appointment?')).toBeInTheDocument()
    expect(
      screen.getByText(
        'Jane Doe — 10:00 AM, August 24. This cannot be undone.',
      ),
    ).toBeInTheDocument()
  })

  it('confirming cancel calls cancelAppointment then re-fetches the current date and re-renders', async () => {
    const fetchMock = mockFetch({
      scheduleResponse: scheduleWithBooking('2026-08-24', '10:00', {
        id: 5,
        customerId: 2,
        customerName: 'Jane Doe',
        barberId: 1,
        barberName: 'John Smith',
        date: '2026-08-24',
        startTime: '10:00',
        finished: false,
        cancelledAt: null,
      }),
    })
    const user = userEvent.setup()
    renderPage()

    await user.click(await screen.findByRole('button', { name: 'Cancel' }))
    fetchMock.mockImplementation((url, options) => {
      const href = url.toString()
      if (href.includes('/api/booking/schedule')) {
        return Promise.resolve({
          ok: true,
          json: async () => emptySchedule('2026-08-24'),
        })
      }
      if (
        href.match(/\/api\/booking\/\d+\/cancel$/) &&
        options?.method === 'POST'
      ) {
        return Promise.resolve({
          ok: true,
          status: 204,
          json: async () => null,
        })
      }
      return Promise.resolve({ ok: false, status: 401 })
    })
    await user.click(screen.getByRole('button', { name: 'Confirm' }))

    expect(await screen.findAllByText('Available')).toHaveLength(16)
    expect(screen.queryByText('Jane Doe')).not.toBeInTheDocument()
  })

  it('shows the already-cancelled error on a 409 response and still refetches', async () => {
    mockFetch({
      scheduleResponse: scheduleWithBooking('2026-08-24', '10:00', {
        id: 5,
        customerId: 2,
        customerName: 'Jane Doe',
        barberId: 1,
        barberName: 'John Smith',
        date: '2026-08-24',
        startTime: '10:00',
        finished: false,
        cancelledAt: null,
      }),
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

  it('shows the server-provided title on a 409 for an appointment that finished before the cancel request landed', async () => {
    mockFetch({
      scheduleResponse: scheduleWithBooking('2026-08-24', '10:00', {
        id: 5,
        customerId: 2,
        customerName: 'Jane Doe',
        barberId: 1,
        barberName: 'John Smith',
        date: '2026-08-24',
        startTime: '10:00',
        finished: false,
        cancelledAt: null,
      }),
      cancel: Promise.resolve({
        ok: false,
        status: 409,
        json: async () => ({
          title:
            'This appointment has already finished and cannot be cancelled.',
        }),
      }),
    })
    const user = userEvent.setup()
    renderPage()

    await user.click(await screen.findByRole('button', { name: 'Cancel' }))
    await user.click(screen.getByRole('button', { name: 'Confirm' }))

    expect(
      await screen.findByText(
        'This appointment has already finished and cannot be cancelled.',
      ),
    ).toBeInTheDocument()
  })

  it('dismissing the popup via Go Back makes no network call', async () => {
    const fetchMock = mockFetch({
      scheduleResponse: scheduleWithBooking('2026-08-24', '10:00', {
        id: 5,
        customerId: 2,
        customerName: 'Jane Doe',
        barberId: 1,
        barberName: 'John Smith',
        date: '2026-08-24',
        startTime: '10:00',
        finished: false,
        cancelledAt: null,
      }),
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

  it('renders an error message with a Try again button when the schedule fetch fails', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue({
      ok: false,
      status: 500,
      json: async () => null,
    })
    renderPage()

    expect(
      await screen.findByText(
        'Could not load your schedule. Please try again.',
      ),
    ).toBeInTheDocument()
    expect(
      screen.getByRole('button', { name: 'Try again' }),
    ).toBeInTheDocument()
  })

  it('clicking Try again re-fetches the date being navigated to, not the previously displayed one', async () => {
    let nextDayAttempts = 0
    const fetchMock = vi
      .spyOn(globalThis, 'fetch')
      .mockImplementation((url) => {
        const href = url.toString()
        if (!href.includes('/api/booking/schedule')) {
          return Promise.resolve({ ok: false, status: 401 })
        }
        const date = new URL(href).searchParams.get('date')
        if (date === '2026-08-25') {
          nextDayAttempts += 1
          if (nextDayAttempts === 1) {
            return Promise.resolve({
              ok: false,
              status: 500,
              json: async () => null,
            })
          }
          return Promise.resolve({
            ok: true,
            json: async () => emptySchedule('2026-08-25'),
          })
        }
        return Promise.resolve({
          ok: true,
          json: async () => emptySchedule(date ?? '2026-08-24'),
        })
      })
    const user = userEvent.setup()
    renderPage()

    expect(await screen.findByText('Monday, August 24')).toBeInTheDocument()

    await user.click(screen.getByLabelText('Next day'))

    expect(
      await screen.findByText(
        'Could not load your schedule. Please try again.',
      ),
    ).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Try again' }))

    expect(await screen.findByText('Tuesday, August 25')).toBeInTheDocument()
    const lastCallUrl = fetchMock.mock.calls.at(-1)[0].toString()
    expect(lastCallUrl).toContain('date=2026-08-25')
  })

  it('disables the date-nav arrows while a cancel is in flight', async () => {
    mockFetch({
      scheduleResponse: scheduleWithBooking('2026-08-24', '10:00', {
        id: 5,
        customerId: 2,
        customerName: 'Jane Doe',
        barberId: 1,
        barberName: 'John Smith',
        date: '2026-08-24',
        startTime: '10:00',
        finished: false,
        cancelledAt: null,
      }),
      cancel: new Promise(() => {}), // never resolves -- cancel stays in flight
    })
    const user = userEvent.setup()
    renderPage()

    await user.click(await screen.findByRole('button', { name: 'Cancel' }))
    await user.click(screen.getByRole('button', { name: 'Confirm' }))

    expect(await screen.findByLabelText('Previous day')).toBeDisabled()
    expect(screen.getByLabelText('Next day')).toBeDisabled()
  })

  it('Admin: loads barbers, then renders "Loading…" then the first barber\'s schedule with their name in the Select Barber trigger', async () => {
    mockFetch({
      barbersResponse: [
        { id: 1, firstName: 'Amy', lastName: 'Barber' },
        { id: 2, firstName: 'Zed', lastName: 'Barber' },
      ],
      scheduleResponse: emptySchedule('2026-08-24'),
    })
    renderPage(SIGNED_IN_ADMIN)

    expect(screen.getByText('Loading…')).toBeInTheDocument()

    expect(
      await screen.findByRole('combobox', { name: 'Select barber' }),
    ).toHaveTextContent('Amy Barber')
    expect(await screen.findAllByText('Available')).toHaveLength(16)
  })

  it('Admin: switching the Select Barber dropdown re-fetches with the new barberId and the still-current date', async () => {
    const fetchMock = mockFetch({
      barbersResponse: [
        { id: 1, firstName: 'Amy', lastName: 'Barber' },
        { id: 2, firstName: 'Zed', lastName: 'Barber' },
      ],
      scheduleResponse: (date) => emptySchedule(date ?? '2026-08-24'),
    })
    const user = userEvent.setup()
    renderPage(SIGNED_IN_ADMIN)

    expect(
      await screen.findByRole('combobox', { name: 'Select barber' }),
    ).toHaveTextContent('Amy Barber')

    await user.click(screen.getByRole('combobox', { name: 'Select barber' }))
    await user.click(await screen.findByRole('option', { name: 'Zed Barber' }))

    expect(
      await screen.findByRole('combobox', { name: 'Select barber' }),
    ).toHaveTextContent('Zed Barber')
    const lastCallUrl = fetchMock.mock.calls.at(-1)[0].toString()
    expect(lastCallUrl).toContain('barberId=2')
    expect(lastCallUrl).toContain('date=2026-08-24')
  })

  it('Admin: zero barbers renders "No barbers available." and never calls GET /api/booking/schedule', async () => {
    const fetchMock = mockFetch({ barbersResponse: [] })
    renderPage(SIGNED_IN_ADMIN)

    expect(await screen.findByText('No barbers available.')).toBeInTheDocument()
    expect(
      fetchMock.mock.calls.some(([url]) =>
        url.toString().includes('/api/booking/schedule'),
      ),
    ).toBe(false)
  })

  it('Admin: barbers fetch failure shows an error with a working Try again that succeeds on retry', async () => {
    mockFetch({ barbersFail: true })
    const user = userEvent.setup()
    renderPage(SIGNED_IN_ADMIN)

    expect(
      await screen.findByText('Could not load barbers. Please try again.'),
    ).toBeInTheDocument()

    mockFetch({
      barbersResponse: [{ id: 1, firstName: 'Amy', lastName: 'Barber' }],
      scheduleResponse: emptySchedule('2026-08-24'),
    })
    await user.click(screen.getByRole('button', { name: 'Try again' }))

    expect(
      await screen.findByRole('combobox', { name: 'Select barber' }),
    ).toHaveTextContent('Amy Barber')
  })

  it('Admin: cancel flow re-fetches with the currently selected barberId', async () => {
    const fetchMock = mockFetch({
      barbersResponse: [{ id: 1, firstName: 'Amy', lastName: 'Barber' }],
      scheduleResponse: scheduleWithBooking('2026-08-24', '10:00', {
        id: 5,
        customerId: 2,
        customerName: 'Jane Doe',
        barberId: 1,
        barberName: 'Amy Barber',
        date: '2026-08-24',
        startTime: '10:00',
        finished: false,
        cancelledAt: null,
      }),
    })
    const user = userEvent.setup()
    renderPage(SIGNED_IN_ADMIN)

    await user.click(await screen.findByRole('button', { name: 'Cancel' }))
    fetchMock.mockImplementation((url, options) => {
      const href = url.toString()
      if (href.includes('/api/booking/schedule')) {
        return Promise.resolve({
          ok: true,
          json: async () => emptySchedule('2026-08-24'),
        })
      }
      if (
        href.match(/\/api\/booking\/\d+\/cancel$/) &&
        options?.method === 'POST'
      ) {
        return Promise.resolve({
          ok: true,
          status: 204,
          json: async () => null,
        })
      }
      return Promise.resolve({ ok: false, status: 401 })
    })
    await user.click(screen.getByRole('button', { name: 'Confirm' }))

    expect(await screen.findAllByText('Available')).toHaveLength(16)
    const lastCallUrl = fetchMock.mock.calls.at(-1)[0].toString()
    expect(lastCallUrl).toContain('barberId=1')
    expect(lastCallUrl).toContain('date=2026-08-24')
  })

  it('Barber-role regression: never calls GET /api/booking/barbers', async () => {
    const fetchMock = mockFetch({
      scheduleResponse: emptySchedule('2026-08-24'),
    })
    renderPage()

    await screen.findAllByText('Available')

    expect(
      fetchMock.mock.calls.some(([url]) =>
        url.toString().includes('/api/booking/barbers'),
      ),
    ).toBe(false)
  })

  it('renders the defensive fallback for a role that is neither Barber nor Admin, with no fetch call', async () => {
    const fetchMock = mockFetch({
      scheduleResponse: emptySchedule('2026-08-24'),
    })
    renderPage({ ...SIGNED_IN_BARBER, role: 'Customer' })

    expect(
      await screen.findByText(
        'Schedule view is not available for this account.',
      ),
    ).toBeInTheDocument()
    expect(
      fetchMock.mock.calls.some(([url]) =>
        url.toString().includes('/api/booking/'),
      ),
    ).toBe(false)
  })
})
