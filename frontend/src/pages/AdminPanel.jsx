import { useEffect, useRef, useState } from 'react'
import { useNavigate } from 'react-router'
import { useAuth } from '../context/AuthContext'
import { adminUpdateAccount, searchAccounts } from '../api/AccountApi'
import Input from '../components/Input'
import Button from '../components/Button'
import ConfirmPopup from '../components/ConfirmPopup'
import SelectDropdown from '../components/SelectDropdown'
import './AdminPanel.css'

const ROLE_OPTIONS = [
  { value: 'Customer', label: 'Customer' },
  { value: 'Barber', label: 'Barber' },
]

const DUPLICATE_EMAIL_TITLE = 'That email is already in use.'

export default function AdminPanel() {
  const navigate = useNavigate()
  const { user, logout } = useAuth()

  const [query, setQuery] = useState('')
  const [searched, setSearched] = useState(false)
  const [accounts, setAccounts] = useState([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')
  const lastQueryRef = useRef('')
  const isMountedRef = useRef(true)

  const [editingAccount, setEditingAccount] = useState(null)
  const [editEmail, setEditEmail] = useState('')
  const [editFirstName, setEditFirstName] = useState('')
  const [editLastName, setEditLastName] = useState('')
  const [editRole, setEditRole] = useState('Customer')
  const [editFieldErrors, setEditFieldErrors] = useState({})
  const [editError, setEditError] = useState('')

  const [isChangingPassword, setIsChangingPassword] = useState(false)
  const [editNewPassword, setEditNewPassword] = useState('')
  const [editConfirmPassword, setEditConfirmPassword] = useState('')
  const [passwordError, setPasswordError] = useState('')
  const [passwordFieldErrors, setPasswordFieldErrors] = useState({})

  const [confirmOpen, setConfirmOpen] = useState(false)
  const [pendingAction, setPendingAction] = useState(null) // 'details' | 'password'
  const [isSubmitting, setIsSubmitting] = useState(false)
  // Bumped at the start of every submitted search. If a newer search starts
  // before an older one's fetch resolves, the older call's captured id no
  // longer matches by the time it resolves, so its result is discarded.
  const requestIdRef = useRef(0)

  useEffect(() => {
    isMountedRef.current = true
    return () => {
      isMountedRef.current = false
    }
  }, [])

  async function runSearch(trimmedQuery) {
    lastQueryRef.current = trimmedQuery
    const requestId = ++requestIdRef.current
    setLoading(true)
    setError('')
    const result = await searchAccounts(user.accessToken, trimmedQuery)
    if (!isMountedRef.current || requestId !== requestIdRef.current) {
      return
    }
    if (result.status === 401) {
      logout()
      navigate('/login', {
        state: { message: 'Your session has expired. Please sign in again.' },
      })
      return
    }
    setLoading(false)
    setSearched(true)
    if (result.ok) {
      setAccounts(result.accounts)
    } else {
      setError('Could not search accounts. Please try again.')
    }
  }

  function handleSubmit(event) {
    event.preventDefault()
    const trimmedQuery = query.trim()
    if (!trimmedQuery) {
      return
    }
    runSearch(trimmedQuery)
  }

  function handleTryAgain() {
    runSearch(lastQueryRef.current)
  }

  function handleOpenEdit(account) {
    setEditingAccount(account)
    setEditEmail(account.email)
    setEditFirstName(account.firstName)
    setEditLastName(account.lastName)
    setEditRole(account.role)
    setEditFieldErrors({})
    setEditError('')
    setIsChangingPassword(false)
    setEditNewPassword('')
    setEditConfirmPassword('')
    setPasswordError('')
    setPasswordFieldErrors({})
  }

  function handleCancelPopup() {
    setEditingAccount(null)
  }

  function handleCancelPassword() {
    setEditNewPassword('')
    setEditConfirmPassword('')
    setPasswordError('')
    setPasswordFieldErrors({})
    setIsChangingPassword(false)
  }

  function handleSaveDetailsClick() {
    setEditError('')
    setPendingAction('details')
    setConfirmOpen(true)
  }

  function handleSavePasswordClick() {
    setPasswordError('')
    setEditError('')
    if (editNewPassword !== editConfirmPassword) {
      setPasswordError('Passwords do not match')
      setEditNewPassword('')
      setEditConfirmPassword('')
      return
    }
    setPendingAction('password')
    setConfirmOpen(true)
  }

  async function handleConfirmEdit() {
    setIsSubmitting(true)
    const isPasswordAction = pendingAction === 'password'
    const result = await adminUpdateAccount(
      user.accessToken,
      editingAccount.id,
      {
        // A password save must never carry an unsaved, unconfirmed identity
        // edit -- send the last-confirmed identity fields, not local form state.
        email: isPasswordAction ? editingAccount.email : editEmail,
        firstName: isPasswordAction ? editingAccount.firstName : editFirstName,
        lastName: isPasswordAction ? editingAccount.lastName : editLastName,
        role: isPasswordAction ? editingAccount.role : editRole,
        newPassword: isPasswordAction ? editNewPassword : null,
      },
    )
    if (!isMountedRef.current) {
      return
    }
    setIsSubmitting(false)

    if (result.ok) {
      setAccounts((current) =>
        current.map((account) =>
          account.id === result.account.id ? result.account : account,
        ),
      )
      setEditingAccount(result.account)
      if (isPasswordAction) {
        handleCancelPassword()
      } else {
        setEditingAccount(null)
      }
      return
    }

    if (result.status === 401) {
      logout()
      navigate('/login', {
        state: { message: 'Your session has expired. Please sign in again.' },
      })
      return
    }

    if (
      result.status === 409 &&
      result.problem?.title === DUPLICATE_EMAIL_TITLE
    ) {
      setEditFieldErrors({ Email: [result.problem.title] })
      return
    }

    if (result.status === 409) {
      setEditError(
        result.problem?.title ??
          'This account was changed elsewhere. Refresh and try again.',
      )
      return
    }

    if (result.status === 400 && result.problem?.errors) {
      if (isPasswordAction) {
        setPasswordFieldErrors(result.problem.errors)
      } else {
        setEditFieldErrors(result.problem.errors)
      }
      return
    }

    setEditError('Something went wrong. Please try again.')
  }

  return (
    <div className="admin-panel">
      <h1 className="admin-panel__title">Admin Panel</h1>

      <form className="admin-panel__search-form" onSubmit={handleSubmit}>
        <Input
          label="Search"
          placeholder="Name or email"
          value={query}
          onChange={(event) => setQuery(event.target.value)}
        />
        <Button type="submit" disabled={loading}>
          Search
        </Button>
      </form>

      {!searched ? (
        <p className="admin-panel__message">
          Search by name or email to find an account.
        </p>
      ) : loading ? (
        <p className="admin-panel__message">Searching…</p>
      ) : error ? (
        <div className="admin-panel__error-state">
          <p className="admin-panel__error">{error}</p>
          <Button variant="secondary" onClick={handleTryAgain}>
            Try again
          </Button>
        </div>
      ) : accounts.length === 0 ? (
        <p className="admin-panel__message">No accounts match your search.</p>
      ) : (
        <div className="admin-panel__results">
          {accounts.map((account) => (
            <button
              type="button"
              key={account.id}
              className="admin-account-row"
              onClick={() => handleOpenEdit(account)}
            >
              <span className="admin-account-row__name">
                {account.firstName} {account.lastName}
              </span>
              <span className="admin-account-row__email">{account.email}</span>
              <span className="admin-account-row__role">{account.role}</span>
            </button>
          ))}
        </div>
      )}

      {editingAccount && (
        // A plain overlay/panel, not the shared Modal component: Modal wraps
        // Radix Dialog, and this popup's Permission field is a Radix Select --
        // nesting a Select inside a Dialog gives the two components' own
        // trapped focus-scopes competing ownership of focus. Real browsers
        // resolve this via Radix's focus-scope stack, but it deadlocks in
        // this project's jsdom test environment, so this popup is hand-rolled
        // instead of composed from Modal.
        <div
          className="admin-edit-popup-overlay"
          onClick={(event) => {
            if (event.target === event.currentTarget) {
              handleCancelPopup()
            }
          }}
        >
          <div
            className="admin-edit-popup"
            role="dialog"
            aria-modal="true"
            aria-labelledby="admin-edit-popup-title"
            onKeyDown={(event) => {
              if (event.key === 'Escape') {
                handleCancelPopup()
              }
            }}
          >
            <h2 id="admin-edit-popup-title" className="admin-edit-popup__title">
              Edit Account
            </h2>
            {editError && (
              <p className="admin-edit-popup__error">{editError}</p>
            )}

            {!isChangingPassword ? (
              <>
                <div className="admin-edit-popup__identity-fields">
                  <Input
                    label="Email"
                    value={editEmail}
                    onChange={(event) => setEditEmail(event.target.value)}
                    error={editFieldErrors.Email?.[0]}
                    disabled={isSubmitting}
                  />
                  <Input
                    label="First Name"
                    value={editFirstName}
                    onChange={(event) => setEditFirstName(event.target.value)}
                    error={editFieldErrors.FirstName?.[0]}
                    disabled={isSubmitting}
                  />
                  <Input
                    label="Last Name"
                    value={editLastName}
                    onChange={(event) => setEditLastName(event.target.value)}
                    error={editFieldErrors.LastName?.[0]}
                    disabled={isSubmitting}
                  />
                  <SelectDropdown
                    label="Permission"
                    value={editRole}
                    onChange={setEditRole}
                    options={ROLE_OPTIONS}
                    disabled={isSubmitting}
                  />
                </div>
                <div className="admin-edit-popup__footer">
                  <Button
                    onClick={handleSaveDetailsClick}
                    disabled={isSubmitting}
                  >
                    Save Changes
                  </Button>
                  <Button
                    variant="secondary"
                    onClick={handleCancelPopup}
                    disabled={isSubmitting}
                  >
                    Cancel
                  </Button>
                </div>
                <Button
                  variant="secondary"
                  onClick={() => setIsChangingPassword(true)}
                  disabled={isSubmitting}
                >
                  Change Password
                </Button>
              </>
            ) : (
              <div className="admin-edit-popup__password-section">
                <Input
                  label="New Password"
                  type="password"
                  value={editNewPassword}
                  onChange={(event) => setEditNewPassword(event.target.value)}
                  error={passwordError || passwordFieldErrors.NewPassword?.[0]}
                  disabled={isSubmitting}
                />
                <Input
                  label="Confirm New Password"
                  type="password"
                  value={editConfirmPassword}
                  onChange={(event) =>
                    setEditConfirmPassword(event.target.value)
                  }
                  disabled={isSubmitting}
                />
                <div className="admin-edit-popup__footer">
                  <Button
                    onClick={handleSavePasswordClick}
                    disabled={isSubmitting}
                  >
                    Save
                  </Button>
                  <Button
                    variant="secondary"
                    onClick={handleCancelPassword}
                    disabled={isSubmitting}
                  >
                    Cancel
                  </Button>
                </div>
              </div>
            )}
          </div>
        </div>
      )}

      <ConfirmPopup
        open={confirmOpen}
        onOpenChange={setConfirmOpen}
        title="Save changes?"
        message={
          pendingAction === 'password'
            ? 'Save the new password for this account?'
            : 'Save changes to this account?'
        }
        onConfirm={handleConfirmEdit}
      />
    </div>
  )
}
