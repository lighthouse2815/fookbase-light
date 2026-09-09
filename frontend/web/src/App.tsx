import { useState } from 'react'
import './App.css'
import TopNavbar from './components/TopNavbar'
import Sidebar, { type PageId } from './components/Sidebar'
import FeedPage from './components/feed/FeedPage'
import ProfilePage from './components/profile/ProfilePage'
import ExplorePage from './components/explore/ExplorePage'
import MessagesPage from './components/messages/MessagesPage'

function App() {
  const [activePage, setActivePage] = useState<PageId>('feed')

  const renderPage = () => {
    switch (activePage) {
      case 'feed': return <FeedPage />
      case 'explore': return <ExplorePage />
      case 'messages': return <MessagesPage />
      case 'profile': return <ProfilePage />
    }
  }

  return (
    <div className="flex flex-col min-h-screen bg-bg">
      {/* Top Navbar */}
      <TopNavbar activePage={activePage} onNavigate={setActivePage} />

      {/* Body: Sidebar + Main */}
      <div className="flex mt-14 min-h-[calc(100vh-56px)]">
        {/* Sidebar */}
        <Sidebar activePage={activePage} onNavigate={setActivePage} />

        {/* Main content */}
        <main
          key={activePage}
          className="ml-[280px] min-h-full flex-1 max-lg:ml-0"
          style={{ animation: 'fade-in 0.25s ease both' }}
        >
          {renderPage()}
        </main>
      </div>
    </div>
  )
}

export default App
