import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { useEffect } from 'react'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Routes, Route } from 'react-router'
import { AuthProvider, useAuth } from '../context/AuthContext'
import { searchAccounts } from '../api/AccountApi'
import AdminPanel from './AdminPanel'

vi.mock('../api/AccountApi', () => ({
  searchAccounts: vi.fn(),
}))

const SIGNED_IN_ADMIN = {
  accessToken: 'token-abc',
  id: 1,
  email: 'admin@example.com',
  firstName: 'John',
  lastName: 'Smith',
  role: 'Admin',
}

// AdminPanel assumes a signed-in user is already in context (RequireRole's
// job on the real route).
function SignInThenRenderPage() {
  const { user, login } = useAuth()

  useEffect(() => {
    login(SIGNED_IN_ADMIN)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  if (!user) return null
  return <AdminPanel />
}

function renderPage() {
  return render(
    <AuthProvider>
      <MemoryRouter initialEntries={['/admin']}>
        <Routes>
          <Route path="/admin" element={<SignInThenRenderPage />} />
        </Routes>
      </MemoryRouter>
    </AuthProvider>,
  )
}

describe('AdminPanel', () => {
  beforeEach(() => {
    searchAccounts.mockReset()
    // AuthProvider bootstraps a session via /api/auth/refresh on mount; default
    // this to "no session" so it never interferes with the direct login() call.
    vi.spyOn(globalThis, 'fetch').mockResolvedValue({ ok: false, status: 401 })
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('renders "Search by name or email to find an account." before any search', async () => {
    renderPage()

    expect(
      await screen.findByText('Search by name or email to find an account.'),
    ).toBeInTheDocument()
  })

  it('submitting a blank/whitespace-only query does not call searchAccounts and leaves the "before any search" message visible', async () => {
    const user = userEvent.setup()
    renderPage()
    await screen.findByText('Search by name or email to find an account.')

    await user.type(screen.getByLabelText('Search'), '   ')
    await user.click(screen.getByRole('button', { name: 'Search' }))

    expect(
      screen.getByText('Search by name or email to find an account.'),
    ).toBeInTheDocument()
    expect(searchAccounts).not.toHaveBeenCalled()
  })

  it('submitting a query that returns matches renders one row per account', async () => {
    searchAccounts.mockResolvedValue({
      ok: true,
      accounts: [
        {
          id: 1,
          email: 'anderson@example.com',
          firstName: 'Anderson',
          lastName: 'Cooper',
          role: 'Customer',
        },
        {
          id: 2,
          email: 'zed@example.com',
          firstName: 'Zed',
          lastName: 'Barberton',
          role: 'Barber',
        },
      ],
    })
    const user = userEvent.setup()
    renderPage()
    await screen.findByText('Search by name or email to find an account.')

    await user.type(screen.getByLabelText('Search'), 'ander')
    await user.click(screen.getByRole('button', { name: 'Search' }))

    expect(await screen.findByText('Anderson Cooper')).toBeInTheDocument()
    expect(screen.getByText('anderson@example.com')).toBeInTheDocument()
    expect(screen.getByText('Zed Barberton')).toBeInTheDocument()
    expect(screen.getByText('zed@example.com')).toBeInTheDocument()
    expect(searchAccounts).toHaveBeenCalledWith('token-abc', 'ander')
  })

  it('submitting a query that returns no matches shows "No accounts match your search."', async () => {
    searchAccounts.mockResolvedValue({ ok: true, accounts: [] })
    const user = userEvent.setup()
    renderPage()
    await screen.findByText('Search by name or email to find an account.')

    await user.type(screen.getByLabelText('Search'), 'zzz-no-such-account')
    await user.click(screen.getByRole('button', { name: 'Search' }))

    expect(
      await screen.findByText('No accounts match your search.'),
    ).toBeInTheDocument()
  })

  it('a failed fetch shows the error message + "Try again" button; clicking it resubmits the same query', async () => {
    searchAccounts.mockResolvedValueOnce({ ok: false, status: 500 })
    const user = userEvent.setup()
    renderPage()
    await screen.findByText('Search by name or email to find an account.')

    await user.type(screen.getByLabelText('Search'), 'ander')
    await user.click(screen.getByRole('button', { name: 'Search' }))

    expect(
      await screen.findByText('Could not search accounts. Please try again.'),
    ).toBeInTheDocument()
    const tryAgainButton = screen.getByRole('button', { name: 'Try again' })

    searchAccounts.mockResolvedValueOnce({
      ok: true,
      accounts: [
        {
          id: 1,
          email: 'anderson@example.com',
          firstName: 'Anderson',
          lastName: 'Cooper',
          role: 'Customer',
        },
      ],
    })
    await user.click(tryAgainButton)

    expect(await screen.findByText('Anderson Cooper')).toBeInTheDocument()
    expect(searchAccounts).toHaveBeenCalledTimes(2)
    expect(searchAccounts).toHaveBeenNthCalledWith(2, 'token-abc', 'ander')
  })
})
