import { describe, it, expect } from 'vitest'
import { render, screen } from '@testing-library/react'
import NavBar from './NavBar'

describe('NavBar', () => {
  it('renders all five nav links unconditionally', () => {
    render(<NavBar />)

    ;[
      'Home',
      'Schedule Appointment',
      'About',
      'My Schedule',
      'Admin Panel',
    ].forEach((label) => {
      expect(screen.getByRole('link', { name: label })).toBeInTheDocument()
    })
  })

  it('renders a static Sign In / Register area', () => {
    render(<NavBar />)

    expect(screen.getByRole('button', { name: 'Sign In' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Register' })).toBeInTheDocument()
  })

  it('renders the wordmark', () => {
    render(<NavBar />)
    expect(screen.getByText('Fake Barbershop')).toBeInTheDocument()
  })
})
