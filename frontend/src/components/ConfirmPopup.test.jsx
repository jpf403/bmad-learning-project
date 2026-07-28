import { describe, it, expect, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import ConfirmPopup from './ConfirmPopup'

function renderPopup(props = {}) {
  const onOpenChange = vi.fn()
  const onConfirm = vi.fn()
  render(
    <ConfirmPopup
      open
      onOpenChange={onOpenChange}
      title="Cancel appointment?"
      message="This cannot be undone."
      onConfirm={onConfirm}
      {...props}
    />,
  )
  return { onOpenChange, onConfirm }
}

describe('ConfirmPopup', () => {
  it('renders exactly two buttons: Go Back and Confirm', () => {
    renderPopup()
    const buttons = screen.getAllByRole('button')
    expect(buttons).toHaveLength(2)
    expect(screen.getByRole('button', { name: 'Go Back' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Confirm' })).toBeInTheDocument()
  })

  it('keeps Go Back as the secondary variant regardless of destructive context', () => {
    renderPopup({ destructive: true })
    expect(screen.getByRole('button', { name: 'Go Back' })).toHaveClass(
      'button--secondary',
    )
  })

  it('colors Confirm as primary for a non-destructive action', () => {
    renderPopup({ destructive: false })
    expect(screen.getByRole('button', { name: 'Confirm' })).toHaveClass(
      'button--primary',
    )
  })

  it('colors Confirm as destructive for a destructive action', () => {
    renderPopup({ destructive: true })
    expect(screen.getByRole('button', { name: 'Confirm' })).toHaveClass(
      'button--destructive',
    )
  })

  it('dismisses via Go Back with no other effect', async () => {
    const user = userEvent.setup()
    const { onOpenChange, onConfirm } = renderPopup()

    await user.click(screen.getByRole('button', { name: 'Go Back' }))

    expect(onOpenChange).toHaveBeenCalledWith(false)
    expect(onConfirm).not.toHaveBeenCalled()
  })

  it('dismisses via Escape with no other effect', async () => {
    const user = userEvent.setup()
    const { onOpenChange, onConfirm } = renderPopup()

    await user.keyboard('{Escape}')

    expect(onOpenChange).toHaveBeenCalledWith(false)
    expect(onConfirm).not.toHaveBeenCalled()
  })

  it('dismisses via outside-click with no other effect', async () => {
    const user = userEvent.setup()
    const { onOpenChange, onConfirm } = renderPopup()

    await user.click(document.querySelector('.modal-overlay'))

    expect(onOpenChange).toHaveBeenCalledWith(false)
    expect(onConfirm).not.toHaveBeenCalled()
  })

  it('invokes onConfirm when Confirm is clicked', async () => {
    const user = userEvent.setup()
    const { onOpenChange, onConfirm } = renderPopup()

    await user.click(screen.getByRole('button', { name: 'Confirm' }))

    expect(onConfirm).toHaveBeenCalledTimes(1)
    expect(onOpenChange).toHaveBeenCalledWith(false)
  })
})
