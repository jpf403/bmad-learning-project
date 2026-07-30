import { describe, it, expect } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Routes, Route } from 'react-router'
import NavBar from './NavBar'

describe('NavBar', () => {
  it('renders Home and About as real links, and the rest as inert text', () => {
    render(
      <MemoryRouter>
        <NavBar />
      </MemoryRouter>,
    )

    ;['Home', 'About'].forEach((label) => {
      expect(screen.getByRole('link', { name: label })).toBeInTheDocument()
    })

    ;['Schedule Appointment', 'My Schedule', 'Admin Panel'].forEach((label) => {
      expect(screen.queryByRole('link', { name: label })).toBeNull()
      expect(screen.getByText(label)).toBeInTheDocument()
    })
  })

  it('renders a static Sign In button and a Register button', () => {
    render(
      <MemoryRouter>
        <NavBar />
      </MemoryRouter>,
    )

    expect(screen.getByRole('button', { name: 'Sign In' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Register' })).toBeInTheDocument()
  })

  it('navigates to /register when the Register button is clicked', async () => {
    const user = userEvent.setup()
    render(
      <MemoryRouter initialEntries={['/']}>
        <Routes>
          <Route path="/" element={<NavBar />} />
          <Route path="/register" element={<div>Register Stub</div>} />
        </Routes>
      </MemoryRouter>,
    )

    await user.click(screen.getByRole('button', { name: 'Register' }))

    expect(screen.getByText('Register Stub')).toBeInTheDocument()
  })

  it('renders the wordmark', () => {
    render(
      <MemoryRouter>
        <NavBar />
      </MemoryRouter>,
    )
    expect(screen.getByText('Fake Barbershop')).toBeInTheDocument()
  })

  it('applies the active-link class to the link matching the current route', () => {
    render(
      <MemoryRouter initialEntries={['/about']}>
        <NavBar />
      </MemoryRouter>,
    )

    expect(screen.getByRole('link', { name: 'About' })).toHaveClass(
      'nav-bar__link--active',
    )
    expect(screen.getByRole('link', { name: 'Home' })).not.toHaveClass(
      'nav-bar__link--active',
    )
  })

  it('normalizes case and a trailing slash when matching the active link', () => {
    render(
      <MemoryRouter initialEntries={['/About/']}>
        <NavBar />
      </MemoryRouter>,
    )

    expect(screen.getByRole('link', { name: 'About' })).toHaveClass(
      'nav-bar__link--active',
    )
  })
})
