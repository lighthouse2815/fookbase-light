import { lazy, Suspense, type ComponentType } from 'react'
import { createBrowserRouter, Navigate } from 'react-router-dom'
import MainLayout from '../layout/MainLayout'

const feedPage = lazy(() => import('../pages/feed/FeedPage'))
const explorePage = lazy(() => import('../pages/explore/ExplorePage'))
const gamesPage = lazy(() => import('../pages/games/GamesPage'))
const messagesPage = lazy(() => import('../pages/messages/MessagesPage'))
const profilePage = lazy(() => import('../pages/profile/ProfilePage'))
const userProfilePage = lazy(() => import('../pages/profile/UserProfilePage'))
const loginPage = lazy(() => import('../pages/auth/LoginPage'))
const groupsPage = lazy(() => import('../pages/groups/GroupsPage'))
const groupDetailPage = lazy(() => import('../pages/groups/GroupDetailPage'))
const reelsPage = lazy(() => import('../pages/reels/ReelsPage'))
const storyArchivePage = lazy(() => import('../pages/stories/StoryArchivePage'))

function page(Page: ComponentType) {
  return <Suspense fallback={<main className="min-h-screen grid place-items-center text-text-muted">Đang tải…</main>}><Page /></Suspense>
}

export const router = createBrowserRouter([
  {
    path: '/login',
    element: page(loginPage),
  },
  {
    path: '/',
    element: <MainLayout />,
    children: [
      {
        index: true,
        element: <Navigate to="/feed" replace />,
      },
      {
        path: 'feed',
        element: page(feedPage),
      },
      {
        path: 'explore',
        element: page(explorePage),
      },
      {
        path: 'games',
        element: page(gamesPage),
      },
      {
        path: 'messages',
        element: page(messagesPage),
      },
      {
        path: 'groups',
        element: page(groupsPage),
      },
      {
        path: 'groups/:groupId',
        element: page(groupDetailPage),
      },
      {
        path: 'reels',
        element: page(reelsPage),
      },
      {
        path: 'stories/archive',
        element: page(storyArchivePage),
      },
      {
        path: 'profile',
        element: page(profilePage),
      },
      {
        path: 'profile/:userId',
        element: page(userProfilePage),
      },
      {
        path: '*',
        element: <Navigate to="/feed" replace />,
      },
    ],
  },
])
