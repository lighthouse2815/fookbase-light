import { RouterProvider } from 'react-router-dom'
import { AuthProvider } from './auth/AuthProvider'
import { RealtimeProvider } from './realtime/RealtimeProvider'
import { router } from './routes'
import { PreferencesProvider } from './preferences'
import './App.css'

export default function App() {
  return (
    <PreferencesProvider>
      <AuthProvider>
        <RealtimeProvider>
          <RouterProvider router={router} />
        </RealtimeProvider>
      </AuthProvider>
    </PreferencesProvider>
  )
}
