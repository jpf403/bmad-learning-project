import { describe, it, expect, vi, afterEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'
import App from './App'

describe('App', () => {
  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('renders Home content, NavBar, and Footer at /', () => {
    render(
      <MemoryRouter initialEntries={['/']}>
        <App />
      </MemoryRouter>,
    )

    expect(
      screen.getByText('Your next haircut, booked in under a minute.'),
    ).toBeInTheDocument()
    expect(screen.getAllByText('Fake Barbershop').length).toBeGreaterThan(0)
    expect(screen.getByText('123 Main Street, Springfield')).toBeInTheDocument()
    expect(screen.getByRole('main')).toBeInTheDocument()
  })

  it('renders About content at /about', () => {
    render(
      <MemoryRouter initialEntries={['/about']}>
        <App />
      </MemoryRouter>,
    )

    expect(screen.getByText('About Fake Barbershop')).toBeInTheDocument()
  })

  it('sends a signed-in user straight to /schedule-appointment from the Home CTA', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation((url) => {
      const u = url.toString()
      if (u.endsWith('/api/auth/refresh')) {
        return Promise.resolve({
          ok: true,
          json: async () => ({ accessToken: 'token-abc' }),
        })
      }
      if (u.endsWith('/api/auth/me')) {
        return Promise.resolve({
          ok: true,
          json: async () => ({
            id: 1,
            email: 'john@example.com',
            firstName: 'John',
            lastName: 'Smith',
            role: 'Customer',
          }),
        })
      }
      return Promise.resolve({
        ok: false,
        status: 404,
        json: async () => null,
      })
    })

    const user = userEvent.setup()
    render(
      <MemoryRouter initialEntries={['/']}>
        <App />
      </MemoryRouter>,
    )

    await user.click(
      await screen.findByRole('button', { name: 'Schedule Appointment' }),
    )

    expect(
      await screen.findByRole('heading', { name: 'Schedule Appointment' }),
    ).toBeInTheDocument()
  })
})
