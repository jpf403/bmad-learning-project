import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { useEffect } from 'react'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Routes, Route } from 'react-router'
import { AuthProvider, useAuth } from '../context/AuthContext'
import {
  adminUpdateAccount,
  createBarberAccount,
  deleteAccount,
  searchAccounts,
} from '../api/AccountApi'
import AdminPanel from './AdminPanel'

vi.mock('../api/AccountApi', () => ({
  searchAccounts: vi.fn(),
  adminUpdateAccount: vi.fn(),
  createBarberAccount: vi.fn(),
  deleteAccount: vi.fn(),
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

const ADMIN_ACCOUNT = {
  id: 2,
  email: 'admin-row@example.com',
  firstName: 'Admin',
  lastName: 'Row',
  role: 'Admin',
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
    createBarberAccount.mockReset()
    deleteAccount.mockReset()
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

  it('opening the popup focuses the Email field first', async () => {
    const user = userEvent.setup()
    await searchAndOpenEditPopup(user)

    expect(screen.getByLabelText('Email')).toHaveFocus()
  })

  it('closing the popup ("Cancel") returns focus to the row that opened it', async () => {
    const user = userEvent.setup()
    await searchAndOpenEditPopup(user)

    await user.click(screen.getAllByRole('button', { name: 'Cancel' })[0])

    expect(screen.getByRole('button', { name: /Target Person/ })).toHaveFocus()
  })

  it('pressing Escape closes the popup without calling adminUpdateAccount', async () => {
    const user = userEvent.setup()
    await searchAndOpenEditPopup(user)

    await user.keyboard('{Escape}')

    expect(screen.queryByText('Edit Account')).toBeNull()
    expect(adminUpdateAccount).not.toHaveBeenCalled()
  })

  it('clicking outside the popup (on the overlay) closes it without calling adminUpdateAccount', async () => {
    const user = userEvent.setup()
    await searchAndOpenEditPopup(user)

    // The overlay itself (not a labelled element) is what the outside-click
    // handler targets.
    const overlay = document.querySelector('.admin-edit-popup-overlay')
    await user.click(overlay)

    expect(screen.queryByText('Edit Account')).toBeNull()
    expect(adminUpdateAccount).not.toHaveBeenCalled()
  })

  it('declining the confirm popup ("Go Back") leaves the edit unsaved and the popup open', async () => {
    const user = userEvent.setup()
    await searchAndOpenEditPopup(user)

    await user.clear(screen.getByLabelText('Email'))
    await user.type(screen.getByLabelText('Email'), 'unsaved-edit@example.com')
    await user.click(screen.getByRole('button', { name: 'Save Changes' }))
    await user.click(await screen.findByRole('button', { name: 'Go Back' }))

    expect(adminUpdateAccount).not.toHaveBeenCalled()
    expect(screen.getByText('Edit Account')).toBeInTheDocument()
    expect(screen.getByLabelText('Email')).toHaveValue(
      'unsaved-edit@example.com',
    )
  })

  it('clicking "Save" with both password fields blank shows "New password is required" without opening the confirm popup', async () => {
    const user = userEvent.setup()
    await searchAndOpenEditPopup(user)

    await user.click(screen.getByRole('button', { name: 'Change Password' }))
    await user.click(screen.getByRole('button', { name: 'Save' }))

    expect(
      await screen.findByText('New password is required'),
    ).toBeInTheDocument()
    expect(
      screen.queryByText('Save the new password for this account?'),
    ).toBeNull()
    expect(adminUpdateAccount).not.toHaveBeenCalled()
  })

  it('a duplicate-email 409 during a password-only save shows a visible error instead of being silently swallowed', async () => {
    adminUpdateAccount.mockResolvedValue({
      ok: false,
      status: 409,
      problem: { title: 'That email is already in use.' },
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

    expect(
      await screen.findByText('That email is already in use.'),
    ).toBeInTheDocument()
  })

  it('clicking an Admin-role row opens the popup read-only, with no Save/Change-Password affordances', async () => {
    searchAccounts.mockResolvedValue({ ok: true, accounts: [ADMIN_ACCOUNT] })
    const user = userEvent.setup()
    renderPage()
    await screen.findByText('Search by name or email to find an account.')

    await user.type(screen.getByLabelText('Search'), 'admin')
    await user.click(screen.getByRole('button', { name: 'Search' }))
    await user.click(await screen.findByRole('button', { name: /Admin Row/ }))

    expect(
      await screen.findByText('The admin account cannot be edited.'),
    ).toBeInTheDocument()
    expect(screen.getByLabelText('Email')).toBeDisabled()
    expect(screen.getByLabelText('First Name')).toBeDisabled()
    expect(screen.getByLabelText('Last Name')).toBeDisabled()
    expect(screen.queryByRole('button', { name: 'Save Changes' })).toBeNull()
    expect(screen.queryByRole('button', { name: 'Change Password' })).toBeNull()
    expect(screen.queryByRole('button', { name: 'Delete' })).toBeNull()
    expect(adminUpdateAccount).not.toHaveBeenCalled()
  })

  it('pressing Shift+Tab from the first field wraps focus to the last focusable element (Change Password)', async () => {
    const user = userEvent.setup()
    await searchAndOpenEditPopup(user)

    expect(screen.getByLabelText('Email')).toHaveFocus()
    await user.tab({ shift: true })

    expect(
      screen.getByRole('button', { name: 'Change Password' }),
    ).toHaveFocus()
  })

  it('the Permission dropdown portals inside the dialog, not to document.body, so the Tab-trap can see it', async () => {
    const user = userEvent.setup()
    await searchAndOpenEditPopup(user)

    await user.click(screen.getByRole('combobox', { name: 'Permission' }))
    const listbox = await screen.findByRole('listbox')
    const dialog = screen.getByRole('dialog')

    expect(dialog).toContainElement(listbox)

    // Close the still-open Select the same way the other Select-driving tests
    // in this file do (selecting an item) rather than Escape/outside-click --
    // jsdom + nested Dialog/Select focus-scopes recurse infinitely otherwise.
    await user.click(screen.getByRole('option', { name: 'Barber' }))
  })

  it('marks the rest of the page inert while the popup is open, and clears it on close', async () => {
    searchAccounts.mockResolvedValue({ ok: true, accounts: [TARGET_ACCOUNT] })
    const user = userEvent.setup()
    const { container } = renderPage()
    await screen.findByText('Search by name or email to find an account.')

    await user.type(screen.getByLabelText('Search'), 'target')
    await user.click(screen.getByRole('button', { name: 'Search' }))
    await user.click(
      await screen.findByRole('button', { name: /Target Person/ }),
    )
    await screen.findByText('Edit Account')

    expect(container).toHaveAttribute('inert')

    await user.click(screen.getAllByRole('button', { name: 'Cancel' })[0])

    expect(container).not.toHaveAttribute('inert')
  })

  it('a non-NewPassword 400 field error during a password save still shows a visible message', async () => {
    adminUpdateAccount.mockResolvedValue({
      ok: false,
      status: 400,
      problem: { errors: { Email: ['Email is invalid.'] } },
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

    expect(
      await screen.findByText('Something went wrong. Please try again.'),
    ).toBeInTheDocument()
  })

  describe('Delete Account', () => {
    it('shows a "Delete" button in the identity view, alongside "Save Changes" and "Cancel"', async () => {
      const user = userEvent.setup()
      await searchAndOpenEditPopup(user)

      expect(
        screen.getByRole('button', { name: 'Save Changes' }),
      ).toBeInTheDocument()
      expect(screen.getByRole('button', { name: 'Delete' })).toBeInTheDocument()
      expect(
        screen.getAllByRole('button', { name: 'Cancel' })[0],
      ).toBeInTheDocument()
    })

    it('clicking "Delete" opens the shared confirm popup with the destructive-styled Confirm button and the delete-specific message', async () => {
      const user = userEvent.setup()
      await searchAndOpenEditPopup(user)

      await user.click(screen.getByRole('button', { name: 'Delete' }))

      expect(await screen.findByText('Delete Account?')).toBeInTheDocument()
      expect(
        screen.getByText('Delete this account? This cannot be undone.'),
      ).toBeInTheDocument()
      expect(
        screen.getByRole('button', { name: 'Confirm' }),
      ).toBeInTheDocument()
    })

    it("confirming a delete calls deleteAccount with the target account's id, closes the edit popup, and removes that account's row from the currently displayed search results", async () => {
      deleteAccount.mockResolvedValue({ ok: true })
      const user = userEvent.setup()
      await searchAndOpenEditPopup(user)

      await user.click(screen.getByRole('button', { name: 'Delete' }))
      await user.click(await screen.findByRole('button', { name: 'Confirm' }))

      expect(deleteAccount).toHaveBeenCalledWith('token-abc', 1)
      expect(screen.queryByText('Edit Account')).toBeNull()
      expect(screen.queryByText('Target Person')).toBeNull()
    })

    it('declining the delete confirm ("Go Back") leaves the account undeleted, the row unchanged, and deleteAccount not called', async () => {
      const user = userEvent.setup()
      await searchAndOpenEditPopup(user)

      await user.click(screen.getByRole('button', { name: 'Delete' }))
      await user.click(await screen.findByRole('button', { name: 'Go Back' }))

      expect(deleteAccount).not.toHaveBeenCalled()
      expect(screen.getByText('Edit Account')).toBeInTheDocument()
      expect(screen.getByText('target@example.com')).toBeInTheDocument()
    })

    it('a 409 conflict response from deleteAccount shows the existing refresh-and-retry message and keeps the popup open', async () => {
      deleteAccount.mockResolvedValue({
        ok: false,
        status: 409,
        problem: {
          title: 'This account was changed elsewhere. Refresh and try again.',
        },
      })
      const user = userEvent.setup()
      await searchAndOpenEditPopup(user)

      await user.click(screen.getByRole('button', { name: 'Delete' }))
      await user.click(await screen.findByRole('button', { name: 'Confirm' }))

      expect(
        await screen.findByText(
          'This account was changed elsewhere. Refresh and try again.',
        ),
      ).toBeInTheDocument()
      expect(screen.getByText('Edit Account')).toBeInTheDocument()
    })

    it('a 401 response from deleteAccount logs out and navigates to /login with the session-expired message', async () => {
      deleteAccount.mockResolvedValue({ ok: false, status: 401 })
      const user = userEvent.setup()
      await searchAndOpenEditPopup(user)

      await user.click(screen.getByRole('button', { name: 'Delete' }))
      await user.click(await screen.findByRole('button', { name: 'Confirm' }))

      expect(await screen.findByText('Login Stub')).toBeInTheDocument()
    })
  })

  describe('Create Barber', () => {
    async function openCreatePopup(user) {
      renderPage()
      await screen.findByText('Search by name or email to find an account.')
      await user.click(screen.getByRole('button', { name: 'Create Barber' }))
      await screen.findByRole('heading', { name: 'Create Barber' })
    }

    it('is visible on page load and opens the create popup with empty fields', async () => {
      const user = userEvent.setup()
      await openCreatePopup(user)

      expect(screen.getByLabelText('Email')).toHaveValue('')
      expect(screen.getByLabelText('First Name')).toHaveValue('')
      expect(screen.getByLabelText('Last Name')).toHaveValue('')
      expect(screen.getByLabelText('Password')).toHaveValue('')
      expect(screen.getByLabelText('Confirm Password')).toHaveValue('')
    })

    it('mismatched passwords show "Passwords do not match", clear both password fields, and do not open the confirm popup', async () => {
      const user = userEvent.setup()
      await openCreatePopup(user)

      await user.type(screen.getByLabelText('Email'), 'new-barber@example.com')
      await user.type(screen.getByLabelText('First Name'), 'John')
      await user.type(screen.getByLabelText('Last Name'), 'Smith')
      await user.type(
        screen.getByLabelText('Password'),
        'correct-horse-battery',
      )
      await user.type(
        screen.getByLabelText('Confirm Password'),
        'different-password',
      )
      await user.click(screen.getByRole('button', { name: 'Create' }))

      expect(
        await screen.findAllByText('Passwords do not match'),
      ).not.toHaveLength(0)
      expect(screen.getByLabelText('Password')).toHaveValue('')
      expect(screen.getByLabelText('Confirm Password')).toHaveValue('')
      expect(screen.queryByText('Create this barber account?')).toBeNull()
      expect(createBarberAccount).not.toHaveBeenCalled()
    })

    it('submitting valid, matching input calls createBarberAccount, closes the popup, and shows a confirmation message', async () => {
      createBarberAccount.mockResolvedValue({
        ok: true,
        account: {
          id: 3,
          email: 'new-barber@example.com',
          firstName: 'John',
          lastName: 'Smith',
          role: 'Barber',
        },
      })
      const user = userEvent.setup()
      await openCreatePopup(user)

      await user.type(screen.getByLabelText('Email'), 'new-barber@example.com')
      await user.type(screen.getByLabelText('First Name'), 'John')
      await user.type(screen.getByLabelText('Last Name'), 'Smith')
      await user.type(
        screen.getByLabelText('Password'),
        'correct-horse-battery',
      )
      await user.type(
        screen.getByLabelText('Confirm Password'),
        'correct-horse-battery',
      )
      await user.click(screen.getByRole('button', { name: 'Create' }))
      await user.click(await screen.findByRole('button', { name: 'Confirm' }))

      expect(createBarberAccount).toHaveBeenCalledWith('token-abc', {
        email: 'new-barber@example.com',
        firstName: 'John',
        lastName: 'Smith',
        password: 'correct-horse-battery',
      })
      expect(
        screen.queryByRole('heading', { name: 'Create Barber' }),
      ).toBeNull()
      expect(
        await screen.findByText('Barber account created.'),
      ).toBeInTheDocument()
    })

    it('a 409 "That email is already in use." response shows that message on the Email field and keeps the popup open with entered values', async () => {
      createBarberAccount.mockResolvedValue({
        ok: false,
        status: 409,
        problem: { title: 'That email is already in use.' },
      })
      const user = userEvent.setup()
      await openCreatePopup(user)

      await user.type(screen.getByLabelText('Email'), 'duplicate@example.com')
      await user.type(screen.getByLabelText('First Name'), 'John')
      await user.type(screen.getByLabelText('Last Name'), 'Smith')
      await user.type(
        screen.getByLabelText('Password'),
        'correct-horse-battery',
      )
      await user.type(
        screen.getByLabelText('Confirm Password'),
        'correct-horse-battery',
      )
      await user.click(screen.getByRole('button', { name: 'Create' }))
      await user.click(await screen.findByRole('button', { name: 'Confirm' }))

      expect(
        await screen.findByText('That email is already in use.'),
      ).toBeInTheDocument()
      expect(
        screen.getByRole('heading', { name: 'Create Barber' }),
      ).toBeInTheDocument()
      expect(screen.getByLabelText('Email')).toHaveValue(
        'duplicate@example.com',
      )
    })

    it('a 400 response with problem.errors surfaces the corresponding field error and keeps the popup open', async () => {
      createBarberAccount.mockResolvedValue({
        ok: false,
        status: 400,
        problem: { errors: { FirstName: ['This field cannot be blank.'] } },
      })
      const user = userEvent.setup()
      await openCreatePopup(user)

      await user.type(screen.getByLabelText('Email'), 'new-barber@example.com')
      await user.type(screen.getByLabelText('Last Name'), 'Smith')
      await user.type(
        screen.getByLabelText('Password'),
        'correct-horse-battery',
      )
      await user.type(
        screen.getByLabelText('Confirm Password'),
        'correct-horse-battery',
      )
      await user.click(screen.getByRole('button', { name: 'Create' }))
      await user.click(await screen.findByRole('button', { name: 'Confirm' }))

      expect(
        await screen.findByText('This field cannot be blank.'),
      ).toBeInTheDocument()
      expect(
        screen.getByRole('heading', { name: 'Create Barber' }),
      ).toBeInTheDocument()
    })

    it('a 401 response logs out and navigates to /login with the session-expired message', async () => {
      createBarberAccount.mockResolvedValue({ ok: false, status: 401 })
      const user = userEvent.setup()
      await openCreatePopup(user)

      await user.type(screen.getByLabelText('Email'), 'new-barber@example.com')
      await user.type(screen.getByLabelText('First Name'), 'John')
      await user.type(screen.getByLabelText('Last Name'), 'Smith')
      await user.type(
        screen.getByLabelText('Password'),
        'correct-horse-battery',
      )
      await user.type(
        screen.getByLabelText('Confirm Password'),
        'correct-horse-battery',
      )
      await user.click(screen.getByRole('button', { name: 'Create' }))
      await user.click(await screen.findByRole('button', { name: 'Confirm' }))

      expect(await screen.findByText('Login Stub')).toBeInTheDocument()
    })

    it('clicking "Cancel" closes the popup without calling createBarberAccount, and reopening shows empty fields again', async () => {
      const user = userEvent.setup()
      await openCreatePopup(user)

      await user.type(screen.getByLabelText('Email'), 'unsaved@example.com')
      await user.click(screen.getByRole('button', { name: 'Cancel' }))

      expect(
        screen.queryByRole('heading', { name: 'Create Barber' }),
      ).toBeNull()
      expect(createBarberAccount).not.toHaveBeenCalled()

      await user.click(screen.getByRole('button', { name: 'Create Barber' }))
      await screen.findByRole('heading', { name: 'Create Barber' })
      expect(screen.getByLabelText('Email')).toHaveValue('')
    })

    it('creating a barber does not add a row to the currently displayed search results', async () => {
      searchAccounts.mockResolvedValue({ ok: true, accounts: [TARGET_ACCOUNT] })
      createBarberAccount.mockResolvedValue({
        ok: true,
        account: {
          id: 3,
          email: 'new-barber@example.com',
          firstName: 'John',
          lastName: 'Smith',
          role: 'Barber',
        },
      })
      const user = userEvent.setup()
      renderPage()
      await screen.findByText('Search by name or email to find an account.')
      await user.type(screen.getByLabelText('Search'), 'target')
      await user.click(screen.getByRole('button', { name: 'Search' }))
      await screen.findByText('Target Person')

      await user.click(screen.getByRole('button', { name: 'Create Barber' }))
      await screen.findByRole('heading', { name: 'Create Barber' })
      await user.type(screen.getByLabelText('Email'), 'new-barber@example.com')
      await user.type(screen.getByLabelText('First Name'), 'John')
      await user.type(screen.getByLabelText('Last Name'), 'Smith')
      await user.type(
        screen.getByLabelText('Password'),
        'correct-horse-battery',
      )
      await user.type(
        screen.getByLabelText('Confirm Password'),
        'correct-horse-battery',
      )
      await user.click(screen.getByRole('button', { name: 'Create' }))
      await user.click(await screen.findByRole('button', { name: 'Confirm' }))
      await screen.findByText('Barber account created.')

      expect(screen.getByText('Target Person')).toBeInTheDocument()
      expect(screen.queryByText('new-barber@example.com')).toBeNull()
    })
  })
})
