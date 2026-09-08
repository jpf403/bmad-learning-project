import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import { AuthProvider, useAuth } from './AuthContext'

// Mirrors AuthContext.jsx's ZPAX_REFRESH_INTERVAL_MS -- a 5-minute safety
// margin ahead of z-pax's 60-minute access-token lifetime.
const ZPAX_REFRESH_INTERVAL_MS = 55 * 60 * 1000

function AuthProbe() {
  const { user, ready } = useAuth()
  if (!ready) return <div>Loading</div>
  if (!user) return <div>Ready: signed-out</div>
  return (
    <div>
      Ready: {user.email} ({user.role}) zpax:
      {user.zpaxAccessToken ?? 'none'}
    </div>
  )
}

describe('AuthContext', () => {
  beforeEach(() => {
    vi.useFakeTimers({ shouldAdvanceTime: true })
  })

  afterEach(() => {
    vi.useRealTimers()
    vi.restoreAllMocks()
  })

  it('rehydrates the user from refresh + me on mount when a session exists', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation((url) => {
      if (url.toString().endsWith('/api/auth/refresh')) {
        return Promise.resolve({
          ok: true,
          json: async () => ({ accessToken: 'new-access-token' }),
        })
      }
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
      if (url.toString().endsWith('/api/auth/sso/zpax-token')) {
        return Promise.resolve({ ok: false, status: 404 })
      }
      if (url.toString().endsWith('/api/auth/sso/zpax-refresh')) {
        return Promise.resolve({ ok: false, status: 404 })
      }
      throw new Error(`Unexpected fetch: ${url}`)
    })

    render(
      <AuthProvider>
        <AuthProbe />
      </AuthProvider>,
    )

    expect(screen.getByText('Loading')).toBeInTheDocument()

    expect(
      await screen.findByText('Ready: john@example.com (Customer) zpax:none'),
    ).toBeInTheDocument()
  })

  it('falls back to zpax-refresh when the one-time zpax-token pickup has already been consumed', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation((url) => {
      if (url.toString().endsWith('/api/auth/refresh')) {
        return Promise.resolve({
          ok: true,
          json: async () => ({ accessToken: 'new-access-token' }),
        })
      }
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
      if (url.toString().endsWith('/api/auth/sso/zpax-token')) {
        return Promise.resolve({ ok: false, status: 404 })
      }
      if (url.toString().endsWith('/api/auth/sso/zpax-refresh')) {
        return Promise.resolve({
          ok: true,
          json: async () => ({
            zpaxAccessToken: 'the-bootstrap-refreshed-token',
          }),
        })
      }
      throw new Error(`Unexpected fetch: ${url}`)
    })

    render(
      <AuthProvider>
        <AuthProbe />
      </AuthProvider>,
    )

    expect(
      await screen.findByText(
        'Ready: john@example.com (Customer) zpax:the-bootstrap-refreshed-token',
      ),
    ).toBeInTheDocument()
  })

  it('forces a full sign-out and redirects to /login when the bootstrap fallback refresh gets a 401', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation((url) => {
      if (url.toString().endsWith('/api/auth/refresh')) {
        return Promise.resolve({
          ok: true,
          json: async () => ({ accessToken: 'new-access-token' }),
        })
      }
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
      if (url.toString().endsWith('/api/auth/sso/zpax-token')) {
        return Promise.resolve({ ok: false, status: 404 })
      }
      if (url.toString().endsWith('/api/auth/sso/zpax-refresh')) {
        return Promise.resolve({ ok: false, status: 401 })
      }
      throw new Error(`Unexpected fetch: ${url}`)
    })

    const originalLocation = window.location
    delete window.location
    window.location = { ...originalLocation, assign: vi.fn() }

    try {
      render(
        <AuthProvider>
          <AuthProbe />
        </AuthProvider>,
      )

      await vi.waitFor(() =>
        expect(window.location.assign).toHaveBeenCalledWith('/login'),
      )
    } finally {
      window.location = originalLocation
    }
  })

  it('holds the z-pax access token in memory when the pickup endpoint returns one', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation((url) => {
      if (url.toString().endsWith('/api/auth/refresh')) {
        return Promise.resolve({
          ok: true,
          json: async () => ({ accessToken: 'new-access-token' }),
        })
      }
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
      if (url.toString().endsWith('/api/auth/sso/zpax-token')) {
        return Promise.resolve({
          ok: true,
          json: async () => ({ zpaxAccessToken: 'the-zpax-access-token' }),
        })
      }
      throw new Error(`Unexpected fetch: ${url}`)
    })

    render(
      <AuthProvider>
        <AuthProbe />
      </AuthProvider>,
    )

    expect(
      await screen.findByText(
        'Ready: john@example.com (Customer) zpax:the-zpax-access-token',
      ),
    ).toBeInTheDocument()
  })

  it('stays signed out with no unhandled rejection when refresh fails', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue({ ok: false, status: 401 })

    render(
      <AuthProvider>
        <AuthProbe />
      </AuthProvider>,
    )

    expect(await screen.findByText('Ready: signed-out')).toBeInTheDocument()
  })

  it('stays signed out with no unhandled rejection when /me returns a malformed body', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation((url) => {
      if (url.toString().endsWith('/api/auth/refresh')) {
        return Promise.resolve({
          ok: true,
          json: async () => ({ accessToken: 'new-access-token' }),
        })
      }
      if (url.toString().endsWith('/api/auth/me')) {
        return Promise.resolve({
          ok: true,
          status: 200,
          json: async () => {
            throw new SyntaxError('Unexpected end of JSON input')
          },
        })
      }
      throw new Error(`Unexpected fetch: ${url}`)
    })

    render(
      <AuthProvider>
        <AuthProbe />
      </AuthProvider>,
    )

    expect(await screen.findByText('Ready: signed-out')).toBeInTheDocument()
  })

  it('adopts a refreshed z-pax token after the scheduled interval elapses', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation((url) => {
      if (url.toString().endsWith('/api/auth/refresh')) {
        return Promise.resolve({
          ok: true,
          json: async () => ({ accessToken: 'new-access-token' }),
        })
      }
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
      if (url.toString().endsWith('/api/auth/sso/zpax-token')) {
        return Promise.resolve({
          ok: true,
          json: async () => ({ zpaxAccessToken: 'the-zpax-access-token' }),
        })
      }
      if (url.toString().endsWith('/api/auth/sso/zpax-refresh')) {
        return Promise.resolve({
          ok: true,
          json: async () => ({ zpaxAccessToken: 'the-refreshed-zpax-token' }),
        })
      }
      throw new Error(`Unexpected fetch: ${url}`)
    })

    render(
      <AuthProvider>
        <AuthProbe />
      </AuthProvider>,
    )

    expect(
      await screen.findByText(
        'Ready: john@example.com (Customer) zpax:the-zpax-access-token',
      ),
    ).toBeInTheDocument()

    await vi.advanceTimersByTimeAsync(ZPAX_REFRESH_INTERVAL_MS)

    expect(
      await screen.findByText(
        'Ready: john@example.com (Customer) zpax:the-refreshed-zpax-token',
      ),
    ).toBeInTheDocument()
  })

  it('degrades to no token, with no retry, when the scheduled refresh fails', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation((url) => {
      if (url.toString().endsWith('/api/auth/refresh')) {
        return Promise.resolve({
          ok: true,
          json: async () => ({ accessToken: 'new-access-token' }),
        })
      }
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
      if (url.toString().endsWith('/api/auth/sso/zpax-token')) {
        return Promise.resolve({
          ok: true,
          json: async () => ({ zpaxAccessToken: 'the-zpax-access-token' }),
        })
      }
      if (url.toString().endsWith('/api/auth/sso/zpax-refresh')) {
        return Promise.resolve({ ok: false, status: 404 })
      }
      throw new Error(`Unexpected fetch: ${url}`)
    })

    render(
      <AuthProvider>
        <AuthProbe />
      </AuthProvider>,
    )

    expect(
      await screen.findByText(
        'Ready: john@example.com (Customer) zpax:the-zpax-access-token',
      ),
    ).toBeInTheDocument()

    const fetchSpy = vi.mocked(globalThis.fetch)
    await vi.advanceTimersByTimeAsync(ZPAX_REFRESH_INTERVAL_MS)

    expect(
      await screen.findByText('Ready: john@example.com (Customer) zpax:none'),
    ).toBeInTheDocument()

    const refreshCallCount = fetchSpy.mock.calls.filter((call) =>
      call[0].toString().endsWith('/api/auth/sso/zpax-refresh'),
    ).length
    expect(refreshCallCount).toBe(1)

    await vi.advanceTimersByTimeAsync(ZPAX_REFRESH_INTERVAL_MS)

    const refreshCallCountAfterSecondInterval = fetchSpy.mock.calls.filter(
      (call) => call[0].toString().endsWith('/api/auth/sso/zpax-refresh'),
    ).length
    expect(refreshCallCountAfterSecondInterval).toBe(1)
  })

  it('forces a full sign-out and redirects to /login when the scheduled refresh gets a 401 (z-pax rejected the refresh token)', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation((url) => {
      if (url.toString().endsWith('/api/auth/refresh')) {
        return Promise.resolve({
          ok: true,
          json: async () => ({ accessToken: 'new-access-token' }),
        })
      }
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
      if (url.toString().endsWith('/api/auth/sso/zpax-token')) {
        return Promise.resolve({
          ok: true,
          json: async () => ({ zpaxAccessToken: 'the-zpax-access-token' }),
        })
      }
      if (url.toString().endsWith('/api/auth/sso/zpax-refresh')) {
        return Promise.resolve({ ok: false, status: 401 })
      }
      throw new Error(`Unexpected fetch: ${url}`)
    })

    const originalLocation = window.location
    delete window.location
    window.location = { ...originalLocation, assign: vi.fn() }

    try {
      render(
        <AuthProvider>
          <AuthProbe />
        </AuthProvider>,
      )

      expect(
        await screen.findByText(
          'Ready: john@example.com (Customer) zpax:the-zpax-access-token',
        ),
      ).toBeInTheDocument()

      await vi.advanceTimersByTimeAsync(ZPAX_REFRESH_INTERVAL_MS)

      expect(await screen.findByText('Ready: signed-out')).toBeInTheDocument()
      expect(window.location.assign).toHaveBeenCalledWith('/login')
    } finally {
      window.location = originalLocation
    }
  })

  it('never schedules further zpax-refresh calls for a password-only session, beyond the one bootstrap fallback attempt', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation((url) => {
      if (url.toString().endsWith('/api/auth/refresh')) {
        return Promise.resolve({
          ok: true,
          json: async () => ({ accessToken: 'new-access-token' }),
        })
      }
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
      if (url.toString().endsWith('/api/auth/sso/zpax-token')) {
        return Promise.resolve({ ok: false, status: 404 })
      }
      if (url.toString().endsWith('/api/auth/sso/zpax-refresh')) {
        return Promise.resolve({ ok: false, status: 404 })
      }
      throw new Error(`Unexpected fetch: ${url}`)
    })

    render(
      <AuthProvider>
        <AuthProbe />
      </AuthProvider>,
    )

    expect(
      await screen.findByText('Ready: john@example.com (Customer) zpax:none'),
    ).toBeInTheDocument()

    const fetchSpy = vi.mocked(globalThis.fetch)
    const refreshCallCountAfterBootstrap = fetchSpy.mock.calls.filter((call) =>
      call[0].toString().endsWith('/api/auth/sso/zpax-refresh'),
    ).length
    expect(refreshCallCountAfterBootstrap).toBe(1)

    await vi.advanceTimersByTimeAsync(ZPAX_REFRESH_INTERVAL_MS + 5 * 60 * 1000)

    const refreshCallCountAfterInterval = fetchSpy.mock.calls.filter((call) =>
      call[0].toString().endsWith('/api/auth/sso/zpax-refresh'),
    ).length
    expect(refreshCallCountAfterInterval).toBe(1)
  })

  it('throws when useAuth is used outside an AuthProvider', () => {
    const consoleError = vi.spyOn(console, 'error').mockImplementation(() => {})

    expect(() => render(<AuthProbe />)).toThrow(
      'useAuth must be used within an AuthProvider',
    )

    consoleError.mockRestore()
  })
})
