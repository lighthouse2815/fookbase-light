import { useState } from 'react'
import './App.css'
import Sidebar, { type PageId } from './components/Sidebar'
import ParticleCanvas from './components/effects/ParticleCanvas'
import FeedPage    from './components/feed/FeedPage'
import ProfilePage from './components/profile/ProfilePage'
import ExplorePage from './components/explore/ExplorePage'
import MessagesPage from './components/messages/MessagesPage'

function App() {
  const [activePage, setActivePage] = useState<PageId>('feed')

  const renderPage = () => {
    switch (activePage) {
      case 'feed':     return <FeedPage />
      case 'explore':  return <ExplorePage />
      case 'messages': return <MessagesPage />
      case 'profile':  return <ProfilePage />
    }
  }

  return (
    <div className="app-layout relative">
      {/* Particle background */}
      <ParticleCanvas />

      {/* Sidebar */}
      <Sidebar activePage={activePage} onNavigate={setActivePage} />

      {/* Main content */}
      <main
        key={activePage}
        className="ml-[240px] min-h-screen relative z-10
                   max-sm:ml-[60px]"
      >
        {renderPage()}
      </main>
    </div>
  )
}

export default App
