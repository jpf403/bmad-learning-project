import { createContext, useContext, useEffect, useRef, useState } from 'react'
import {
  getCurrentUser,
  getZpaxToken,
  refreshSession,
  refreshZpaxToken,
} from '../api/AuthApi'

const AuthContext = createContext(null)

const ZPAX_REFRESH_INTERVAL_MS = 55 * 60 * 1000
// Cross-tab leader election so only one tab actually calls zpax-refresh per
// cycle -- every other open tab just adopts that leader's broadcast result.
// Without this, two tabs whose intervals fire around the same moment can both
// redeem the same zpaxRefreshToken cookie; the second one presents an
// already-rotated-out token and gets treated as a genuine rejection.
const LEADER_STALE_MS = ZPAX_REFRESH_INTERVAL_MS * 2
const LEADER_ID_KEY = 'zpaxRefreshLeaderId'
const LEADER_HEARTBEAT_KEY = 'zpaxRefreshLeaderHeartbeat'

function claimLeadership(tabId) {
  const lastHeartbeat = Number(localStorage.getItem(LEADER_HEARTBEAT_KEY) || 0)
  const currentLeaderId = localStorage.getItem(LEADER_ID_KEY)
  const isStale = Date.now() - lastHeartbeat > LEADER_STALE_MS
  if (currentLeaderId !== tabId && !isStale) {
    return false
  }
  localStorage.setItem(LEADER_ID_KEY, tabId)
  localStorage.setItem(LEADER_HEARTBEAT_KEY, String(Date.now()))
  return true
}

// A stale leader entry surviving a sign-out would make the next session's
// first tab wrongly defer to a "leader" that no longer exists (its page is
// gone) for up to LEADER_STALE_MS, so every sign-out path clears it -- but
// only if *this* tab is the one actually recorded as leader. A different,
// non-leader tab signing out must not wipe another tab's still-legitimate,
// still-active leadership out from under it.
function clearLeadershipIfOwned(tabId) {
  if (localStorage.getItem(LEADER_ID_KEY) === tabId) {
    localStorage.removeItem(LEADER_ID_KEY)
    localStorage.removeItem(LEADER_HEARTBEAT_KEY)
  }
}

export function AuthProvider({ children }) {
  const [user, setUser] = useState(null)
  const [ready, setReady] = useState(false)

  const accessTokenRef = useRef(user?.accessToken ?? null)
  const refreshStartedRef = useRef(false)
  const tabIdRef = useRef(null)
  const hasZpaxToken = Boolean(user?.zpaxAccessToken)

  useEffect(() => {
    accessTokenRef.current = user?.accessToken ?? null
  }, [user?.accessToken])

  const setZpaxToken = (token) =>
    setUser((prev) => (prev ? { ...prev, zpaxAccessToken: token } : prev))

  useEffect(() => {
    // Depending on the boolean "has a token at all" rather than the token's
    // own value means a *successful* refresh (which changes that value but
    // not its truthiness) never re-triggers this effect -- so the interval
    // it creates is never torn down and left unreplaced.
    if (!hasZpaxToken || refreshStartedRef.current) {
      return
    }
    refreshStartedRef.current = true

    const tabId = crypto.randomUUID()
    tabIdRef.current = tabId
    const channel = new BroadcastChannel('zpax-refresh')
    let stopped = false

    // A closed tab runs no React cleanup, so without this a leader tab that's
    // simply closed (not signed out) would leave its heartbeat looking fresh
    // -- every other tab would keep deferring to it for up to LEADER_STALE_MS
    // even though nothing is left to ever broadcast a refresh. Releasing
    // leadership on a clean close lets a survivor claim it on its very next
    // tick instead. A crash/force-quit skips this and still falls back to
    // the staleness window.
    const releaseLeadershipOnClose = () => clearLeadershipIfOwned(tabId)
    window.addEventListener('pagehide', releaseLeadershipOnClose)

    channel.onmessage = (event) => {
      const { type, token } = event.data ?? {}
      if (type === 'refreshed') {
        setZpaxToken(token)
      } else if (type === 'degraded') {
        stopped = true
        setZpaxToken(null)
      } else if (type === 'signed-out') {
        stopped = true
        setUser(null)
        window.location.assign('/login')
      }
    }

    const intervalId = setInterval(async () => {
      if (stopped || !claimLeadership(tabId)) {
        return
      }

      // Refresh this app's own session first -- accessTokenRef.current can
      // otherwise be stale by the time the interval fires (this app's own
      // access token isn't proactively refreshed anywhere else), and a bare
      // 401 from [Authorize] itself is indistinguishable from a genuine
      // z-pax rejection once it reaches the branch below. If this app's own
      // session is the one that's actually dead, that's unambiguous -- sign
      // out now rather than letting the zpax-refresh call below misreport it
      // as a z-pax rejection.
      const sessionResult = await refreshSession()
      if (!sessionResult.ok) {
        stopped = true
        channel.postMessage({ type: 'signed-out' })
        setUser(null)
        clearLeadershipIfOwned(tabId)
        window.location.assign('/login')
        return
      }
      accessTokenRef.current = sessionResult.accessToken
      setUser((prev) =>
        prev ? { ...prev, accessToken: sessionResult.accessToken } : prev,
      )

      const result = await refreshZpaxToken(accessTokenRef.current)
      if (result.ok) {
        setZpaxToken(result.zpaxAccessToken)
        channel.postMessage({
          type: 'refreshed',
          token: result.zpaxAccessToken,
        })
      } else if (result.status === 401) {
        stopped = true
        channel.postMessage({ type: 'signed-out' })
        setUser(null)
        clearLeadershipIfOwned(tabId)
        window.location.assign('/login')
      } else {
        stopped = true
        channel.postMessage({ type: 'degraded' })
        setZpaxToken(null)
      }
    }, ZPAX_REFRESH_INTERVAL_MS)

    return () => {
      clearInterval(intervalId)
      channel.close()
      window.removeEventListener('pagehide', releaseLeadershipOnClose)
    }
  }, [hasZpaxToken])

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
          } else if (zpaxRefreshResult.status === 401) {
            clearLeadershipIfOwned(tabIdRef.current)
            window.location.assign('/login')
            return
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
        logout: () => {
          clearLeadershipIfOwned(tabIdRef.current)
          setUser(null)
        },
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
