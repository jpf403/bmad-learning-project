import { describe, it, expect } from 'vitest'
import { render, screen } from '@testing-library/react'
import Footer from './Footer'

describe('Footer', () => {
  it('renders the wordmark, address, phone, hours, and copyright', () => {
    render(<Footer />)

    expect(screen.getByText('Fake Barbershop')).toBeInTheDocument()
    expect(screen.getByText('123 Main Street, Springfield')).toBeInTheDocument()
    expect(screen.getByText('(555) 010-2020')).toBeInTheDocument()
    expect(screen.getByText('Mon–Fri, 9:00 AM – 4:30 PM')).toBeInTheDocument()
    expect(screen.getByText('© 2026 Fake Barbershop')).toBeInTheDocument()
  })

  it('renders no links or icons', () => {
    render(<Footer />)
    expect(screen.queryByRole('link')).not.toBeInTheDocument()
    expect(screen.queryByRole('img')).not.toBeInTheDocument()
  })
})
