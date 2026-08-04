import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { useEffect } from 'react'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Routes, Route } from 'react-router'
import { AuthProvider, useAuth } from '../context/AuthContext'
import Account from './Account'

const SIGNED_IN_USER = {
  accessToken: 'token-abc',
  id: 1,
  email: 'john@example.com',
  firstName: 'John',
  lastName: 'Smith',
  role: 'Customer',
}

// Account assumes a signed-in user is already in context (RequireRole's job on
// the real route) -- sign in during an effect and only mount Account once the
// context user is settled, so Account never renders with a null user.
function SignInThenRenderAccount() {
  const { user, login } = useAuth()

  useEffect(() => {
    login(SIGNED_IN_USER)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  if (!user) return null
  return <Account />
}

function renderAccount() {
  return render(
    <AuthProvider>
      <MemoryRouter initialEntries={['/account']}>
        <Routes>
          <Route path="/account" element={<SignInThenRenderAccount />} />
          <Route path="/login" element={<div>Login Stub</div>} />
        </Routes>
      </MemoryRouter>
    </AuthProvider>,
  )
}

function accountMeCalls(fetchSpy) {
  return fetchSpy.mock.calls.filter(([url]) =>
    url.toString().endsWith('/api/account/me'),
  )
}

describe('Account', () => {
  beforeEach(() => {
    // AuthProvider bootstraps a session via /api/auth/refresh on mount; default
    // this to "no session" so it never interferes with the direct login() call.
    vi.spyOn(globalThis, 'fetch').mockResolvedValue({ ok: false, status: 401 })
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('renders the email as plain text with no input box and no edit affordance', async () => {
    renderAccount()

    expect(await screen.findByText('john@example.com')).toBeInTheDocument()
    expect(screen.queryByLabelText('Email')).toBeNull()
    expect(screen.queryByLabelText('Edit email')).toBeNull()
  })

  it('shows name and password in their collapsed view state by default', async () => {
    renderAccount()

    expect(await screen.findByText('John Smith')).toBeInTheDocument()
    expect(screen.queryByLabelText('First Name')).toBeNull()
    expect(screen.queryByLabelText('Last Name')).toBeNull()
    expect(screen.queryByLabelText('New Password')).toBeNull()
    expect(screen.queryByLabelText('Confirm New Password')).toBeNull()
    expect(
      screen.getByRole('button', { name: 'Change Password' }),
    ).toBeInTheDocument()
  })

  it('reveals name inputs on edit, and Cancel reverts without calling fetch', async () => {
    const fetchSpy = vi.spyOn(globalThis, 'fetch').mockResolvedValue({
      ok: false,
      status: 401,
    })
    const user = userEvent.setup()
    renderAccount()
    await screen.findByText('John Smith')

    await user.click(screen.getByRole('button', { name: 'Edit name' }))

    expect(screen.getByLabelText('First Name')).toHaveValue('John')
    expect(screen.getByLabelText('Last Name')).toHaveValue('Smith')

    await user.click(screen.getByRole('button', { name: 'Cancel' }))

    expect(screen.queryByLabelText('First Name')).toBeNull()
    expect(screen.getByText('John Smith')).toBeInTheDocument()
    expect(accountMeCalls(fetchSpy)).toHaveLength(0)
  })

  it('reveals password inputs on Change Password, and Cancel clears and hides them without calling fetch', async () => {
    const fetchSpy = vi.spyOn(globalThis, 'fetch').mockResolvedValue({
      ok: false,
      status: 401,
    })
    const user = userEvent.setup()
    renderAccount()
    await screen.findByText('John Smith')

    await user.click(screen.getByRole('button', { name: 'Change Password' }))
    await user.type(screen.getByLabelText('Current Password'), 'old-password')
    await user.type(screen.getByLabelText('New Password'), 'new-password-123')
    await user.type(
      screen.getByLabelText('Confirm New Password'),
      'new-password-123',
    )

    await user.click(screen.getByRole('button', { name: 'Cancel' }))

    expect(screen.queryByLabelText('Current Password')).toBeNull()
    expect(screen.queryByLabelText('New Password')).toBeNull()
    expect(screen.queryByLabelText('Confirm New Password')).toBeNull()
    expect(
      screen.getByRole('button', { name: 'Change Password' }),
    ).toBeInTheDocument()
    expect(accountMeCalls(fetchSpy)).toHaveLength(0)
  })

  it('opening Change Password while editing name cancels the name edit and reverts unsaved changes', async () => {
    const user = userEvent.setup()
    renderAccount()
    await screen.findByText('John Smith')

    await user.click(screen.getByRole('button', { name: 'Edit name' }))
    await user.clear(screen.getByLabelText('First Name'))
    await user.type(screen.getByLabelText('First Name'), 'Unsaved Edit')

    await user.click(screen.getByRole('button', { name: 'Change Password' }))

    expect(screen.queryByLabelText('First Name')).toBeNull()
    expect(screen.getByText('John Smith')).toBeInTheDocument()
    expect(screen.getByLabelText('Current Password')).toBeInTheDocument()
  })

  it('opening Edit name while changing password cancels the password edit and clears its fields', async () => {
    const user = userEvent.setup()
    renderAccount()
    await screen.findByText('John Smith')

    await user.click(screen.getByRole('button', { name: 'Change Password' }))
    await user.type(screen.getByLabelText('Current Password'), 'old-password')
    await user.type(screen.getByLabelText('New Password'), 'new-password-123')

    await user.click(screen.getByRole('button', { name: 'Edit name' }))

    expect(screen.queryByLabelText('Current Password')).toBeNull()
    expect(screen.queryByLabelText('New Password')).toBeNull()
    expect(
      screen.getByRole('button', { name: 'Change Password' }),
    ).toBeInTheDocument()
    expect(screen.getByLabelText('First Name')).toHaveValue('John')

    // Re-opening password edit after the cancel confirms the fields were
    // actually cleared, not just hidden.
    await user.click(screen.getByRole('button', { name: 'Cancel' }))
    await user.click(screen.getByRole('button', { name: 'Change Password' }))
    expect(screen.getByLabelText('Current Password')).toHaveValue('')
    expect(screen.getByLabelText('New Password')).toHaveValue('')
  })

  it('shows an error and does not call fetch when Save is clicked with no current password', async () => {
    const fetchSpy = vi.spyOn(globalThis, 'fetch').mockResolvedValue({
      ok: false,
      status: 401,
    })
    const user = userEvent.setup()
    renderAccount()
    await screen.findByText('John Smith')

    await user.click(screen.getByRole('button', { name: 'Change Password' }))
    await user.type(screen.getByLabelText('New Password'), 'new-password-123')
    await user.type(
      screen.getByLabelText('Confirm New Password'),
      'new-password-123',
    )
    await user.click(screen.getByRole('button', { name: 'Save' }))

    expect(
      await screen.findByText('Current password is required'),
    ).toBeInTheDocument()
    expect(screen.queryByText('Save your new password?')).toBeNull()
    expect(accountMeCalls(fetchSpy)).toHaveLength(0)
  })

  it('shows an error and does not call fetch or say "Changes saved" when Save is clicked with a current password but no new password', async () => {
    const fetchSpy = vi.spyOn(globalThis, 'fetch').mockResolvedValue({
      ok: false,
      status: 401,
    })
    const user = userEvent.setup()
    renderAccount()
    await screen.findByText('John Smith')

    await user.click(screen.getByRole('button', { name: 'Change Password' }))
    await user.type(screen.getByLabelText('Current Password'), 'old-password')
    await user.click(screen.getByRole('button', { name: 'Save' }))

    expect(
      await screen.findByText('New password is required'),
    ).toBeInTheDocument()
    expect(screen.queryByText('Save your new password?')).toBeNull()
    expect(screen.queryByText('Changes saved.')).toBeNull()
    expect(accountMeCalls(fetchSpy)).toHaveLength(0)
  })

  it('saving a name change opens the confirm popup and, on confirm, updates without a newPassword and shows the saved message', async () => {
    const fetchSpy = vi.spyOn(globalThis, 'fetch').mockImplementation((url) => {
      if (url.toString().endsWith('/api/account/me')) {
        return Promise.resolve({
          ok: true,
          json: async () => ({
            id: 1,
            email: 'john@example.com',
            firstName: 'Johnny',
            lastName: 'Smith',
            role: 'Customer',
          }),
        })
      }
      return Promise.resolve({ ok: false, status: 401 })
    })
    const user = userEvent.setup()
    renderAccount()
    await screen.findByText('John Smith')

    await user.click(screen.getByRole('button', { name: 'Edit name' }))
    await user.clear(screen.getByLabelText('First Name'))
    await user.type(screen.getByLabelText('First Name'), 'Johnny')
    await user.click(screen.getByRole('button', { name: 'Save' }))

    expect(
      await screen.findByText('Save these changes to your account?'),
    ).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Confirm' }))

    expect(await screen.findByText('Changes saved.')).toBeInTheDocument()
    expect(screen.getByText('Johnny Smith')).toBeInTheDocument()
    expect(screen.queryByLabelText('First Name')).toBeNull()

    const calls = accountMeCalls(fetchSpy)
    expect(calls).toHaveLength(1)
    const requestBody = JSON.parse(calls[0][1].body)
    expect(requestBody).toEqual({
      firstName: 'Johnny',
      lastName: 'Smith',
      newPassword: null,
      currentPassword: null,
    })
  })

  it('saving a password change with matching fields opens the confirm popup and sends the unchanged name plus the new password', async () => {
    const fetchSpy = vi.spyOn(globalThis, 'fetch').mockImplementation((url) => {
      if (url.toString().endsWith('/api/account/me')) {
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
    const user = userEvent.setup()
    renderAccount()
    await screen.findByText('John Smith')

    await user.click(screen.getByRole('button', { name: 'Change Password' }))
    await user.type(screen.getByLabelText('Current Password'), 'old-password')
    await user.type(screen.getByLabelText('New Password'), 'new-password-123')
    await user.type(
      screen.getByLabelText('Confirm New Password'),
      'new-password-123',
    )
    await user.click(screen.getByRole('button', { name: 'Save' }))

    expect(
      await screen.findByText('Save your new password?'),
    ).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Confirm' }))

    expect(await screen.findByText('Changes saved.')).toBeInTheDocument()

    const calls = accountMeCalls(fetchSpy)
    expect(calls).toHaveLength(1)
    const requestBody = JSON.parse(calls[0][1].body)
    expect(requestBody).toEqual({
      firstName: 'John',
      lastName: 'Smith',
      newPassword: 'new-password-123',
      currentPassword: 'old-password',
    })
  })

  it('shows a mismatch error without opening the confirm popup or calling fetch, and clears only the new/confirm password fields', async () => {
    const fetchSpy = vi.spyOn(globalThis, 'fetch').mockResolvedValue({
      ok: false,
      status: 401,
    })
    const user = userEvent.setup()
    renderAccount()
    await screen.findByText('John Smith')

    await user.click(screen.getByRole('button', { name: 'Change Password' }))
    await user.type(screen.getByLabelText('Current Password'), 'old-password')
    await user.type(screen.getByLabelText('New Password'), 'new-password-123')
    await user.type(screen.getByLabelText('Confirm New Password'), 'different')
    await user.click(screen.getByRole('button', { name: 'Save' }))

    expect(
      await screen.findByText('Passwords do not match'),
    ).toBeInTheDocument()
    expect(screen.queryByText('Save your new password?')).toBeNull()
    expect(screen.getByLabelText('Current Password')).toHaveValue(
      'old-password',
    )
    expect(screen.getByLabelText('New Password')).toHaveValue('')
    expect(screen.getByLabelText('Confirm New Password')).toHaveValue('')
    expect(accountMeCalls(fetchSpy)).toHaveLength(0)
  })

  it('surfaces the server-reported "current password is incorrect" message on the Current Password field', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation((url) => {
      if (url.toString().endsWith('/api/account/me')) {
        return Promise.resolve({
          ok: false,
          status: 400,
          json: async () => ({ title: 'Current password is incorrect.' }),
        })
      }
      return Promise.resolve({ ok: false, status: 401 })
    })
    const user = userEvent.setup()
    renderAccount()
    await screen.findByText('John Smith')

    await user.click(screen.getByRole('button', { name: 'Change Password' }))
    await user.type(screen.getByLabelText('Current Password'), 'wrong-password')
    await user.type(screen.getByLabelText('New Password'), 'new-password-123')
    await user.type(
      screen.getByLabelText('Confirm New Password'),
      'new-password-123',
    )
    await user.click(screen.getByRole('button', { name: 'Save' }))
    await user.click(screen.getByRole('button', { name: 'Confirm' }))

    expect(
      await screen.findByText('Current password is incorrect.'),
    ).toBeInTheDocument()
  })

  it('surfaces the server-reported "same as current password" message on the New Password field', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation((url) => {
      if (url.toString().endsWith('/api/account/me')) {
        return Promise.resolve({
          ok: false,
          status: 400,
          json: async () => ({
            title: 'New password must be different from your current password.',
          }),
        })
      }
      return Promise.resolve({ ok: false, status: 401 })
    })
    const user = userEvent.setup()
    renderAccount()
    await screen.findByText('John Smith')

    await user.click(screen.getByRole('button', { name: 'Change Password' }))
    await user.type(screen.getByLabelText('Current Password'), 'old-password')
    await user.type(screen.getByLabelText('New Password'), 'old-password')
    await user.type(
      screen.getByLabelText('Confirm New Password'),
      'old-password',
    )
    await user.click(screen.getByRole('button', { name: 'Save' }))
    await user.click(screen.getByRole('button', { name: 'Confirm' }))

    expect(
      await screen.findByText(
        'New password must be different from your current password.',
      ),
    ).toBeInTheDocument()
  })

  it('redirects to sign in with a session-expired message on a 401 response while saving', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation((url) => {
      if (url.toString().endsWith('/api/account/me')) {
        return Promise.resolve({
          ok: false,
          status: 401,
          json: async () => ({ title: 'Session expired.' }),
        })
      }
      return Promise.resolve({ ok: false, status: 401 })
    })
    const user = userEvent.setup()
    renderAccount()
    await screen.findByText('John Smith')

    await user.click(screen.getByRole('button', { name: 'Edit name' }))
    await user.click(screen.getByRole('button', { name: 'Save' }))
    await user.click(screen.getByRole('button', { name: 'Confirm' }))

    expect(await screen.findByText('Login Stub')).toBeInTheDocument()
  })

  it('shows the rate-limit message on a 429 response while saving', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation((url) => {
      if (url.toString().endsWith('/api/account/me')) {
        return Promise.resolve({
          ok: false,
          status: 429,
          json: async () => ({
            title: 'Too many attempts. Try again in a few minutes.',
          }),
        })
      }
      return Promise.resolve({ ok: false, status: 401 })
    })
    const user = userEvent.setup()
    renderAccount()
    await screen.findByText('John Smith')

    await user.click(screen.getByRole('button', { name: 'Edit name' }))
    await user.click(screen.getByRole('button', { name: 'Save' }))
    await user.click(screen.getByRole('button', { name: 'Confirm' }))

    expect(
      await screen.findByText('Too many attempts. Try again in a few minutes.'),
    ).toBeInTheDocument()
  })

  it('shows the generic error message when the save request throws a network error', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation((url) => {
      if (url.toString().endsWith('/api/account/me')) {
        return Promise.reject(new Error('network down'))
      }
      return Promise.resolve({ ok: false, status: 401 })
    })
    const user = userEvent.setup()
    renderAccount()
    await screen.findByText('John Smith')

    await user.click(screen.getByRole('button', { name: 'Edit name' }))
    await user.click(screen.getByRole('button', { name: 'Save' }))
    await user.click(screen.getByRole('button', { name: 'Confirm' }))

    expect(
      await screen.findByText('Something went wrong. Please try again.'),
    ).toBeInTheDocument()
  })

  it('shows the generic error message when the save response is 200 with a malformed body', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation((url) => {
      if (url.toString().endsWith('/api/account/me')) {
        return Promise.resolve({
          ok: true,
          json: async () => {
            throw new Error('invalid json')
          },
        })
      }
      return Promise.resolve({ ok: false, status: 401 })
    })
    const user = userEvent.setup()
    renderAccount()
    await screen.findByText('John Smith')

    await user.click(screen.getByRole('button', { name: 'Edit name' }))
    await user.click(screen.getByRole('button', { name: 'Save' }))
    await user.click(screen.getByRole('button', { name: 'Confirm' }))

    expect(
      await screen.findByText('Something went wrong. Please try again.'),
    ).toBeInTheDocument()
  })

  it('shows the conflict message on a 409 response', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation((url) => {
      if (url.toString().endsWith('/api/account/me')) {
        return Promise.resolve({
          ok: false,
          status: 409,
          json: async () => ({
            title:
              'This account was updated elsewhere. Please refresh and try again.',
          }),
        })
      }
      return Promise.resolve({ ok: false, status: 401 })
    })
    const user = userEvent.setup()
    renderAccount()
    await screen.findByText('John Smith')

    await user.click(screen.getByRole('button', { name: 'Edit name' }))
    await user.click(screen.getByRole('button', { name: 'Save' }))
    await user.click(screen.getByRole('button', { name: 'Confirm' }))

    expect(
      await screen.findByText(
        'This account was updated elsewhere. Please refresh and try again.',
      ),
    ).toBeInTheDocument()
  })

  it('surfaces a 400 FirstName error on the First Name field while the name section is in edit mode', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation((url) => {
      if (url.toString().endsWith('/api/account/me')) {
        return Promise.resolve({
          ok: false,
          status: 400,
          json: async () => ({
            errors: { FirstName: ['First name is required.'] },
          }),
        })
      }
      return Promise.resolve({ ok: false, status: 401 })
    })
    const user = userEvent.setup()
    renderAccount()
    await screen.findByText('John Smith')

    await user.click(screen.getByRole('button', { name: 'Edit name' }))
    await user.click(screen.getByRole('button', { name: 'Save' }))
    await user.click(screen.getByRole('button', { name: 'Confirm' }))

    expect(
      await screen.findByText('First name is required.'),
    ).toBeInTheDocument()
    expect(screen.getByLabelText('First Name')).toBeInTheDocument()
  })
})
