import NotificationCenter from '../../shared/components/NotificationCenter'

export default function NotificationsPage() {
  return <main className="min-h-screen px-3 py-4 sm:px-4">
    <div className="mx-auto w-full max-w-[680px] rounded-2xl border border-border bg-surface pb-1 shadow-sm">
      <NotificationCenter />
    </div>
  </main>
}
