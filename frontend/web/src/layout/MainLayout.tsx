import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { useAuth } from '../auth/useAuth'
import TopNavbar from './TopNavbar'
import Sidebar from './Sidebar'

const sidebarPaths = new Set(['/feed', '/explore', '/saved', '/memories'])

export default function MainLayout() {
  const location = useLocation()
  const { session } = useAuth()
  const shouldShowSidebar = sidebarPaths.has(location.pathname)

  if (!session) return <Navigate to="/login" replace />

  return (
    <div className="flex flex-col min-h-screen bg-bg">
      {/* Top Navbar */}
      <TopNavbar />

      {/* Body: Sidebar + Main Content */}
      <div className="flex mt-14 min-h-[calc(100vh-56px)]">
        {shouldShowSidebar && <Sidebar />}

        {/* Main content */}
        <main
          key={location.pathname}
          className={shouldShowSidebar ? 'ml-[360px] min-h-full flex-1 max-xl:ml-0' : 'min-h-full flex-1'}
          style={{ animation: 'fade-in 0.25s ease both' }}
        >
          <Outlet />
        </main>
      </div>
    </div>
  )
}
