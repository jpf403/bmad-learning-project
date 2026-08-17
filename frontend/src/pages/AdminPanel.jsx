import { useEffect, useRef, useState } from 'react'
import { createPortal } from 'react-dom'
import { useNavigate } from 'react-router'
import { useAuth } from '../context/AuthContext'
import {
  adminUpdateAccount,
  createBarberAccount,
  searchAccounts,
} from '../api/AccountApi'
import Input from '../components/Input'
import Button from '../components/Button'
import Modal from '../components/Modal'
import ConfirmPopup from '../components/ConfirmPopup'
import SelectDropdown from '../components/SelectDropdown'
import './AdminPanel.css'

const ROLE_OPTIONS = [
  { value: 'Customer', label: 'Customer' },
  { value: 'Barber', label: 'Barber' },
]

const DUPLICATE_EMAIL_TITLE = 'That email is already in use.'
const FOCUSABLE_SELECTOR =
  'input:not([disabled]), button:not([disabled]), select:not([disabled]), [tabindex]:not([tabindex="-1"])'

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
  // A callback ref (state, not useRef) so that passing this node as the
  // Permission dropdown's portal container re-renders once it's attached,
  // instead of silently keeping the Select's default document.body portal.
  const [dialogNode, setDialogNode] = useState(null)
  const overlayNodeRef = useRef(null)
  const lastTriggerRef = useRef(null)
  const wasOpenRef = useRef(false)

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

  const [createOpen, setCreateOpen] = useState(false)
  const [createEmail, setCreateEmail] = useState('')
  const [createFirstName, setCreateFirstName] = useState('')
  const [createLastName, setCreateLastName] = useState('')
  const [createPassword, setCreatePassword] = useState('')
  const [createConfirmPassword, setCreateConfirmPassword] = useState('')
  const [createFieldErrors, setCreateFieldErrors] = useState({})
  const [createPasswordError, setCreatePasswordError] = useState('')
  const [createError, setCreateError] = useState('')
  const [isCreating, setIsCreating] = useState(false)
  const [createConfirmOpen, setCreateConfirmOpen] = useState(false)
  const [createdMessage, setCreatedMessage] = useState('')
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

  // Modal (Radix Dialog) would provide initial focus, a Tab-trap, and
  // focus-return for free -- this hand-rolled dialog reimplements all three
  // itself since it can't compose Modal (see the note on the dialog below).
  // Keyed on the open/closed transition (via wasOpenRef), not on
  // editingAccount's object identity -- a successful save replaces
  // editingAccount with a fresh object while the popup stays open, which
  // must not re-steal focus back to the first field. wasOpenRef only flips
  // to true once focus is actually moved -- dialogNode (a callback-ref state,
  // needed so it can double as the Select portal's container below) isn't
  // attached yet on the same render editingAccount first becomes truthy, so
  // the very first effect run must retry rather than consume the transition.
  useEffect(() => {
    if (editingAccount) {
      if (!wasOpenRef.current && dialogNode) {
        const firstFocusable = dialogNode.querySelector(FOCUSABLE_SELECTOR)
        firstFocusable?.focus()
        wasOpenRef.current = true
      }
    } else {
      if (wasOpenRef.current && lastTriggerRef.current) {
        lastTriggerRef.current.focus()
        lastTriggerRef.current = null
      }
      wasOpenRef.current = false
    }
  }, [editingAccount, dialogNode])

  // Modal (Radix Dialog) also hides the rest of the page from assistive tech
  // while open (via its portal). This dialog isn't portaled through Modal, so
  // it reimplements that directly: portal itself to document.body and mark
  // every other body-level child inert while open.
  useEffect(() => {
    if (!editingAccount) {
      return
    }
    const overlayNode = overlayNodeRef.current
    const siblings = Array.from(document.body.children).filter(
      (node) => node !== overlayNode,
    )
    siblings.forEach((node) => node.setAttribute('inert', ''))
    return () => {
      siblings.forEach((node) => node.removeAttribute('inert'))
    }
  }, [editingAccount])

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

  function handleOpenEdit(account, triggerElement) {
    lastTriggerRef.current = triggerElement ?? null
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
    if (isSubmitting) {
      return
    }
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
    if (!editNewPassword) {
      setPasswordError('New password is required')
      return
    }
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
      result.problem?.title === DUPLICATE_EMAIL_TITLE &&
      !isPasswordAction
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
        // The password view only renders a NewPassword field error -- any
        // other key would otherwise be captured but never shown.
        if (!('NewPassword' in result.problem.errors)) {
          setEditError('Something went wrong. Please try again.')
        }
      } else {
        setEditFieldErrors(result.problem.errors)
      }
      return
    }

    setEditError('Something went wrong. Please try again.')
  }

  const stripWhitespace = (value) => value.replace(/\s/g, '')

  function handleOpenCreate() {
    setCreateEmail('')
    setCreateFirstName('')
    setCreateLastName('')
    setCreatePassword('')
    setCreateConfirmPassword('')
    setCreateFieldErrors({})
    setCreatePasswordError('')
    setCreateError('')
    setIsCreating(false)
    setCreateConfirmOpen(false)
    setCreatedMessage('')
    setCreateOpen(true)
  }

  function handleCreatePasswordChange(event) {
    setCreatePassword(stripWhitespace(event.target.value))
  }

  function handleCreateConfirmPasswordChange(event) {
    setCreateConfirmPassword(stripWhitespace(event.target.value))
  }

  function handleSaveCreateClick() {
    setCreateError('')
    setCreateFieldErrors({})
    setCreatePasswordError('')
    if (createPassword !== createConfirmPassword) {
      setCreatePasswordError('Passwords do not match')
      setCreatePassword('')
      setCreateConfirmPassword('')
      return
    }
    setCreateConfirmOpen(true)
  }

  async function handleConfirmCreate() {
    setIsCreating(true)
    const result = await createBarberAccount(user.accessToken, {
      email: createEmail,
      firstName: createFirstName,
      lastName: createLastName,
      password: createPassword,
    })
    if (!isMountedRef.current) {
      return
    }
    setIsCreating(false)

    if (result.ok) {
      setCreateOpen(false)
      setCreateEmail('')
      setCreateFirstName('')
      setCreateLastName('')
      setCreatePassword('')
      setCreateConfirmPassword('')
      setCreateFieldErrors({})
      setCreatePasswordError('')
      setCreateError('')
      setCreatedMessage('Barber account created.')
      return
    }

    if (result.status === 401) {
      logout()
      navigate('/login', {
        state: { message: 'Your session has expired. Please sign in again.' },
      })
      return
    }

    if (result.status === 409) {
      setCreateFieldErrors({
        Email: [result.problem?.title ?? DUPLICATE_EMAIL_TITLE],
      })
      return
    }

    if (result.status === 400 && result.problem?.errors) {
      setCreateFieldErrors(result.problem.errors)
      return
    }

    setCreateError('Something went wrong. Please try again.')
  }

  function handleCancelCreate() {
    if (isCreating) {
      return
    }
    setCreateOpen(false)
    setCreateEmail('')
    setCreateFirstName('')
    setCreateLastName('')
    setCreatePassword('')
    setCreateConfirmPassword('')
    setCreateFieldErrors({})
    setCreatePasswordError('')
    setCreateError('')
  }

  const isAdminAccount = editingAccount?.role === 'Admin'
  const identityDisabled = isSubmitting || isAdminAccount

  return (
    <div className="admin-panel">
      <h1 className="admin-panel__title">Admin Panel</h1>

      <div className="admin-panel__actions">
        <Button onClick={handleOpenCreate}>Create Barber</Button>
        {createdMessage && (
          <p className="admin-panel__created-message">{createdMessage}</p>
        )}
      </div>

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
              onClick={(event) => handleOpenEdit(account, event.currentTarget)}
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

      {editingAccount &&
        // A plain overlay/panel, not the shared Modal component: Modal wraps
        // Radix Dialog, and this popup's Permission field is a Radix Select --
        // nesting a Select inside a Dialog gives the two components' own
        // trapped focus-scopes competing ownership of focus. Real browsers
        // resolve this via Radix's focus-scope stack, but it deadlocks in
        // this project's jsdom test environment, so this popup is hand-rolled
        // instead of composed from Modal. Portaled to document.body (like
        // Modal's own Dialog.Portal) so the inert-background effect above can
        // treat it as a top-level sibling rather than something nested inside
        // the page content it needs to disable.
        createPortal(
          <div
            ref={overlayNodeRef}
            className="admin-edit-popup-overlay"
            onClick={(event) => {
              if (event.target === event.currentTarget) {
                handleCancelPopup()
              }
            }}
          >
            <div
              ref={setDialogNode}
              className="admin-edit-popup"
              role="dialog"
              aria-modal="true"
              aria-labelledby="admin-edit-popup-title"
              onKeyDown={(event) => {
                if (event.key === 'Escape') {
                  handleCancelPopup()
                  return
                }
                if (event.key === 'Tab') {
                  const focusable = Array.from(
                    dialogNode?.querySelectorAll(FOCUSABLE_SELECTOR) ?? [],
                  )
                  if (focusable.length === 0) {
                    return
                  }
                  const first = focusable[0]
                  const last = focusable[focusable.length - 1]
                  if (event.shiftKey && document.activeElement === first) {
                    event.preventDefault()
                    last.focus()
                  } else if (
                    !event.shiftKey &&
                    document.activeElement === last
                  ) {
                    event.preventDefault()
                    first.focus()
                  }
                }
              }}
            >
              <h2
                id="admin-edit-popup-title"
                className="admin-edit-popup__title"
              >
                Edit Account
              </h2>
              {isAdminAccount && (
                <p className="admin-edit-popup__error">
                  The admin account cannot be edited.
                </p>
              )}
              {editError && (
                <p className="admin-edit-popup__error">{editError}</p>
              )}

              {!isChangingPassword ? (
                <>
                  <div className="admin-edit-popup__section">
                    <div className="admin-edit-popup__identity-fields">
                      <Input
                        label="Email"
                        value={editEmail}
                        onChange={(event) => setEditEmail(event.target.value)}
                        error={editFieldErrors.Email?.[0]}
                        disabled={identityDisabled}
                      />
                      <Input
                        label="First Name"
                        value={editFirstName}
                        onChange={(event) =>
                          setEditFirstName(event.target.value)
                        }
                        error={editFieldErrors.FirstName?.[0]}
                        disabled={identityDisabled}
                      />
                      <Input
                        label="Last Name"
                        value={editLastName}
                        onChange={(event) =>
                          setEditLastName(event.target.value)
                        }
                        error={editFieldErrors.LastName?.[0]}
                        disabled={identityDisabled}
                      />
                      <SelectDropdown
                        label="Permission"
                        value={editRole}
                        onChange={setEditRole}
                        options={ROLE_OPTIONS}
                        disabled={identityDisabled}
                        portalContainer={dialogNode}
                      />
                    </div>
                    <div className="admin-edit-popup__footer">
                      {!isAdminAccount && (
                        <Button
                          onClick={handleSaveDetailsClick}
                          disabled={isSubmitting}
                        >
                          Save Changes
                        </Button>
                      )}
                      <Button
                        variant="secondary"
                        onClick={handleCancelPopup}
                        disabled={isSubmitting}
                      >
                        Cancel
                      </Button>
                    </div>
                  </div>
                  {!isAdminAccount && (
                    <Button
                      variant="secondary"
                      onClick={() => setIsChangingPassword(true)}
                      disabled={isSubmitting}
                    >
                      Change Password
                    </Button>
                  )}
                </>
              ) : (
                <div className="admin-edit-popup__section">
                  <div className="admin-edit-popup__password-section">
                    <Input
                      label="New Password"
                      type="password"
                      value={editNewPassword}
                      onChange={(event) =>
                        setEditNewPassword(event.target.value)
                      }
                      error={
                        passwordError || passwordFieldErrors.NewPassword?.[0]
                      }
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
                  </div>
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
          </div>,
          document.body,
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

      <Modal
        open={createOpen}
        onOpenChange={(open) => {
          if (!open) {
            handleCancelCreate()
          }
        }}
        title="Create Barber"
      >
        <div className="admin-create-popup">
          {createError && (
            <p className="admin-create-popup__error">{createError}</p>
          )}
          <div className="admin-create-popup__fields">
            <Input
              label="Email"
              value={createEmail}
              onChange={(event) => setCreateEmail(event.target.value)}
              error={createFieldErrors.Email?.[0]}
              disabled={isCreating}
            />
            <Input
              label="First Name"
              value={createFirstName}
              onChange={(event) => setCreateFirstName(event.target.value)}
              error={createFieldErrors.FirstName?.[0]}
              disabled={isCreating}
            />
            <Input
              label="Last Name"
              value={createLastName}
              onChange={(event) => setCreateLastName(event.target.value)}
              error={createFieldErrors.LastName?.[0]}
              disabled={isCreating}
            />
            <Input
              label="Password"
              type="password"
              value={createPassword}
              onChange={handleCreatePasswordChange}
              error={createPasswordError || createFieldErrors.Password?.[0]}
              disabled={isCreating}
            />
            <Input
              label="Confirm Password"
              type="password"
              value={createConfirmPassword}
              onChange={handleCreateConfirmPasswordChange}
              error={createPasswordError || createFieldErrors.Password?.[0]}
              disabled={isCreating}
            />
          </div>
          <div className="admin-create-popup__footer">
            <Button onClick={handleSaveCreateClick} disabled={isCreating}>
              Create
            </Button>
            <Button
              variant="secondary"
              onClick={handleCancelCreate}
              disabled={isCreating}
            >
              Cancel
            </Button>
          </div>
        </div>
      </Modal>

      <ConfirmPopup
        open={createConfirmOpen}
        onOpenChange={setCreateConfirmOpen}
        title="Create Barber?"
        message="Create this barber account?"
        onConfirm={handleConfirmCreate}
      />
    </div>
  )
}
