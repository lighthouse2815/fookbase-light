import { Outlet, useLocation } from 'react-router-dom'
import TopNavbar from './TopNavbar'
import Sidebar from './Sidebar'

export default function MainLayout() {
  const location = useLocation()

  return (
    <div className="flex flex-col min-h-screen bg-bg">
      {/* Top Navbar */}
      <TopNavbar />

      {/* Body: Sidebar + Main Content */}
      <div className="flex mt-14 min-h-[calc(100vh-56px)]">
        {/* Sidebar */}
        <Sidebar />

        {/* Main content */}
        <main
          key={location.pathname}
          className="ml-[280px] min-h-full flex-1 max-lg:ml-0"
          style={{ animation: 'fade-in 0.25s ease both' }}
        >
          <Outlet />
        </main>
      </div>
    </div>
  )
}

