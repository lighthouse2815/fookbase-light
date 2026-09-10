import { createBrowserRouter, Navigate } from 'react-router-dom'
import MainLayout from '../layout/MainLayout'
import FeedPage from '../pages/feed/FeedPage'
import ExplorePage from '../pages/explore/ExplorePage'
import GamesPage from '../pages/games/GamesPage'
import MessagesPage from '../pages/messages/MessagesPage'
import ProfilePage from '../pages/profile/ProfilePage'
import UserProfilePage from '../pages/profile/UserProfilePage'
import LoginPage from '../pages/auth/LoginPage'
import { GroupDetailPage, GroupsPage } from '../pages/groups'

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
        path: 'games',
        element: <GamesPage />,
      },
      {
        path: 'messages',
        element: <MessagesPage />,
      },
      {
        path: 'groups',
        element: <GroupsPage />,
      },
      {
        path: 'groups/:groupId',
        element: <GroupDetailPage />,
      },
      {
        path: 'profile',
        element: <ProfilePage />,
      },
      {
        path: 'profile/:userId',
        element: <UserProfilePage />,
      },
      {
        path: '*',
        element: <Navigate to="/feed" replace />,
      },
    ],
  },
])
