import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { StrictMode, useEffect } from 'react'
import { render } from '@testing-library/react'
import { AuthProvider, useAuth } from '../context/AuthContext'
import { loadScript } from '../lib/loadScript'
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
    )
    expect(window.MyzpaxBanner.init).toHaveBeenCalledTimes(1)
    expect(window.MyzpaxBanner.init).toHaveBeenCalledWith({
      getToken: expect.any(Function),
      currentAppId: 'barbershop_demo',
      position: 'static',
    })

    const { getToken } = window.MyzpaxBanner.init.mock.calls[0][0]
    expect(getToken()).toBe('the-zpax-access-token')
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
