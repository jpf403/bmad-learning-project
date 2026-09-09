import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/react'
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
    // Cross-tab leader-election state lives in localStorage, which jsdom
    // persists across tests in the same file -- clear it so one test's
    // leader claim can't make a later test's own tab defer to a "leader"
    // that no longer exists.
    localStorage.clear()
  })

  afterEach(() => {
    vi.useRealTimers()
    vi.restoreAllMocks()
    localStorage.clear()
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

  it('adopts a refreshed z-pax token after the scheduled interval elapses, and keeps refreshing on later cycles', async () => {
    let refreshCount = 0
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
        refreshCount += 1
        return Promise.resolve({
          ok: true,
          json: async () => ({
            zpaxAccessToken: `the-refreshed-zpax-token-${refreshCount}`,
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
        'Ready: john@example.com (Customer) zpax:the-zpax-access-token',
      ),
    ).toBeInTheDocument()

    await vi.advanceTimersByTimeAsync(ZPAX_REFRESH_INTERVAL_MS)

    expect(
      await screen.findByText(
        'Ready: john@example.com (Customer) zpax:the-refreshed-zpax-token-1',
      ),
    ).toBeInTheDocument()

    // A second successful cycle must still fire -- proves the interval isn't
    // torn down and left unreplaced after its first successful refresh.
    await vi.advanceTimersByTimeAsync(ZPAX_REFRESH_INTERVAL_MS)

    expect(
      await screen.findByText(
        'Ready: john@example.com (Customer) zpax:the-refreshed-zpax-token-2',
      ),
    ).toBeInTheDocument()
  })

  it('only lets one of two open tabs call zpax-refresh per cycle, and the other adopts its broadcast result', async () => {
    let refreshCallCount = 0
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
        refreshCallCount += 1
        return Promise.resolve({
          ok: true,
          json: async () => ({ zpaxAccessToken: 'the-refreshed-zpax-token' }),
        })
      }
      throw new Error(`Unexpected fetch: ${url}`)
    })

    function AuthProbeNamed({ name }) {
      const { user, ready } = useAuth()
      if (!ready) return <div>{name}: Loading</div>
      if (!user) return <div>{name}: signed-out</div>
      return (
        <div>
          {name}: zpax:{user.zpaxAccessToken ?? 'none'}
        </div>
      )
    }

    render(
      <>
        <AuthProvider>
          <AuthProbeNamed name="TabA" />
        </AuthProvider>
        <AuthProvider>
          <AuthProbeNamed name="TabB" />
        </AuthProvider>
      </>,
    )

    expect(
      await screen.findByText('TabA: zpax:the-zpax-access-token'),
    ).toBeInTheDocument()
    expect(
      await screen.findByText('TabB: zpax:the-zpax-access-token'),
    ).toBeInTheDocument()

    await vi.advanceTimersByTimeAsync(ZPAX_REFRESH_INTERVAL_MS)

    expect(
      await screen.findByText('TabA: zpax:the-refreshed-zpax-token'),
    ).toBeInTheDocument()
    expect(
      await screen.findByText('TabB: zpax:the-refreshed-zpax-token'),
    ).toBeInTheDocument()
    expect(refreshCallCount).toBe(1)
  })

  it('releases leadership on pagehide so a surviving tab need not wait out the staleness window', async () => {
    let refreshCallCount = 0
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
        refreshCallCount += 1
        return Promise.resolve({
          ok: true,
          json: async () => ({
            zpaxAccessToken: `the-refreshed-zpax-token-${refreshCallCount}`,
          }),
        })
      }
      throw new Error(`Unexpected fetch: ${url}`)
    })

    function AuthProbeNamed({ name }) {
      const { user, ready } = useAuth()
      if (!ready) return <div>{name}: Loading</div>
      if (!user) return <div>{name}: signed-out</div>
      return (
        <div>
          {name}: zpax:{user.zpaxAccessToken ?? 'none'}
        </div>
      )
    }

    const tabA = render(
      <AuthProvider>
        <AuthProbeNamed name="TabA" />
      </AuthProvider>,
    )
    render(
      <AuthProvider>
        <AuthProbeNamed name="TabB" />
      </AuthProvider>,
    )

    // TabA wins leadership by completing the first scheduled cycle.
    await screen.findByText('TabA: zpax:the-zpax-access-token')
    await screen.findByText('TabB: zpax:the-zpax-access-token')
    await vi.advanceTimersByTimeAsync(ZPAX_REFRESH_INTERVAL_MS)
    await screen.findByText('TabA: zpax:the-refreshed-zpax-token-1')
    await screen.findByText('TabB: zpax:the-refreshed-zpax-token-1')
    expect(refreshCallCount).toBe(1)
    expect(localStorage.getItem('zpaxRefreshLeaderId')).not.toBeNull()

    // TabA closes: pagehide fires first (this is what a real tab close does,
    // and is what actually releases leadership), then the tab itself is torn
    // down so its own interval stops competing on later cycles -- mirroring
    // a real closed tab, which no longer has any JS running at all.
    fireEvent(window, new Event('pagehide'))
    expect(localStorage.getItem('zpaxRefreshLeaderId')).toBeNull()
    tabA.unmount()

    // TabB's own next cycle can now claim leadership immediately -- it does
    // not have to wait out the 110-minute staleness window.
    await vi.advanceTimersByTimeAsync(ZPAX_REFRESH_INTERVAL_MS)
    await screen.findByText('TabB: zpax:the-refreshed-zpax-token-2')
    expect(refreshCallCount).toBe(2)
  })

  it("a non-leader tab signing out does not wipe another tab's still-active leadership", async () => {
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

    function LogoutButton() {
      const { logout } = useAuth()
      return <button onClick={logout}>Sign out this tab</button>
    }

    render(
      <>
        <AuthProvider>
          <AuthProbe />
        </AuthProvider>
        <AuthProvider>
          <LogoutButton />
        </AuthProvider>
      </>,
    )

    // TabA becomes leader by completing a scheduled refresh cycle.
    await screen.findByText(
      'Ready: john@example.com (Customer) zpax:the-zpax-access-token',
    )
    await vi.advanceTimersByTimeAsync(ZPAX_REFRESH_INTERVAL_MS)
    await screen.findByText(
      'Ready: john@example.com (Customer) zpax:the-refreshed-zpax-token',
    )
    const leaderIdAfterTabALeads = localStorage.getItem('zpaxRefreshLeaderId')
    expect(leaderIdAfterTabALeads).not.toBeNull()

    // TabB also bootstrapped a session and armed its own scheduled cycle,
    // but lost the leadership claim to TabA. It signs out here -- that must
    // not clear TabA's still-legitimate leadership record just because
    // TabB also holds an AuthProvider.
    fireEvent.click(screen.getByText('Sign out this tab'))

    expect(localStorage.getItem('zpaxRefreshLeaderId')).toBe(
      leaderIdAfterTabALeads,
    )
  })

  it('refreshes this app’s own session before the scheduled zpax-refresh call, and uses the new access token for it', async () => {
    let ownRefreshCount = 0
    let capturedAuthHeader = null
    vi.spyOn(globalThis, 'fetch').mockImplementation((url, init) => {
      if (url.toString().endsWith('/api/auth/refresh')) {
        ownRefreshCount += 1
        return Promise.resolve({
          ok: true,
          json: async () => ({
            accessToken: `own-access-token-${ownRefreshCount}`,
          }),
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
        capturedAuthHeader = init?.headers?.Authorization ?? null
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

    await screen.findByText(
      'Ready: john@example.com (Customer) zpax:the-refreshed-zpax-token',
    )

    // The scheduled cycle's own-session refresh happened (bootstrap's own
    // refresh is call #1, the interval's is call #2), and its result -- not
    // the original bootstrap token -- was the Bearer credential sent to
    // zpax-refresh.
    expect(ownRefreshCount).toBe(2)
    expect(capturedAuthHeader).toBe('Bearer own-access-token-2')
  })

  it('signs out immediately, without ever calling zpax-refresh, when the scheduled cycle finds this app’s own session already dead', async () => {
    let sessionRefreshCount = 0
    let zpaxRefreshCalled = false
    vi.spyOn(globalThis, 'fetch').mockImplementation((url) => {
      if (url.toString().endsWith('/api/auth/refresh')) {
        sessionRefreshCount += 1
        if (sessionRefreshCount === 1) {
          return Promise.resolve({
            ok: true,
            json: async () => ({ accessToken: 'new-access-token' }),
          })
        }
        return Promise.resolve({ ok: false, status: 401 })
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
        zpaxRefreshCalled = true
        return Promise.resolve({
          ok: true,
          json: async () => ({ zpaxAccessToken: 'the-refreshed-zpax-token' }),
        })
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

      await vi.waitFor(() =>
        expect(window.location.assign).toHaveBeenCalledWith('/login'),
      )
      expect(zpaxRefreshCalled).toBe(false)
    } finally {
      window.location = originalLocation
    }
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
