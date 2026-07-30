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

  const handleSubmit = async (event) => {
    event.preventDefault()

    if (password !== confirmPassword) {
      setPasswordError('Passwords do not match')
      setPassword('')
      setConfirmPassword('')
      return
    }

    setEmailError('')
    setPasswordError('')

    const result = await registerAccount({
      email,
      password,
      firstName,
      lastName,
    })

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
      const message =
        result.problem?.errors?.Email?.[0] ?? 'Enter a valid email address.'
      setEmailError(message)
    }
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
          />
          <Input
            label="Last name"
            value={lastName}
            onChange={(event) => setLastName(event.target.value)}
          />
          <Input
            label="Email"
            value={email}
            error={emailError}
            onChange={(event) => setEmail(event.target.value)}
          />
          <Input
            label="Password"
            type="password"
            value={password}
            error={passwordError}
            onChange={(event) => setPassword(event.target.value)}
          />
          <Input
            label="Confirm password"
            type="password"
            value={confirmPassword}
            error={passwordError}
            onChange={(event) => setConfirmPassword(event.target.value)}
          />
          <Button variant="primary" type="submit">
            Register
          </Button>
        </form>
      </FormSection>
    </div>
  )
}
