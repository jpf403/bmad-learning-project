import { describe, it, expect, vi, afterEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import { AuthProvider, useAuth } from './AuthContext'

function AuthProbe() {
  const { user, ready } = useAuth()
  if (!ready) return <div>Loading</div>
  return (
    <div>Ready: {user ? `${user.email} (${user.role})` : 'signed-out'}</div>
  )
}

describe('AuthContext', () => {
  afterEach(() => {
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
      throw new Error(`Unexpected fetch: ${url}`)
    })

    render(
      <AuthProvider>
        <AuthProbe />
      </AuthProvider>,
    )

    expect(screen.getByText('Loading')).toBeInTheDocument()

    expect(
      await screen.findByText('Ready: john@example.com (Customer)'),
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

  it('throws when useAuth is used outside an AuthProvider', () => {
    const consoleError = vi.spyOn(console, 'error').mockImplementation(() => {})

    expect(() => render(<AuthProbe />)).toThrow(
      'useAuth must be used within an AuthProvider',
    )

    consoleError.mockRestore()
  })
})
