import { createBrowserRouter, Navigate } from 'react-router-dom'
import MainLayout from '../layout/MainLayout'
import FeedPage from '../pages/feed/FeedPage'
import ExplorePage from '../pages/explore/ExplorePage'
import MessagesPage from '../pages/messages/MessagesPage'
import ProfilePage from '../pages/profile/ProfilePage'
import LoginPage from '../pages/auth/LoginPage'

export const router = createBrowserRouter([
  {
    path: '/login',
    element: <LoginPage />,
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
        element: <FeedPage />,
      },
      {
        path: 'explore',
        element: <ExplorePage />,
      },
      {
        path: 'messages',
        element: <MessagesPage />,
      },
      {
        path: 'profile',
        element: <ProfilePage />,
      },
      {
        path: '*',
        element: <Navigate to="/feed" replace />,
      },
    ],
  },
])
