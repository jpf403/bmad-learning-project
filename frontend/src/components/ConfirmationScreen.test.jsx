import { describe, it, expect } from 'vitest'
import { render, screen } from '@testing-library/react'
import ConfirmationScreen from './ConfirmationScreen'

describe('ConfirmationScreen', () => {
  it('renders the exact expected confirmation copy', () => {
    render(
      <ConfirmationScreen
        barberName="Amy Barber"
        date="2026-07-24"
        startTime="09:00"
      />,
    )

    expect(
      screen.getByText(
        'Appointment booked with Amy Barber at 9:00 AM on July 24.',
      ),
    ).toBeInTheDocument()
  })
})
