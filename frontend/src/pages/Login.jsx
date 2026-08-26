import { useEffect, useState } from 'react'
import { useLocation, useNavigate, useSearchParams } from 'react-router'
import { loginAccount } from '../api/AuthApi'
import { useAuth } from '../context/AuthContext'
import { API_BASE_URL } from '../api/ApiConfig'
import Input from '../components/Input'
import Button from '../components/Button'
import FormSection from '../components/FormSection'
import { LANDING_ROUTE } from '../landingRoutes'
import './Login.css'

export default function Login() {
  const navigate = useNavigate()
  const location = useLocation()
  const [searchParams, setSearchParams] = useSearchParams()
  const { login } = useAuth()

  const [successMessage] = useState(location.state?.message ?? '')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [formError, setFormError] = useState(() =>
    searchParams.get('error') === 'sso_failed'
      ? 'Sign-in with z-pax failed. Please try again.'
      : '',
  )
  const [isSubmitting, setIsSubmitting] = useState(false)

  useEffect(() => {
    if (location.state?.message) {
      navigate(location.pathname, { replace: true, state: {} })
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  useEffect(() => {
    if (searchParams.get('error') === 'sso_failed') {
      setSearchParams({}, { replace: true })
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  const handleSubmit = async (event) => {
    event.preventDefault()

    if (isSubmitting) {
      return
    }

    setFormError('')
    setIsSubmitting(true)

    const result = await loginAccount({ email, password })

    setIsSubmitting(false)

    if (result.ok) {
      login(result.session)
      navigate(LANDING_ROUTE[result.session.role] ?? '/')
      return
    }

    if (result.status === 429) {
      setFormError('Too many attempts. Try again in a few minutes.')
      return
    }

    if (result.status === 401) {
      setFormError('Invalid email or password.')
      return
    }

    if (result.status === 400) {
      setFormError('Please check the form and try again.')
      return
    }

    setFormError('Something went wrong. Please try again.')
  }

  const handleSsoLogin = () => {
    window.location.href = `${API_BASE_URL}/api/auth/sso/login`
  }

  return (
    <div className="login">
      <h1 className="login__title">Sign In</h1>

      {successMessage && !formError && !isSubmitting && (
        <p className="login__success-banner">{successMessage}</p>
      )}

      <FormSection>
        <form className="login__form" onSubmit={handleSubmit}>
          <Input
            label="Email"
            value={email}
            onChange={(event) => setEmail(event.target.value)}
          />
          <Input
            label="Password"
            type="password"
            value={password}
            onChange={(event) => setPassword(event.target.value)}
          />
          {formError && <p className="login__form-error">{formError}</p>}
          <Button variant="primary" type="submit" disabled={isSubmitting}>
            Sign In
          </Button>
        </form>

        <div className="login__divider">
          <span>or</span>
        </div>

        <Button variant="secondary" type="button" onClick={handleSsoLogin}>
          Sign in with z-pax
        </Button>
      </FormSection>
    </div>
  )
}
