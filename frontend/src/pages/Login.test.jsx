import { describe, it, expect, vi, afterEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Routes, Route } from 'react-router'
import { AuthProvider } from '../context/AuthContext'
import Login from './Login'

function renderLogin({ initialEntries = ['/login'] } = {}) {
  return render(
    <AuthProvider>
      <MemoryRouter initialEntries={initialEntries}>
        <Routes>
          <Route path="/login" element={<Login />} />
          <Route path="/" element={<div>Home Stub</div>} />
          <Route
            path="/schedule-appointment"
            element={<div>Schedule Appointment Stub</div>}
          />
          <Route path="/my-schedule" element={<div>My Schedule Stub</div>} />
        </Routes>
      </MemoryRouter>
    </AuthProvider>,
  )
}

async function fillForm(
  user,
  {
    email = 'john@example.com',
    password = 'correct-horse-battery-staple',
  } = {},
) {
  await user.type(screen.getByLabelText('Email'), email)
  await user.type(screen.getByLabelText('Password'), password)
}

describe('Login', () => {
  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('renders email and password fields inside the form-section wrapper', () => {
    const { container } = renderLogin()

    expect(container.querySelector('.form-section')).toBeInTheDocument()
    expect(screen.getByLabelText('Email')).toBeInTheDocument()
    expect(screen.getByLabelText('Password')).toBeInTheDocument()
  })

  it('masks the password field', () => {
    renderLogin()

    expect(screen.getByLabelText('Password')).toHaveAttribute(
      'type',
      'password',
    )
  })

  it('renders the success banner when location.state.message is set', () => {
    render(
      <AuthProvider>
        <MemoryRouter
          initialEntries={[
            {
              pathname: '/login',
              state: { message: 'Account created. Sign in to continue.' },
            },
          ]}
        >
          <Routes>
            <Route path="/login" element={<Login />} />
          </Routes>
        </MemoryRouter>
      </AuthProvider>,
    )

    expect(
      screen.getByText('Account created. Sign in to continue.'),
    ).toBeInTheDocument()
  })

  it.each([
    ['Customer', 'Schedule Appointment Stub'],
    ['Barber', 'My Schedule Stub'],
    ['Admin', 'My Schedule Stub'],
  ])(
    'navigates to the correct route for a %s login',
    async (role, expectedText) => {
      vi.spyOn(globalThis, 'fetch').mockResolvedValue({
        ok: true,
        status: 200,
        json: async () => ({
          accessToken: 'token-abc',
          id: 1,
          email: 'john@example.com',
          firstName: 'John',
          lastName: 'Smith',
          role,
        }),
      })
      const user = userEvent.setup()
      renderLogin()

      await fillForm(user)
      await user.click(screen.getByRole('button', { name: 'Sign In' }))

      expect(await screen.findByText(expectedText)).toBeInTheDocument()
    },
  )

  it('shows the rate-limit message on a 429', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue({
      ok: false,
      status: 429,
      json: async () => ({
        title: 'Too many attempts. Try again in a few minutes.',
      }),
    })
    const user = userEvent.setup()
    renderLogin()

    await fillForm(user)
    await user.click(screen.getByRole('button', { name: 'Sign In' }))

    expect(
      await screen.findByText('Too many attempts. Try again in a few minutes.'),
    ).toBeInTheDocument()
  })

  it('shows the invalid-credentials message on a 401', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue({
      ok: false,
      status: 401,
      json: async () => ({ title: 'Invalid email or password.' }),
    })
    const user = userEvent.setup()
    renderLogin()

    await fillForm(user)
    await user.click(screen.getByRole('button', { name: 'Sign In' }))

    expect(
      await screen.findByText('Invalid email or password.'),
    ).toBeInTheDocument()
  })

  it('shows a form-check message on a 400', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue({
      ok: false,
      status: 400,
      json: async () => ({ errors: { Email: ['Required'] } }),
    })
    const user = userEvent.setup()
    renderLogin()

    await fillForm(user)
    await user.click(screen.getByRole('button', { name: 'Sign In' }))

    expect(
      await screen.findByText('Please check the form and try again.'),
    ).toBeInTheDocument()
  })

  it('shows a generic error on network failure', async () => {
    vi.spyOn(globalThis, 'fetch').mockRejectedValue(
      new TypeError('Failed to fetch'),
    )
    const user = userEvent.setup()
    renderLogin()

    await fillForm(user)
    await user.click(screen.getByRole('button', { name: 'Sign In' }))

    expect(
      await screen.findByText('Something went wrong. Please try again.'),
    ).toBeInTheDocument()
  })

  it('shows a generic error on an unexpected response status', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue({
      ok: false,
      status: 500,
      json: async () => ({ title: 'Something went wrong. Please try again.' }),
    })
    const user = userEvent.setup()
    renderLogin()

    await fillForm(user)
    await user.click(screen.getByRole('button', { name: 'Sign In' }))

    expect(
      await screen.findByText('Something went wrong. Please try again.'),
    ).toBeInTheDocument()
  })

  it('disables the submit button while the request is in flight', async () => {
    let resolveFetch
    vi.spyOn(globalThis, 'fetch').mockReturnValue(
      new Promise((resolve) => {
        resolveFetch = resolve
      }),
    )
    const user = userEvent.setup()
    renderLogin()

    await fillForm(user)
    await user.click(screen.getByRole('button', { name: 'Sign In' }))

    expect(screen.getByRole('button', { name: 'Sign In' })).toBeDisabled()

    resolveFetch({
      ok: true,
      status: 200,
      json: async () => ({
        accessToken: 'token-abc',
        id: 1,
        email: 'john@example.com',
        firstName: 'John',
        lastName: 'Smith',
        role: 'Customer',
      }),
    })
    await screen.findByText('Schedule Appointment Stub')
  })
})
