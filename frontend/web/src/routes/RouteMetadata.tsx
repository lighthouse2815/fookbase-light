import { useLayoutEffect } from 'react'
import { Outlet, useLocation } from 'react-router-dom'
import { updateRouteMetadata } from '../seo/metadata'
import { usePreferences } from '../preferences'

export default function RouteMetadata() {
  const { pathname } = useLocation()
  const { language } = usePreferences()

  useLayoutEffect(() => {
    updateRouteMetadata(pathname, language)
  }, [pathname, language])

  return <Outlet />
}
