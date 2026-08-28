import { useEffect, useRef } from 'react'
import { useAuth } from '../context/AuthContext'
import { loadScript } from '../lib/loadScript'

const BANNER_SCRIPT_SRC =
  'https://dev.zpax-banner.myzpax.com/banner/v1/banner.js'
const CURRENT_APP_ID = 'barbershop_demo'

export default function MyzpaxBanner() {
  const { user } = useAuth()
  const token = user?.zpaxAccessToken ?? null

  const tokenRef = useRef(token)
  const initializedRef = useRef(false)

  useEffect(() => {
    tokenRef.current = token
  }, [token])

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
        })
      },
      () => {
        console.error('myzPAX banner script failed to load.')
      },
    )
  }, [token])

  return null
}
