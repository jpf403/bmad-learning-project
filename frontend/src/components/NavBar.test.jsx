import { describe, it, expect, vi, afterEach } from 'vitest'
import { useEffect } from 'react'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Routes, Route } from 'react-router'
import { AuthProvider, useAuth } from '../context/AuthContext'
import NavBar from './NavBar'

const SIGNED_IN_USER = {
  accessToken: 'token-abc',
  id: 1,
  email: 'john@example.com',
  firstName: 'John',
  lastName: 'Smith',
  role: 'Customer',
}

function SignInOnMount({ user, children }) {
  const { login } = useAuth()

  useEffect(() => {
    login(user)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  return children
}

function renderNavBar({ signedIn = false, initialEntries = ['/'] } = {}) {
  return render(
    <AuthProvider>
      <MemoryRouter initialEntries={initialEntries}>
        <Routes>
          <Route
            path="/"
            element={
              signedIn ? (
                <SignInOnMount user={SIGNED_IN_USER}>
                  <NavBar />
                </SignInOnMount>
              ) : (
                <NavBar />
              )
            }
          />
          <Route path="/about" element={<div>About Stub</div>} />
          <Route path="/login" element={<div>Login Stub</div>} />
          <Route path="/register" element={<div>Register Stub</div>} />
        </Routes>
      </MemoryRouter>
    </AuthProvider>,
  )
}

describe('NavBar', () => {
  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('renders Home and About as real links, and the rest as inert text', () => {
    renderNavBar()

    ;['Home', 'About'].forEach((label) => {
      expect(screen.getByRole('link', { name: label })).toBeInTheDocument()
    })

    ;['Schedule Appointment', 'My Schedule', 'Admin Panel'].forEach((label) => {
      expect(screen.queryByRole('link', { name: label })).toBeNull()
      expect(screen.getByText(label)).toBeInTheDocument()
    })
  })

  it('renders the wordmark', () => {
    renderNavBar()
    expect(screen.getByText('Fake Barbershop')).toBeInTheDocument()
  })

  it('applies the active-link class to the link matching the current route', () => {
    render(
      <AuthProvider>
        <MemoryRouter initialEntries={['/about']}>
          <NavBar />
        </MemoryRouter>
      </AuthProvider>,
    )

    expect(screen.getByRole('link', { name: 'About' })).toHaveClass(
      'nav-bar__link--active',
    )
    expect(screen.getByRole('link', { name: 'Home' })).not.toHaveClass(
      'nav-bar__link--active',
    )
  })

  it('normalizes case and a trailing slash when matching the active link', () => {
    render(
      <AuthProvider>
        <MemoryRouter initialEntries={['/About/']}>
          <NavBar />
        </MemoryRouter>
      </AuthProvider>,
    )

    expect(screen.getByRole('link', { name: 'About' })).toHaveClass(
      'nav-bar__link--active',
    )
  })

  describe('when signed out', () => {
    it('renders Sign In and Register buttons', () => {
      renderNavBar()

      expect(
        screen.getByRole('button', { name: 'Sign In' }),
      ).toBeInTheDocument()
      expect(
        screen.getByRole('button', { name: 'Register' }),
      ).toBeInTheDocument()
    })

    it('navigates to /login when Sign In is clicked', async () => {
      const user = userEvent.setup()
      renderNavBar()

      await user.click(screen.getByRole('button', { name: 'Sign In' }))

      expect(screen.getByText('Login Stub')).toBeInTheDocument()
    })

    it('navigates to /register when Register is clicked', async () => {
      const user = userEvent.setup()
      renderNavBar()

      await user.click(screen.getByRole('button', { name: 'Register' }))

      expect(screen.getByText('Register Stub')).toBeInTheDocument()
    })
  })

  describe('when signed in', () => {
    it('replaces Sign In and Register with a profile dropdown trigger', () => {
      renderNavBar({ signedIn: true })

      expect(screen.queryByRole('button', { name: 'Sign In' })).toBeNull()
      expect(screen.queryByRole('button', { name: 'Register' })).toBeNull()
      expect(
        screen.getByRole('button', { name: 'Account menu' }),
      ).toBeInTheDocument()
    })

    it('clears the session and navigates to / on Logout', async () => {
      vi.spyOn(globalThis, 'fetch').mockResolvedValue({ ok: true })
      const user = userEvent.setup()
      renderNavBar({ signedIn: true })

      await user.click(screen.getByRole('button', { name: 'Account menu' }))
      await user.click(await screen.findByText('Logout'))

      expect(
        await screen.findByRole('button', { name: 'Sign In' }),
      ).toBeInTheDocument()
    })
  })
})
