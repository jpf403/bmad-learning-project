import { describe, it, expect, vi, afterEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Routes, Route, useLocation } from 'react-router'
import Register from './Register'

function LoginStub() {
  const location = useLocation()
  return <div>Login Stub: {location.state?.message}</div>
}

function renderRegister() {
  return render(
    <MemoryRouter initialEntries={['/register']}>
      <Routes>
        <Route path="/register" element={<Register />} />
        <Route path="/login" element={<LoginStub />} />
      </Routes>
    </MemoryRouter>,
  )
}

async function fillForm(
  user,
  {
    email = 'john@example.com',
    password = 'hunter2',
    confirmPassword = password,
  } = {},
) {
  await user.type(screen.getByLabelText('First name'), 'John')
  await user.type(screen.getByLabelText('Last name'), 'Smith')
  await user.type(screen.getByLabelText('Email'), email)
  await user.type(screen.getByLabelText('Password'), password)
  await user.type(screen.getByLabelText('Confirm password'), confirmPassword)
}

describe('Register', () => {
  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('renders all fields inside the form-section wrapper', () => {
    const { container } = renderRegister()

    expect(container.querySelector('.form-section')).toBeInTheDocument()
    expect(screen.getByLabelText('First name')).toBeInTheDocument()
    expect(screen.getByLabelText('Last name')).toBeInTheDocument()
    expect(screen.getByLabelText('Email')).toBeInTheDocument()
    expect(screen.getByLabelText('Password')).toBeInTheDocument()
    expect(screen.getByLabelText('Confirm password')).toBeInTheDocument()
  })

  it('navigates to /login with the confirmation message on successful registration', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue({ ok: true, status: 201 })
    const user = userEvent.setup()
    renderRegister()

    await fillForm(user)
    await user.click(screen.getByRole('button', { name: 'Register' }))

    expect(
      await screen.findByText(
        'Login Stub: Account created. Sign in to continue.',
      ),
    ).toBeInTheDocument()
  })

  it('shows a mismatch error, clears both password fields, retains other fields, and never calls fetch', async () => {
    const fetchSpy = vi.spyOn(globalThis, 'fetch')
    const user = userEvent.setup()
    renderRegister()

    await fillForm(user, { password: 'hunter2', confirmPassword: 'different' })
    await user.click(screen.getByRole('button', { name: 'Register' }))

    expect(await screen.findAllByText('Passwords do not match')).toHaveLength(2)
    expect(screen.getByLabelText('Password')).toHaveValue('')
    expect(screen.getByLabelText('Confirm password')).toHaveValue('')
    expect(screen.getByLabelText('First name')).toHaveValue('John')
    expect(screen.getByLabelText('Last name')).toHaveValue('Smith')
    expect(screen.getByLabelText('Email')).toHaveValue('john@example.com')
    expect(fetchSpy).not.toHaveBeenCalled()
  })

  it('shows a duplicate-email error and retains the entered email on 409', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue({
      ok: false,
      status: 409,
      json: async () => ({ title: 'That email is already in use.' }),
    })
    const user = userEvent.setup()
    renderRegister()

    await fillForm(user)
    await user.click(screen.getByRole('button', { name: 'Register' }))

    expect(
      await screen.findByText('That email is already in use.'),
    ).toBeInTheDocument()
    expect(screen.getByLabelText('Email')).toHaveValue('john@example.com')
  })

  it('shows a format error and retains the entered email on 400', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue({
      ok: false,
      status: 400,
      json: async () => ({
        errors: { Email: ['Enter a valid email address.'] },
      }),
    })
    const user = userEvent.setup()
    renderRegister()

    await fillForm(user, { email: 'testbademail' })
    await user.click(screen.getByRole('button', { name: 'Register' }))

    expect(
      await screen.findByText('Enter a valid email address.'),
    ).toBeInTheDocument()
    expect(screen.getByLabelText('Email')).toHaveValue('testbademail')
  })
})
