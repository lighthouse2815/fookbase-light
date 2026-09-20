import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { useAuth } from '../auth/useAuth'
import TopNavbar from './TopNavbar'
import Sidebar from './Sidebar'

const sidebarPaths = new Set(['/feed', '/saved', '/memories'])

export default function MainLayout() {
  const location = useLocation()
  const { session } = useAuth()
  const shouldShowSidebar = sidebarPaths.has(location.pathname)
  const isCenteredFeed = location.pathname === '/feed'

  if (!session) return <Navigate to="/login" replace />

  return (
    <div className="flex flex-col min-h-screen bg-bg">
      {/* Top Navbar */}
      <TopNavbar />

      {/* Body: Sidebar + Main Content */}
      <div className="flex mt-[var(--app-header-height)] min-h-[calc(100dvh-var(--app-header-height))]">
        {shouldShowSidebar && <Sidebar alignWithCenteredFeed={isCenteredFeed} />}

        {/* Main content */}
        <main
          key={location.pathname}
          className={shouldShowSidebar && !isCenteredFeed ? 'ml-[360px] min-h-full min-w-0 flex-1 max-xl:ml-0' : 'min-h-full min-w-0 flex-1'}
          style={{ animation: 'fade-in 0.25s ease both' }}
        >
          <Outlet />
        </main>
      </div>
    </div>
  )
}
