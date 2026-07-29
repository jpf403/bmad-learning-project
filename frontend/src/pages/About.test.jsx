import { describe, it, expect } from 'vitest'
import { render, screen } from '@testing-library/react'
import About from './About'

describe('About', () => {
  it('renders the address, phone, hours, and barber list', () => {
    render(<About />)

    expect(screen.getByText('123 Main Street, Springfield')).toBeInTheDocument()
    expect(screen.getByText('(555) 010-2020')).toBeInTheDocument()
    expect(screen.getByText('Mon–Fri, 9:00 AM – 4:30 PM')).toBeInTheDocument()
    expect(screen.getByText('Manny, Dana, and Theo')).toBeInTheDocument()
  })
})
