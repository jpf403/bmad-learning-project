import { describe, it, expect } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import Input from './Input'

describe('Input', () => {
  it('associates the label with the input', () => {
    render(<Input label="Email" placeholder="you@example.com" />)
    expect(screen.getByLabelText('Email')).toHaveAttribute(
      'placeholder',
      'you@example.com',
    )
  })

  it('receives focus on interaction', async () => {
    const user = userEvent.setup()
    render(<Input label="Email" />)

    const input = screen.getByLabelText('Email')
    await user.click(input)

    expect(input).toHaveFocus()
  })

  it('renders a caption with the muted treatment when no error is set', () => {
    render(<Input label="Email" caption="We will never share this" />)
    const caption = screen.getByText('We will never share this')
    expect(caption).not.toHaveClass('input-field__caption--error')
  })

  it('renders an error message with the error treatment', () => {
    render(<Input label="Confirm Password" error="Passwords do not match" />)
    const caption = screen.getByText('Passwords do not match')
    expect(caption).toHaveClass('input-field__caption--error')
  })

  it('renders no caption element when neither caption nor error is set', () => {
    render(<Input label="Email" />)
    expect(
      screen.queryByText(/./, { selector: '.input-field__caption' }),
    ).not.toBeInTheDocument()
  })
})
