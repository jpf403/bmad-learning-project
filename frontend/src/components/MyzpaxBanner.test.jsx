import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { StrictMode, useEffect } from 'react'
import { render, screen, waitFor } from '@testing-library/react'
import { AuthProvider, useAuth } from '../context/AuthContext'
import { loadScript } from '../lib/loadScript'
import { API_BASE_URL } from '../api/ApiConfig'
import MyzpaxBanner from './MyzpaxBanner'

vi.mock('../lib/loadScript', () => ({
  loadScript: vi.fn(),
}))

function SignInOnMount({ user, children }) {
  const { login } = useAuth()

  useEffect(() => {
    login(user)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  return children
}

function AuthStateProbe() {
  const { user } = useAuth()
  return <div data-testid="auth-state">{user ? 'signed-in' : 'signed-out'}</div>
}

function renderBanner(user) {
  return render(
    <AuthProvider>
      {user ? (
        <SignInOnMount user={user}>
          <MyzpaxBanner />
        </SignInOnMount>
      ) : (
        <MyzpaxBanner />
      )}
    </AuthProvider>,
  )
}

const SIGNED_IN_WITH_TOKEN = {
  accessToken: 'token-abc',
  id: 1,
  email: 'john@example.com',
  role: 'Customer',
  zpaxAccessToken: 'the-zpax-access-token',
}

describe('MyzpaxBanner', () => {
  beforeEach(() => {
    // AuthProvider bootstraps a session on mount via /api/auth/refresh; stub it
    // so every test gets a deterministic, no-network "no session" result unless
    // the test itself logs a user in via SignInOnMount.
    vi.spyOn(globalThis, 'fetch').mockResolvedValue({ ok: false, status: 401 })
    loadScript.mockImplementation((_src, onLoad) => onLoad())
    window.MyzpaxBanner = { init: vi.fn() }
  })

  afterEach(() => {
    vi.restoreAllMocks()
    delete window.MyzpaxBanner
  })

  it('mounts no script when there is no signed-in user', () => {
    renderBanner(null)

    expect(loadScript).not.toHaveBeenCalled()
  })

  it('mounts no script when the signed-in user has no z-pax token', () => {
    renderBanner({ ...SIGNED_IN_WITH_TOKEN, zpaxAccessToken: null })

    expect(loadScript).not.toHaveBeenCalled()
  })

  it('loads the banner script and initializes it once a z-pax token is present', () => {
    renderBanner(SIGNED_IN_WITH_TOKEN)

    expect(loadScript).toHaveBeenCalledTimes(1)
    expect(loadScript).toHaveBeenCalledWith(
      'https://dev.zpax-banner.myzpax.com/banner/v1/banner.js',
      expect.any(Function),
      expect.any(Function),
    )
    expect(window.MyzpaxBanner.init).toHaveBeenCalledTimes(1)
    expect(window.MyzpaxBanner.init).toHaveBeenCalledWith({
      getToken: expect.any(Function),
      currentAppId: 'barbershop_demo',
      position: 'static',
      onLogout: expect.any(Function),
    })

    const { getToken } = window.MyzpaxBanner.init.mock.calls[0][0]
    expect(getToken()).toBe('the-zpax-access-token')
  })

  it('tears down the app session and redirects to the sso logout endpoint when onLogout fires', async () => {
    render(
      <AuthProvider>
        <SignInOnMount user={SIGNED_IN_WITH_TOKEN}>
          <MyzpaxBanner />
          <AuthStateProbe />
        </SignInOnMount>
      </AuthProvider>,
    )

    expect(await screen.findByTestId('auth-state')).toHaveTextContent(
      'signed-in',
    )

    const originalLocation = window.location
    delete window.location
    window.location = { ...originalLocation, assign: vi.fn() }

    try {
      const { onLogout } = window.MyzpaxBanner.init.mock.calls[0][0]
      await onLogout()

      expect(globalThis.fetch).toHaveBeenCalledWith(
        `${API_BASE_URL}/api/auth/logout`,
        expect.objectContaining({
          method: 'POST',
          headers: { Authorization: 'Bearer token-abc' },
        }),
      )
      await waitFor(() =>
        expect(screen.getByTestId('auth-state')).toHaveTextContent(
          'signed-out',
        ),
      )
      expect(window.location.assign).toHaveBeenCalledWith(
        `${API_BASE_URL}/api/auth/sso/logout`,
      )
    } finally {
      window.location = originalLocation
    }
  })

  it('skips the app logout fetch when there is no app access token', async () => {
    render(
      <AuthProvider>
        <SignInOnMount user={{ ...SIGNED_IN_WITH_TOKEN, accessToken: null }}>
          <MyzpaxBanner />
        </SignInOnMount>
      </AuthProvider>,
    )

    const originalLocation = window.location
    delete window.location
    window.location = { ...originalLocation, assign: vi.fn() }

    try {
      const { onLogout } = window.MyzpaxBanner.init.mock.calls[0][0]
      await onLogout()

      expect(globalThis.fetch).not.toHaveBeenCalledWith(
        `${API_BASE_URL}/api/auth/logout`,
        expect.anything(),
      )
      expect(window.location.assign).toHaveBeenCalledWith(
        `${API_BASE_URL}/api/auth/sso/logout`,
      )
    } finally {
      window.location = originalLocation
    }
  })

  it('ignores a re-entrant onLogout call while one is already in flight', async () => {
    render(
      <AuthProvider>
        <SignInOnMount user={SIGNED_IN_WITH_TOKEN}>
          <MyzpaxBanner />
        </SignInOnMount>
      </AuthProvider>,
    )

    const originalLocation = window.location
    delete window.location
    window.location = { ...originalLocation, assign: vi.fn() }

    try {
      const { onLogout } = window.MyzpaxBanner.init.mock.calls[0][0]
      const first = onLogout()
      const second = onLogout()
      await Promise.all([first, second])

      const logoutCalls = globalThis.fetch.mock.calls.filter(
        ([url]) => url === `${API_BASE_URL}/api/auth/logout`,
      )
      expect(logoutCalls).toHaveLength(1)
      expect(window.location.assign).toHaveBeenCalledTimes(1)
    } finally {
      window.location = originalLocation
    }
  })

  it('logs an error and does not throw when the banner script fails to load', () => {
    loadScript.mockImplementation((_src, _onLoad, onError) => onError())
    const consoleError = vi.spyOn(console, 'error').mockImplementation(() => {})

    expect(() => renderBanner(SIGNED_IN_WITH_TOKEN)).not.toThrow()

    expect(window.MyzpaxBanner.init).not.toHaveBeenCalled()
    expect(consoleError).toHaveBeenCalled()
  })

  it('logs an error and does not throw when the loaded script does not define window.MyzpaxBanner', () => {
    delete window.MyzpaxBanner
    const consoleError = vi.spyOn(console, 'error').mockImplementation(() => {})

    expect(() => renderBanner(SIGNED_IN_WITH_TOKEN)).not.toThrow()

    expect(consoleError).toHaveBeenCalled()
  })

  it('initializes the banner exactly once under StrictMode double-invoked effects', () => {
    render(
      <StrictMode>
        <AuthProvider>
          <SignInOnMount user={SIGNED_IN_WITH_TOKEN}>
            <MyzpaxBanner />
          </SignInOnMount>
        </AuthProvider>
      </StrictMode>,
    )

    expect(window.MyzpaxBanner.init).toHaveBeenCalledTimes(1)
  })
})
