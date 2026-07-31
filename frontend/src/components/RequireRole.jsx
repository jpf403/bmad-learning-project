import { useEffect, useState } from 'react'
import { Navigate } from 'react-router'
import { useAuth } from '../context/AuthContext'
import { getCurrentUser } from '../api/AuthApi'

const LANDING_ROUTE = {
  Customer: '/schedule-appointment',
  Barber: '/my-schedule',
  Admin: '/my-schedule',
}

export default function RequireRole({ roles, children }) {
  const { user } = useAuth()
  const [check, setCheck] = useState({ status: 'pending' })

  useEffect(() => {
    if (!user) return

    let cancelled = false

    getCurrentUser(user.accessToken).then((result) => {
      if (cancelled) return
      if (!result.ok) {
        setCheck({ status: 'unauthenticated' })
      } else if (!roles.includes(result.identity.role)) {
        setCheck({ status: 'wrong-role', role: result.identity.role })
      } else {
        setCheck({ status: 'allowed' })
      }
    })

    return () => {
      cancelled = true
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [user])

  if (!user) return <Navigate to="/login" replace />
  if (check.status === 'pending') return null
  if (check.status === 'unauthenticated')
    return <Navigate to="/login" replace />
  if (check.status === 'wrong-role')
    return <Navigate to={LANDING_ROUTE[check.role]} replace />
  return children
}
