import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { useEffect } from 'react'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Routes, Route } from 'react-router'
import { AuthProvider, useAuth } from '../context/AuthContext'
import { adminUpdateAccount, searchAccounts } from '../api/AccountApi'
import AdminPanel from './AdminPanel'

vi.mock('../api/AccountApi', () => ({
  searchAccounts: vi.fn(),
  adminUpdateAccount: vi.fn(),
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
          <Route path="/login" element={<div>Login Stub</div>} />
        </Routes>
      </MemoryRouter>
    </AuthProvider>,
  )
}

const TARGET_ACCOUNT = {
  id: 1,
  email: 'target@example.com',
  firstName: 'Target',
  lastName: 'Person',
  role: 'Customer',
}

async function searchAndOpenEditPopup(user) {
  searchAccounts.mockResolvedValue({ ok: true, accounts: [TARGET_ACCOUNT] })
  renderPage()
  await screen.findByText('Search by name or email to find an account.')

  await user.type(screen.getByLabelText('Search'), 'target')
  await user.click(screen.getByRole('button', { name: 'Search' }))

  await user.click(await screen.findByRole('button', { name: /Target Person/ }))
  await screen.findByText('Edit Account')
}

describe('AdminPanel', () => {
  beforeEach(() => {
    searchAccounts.mockReset()
    adminUpdateAccount.mockReset()
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

  it("clicking an account row opens the edit popup pre-filled with that row's data, password section collapsed", async () => {
    const user = userEvent.setup()
    await searchAndOpenEditPopup(user)

    expect(screen.getByLabelText('Email')).toHaveValue('target@example.com')
    expect(screen.getByLabelText('First Name')).toHaveValue('Target')
    expect(screen.getByLabelText('Last Name')).toHaveValue('Person')
    expect(
      screen.getByRole('combobox', { name: 'Permission' }),
    ).toHaveTextContent('Customer')
    expect(
      screen.getByRole('button', { name: 'Change Password' }),
    ).toBeInTheDocument()
    expect(screen.queryByLabelText('New Password')).toBeNull()
  })

  it('the permission dropdown offers exactly "Customer" and "Barber", never "Admin"', async () => {
    const user = userEvent.setup()
    await searchAndOpenEditPopup(user)

    await user.click(screen.getByRole('combobox', { name: 'Permission' }))

    const customerOption = await screen.findByRole('option', {
      name: 'Customer',
    })
    expect(customerOption).toBeInTheDocument()
    expect(screen.getByRole('option', { name: 'Barber' })).toBeInTheDocument()
    expect(screen.queryByRole('option', { name: 'Admin' })).toBeNull()

    // Close the still-open Select the same way the other Select-driving tests
    // in this file do (selecting an item) rather than Escape/outside-click --
    // jsdom + nested Dialog/Select focus-scopes recurse infinitely otherwise.
    await user.click(customerOption)
  })

  it('saving a valid identity-field edit calls adminUpdateAccount with newPassword null, closes the popup, and updates the row', async () => {
    adminUpdateAccount.mockResolvedValue({
      ok: true,
      account: {
        id: 1,
        email: 'updated@example.com',
        firstName: 'Updated',
        lastName: 'Name',
        role: 'Barber',
      },
    })
    const user = userEvent.setup()
    await searchAndOpenEditPopup(user)

    await user.clear(screen.getByLabelText('Email'))
    await user.type(screen.getByLabelText('Email'), 'updated@example.com')
    await user.clear(screen.getByLabelText('First Name'))
    await user.type(screen.getByLabelText('First Name'), 'Updated')
    await user.clear(screen.getByLabelText('Last Name'))
    await user.type(screen.getByLabelText('Last Name'), 'Name')
    await user.click(screen.getByRole('combobox', { name: 'Permission' }))
    await user.click(await screen.findByRole('option', { name: 'Barber' }))

    await user.click(screen.getByRole('button', { name: 'Save Changes' }))
    await user.click(await screen.findByRole('button', { name: 'Confirm' }))

    expect(adminUpdateAccount).toHaveBeenCalledWith('token-abc', 1, {
      email: 'updated@example.com',
      firstName: 'Updated',
      lastName: 'Name',
      role: 'Barber',
      newPassword: null,
    })
    expect(await screen.findByText('Updated Name')).toBeInTheDocument()
    expect(screen.queryByText('Edit Account')).toBeNull()
  })

  it('clicking "Change Password" reveals the password fields; mismatched values show "Passwords do not match" without opening the confirm popup', async () => {
    const user = userEvent.setup()
    await searchAndOpenEditPopup(user)

    await user.click(screen.getByRole('button', { name: 'Change Password' }))
    await user.type(screen.getByLabelText('New Password'), 'new-correct-horse')
    await user.type(
      screen.getByLabelText('Confirm New Password'),
      'different-password',
    )
    await user.click(screen.getByRole('button', { name: 'Save' }))

    expect(
      await screen.findByText('Passwords do not match'),
    ).toBeInTheDocument()
    expect(screen.getByLabelText('New Password')).toHaveValue('')
    expect(screen.getByLabelText('Confirm New Password')).toHaveValue('')
    expect(
      screen.queryByText('Save the new password for this account?'),
    ).toBeNull()
    expect(adminUpdateAccount).not.toHaveBeenCalled()
  })

  it("saving a valid password change sends the account's original identity values, and collapses the password section without closing the popup", async () => {
    adminUpdateAccount.mockResolvedValue({
      ok: true,
      account: TARGET_ACCOUNT,
    })
    const user = userEvent.setup()
    await searchAndOpenEditPopup(user)

    await user.click(screen.getByRole('button', { name: 'Change Password' }))
    await user.type(screen.getByLabelText('New Password'), 'new-correct-horse')
    await user.type(
      screen.getByLabelText('Confirm New Password'),
      'new-correct-horse',
    )
    await user.click(screen.getByRole('button', { name: 'Save' }))
    await user.click(await screen.findByRole('button', { name: 'Confirm' }))

    expect(adminUpdateAccount).toHaveBeenCalledWith('token-abc', 1, {
      email: 'target@example.com',
      firstName: 'Target',
      lastName: 'Person',
      role: 'Customer',
      newPassword: 'new-correct-horse',
    })
    expect(await screen.findByText('Edit Account')).toBeInTheDocument()
    expect(
      screen.getByRole('button', { name: 'Change Password' }),
    ).toBeInTheDocument()
    expect(screen.queryByLabelText('New Password')).toBeNull()
  })

  it('editing an identity field without saving, then completing a password-only save, still sends the original (last-confirmed) email', async () => {
    adminUpdateAccount.mockResolvedValue({
      ok: true,
      account: TARGET_ACCOUNT,
    })
    const user = userEvent.setup()
    await searchAndOpenEditPopup(user)

    await user.clear(screen.getByLabelText('Email'))
    await user.type(screen.getByLabelText('Email'), 'unsaved-edit@example.com')

    await user.click(screen.getByRole('button', { name: 'Change Password' }))
    await user.type(screen.getByLabelText('New Password'), 'new-correct-horse')
    await user.type(
      screen.getByLabelText('Confirm New Password'),
      'new-correct-horse',
    )
    await user.click(screen.getByRole('button', { name: 'Save' }))
    await user.click(await screen.findByRole('button', { name: 'Confirm' }))

    expect(adminUpdateAccount).toHaveBeenCalledWith('token-abc', 1, {
      email: 'target@example.com',
      firstName: 'Target',
      lastName: 'Person',
      role: 'Customer',
      newPassword: 'new-correct-horse',
    })
  })

  it('clicking the password section\'s own "Cancel" collapses it, clears both fields, without closing the popup or touching identity fields', async () => {
    const user = userEvent.setup()
    await searchAndOpenEditPopup(user)

    await user.clear(screen.getByLabelText('Email'))
    await user.type(screen.getByLabelText('Email'), 'unsaved-edit@example.com')
    await user.click(screen.getByRole('button', { name: 'Change Password' }))
    await user.type(screen.getByLabelText('New Password'), 'new-correct-horse')

    // The identity section (and its own Cancel) is hidden while the password
    // section is showing, so only the password section's Cancel is on screen.
    await user.click(screen.getByRole('button', { name: 'Cancel' }))

    expect(screen.queryByLabelText('New Password')).toBeNull()
    expect(screen.getByText('Edit Account')).toBeInTheDocument()
    expect(screen.getByLabelText('Email')).toHaveValue(
      'unsaved-edit@example.com',
    )
  })

  it('a 409 "That email is already in use." response shows that message on the Email field and keeps the popup open with entered values', async () => {
    adminUpdateAccount.mockResolvedValue({
      ok: false,
      status: 409,
      problem: { title: 'That email is already in use.' },
    })
    const user = userEvent.setup()
    await searchAndOpenEditPopup(user)

    await user.clear(screen.getByLabelText('Email'))
    await user.type(screen.getByLabelText('Email'), 'duplicate@example.com')
    await user.click(screen.getByRole('button', { name: 'Save Changes' }))
    await user.click(await screen.findByRole('button', { name: 'Confirm' }))

    expect(
      await screen.findByText('That email is already in use.'),
    ).toBeInTheDocument()
    expect(screen.getByText('Edit Account')).toBeInTheDocument()
    expect(screen.getByLabelText('Email')).toHaveValue('duplicate@example.com')
  })

  it('a 409 conflict (non-duplicate-email) response shows the refresh-and-retry message and keeps the popup open', async () => {
    adminUpdateAccount.mockResolvedValue({
      ok: false,
      status: 409,
      problem: {
        title: 'This account was changed elsewhere. Refresh and try again.',
      },
    })
    const user = userEvent.setup()
    await searchAndOpenEditPopup(user)

    await user.click(screen.getByRole('button', { name: 'Save Changes' }))
    await user.click(await screen.findByRole('button', { name: 'Confirm' }))

    expect(
      await screen.findByText(
        'This account was changed elsewhere. Refresh and try again.',
      ),
    ).toBeInTheDocument()
    expect(screen.getByText('Edit Account')).toBeInTheDocument()
  })

  it('a 401 response logs out and navigates to /login with the session-expired message', async () => {
    adminUpdateAccount.mockResolvedValue({ ok: false, status: 401 })
    const user = userEvent.setup()
    await searchAndOpenEditPopup(user)

    await user.click(screen.getByRole('button', { name: 'Save Changes' }))
    await user.click(await screen.findByRole('button', { name: 'Confirm' }))

    expect(await screen.findByText('Login Stub')).toBeInTheDocument()
  })

  it('clicking the popup\'s own "Cancel" button closes it without calling adminUpdateAccount, leaving the row unchanged', async () => {
    const user = userEvent.setup()
    await searchAndOpenEditPopup(user)

    await user.clear(screen.getByLabelText('Email'))
    await user.type(screen.getByLabelText('Email'), 'unsaved-edit@example.com')

    await user.click(screen.getAllByRole('button', { name: 'Cancel' })[0])

    expect(screen.queryByText('Edit Account')).toBeNull()
    expect(screen.getByText('target@example.com')).toBeInTheDocument()
    expect(adminUpdateAccount).not.toHaveBeenCalled()
  })
})
