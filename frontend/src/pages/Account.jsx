import { useState } from 'react'
import { useNavigate } from 'react-router'
import { useAuth } from '../context/AuthContext'
import { updateAccount } from '../api/AccountApi'
import FormSection from '../components/FormSection'
import Input from '../components/Input'
import Button from '../components/Button'
import ConfirmPopup from '../components/ConfirmPopup'
import './Account.css'

const CURRENT_PASSWORD_INCORRECT_TITLE = 'Current password is incorrect.'
const SAME_AS_CURRENT_PASSWORD_TITLE =
  'New password must be different from your current password.'

export default function Account() {
  const navigate = useNavigate()
  const { user, login, logout } = useAuth()

  const [isEditingName, setIsEditingName] = useState(false)
  const [firstName, setFirstName] = useState(user.firstName)
  const [lastName, setLastName] = useState(user.lastName)

  const [isChangingPassword, setIsChangingPassword] = useState(false)
  const [currentPassword, setCurrentPassword] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [currentPasswordError, setCurrentPasswordError] = useState('')
  const [passwordError, setPasswordError] = useState('')

  const [fieldErrors, setFieldErrors] = useState({})
  const [savedMessage, setSavedMessage] = useState('')
  const [errorMessage, setErrorMessage] = useState('')
  const [isSubmitting, setIsSubmitting] = useState(false)

  const [confirmOpen, setConfirmOpen] = useState(false)
  const [pendingAction, setPendingAction] = useState(null) // 'name' | 'password'

  function clearMessages() {
    setSavedMessage('')
    setErrorMessage('')
    setFieldErrors({})
  }

  function handleCancelName() {
    setFirstName(user.firstName)
    setLastName(user.lastName)
    setIsEditingName(false)
  }

  function handleStartEditingName() {
    clearMessages()
    handleCancelPassword()
    setIsEditingName(true)
  }

  function handleSaveNameClick() {
    clearMessages()
    setPendingAction('name')
    setConfirmOpen(true)
  }

  function handleCancelPassword() {
    setCurrentPassword('')
    setNewPassword('')
    setConfirmPassword('')
    setCurrentPasswordError('')
    setPasswordError('')
    setIsChangingPassword(false)
  }

  function handleStartChangingPassword() {
    clearMessages()
    handleCancelName()
    setIsChangingPassword(true)
  }

  function handleSavePasswordClick() {
    clearMessages()
    setCurrentPasswordError('')
    if (!currentPassword) {
      setCurrentPasswordError('Current password is required')
      return
    }
    if (!newPassword) {
      setPasswordError('New password is required')
      return
    }
    if (newPassword !== confirmPassword) {
      setPasswordError('Passwords do not match')
      setNewPassword('')
      setConfirmPassword('')
      return
    }
    setPasswordError('')
    setPendingAction('password')
    setConfirmOpen(true)
  }

  async function handleConfirm() {
    setIsSubmitting(true)
    const isPasswordAction = pendingAction === 'password'
    const result = await updateAccount(user.accessToken, {
      // A password save must never carry a name change, even one sitting
      // unconfirmed in the (mutually-exclusive) name-edit fields -- send the
      // last-confirmed name, not local component state.
      firstName: isPasswordAction ? user.firstName : firstName,
      lastName: isPasswordAction ? user.lastName : lastName,
      newPassword: isPasswordAction ? newPassword : undefined,
      currentPassword: isPasswordAction ? currentPassword : undefined,
    })
    setIsSubmitting(false)

    if (result.ok) {
      login({
        ...user,
        firstName: result.identity.firstName,
        lastName: result.identity.lastName,
      })
      if (pendingAction === 'name') {
        setIsEditingName(false)
      } else {
        setCurrentPassword('')
        setNewPassword('')
        setConfirmPassword('')
        setIsChangingPassword(false)
      }
      setSavedMessage('Changes saved.')
      return
    }

    if (result.status === 401) {
      logout()
      navigate('/login', {
        state: { message: 'Your session has expired. Please sign in again.' },
      })
    } else if (result.status === 429) {
      setErrorMessage(
        result.problem?.title ??
          'Too many attempts. Try again in a few minutes.',
      )
    } else if (result.status === 409) {
      setErrorMessage(
        result.problem?.title ??
          'This account was updated elsewhere. Please refresh and try again.',
      )
    } else if (result.status === 400 && result.problem?.errors) {
      setFieldErrors(result.problem.errors)
    } else if (
      result.status === 400 &&
      result.problem?.title === CURRENT_PASSWORD_INCORRECT_TITLE
    ) {
      setFieldErrors({ CurrentPassword: [result.problem.title] })
    } else if (
      result.status === 400 &&
      result.problem?.title === SAME_AS_CURRENT_PASSWORD_TITLE
    ) {
      setFieldErrors({ NewPassword: [result.problem.title] })
    } else {
      setErrorMessage('Something went wrong. Please try again.')
    }
  }

  return (
    <div className="account">
      <h1 className="account__title">Account</h1>

      <FormSection>
        <div className="account-page">
          {savedMessage && (
            <p className="account-page__saved-message">{savedMessage}</p>
          )}
          {errorMessage && (
            <p className="account-page__error-message">{errorMessage}</p>
          )}

          <div className="account-page__field-display">
            <span className="input-field__label">Email</span>
            <span>{user.email}</span>
          </div>

          <section className="account-page__section">
            {!isEditingName ? (
              <div className="account-page__name-display">
                <span>
                  {firstName} {lastName}
                </span>
                <button
                  type="button"
                  className="account-page__edit-icon"
                  aria-label="Edit name"
                  onClick={handleStartEditingName}
                  disabled={isSubmitting}
                >
                  ✎
                </button>
              </div>
            ) : (
              <div className="account-page__name-edit">
                <div className="account-page__name-fields">
                  <Input
                    label="First Name"
                    value={firstName}
                    onChange={(event) => setFirstName(event.target.value)}
                    error={fieldErrors.FirstName?.[0]}
                  />
                  <Input
                    label="Last Name"
                    value={lastName}
                    onChange={(event) => setLastName(event.target.value)}
                    error={fieldErrors.LastName?.[0]}
                  />
                </div>
                <div className="account-page__actions">
                  <Button onClick={handleSaveNameClick} disabled={isSubmitting}>
                    Save
                  </Button>
                  <Button
                    variant="secondary"
                    onClick={handleCancelName}
                    disabled={isSubmitting}
                  >
                    Cancel
                  </Button>
                </div>
              </div>
            )}
          </section>

          <section className="account-page__section">
            {!isChangingPassword ? (
              <Button
                variant="secondary"
                onClick={handleStartChangingPassword}
                disabled={isSubmitting}
              >
                Change Password
              </Button>
            ) : (
              <div className="account-page__password-edit">
                <Input
                  label="Current Password"
                  type="password"
                  value={currentPassword}
                  onChange={(event) => setCurrentPassword(event.target.value)}
                  error={
                    currentPasswordError || fieldErrors.CurrentPassword?.[0]
                  }
                />
                <Input
                  label="New Password"
                  type="password"
                  value={newPassword}
                  onChange={(event) => setNewPassword(event.target.value)}
                  error={passwordError || fieldErrors.NewPassword?.[0]}
                />
                <Input
                  label="Confirm New Password"
                  type="password"
                  value={confirmPassword}
                  onChange={(event) => setConfirmPassword(event.target.value)}
                />
                <div className="account-page__actions">
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
          </section>
        </div>
      </FormSection>

      <ConfirmPopup
        open={confirmOpen}
        onOpenChange={setConfirmOpen}
        title="Save changes?"
        message={
          pendingAction === 'password'
            ? 'Save your new password?'
            : 'Save these changes to your account?'
        }
        onConfirm={handleConfirm}
      />
    </div>
  )
}
