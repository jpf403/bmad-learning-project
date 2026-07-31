import { useEffect, useState } from 'react'
import { Navigate } from 'react-router'
import { useAuth } from '../context/AuthContext'
import { getCurrentUser } from '../api/AuthApi'
import { LANDING_ROUTE } from '../landingRoutes'

export default function RequireRole({ roles, children }) {
  const { user, ready } = useAuth()
  const [check, setCheck] = useState({ status: 'pending' })

  useEffect(() => {
    if (!user) return

    let cancelled = false

    async function verify() {
      let result = await getCurrentUser(user.accessToken)
      if (!result.ok && result.status === null) {
        // A null status means the fetch itself failed (network error), not a
        // real 401/403 from the server -- retry once before treating it the
        // same as an actual session expiry.
        result = await getCurrentUser(user.accessToken)
      }
      if (cancelled) return

      if (!result.ok) {
        setCheck({ status: 'unauthenticated' })
      } else if (!roles.includes(result.identity.role)) {
        setCheck({ status: 'wrong-role', role: result.identity.role })
      } else {
        setCheck({ status: 'allowed' })
      }
    }

    verify()

    return () => {
      cancelled = true
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [user])

  if (!ready) return null
  if (!user) return <Navigate to="/login" replace />
  if (check.status === 'pending') return null
  if (check.status === 'unauthenticated')
    return <Navigate to="/login" replace />
  if (check.status === 'wrong-role')
    return <Navigate to={LANDING_ROUTE[check.role] ?? '/'} replace />
  return children
}
