import { describe, it, expect } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
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

  it('renders a static Sign In / Register area', () => {
    render(
      <MemoryRouter>
        <NavBar />
      </MemoryRouter>,
    )

    expect(screen.getByRole('button', { name: 'Sign In' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Register' })).toBeInTheDocument()
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
})
