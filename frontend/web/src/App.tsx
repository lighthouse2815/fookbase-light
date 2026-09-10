import { RouterProvider } from 'react-router-dom'
import { AuthProvider } from './auth/AuthProvider'
import { RealtimeProvider } from './realtime/RealtimeProvider'
import { router } from './routes'
import './App.css'

export default function App() {
  return (
    <AuthProvider>
      <RealtimeProvider>
        <RouterProvider router={router} />
      </RealtimeProvider>
    </AuthProvider>
  )
}
