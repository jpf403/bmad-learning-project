import { describe, it, expect, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import SelectDropdown from './SelectDropdown'

describe('SelectDropdown', () => {
  it('renders the emptyMessage when options is empty', () => {
    render(
      <SelectDropdown
        value=""
        onChange={() => {}}
        options={[]}
        emptyMessage="No barbers available"
      />,
    )

    expect(screen.getByText('No barbers available')).toBeInTheDocument()
    expect(screen.queryByRole('combobox')).toBeNull()
  })

  it('renders and selects an option, calling onChange with its value', async () => {
    const user = userEvent.setup()
    const handleChange = vi.fn()

    render(
      <SelectDropdown
        value=""
        onChange={handleChange}
        placeholder="Select a barber"
        options={[
          { value: '1', label: 'Amy Barber' },
          { value: '2', label: 'Bob Barbington' },
        ]}
      />,
    )

    await user.click(screen.getByRole('combobox'))
    await user.click(
      await screen.findByRole('option', { name: 'Bob Barbington' }),
    )

    expect(handleChange).toHaveBeenCalledWith('2')
  })
})
