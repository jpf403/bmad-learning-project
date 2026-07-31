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
          <Route path="/" element={<div>Home Stub</div>} />
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

  it('retries once after a transient network failure and renders children on success', async () => {
    let meCallCount = 0
    vi.spyOn(globalThis, 'fetch').mockImplementation((url) => {
      if (url.toString().endsWith('/api/auth/me')) {
        meCallCount += 1
        if (meCallCount === 1) {
          return Promise.reject(new Error('network error'))
        }
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
    expect(meCallCount).toBe(2)
  })

  it('redirects to /login when /me fails on both the initial attempt and the retry', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation((url) => {
      if (url.toString().endsWith('/api/auth/me')) {
        return Promise.reject(new Error('network error'))
      }
      return Promise.resolve({ ok: false, status: 401 })
    })

    renderGuarded({ signedIn: true, roles: ['Barber', 'Admin'] })

    expect(await screen.findByText('Login Stub')).toBeInTheDocument()
  })

  it("falls back to '/' when the wrong-role redirect target is an unrecognized role", async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation((url) => {
      if (url.toString().endsWith('/api/auth/me')) {
        return Promise.resolve({
          ok: true,
          json: async () => ({
            id: 1,
            email: 'john@example.com',
            firstName: 'John',
            lastName: 'Smith',
            role: 'Unknown',
          }),
        })
      }
      return Promise.resolve({ ok: false, status: 401 })
    })

    renderGuarded({ signedIn: true, roles: ['Barber', 'Admin'] })

    expect(await screen.findByText('Home Stub')).toBeInTheDocument()
  })

  it('does not redirect to /login while the session bootstrap is still in flight on a fresh load', async () => {
    let resolveRefresh
    const refreshPromise = new Promise((resolve) => {
      resolveRefresh = resolve
    })

    vi.spyOn(globalThis, 'fetch').mockImplementation((url) => {
      const u = url.toString()
      if (u.endsWith('/api/auth/refresh')) return refreshPromise
      if (u.endsWith('/api/auth/me')) {
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

    render(
      <AuthProvider>
        <MemoryRouter initialEntries={['/my-schedule']}>
          <Routes>
            <Route
              path="/my-schedule"
              element={
                <RequireRole roles={['Barber', 'Admin']}>
                  <div>Protected Content</div>
                </RequireRole>
              }
            />
            <Route path="/login" element={<div>Login Stub</div>} />
          </Routes>
        </MemoryRouter>
      </AuthProvider>,
    )

    expect(screen.queryByText('Login Stub')).not.toBeInTheDocument()
    expect(screen.queryByText('Protected Content')).not.toBeInTheDocument()

    resolveRefresh({
      ok: true,
      json: async () => ({ accessToken: 'token-abc' }),
    })

    expect(await screen.findByText('Protected Content')).toBeInTheDocument()
  })
})
