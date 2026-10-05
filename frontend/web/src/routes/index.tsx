import { lazy, Suspense, type ComponentType } from 'react'
import { createBrowserRouter, Navigate } from 'react-router-dom'
import MainLayout from '../layout/MainLayout'
import ZolaLightRedirect from '../pages/ZolaLightRedirect'
import RouteErrorPage from './RouteErrorPage'
import RouteMetadata from './RouteMetadata'
import HomePage from '../pages/home/HomePage'

const feedPage = lazy(() => import('../pages/feed/FeedPage'))
const explorePage = lazy(() => import('../pages/explore/ExplorePage'))
const gamesPage = lazy(() => import('../pages/games/GamesPage'))
const gamePage = lazy(() => import('../game/Game'))
const profilePage = lazy(() => import('../pages/profile/ProfilePage'))
const userProfilePage = lazy(() => import('../pages/profile/UserProfilePage'))
const loginPage = lazy(() => import('../pages/auth/LoginPage'))
const groupsPage = lazy(() => import('../pages/groups/GroupsPage'))
const groupDetailPage = lazy(() => import('../pages/groups/GroupDetailPage'))
const reelsPage = lazy(() => import('../pages/reels/ReelsPage'))
const storyArchivePage = lazy(() => import('../pages/stories/StoryArchivePage'))
const storyDetailPage = lazy(() => import('../pages/stories/StoryDetailPage'))
const pagesPage = lazy(() => import('../pages/pages/PagesPage'))
const pageCreatePage = lazy(() => import('../pages/pages/PageCreatePage'))
const pageDetailPage = lazy(() => import('../pages/pages/PageDetailPage'))
const searchPage = lazy(() => import('../pages/search/SearchPage'))
const savedPostsPage = lazy(() => import('../pages/saved/SavedPostsPage'))
const hashtagPage = lazy(() => import('../pages/hashtags/HashtagPage'))
const eventsPage = lazy(() => import('../pages/events/EventsPage'))
const eventCreatePage = lazy(() => import('../pages/events/EventCreatePage'))
const eventDetailPage = lazy(() => import('../pages/events/EventDetailPage'))
const photosPage = lazy(() => import('../pages/photos/PhotosPage'))
const albumDetailPage = lazy(() => import('../pages/photos/AlbumDetailPage'))
const memoriesPage = lazy(() => import('../pages/memories/MemoriesPage'))
const birthdaysPage = lazy(() => import('../pages/birthdays/BirthdaysPage'))
const privacySettingsPage = lazy(() => import('../pages/settings/PrivacySettingsPage'))
const securitySettingsPage = lazy(() => import('../pages/settings/SecuritySettingsPage'))
const notificationsPage = lazy(() => import('../pages/notifications/NotificationsPage'))
const postDetailPage = lazy(() => import('../pages/posts/PostDetailPage'))
const aiChatPage = lazy(() => import('../pages/ai/AiChatPage'))

function page(Page: ComponentType) {
  return <Suspense fallback={<main className="min-h-screen grid place-items-center text-text-muted">Đang tải…</main>}><Page /></Suspense>
}

export const router = createBrowserRouter([{ element: <RouteMetadata />, children: [
  {
    path: '/',
    element: <HomePage />,
    errorElement: <RouteErrorPage />,
  },
  {
    path: '/login',
    element: page(loginPage),
    errorElement: <RouteErrorPage />,
  },
  {
    path: '/',
    element: <MainLayout />,
    errorElement: <RouteErrorPage />,
    children: [
      {
        path: 'feed',
        element: page(feedPage),
      },
      {
        path: 'search',
        element: page(searchPage),
      },
      {
        path: 'notifications',
        element: page(notificationsPage),
      },
      { path: 'posts/:postId', element: page(postDetailPage) },
      {
        path: 'saved',
        element: page(savedPostsPage),
      },
      {
        path: 'hashtag/:tag',
        element: page(hashtagPage),
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
        path: 'game',
        element: page(gamePage),
      },
      {
        path: 'ai-chat',
        element: page(aiChatPage),
      },
      {
        path: 'messages',
        element: <ZolaLightRedirect />,
      },
      {
        path: 'groups',
        element: page(groupsPage),
      },
      {
        path: 'groups/:groupId',
        element: page(groupDetailPage),
      },
      { path: 'events', element: page(eventsPage) },
      { path: 'events/create', element: page(eventCreatePage) },
      { path: 'events/:eventId', element: page(eventDetailPage) },
      { path: 'photos', element: page(photosPage) },
      { path: 'albums/:albumId', element: page(albumDetailPage) },
      { path: 'memories', element: page(memoriesPage) },
      { path: 'birthdays', element: page(birthdaysPage) },
      { path: 'settings/privacy', element: page(privacySettingsPage) },
      { path: 'settings/security', element: page(securitySettingsPage) },
      {
        path: 'pages',
        element: page(pagesPage),
      },
      {
        path: 'pages/create',
        element: page(pageCreatePage),
      },
      {
        path: 'pages/:username',
        element: page(pageDetailPage),
      },
      {
        path: 'reels',
        element: page(reelsPage),
      },
      {
        path: 'stories/archive',
        element: page(storyArchivePage),
      },
      { path: 'stories/:storyId', element: page(storyDetailPage) },
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
    ].map(route => ({ ...route, errorElement: <RouteErrorPage /> })),
  },
]}])
