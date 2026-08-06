import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { useEffect } from 'react'
import { render, screen, within } from '@testing-library/react'
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

function renderNavBar({
  signedIn = false,
  role = 'Customer',
  initialEntries = ['/'],
} = {}) {
  const user = { ...SIGNED_IN_USER, role }

  return render(
    <AuthProvider>
      <MemoryRouter initialEntries={initialEntries}>
        <Routes>
          <Route
            path="/"
            element={
              signedIn ? (
                <SignInOnMount user={user}>
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
          <Route
            path="/schedule-appointment"
            element={<div>Schedule Appointment Stub</div>}
          />
          <Route path="/my-schedule" element={<div>My Schedule Stub</div>} />
          <Route path="/admin" element={<div>Admin Panel Stub</div>} />
        </Routes>
      </MemoryRouter>
    </AuthProvider>,
  )
}

describe('NavBar', () => {
  beforeEach(() => {
    // AuthProvider bootstraps a session on mount via /api/auth/refresh; stub it
    // so every test gets a deterministic, no-network "no session" result unless
    // the test overrides this stub itself.
    vi.spyOn(globalThis, 'fetch').mockResolvedValue({ ok: false, status: 401 })
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('renders Home and About as real links', () => {
    renderNavBar()

    ;['Home', 'About'].forEach((label) => {
      expect(screen.getByRole('link', { name: label })).toBeInTheDocument()
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

  describe('role-gated links', () => {
    it('renders none of the role-gated links when signed out', () => {
      renderNavBar()

      ;['Schedule Appointment', 'My Schedule', 'Admin Panel'].forEach(
        (label) => {
          expect(screen.queryByText(label)).toBeNull()
          expect(screen.queryByRole('link', { name: label })).toBeNull()
        },
      )
    })

    it('renders only Schedule Appointment for a signed-in Customer', async () => {
      renderNavBar({ signedIn: true, role: 'Customer' })

      expect(
        await screen.findByRole('link', { name: 'Schedule Appointment' }),
      ).toBeInTheDocument()
      expect(screen.queryByText('My Schedule')).toBeNull()
      expect(screen.queryByText('Admin Panel')).toBeNull()
    })

    it('renders Schedule Appointment and My Schedule, but not Admin Panel, for a signed-in Barber', async () => {
      renderNavBar({ signedIn: true, role: 'Barber' })

      expect(
        await screen.findByRole('link', { name: 'Schedule Appointment' }),
      ).toBeInTheDocument()
      expect(
        screen.getByRole('link', { name: 'My Schedule' }),
      ).toBeInTheDocument()
      expect(screen.queryByText('Admin Panel')).toBeNull()
    })

    it('renders all three role-gated links for a signed-in Admin', async () => {
      renderNavBar({ signedIn: true, role: 'Admin' })

      expect(
        await screen.findByRole('link', { name: 'Schedule Appointment' }),
      ).toBeInTheDocument()
      expect(
        screen.getByRole('link', { name: 'My Schedule' }),
      ).toBeInTheDocument()
      expect(
        screen.getByRole('link', { name: 'Admin Panel' }),
      ).toBeInTheDocument()
    })
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
      vi.spyOn(globalThis, 'fetch').mockImplementation((url) => {
        if (url.toString().endsWith('/api/auth/logout')) {
          return Promise.resolve({ ok: true })
        }
        return Promise.resolve({ ok: false, status: 401 })
      })
      const user = userEvent.setup()
      renderNavBar({ signedIn: true })

      await user.click(screen.getByRole('button', { name: 'Account menu' }))
      await user.click(await screen.findByText('Logout'))

      expect(
        await screen.findByRole('button', { name: 'Sign In' }),
      ).toBeInTheDocument()
    })
  })

  describe('collapsed navigation menu', () => {
    it('renders a menu button that toggles a dropdown of the nav links', async () => {
      const user = userEvent.setup()
      renderNavBar()

      await user.click(screen.getByRole('button', { name: 'Menu' }))

      const menu = await screen.findByRole('menu')
      expect(
        within(menu).getByRole('menuitem', { name: 'Home' }),
      ).toBeInTheDocument()
      expect(
        within(menu).getByRole('menuitem', { name: 'About' }),
      ).toBeInTheDocument()
    })

    it('includes role-gated links in the collapsed menu for a signed-in Customer', async () => {
      const user = userEvent.setup()
      renderNavBar({ signedIn: true, role: 'Customer' })
      await screen.findByRole('link', { name: 'Schedule Appointment' })

      await user.click(screen.getByRole('button', { name: 'Menu' }))

      const menu = await screen.findByRole('menu')
      expect(
        within(menu).getByRole('menuitem', { name: 'Schedule Appointment' }),
      ).toBeInTheDocument()
    })
  })
})
