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
  const initializedRef = useRef(false)

  useEffect(() => {
    tokenRef.current = token
  }, [token])

  useEffect(() => {
    accessTokenRef.current = user?.accessToken ?? null
  }, [user?.accessToken])

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
            await logoutAccount(accessTokenRef.current)
            logout()
            window.location.assign(`${API_BASE_URL}/api/auth/sso/logout`)
          },
        })
      },
      () => {
        console.error('myzPAX banner script failed to load.')
      },
    )
  }, [token, logout])

  return null
}
