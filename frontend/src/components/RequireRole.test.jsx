import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { useEffect } from 'react'
import { render, screen } from '@testing-library/react'
import { MemoryRouter, Routes, Route, useNavigate } from 'react-router'
import { AuthProvider, useAuth } from '../context/AuthContext'
import RequireRole from './RequireRole'

const SIGNED_IN_USER = {
  accessToken: 'token-abc',
  id: 1,
  email: 'john@example.com',
  firstName: 'John',
  lastName: 'Smith',
  role: 'Customer',
}

// Signs in (if a user is given) and only then navigates to the guarded route,
// so RequireRole's first-ever render already sees the settled AuthContext
// user -- mirroring how Login sets context state before the app ever
// navigates to a protected page.
function SignInThenNavigate({ user, to }) {
  const { login } = useAuth()
  const navigate = useNavigate()

  useEffect(() => {
    if (user) login(user)
    navigate(to, { replace: true })
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  return null
}

function renderGuarded({ signedIn = false, roles = ['Barber', 'Admin'] } = {}) {
  return render(
    <AuthProvider>
      <MemoryRouter initialEntries={['/entry']}>
        <Routes>
          <Route
            path="/entry"
            element={
              <SignInThenNavigate
                user={signedIn ? SIGNED_IN_USER : null}
                to="/my-schedule"
              />
            }
          />
          <Route
            path="/my-schedule"
            element={
              <RequireRole roles={roles}>
                <div>Protected Content</div>
              </RequireRole>
            }
          />
          <Route path="/login" element={<div>Login Stub</div>} />
          <Route
            path="/schedule-appointment"
            element={<div>Schedule Appointment Stub</div>}
          />
        </Routes>
      </MemoryRouter>
    </AuthProvider>,
  )
}

describe('RequireRole', () => {
  beforeEach(() => {
    // AuthProvider bootstraps a session via /api/auth/refresh on mount; default
    // this to "no session" so it never overrides the user set in the test.
    vi.spyOn(globalThis, 'fetch').mockResolvedValue({ ok: false, status: 401 })
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('redirects to /login when no signed-in user is in context', async () => {
    renderGuarded({ signedIn: false })

    expect(await screen.findByText('Login Stub')).toBeInTheDocument()
  })

  it("redirects to the user's default landing route when the role check fails", async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation((url) => {
      if (url.toString().endsWith('/api/auth/me')) {
        return Promise.resolve({
          ok: true,
          json: async () => ({
            id: 1,
            email: 'john@example.com',
            firstName: 'John',
            lastName: 'Smith',
            role: 'Customer',
          }),
        })
      }
      return Promise.resolve({ ok: false, status: 401 })
    })

    renderGuarded({ signedIn: true, roles: ['Barber', 'Admin'] })

    expect(
      await screen.findByText('Schedule Appointment Stub'),
    ).toBeInTheDocument()
  })

  it('renders children when the fresh /me role is allowed', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation((url) => {
      if (url.toString().endsWith('/api/auth/me')) {
        return Promise.resolve({
          ok: true,
          json: async () => ({
            id: 1,
            email: 'john@example.com',
            firstName: 'John',
            lastName: 'Smith',
            role: 'Barber',
          }),
        })
      }
      return Promise.resolve({ ok: false, status: 401 })
    })

    renderGuarded({ signedIn: true, roles: ['Barber', 'Admin'] })

    expect(await screen.findByText('Protected Content')).toBeInTheDocument()
  })
})
