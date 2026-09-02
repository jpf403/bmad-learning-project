import { useEffect, useRef } from 'react'
import { useAuth } from '../context/AuthContext'
import { loadScript } from '../lib/loadScript'
import { API_BASE_URL } from '../api/ApiConfig'
import { logoutAccount } from '../api/AuthApi'

const BANNER_SCRIPT_SRC =
  'https://dev.zpax-banner.myzpax.com/banner/v1/banner.js'
const CURRENT_APP_ID = 'barbershop_demo'

export default function MyzpaxBanner() {
  const { user, logout } = useAuth()
  const token = user?.zpaxAccessToken ?? null

  const tokenRef = useRef(token)
  const accessTokenRef = useRef(user?.accessToken ?? null)
  const logoutRef = useRef(logout)
  const initializedRef = useRef(false)
  const loggingOutRef = useRef(false)

  useEffect(() => {
    tokenRef.current = token
  }, [token])

  useEffect(() => {
    accessTokenRef.current = user?.accessToken ?? null
  }, [user?.accessToken])

  useEffect(() => {
    logoutRef.current = logout
  }, [logout])

  useEffect(() => {
    if (!token || initializedRef.current) {
      return
    }
    initializedRef.current = true

    loadScript(
      BANNER_SCRIPT_SRC,
      () => {
        if (!window.MyzpaxBanner?.init) {
          console.error(
            'myzPAX banner script loaded but did not define window.MyzpaxBanner.',
          )
          return
        }
        window.MyzpaxBanner.init({
          getToken: () => tokenRef.current,
          currentAppId: CURRENT_APP_ID,
          position: 'static',
          onLogout: async () => {
            if (loggingOutRef.current) {
              return
            }
            loggingOutRef.current = true
            if (accessTokenRef.current) {
              await logoutAccount(accessTokenRef.current)
            }
            logoutRef.current()
            window.location.assign(`${API_BASE_URL}/api/auth/sso/logout`)
          },
        })
      },
      () => {
        console.error('myzPAX banner script failed to load.')
      },
    )
  }, [token])

  return null
}
