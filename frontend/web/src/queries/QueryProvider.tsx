import { useEffect, useState, type ReactNode } from 'react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { useAuth } from '../auth/useAuth'
import { authSessionChangedEvent, getAuthSession } from '../auth/session'

function SessionQueries({ children, userId }: { children: ReactNode; userId: string | undefined }) {
  const [client] = useState(() => new QueryClient({
    defaultOptions: { queries: { retry: false, staleTime: 30_000, refetchOnWindowFocus: false } },
  }))
  useEffect(() => {
    const clearChangedSession = () => { if (getAuthSession()?.user.id !== userId) client.clear() }
    window.addEventListener(authSessionChangedEvent, clearChangedSession)
    return () => window.removeEventListener(authSessionChangedEvent, clearChangedSession)
  }, [client, userId])
  return <QueryClientProvider client={client}>{children}</QueryClientProvider>
}

export function QueryProvider({ children }: { children: ReactNode }) {
  const { session } = useAuth()
  return <SessionQueries key={session?.user.id ?? 'anonymous'} userId={session?.user.id}>{children}</SessionQueries>
}
