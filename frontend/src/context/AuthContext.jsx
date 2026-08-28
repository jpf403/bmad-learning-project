import { createContext, useContext, useEffect, useState } from 'react'
import { getCurrentUser, getZpaxToken, refreshSession } from '../api/AuthApi'

const AuthContext = createContext(null)

export function AuthProvider({ children }) {
  const [user, setUser] = useState(null)
  const [ready, setReady] = useState(false)

  useEffect(() => {
    let cancelled = false

    async function bootstrap() {
      const refreshResult = await refreshSession()
      if (!refreshResult.ok) {
        if (!cancelled) setReady(true)
        return
      }

      const meResult = await getCurrentUser(refreshResult.accessToken)
      if (cancelled) return
      if (meResult.ok) {
        const zpaxResult = await getZpaxToken(refreshResult.accessToken)
        if (cancelled) return
        setUser({
          accessToken: refreshResult.accessToken,
          ...meResult.identity,
          zpaxAccessToken: zpaxResult.ok ? zpaxResult.zpaxAccessToken : null,
        })
      }
      setReady(true)
    }

    bootstrap()
    return () => {
      cancelled = true
    }
  }, [])

  return (
    <AuthContext.Provider
      value={{ user, ready, login: setUser, logout: () => setUser(null) }}
    >
      {children}
    </AuthContext.Provider>
  )
}

// eslint-disable-next-line react-refresh/only-export-components
export function useAuth() {
  const context = useContext(AuthContext)
  if (context === null) {
    throw new Error('useAuth must be used within an AuthProvider')
  }
  return context
}
