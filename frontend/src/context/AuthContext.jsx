import { createContext, useContext, useEffect, useRef, useState } from 'react'
import {
  getCurrentUser,
  getZpaxToken,
  refreshSession,
  refreshZpaxToken,
} from '../api/AuthApi'

const AuthContext = createContext(null)

const ZPAX_REFRESH_INTERVAL_MS = 15 * 60 * 1000

export function AuthProvider({ children }) {
  const [user, setUser] = useState(null)
  const [ready, setReady] = useState(false)

  const accessTokenRef = useRef(user?.accessToken ?? null)
  const refreshStartedRef = useRef(false)

  useEffect(() => {
    accessTokenRef.current = user?.accessToken ?? null
  }, [user?.accessToken])

  const setZpaxToken = (token) =>
    setUser((prev) => (prev ? { ...prev, zpaxAccessToken: token } : prev))

  useEffect(() => {
    if (!user?.zpaxAccessToken || refreshStartedRef.current) {
      return
    }
    refreshStartedRef.current = true

    const intervalId = setInterval(async () => {
      const result = await refreshZpaxToken(accessTokenRef.current)
      if (result.ok) {
        setZpaxToken(result.zpaxAccessToken)
      } else {
        setZpaxToken(null)
        clearInterval(intervalId)
      }
    }, ZPAX_REFRESH_INTERVAL_MS)

    return () => clearInterval(intervalId)
  }, [user?.zpaxAccessToken])

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
        let zpaxAccessToken = null
        const zpaxResult = await getZpaxToken(refreshResult.accessToken)
        if (cancelled) return
        if (zpaxResult.ok) {
          zpaxAccessToken = zpaxResult.zpaxAccessToken
        } else {
          const zpaxRefreshResult = await refreshZpaxToken(
            refreshResult.accessToken,
          )
          if (cancelled) return
          if (zpaxRefreshResult.ok) {
            zpaxAccessToken = zpaxRefreshResult.zpaxAccessToken
          }
        }
        setUser({
          accessToken: refreshResult.accessToken,
          ...meResult.identity,
          zpaxAccessToken,
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
      value={{
        user,
        ready,
        login: setUser,
        logout: () => setUser(null),
        setZpaxToken,
      }}
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
