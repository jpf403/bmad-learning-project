import { describe, it, expect, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import Button from './Button'

describe('Button', () => {
  it.each([
    ['primary', 'button--primary'],
    ['secondary', 'button--secondary'],
    ['destructive', 'button--destructive'],
  ])('renders the %s variant with the %s class', (variant, expectedClass) => {
    render(<Button variant={variant}>Label</Button>)
    const button = screen.getByRole('button', { name: 'Label' })
    expect(button).toHaveClass(expectedClass)
  })

  it('defaults to the primary variant', () => {
    render(<Button>Label</Button>)
    expect(screen.getByRole('button', { name: 'Label' })).toHaveClass(
      'button--primary',
    )
  })

  it('activates on Enter and Space via keyboard', async () => {
    const user = userEvent.setup()
    const onClick = vi.fn()
    render(<Button onClick={onClick}>Label</Button>)

    const button = screen.getByRole('button', { name: 'Label' })
    button.focus()

    await user.keyboard('{Enter}')
    await user.keyboard(' ')

    expect(onClick).toHaveBeenCalledTimes(2)
  })

  it('activates on a single complete tap (click)', async () => {
    const user = userEvent.setup()
    const onClick = vi.fn()
    render(<Button onClick={onClick}>Label</Button>)

    await user.click(screen.getByRole('button', { name: 'Label' }))

    expect(onClick).toHaveBeenCalledTimes(1)
  })
})
