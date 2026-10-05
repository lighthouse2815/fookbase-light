import { Navigate } from 'react-router-dom'
import { useAuth } from '../../auth/useAuth'
import LandingContent from './LandingContent'

export default function HomePage() {
  const { session } = useAuth()

  if (session) return <Navigate to="/feed" replace />

  return <LandingContent />
}
