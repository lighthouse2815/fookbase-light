import { RouterProvider } from 'react-router-dom'
import { AuthProvider } from './auth/AuthProvider'
import { RealtimeProvider } from './realtime/RealtimeProvider'
import { router } from './routes'
import { PreferencesProvider } from './preferences'
import ToastViewport from './shared/components/ToastViewport'
import { QueryProvider } from './queries/QueryProvider'
import './App.css'

export default function App() {
  return (
    <PreferencesProvider>
      <AuthProvider>
        <QueryProvider>
          <RealtimeProvider>
            <RouterProvider router={router} />
            <ToastViewport />
          </RealtimeProvider>
        </QueryProvider>
      </AuthProvider>
    </PreferencesProvider>
  )
}
