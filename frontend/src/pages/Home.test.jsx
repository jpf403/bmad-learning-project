import { describe, it, expect } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Routes, Route } from 'react-router'
import Home from './Home'

function renderHome({ isSignedIn } = {}) {
  return render(
    <MemoryRouter initialEntries={['/']}>
      <Routes>
        <Route path="/" element={<Home isSignedIn={isSignedIn} />} />
        <Route path="/login" element={<div>Login Stub</div>} />
        <Route
          path="/schedule-appointment"
          element={<div>Schedule Appointment Stub</div>}
        />
      </Routes>
    </MemoryRouter>,
  )
}

describe('Home', () => {
  it('renders the hero headline, tagline, and CTA', () => {
    renderHome()

    expect(
      screen.getByText('Your next haircut, booked in under a minute.'),
    ).toBeInTheDocument()
    expect(
      screen.getByText('Walk-in convenience, without the wait.'),
    ).toBeInTheDocument()
    expect(
      screen.getByRole('button', { name: 'Schedule Appointment' }),
    ).toBeInTheDocument()
  })

  it('navigates to /login when signed out and the CTA is clicked', async () => {
    const user = userEvent.setup()
    renderHome({ isSignedIn: false })

    await user.click(
      screen.getByRole('button', { name: 'Schedule Appointment' }),
    )

    expect(screen.getByText('Login Stub')).toBeInTheDocument()
  })

  it('navigates to /schedule-appointment when signed in and the CTA is clicked', async () => {
    const user = userEvent.setup()
    renderHome({ isSignedIn: true })

    await user.click(
      screen.getByRole('button', { name: 'Schedule Appointment' }),
    )

    expect(screen.getByText('Schedule Appointment Stub')).toBeInTheDocument()
  })
})
