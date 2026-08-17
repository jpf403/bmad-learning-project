import { useState } from 'react'
import { useNavigate } from 'react-router'
import { registerAccount } from '../api/AuthApi'
import Input from '../components/Input'
import Button from '../components/Button'
import FormSection from '../components/FormSection'
import './Register.css'

export default function Register() {
  const navigate = useNavigate()

  const [email, setEmail] = useState('')
  const [firstName, setFirstName] = useState('')
  const [lastName, setLastName] = useState('')
  const [password, setPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [emailError, setEmailError] = useState('')
  const [passwordError, setPasswordError] = useState('')
  const [formError, setFormError] = useState('')
  const [isSubmitting, setIsSubmitting] = useState(false)

  const stripWhitespace = (value) => value.replace(/\s/g, '')

  const handleSubmit = async (event) => {
    event.preventDefault()

    if (isSubmitting) {
      return
    }

    if (password !== confirmPassword) {
      setEmailError('')
      setFormError('')
      setPasswordError('Passwords do not match')
      setPassword('')
      setConfirmPassword('')
      return
    }

    if (password.length < 8) {
      setEmailError('')
      setFormError('')
      setPasswordError('Password must be at least 8 characters.')
      return
    }

    setEmailError('')
    setPasswordError('')
    setFormError('')
    setIsSubmitting(true)

    const result = await registerAccount({
      email,
      password,
      firstName,
      lastName,
    })

    setIsSubmitting(false)

    if (result.ok) {
      navigate('/login', {
        state: { message: 'Account created. Sign in to continue.' },
      })
      return
    }

    if (result.status === 409) {
      setEmailError('That email is already in use.')
      return
    }

    if (result.status === 400) {
      const message = result.problem?.errors?.Email?.[0]
      if (message) {
        setEmailError(message)
      } else {
        setFormError('Please check the form and try again.')
      }
      return
    }

    setFormError('Something went wrong. Please try again.')
  }

  return (
    <div className="register">
      <h1 className="register__title">Register</h1>

      <FormSection>
        <form className="register__form" onSubmit={handleSubmit}>
          <Input
            label="First name"
            value={firstName}
            onChange={(event) => setFirstName(event.target.value)}
            autoComplete="off"
          />
          <Input
            label="Last name"
            value={lastName}
            onChange={(event) => setLastName(event.target.value)}
            autoComplete="off"
          />
          <Input
            label="Email"
            value={email}
            error={emailError}
            onChange={(event) => setEmail(event.target.value)}
            autoComplete="off"
          />
          <Input
            label="Password"
            type="password"
            value={password}
            error={passwordError}
            onChange={(event) =>
              setPassword(stripWhitespace(event.target.value))
            }
            autoComplete="new-password"
          />
          <Input
            label="Confirm password"
            type="password"
            value={confirmPassword}
            error={passwordError}
            onChange={(event) =>
              setConfirmPassword(stripWhitespace(event.target.value))
            }
            autoComplete="new-password"
          />
          {formError && <p className="register__form-error">{formError}</p>}
          <Button variant="primary" type="submit" disabled={isSubmitting}>
            Register
          </Button>
        </form>
      </FormSection>
    </div>
  )
}
