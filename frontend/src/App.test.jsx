import { describe, it, expect } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import App from './App'

describe('App', () => {
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
    expect(
      screen.getByText('123 Main Street, Springfield'),
    ).toBeInTheDocument()
  })

  it('renders About content at /about', () => {
    render(
      <MemoryRouter initialEntries={['/about']}>
        <App />
      </MemoryRouter>,
    )

    expect(screen.getByText('About Fake Barbershop')).toBeInTheDocument()
  })
})
