import { useEffect, useRef, useState } from 'react'
import { adminApi } from './api/admin'
import type { AdminDashboard as DashboardData, AdminUser, ModerationReport, ReportStatus, UserModerationState } from './api/admin'
import { ApiError } from './api/client'
import { PreferenceControls, usePreferences } from './preferences'

type Tab = 'overview' | 'reports' | 'users'
type ReportFilter = ReportStatus | 'all'

const shortId = (value: string) => `${value.slice(0, 8)}…${value.slice(-4)}`

function Metric({ label, value, tone, locale }: { label: string; value: number; tone: string; locale: string }) {
  return <div className="rounded-2xl border border-border bg-surface p-5"><p className="text-sm text-text-muted">{label}</p><p className={`mt-2 font-heading text-3xl font-bold ${tone}`}>{value.toLocaleString(locale)}</p></div>
}

export default function AdminDashboard({ username, onSignOut }: { username: string; onSignOut: () => Promise<void> }) {
  const { language, t } = usePreferences()
  const locale = language === 'vi' ? 'vi-VN' : 'en-US'
  const formatDate = (value: string) => new Intl.DateTimeFormat(locale, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value))
  const filters: { value: ReportFilter; label: string }[] = [
    { value: 'pending', label: t('pending') }, { value: 'all', label: t('all') },
    { value: 'reviewed', label: t('reviewed') }, { value: 'resolved', label: t('resolved') }, { value: 'dismissed', label: t('dismissed') },
  ]
  const [tab, setTab] = useState<Tab>('overview')
  const [dashboard, setDashboard] = useState<DashboardData | null>(null)
  const [reports, setReports] = useState<ModerationReport[]>([])
  const [users, setUsers] = useState<AdminUser[]>([])
  const [reportFilter, setReportFilter] = useState<ReportFilter>('pending')
  const [userQuery, setUserQuery] = useState('')
  const [reportTotal, setReportTotal] = useState(0)
  const [userTotal, setUserTotal] = useState(0)
  const [reportOffset, setReportOffset] = useState(0)
  const [userOffset, setUserOffset] = useState(0)
  const [loading, setLoading] = useState(true)
  const [isLoadingReports, setIsLoadingReports] = useState(false)
  const [isLoadingUsers, setIsLoadingUsers] = useState(false)
  const [actionId, setActionId] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [reportsPageError, setReportsPageError] = useState<string | null>(null)
  const [usersPageError, setUsersPageError] = useState<string | null>(null)
  const [newReportsCount, setNewReportsCount] = useState(0)
  const [userStates, setUserStates] = useState<Record<string, UserModerationState>>({})
  const knownPendingReportIds = useRef<Set<string> | null>(null)

  const loadDashboard = async () => setDashboard(await adminApi.getDashboard())

  const loadReports = async (offset = 0, append = false, filter = reportFilter) => {
    const page = await adminApi.getReports(filter === 'all' ? undefined : filter, offset)
    setReports((current) => append ? [...current, ...page.items] : page.items)
    setReportTotal(page.total)
    setReportOffset(page.offset + page.items.length)
  }

  const loadUsers = async (offset = 0, append = false, query = userQuery) => {
    const page = await adminApi.getUsers(query, offset)
    setUsers((current) => append ? [...current, ...page.items] : page.items)
    setUserTotal(page.total)
    setUserOffset(page.offset + page.items.length)
  }

  const refresh = async () => {
    setLoading(true)
    setIsLoadingReports(true)
    setIsLoadingUsers(true)
    setError(null)
    try {
      await Promise.all([loadDashboard(), loadReports(), loadUsers()])
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : t('unableLoad'))
    } finally {
      setLoading(false)
      setIsLoadingReports(false)
      setIsLoadingUsers(false)
    }
  }

  useEffect(() => {
    let isActive = true

    const loadInitialData = async () => {
      try {
        const [nextDashboard, reportPage, userPage] = await Promise.all([
          adminApi.getDashboard(),
          adminApi.getReports('pending', 0),
          adminApi.getUsers('', 0),
        ])
        if (!isActive) return

        setDashboard(nextDashboard)
        setReports(reportPage.items)
        setReportTotal(reportPage.total)
        setReportOffset(reportPage.offset + reportPage.items.length)
        setUsers(userPage.items)
        setUserTotal(userPage.total)
        setUserOffset(userPage.offset + userPage.items.length)
      } catch (requestError) {
        if (isActive) setError(requestError instanceof ApiError ? requestError.message : 'Unable to load admin data.')
      } finally {
        if (isActive) setLoading(false)
      }
    }

    void loadInitialData()
    return () => { isActive = false }
  }, [])

  useEffect(() => {
    let isActive = true

    const checkForNewReports = async () => {
      try {
        const page = await adminApi.getReports('pending', 0, 100)
        if (!isActive) return

        const currentIds = new Set(page.items.map((report) => report.id))
        if (knownPendingReportIds.current) {
          const count = page.items.filter((report) => !knownPendingReportIds.current!.has(report.id)).length
          if (count > 0) setNewReportsCount((current) => current + count)
        }
        knownPendingReportIds.current = currentIds
        setDashboard((current) => current ? { ...current, pendingReports: page.total } : current)
      } catch {
        // The regular data request continues to surface connection errors to the administrator.
      }
    }

    void checkForNewReports()
    const intervalId = window.setInterval(() => {
      if (document.visibilityState === 'visible') void checkForNewReports()
    }, 30_000)

    return () => {
      isActive = false
      window.clearInterval(intervalId)
    }
  }, [])

  const changeReportFilter = (filter: ReportFilter) => {
    setReportFilter(filter)
    setIsLoadingReports(true)
    setReportsPageError(null)
    void loadReports(0, false, filter)
      .catch((requestError: unknown) => setReportsPageError(requestError instanceof ApiError ? requestError.message : t('unableReports')))
      .finally(() => setIsLoadingReports(false))
  }

  const loadMoreReports = async () => {
    setIsLoadingReports(true)
    setReportsPageError(null)
    try {
      await loadReports(reportOffset, true)
    } catch (requestError) {
      setReportsPageError(requestError instanceof ApiError ? requestError.message : t('unableReports'))
    } finally {
      setIsLoadingReports(false)
    }
  }

  const searchUsers = async (append = false) => {
    setIsLoadingUsers(true)
    setUsersPageError(null)
    try {
      await loadUsers(append ? userOffset : 0, append)
    } catch (requestError) {
      setUsersPageError(requestError instanceof ApiError ? requestError.message : t('unableSearch'))
    } finally {
      setIsLoadingUsers(false)
    }
  }

  const showPendingReports = () => {
    setNewReportsCount(0)
    setTab('reports')
    changeReportFilter('pending')
  }

  const removePost = async (report: ModerationReport) => {
    if (!window.confirm(t('removePostConfirm'))) return
    setActionId(`post-${report.id}`)
    try {
      await adminApi.removeReportedContent(report.id)
      setReports((current) => current.map((item) => item.id === report.id ? { ...item, status: 'reviewed' } : item))
      await loadDashboard()
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : t('unableRemovePost'))
    } finally {
      setActionId(null)
    }
  }

  const dismissReport = async (report: ModerationReport) => {
    if (!window.confirm(`Dismiss report ${shortId(report.id)}?`)) return
    setActionId(`dismiss-${report.id}`)
    try {
      await adminApi.dismissReport(report.id)
      setReports((current) => current.map((item) => item.id === report.id ? { ...item, status: 'dismissed' } : item))
      await loadDashboard()
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : t('unableUpdateReport'))
    } finally { setActionId(null) }
  }

  const warnReportUser = async (report: ModerationReport) => {
    if (!window.confirm('Send an account warning for this report?')) return
    setActionId(`warn-${report.id}`)
    try {
      await adminApi.warnReportedUser(report.id)
      setReports((current) => current.map((item) => item.id === report.id ? { ...item, status: 'reviewed' } : item))
      await loadDashboard()
    } catch (requestError) { setError(requestError instanceof ApiError ? requestError.message : t('unableUpdateReport')) } finally { setActionId(null) }
  }

  const suspendReportUser = async (report: ModerationReport) => {
    const value = window.prompt('Suspension duration in hours (1–8760):', '24')
    const durationHours = Number(value)
    if (!Number.isInteger(durationHours) || durationHours < 1 || durationHours > 8760 || !window.confirm('Suspend this account?')) return
    setActionId(`suspend-${report.id}`)
    try {
      await adminApi.suspendReportedUser(report.id, durationHours)
      setReports((current) => current.map((item) => item.id === report.id ? { ...item, status: 'reviewed' } : item))
      await loadDashboard()
    } catch (requestError) { setError(requestError instanceof ApiError ? requestError.message : t('unableUpdateAccount')) } finally { setActionId(null) }
  }

  const moderateUser = async (user: AdminUser, action: 'warn' | 'suspend' | 'unsuspend' | 'disable' | 'enable') => {
    if ((action === 'suspend' || action === 'disable') && !window.confirm(`${action === 'suspend' ? 'Suspend' : 'Disable'} @${user.username}?`)) return
    let durationHours = 0
    if (action === 'suspend') {
      durationHours = Number(window.prompt('Suspension duration in hours (1–8760):', '24'))
      if (!Number.isInteger(durationHours) || durationHours < 1 || durationHours > 8760) return
    }
    setActionId(`moderation-${action}-${user.id}`)
    try {
      if (action === 'warn') await adminApi.warnUser(user.id)
      if (action === 'suspend') await adminApi.suspendUser(user.id, durationHours)
      if (action === 'unsuspend') await adminApi.unsuspendUser(user.id)
      if (action === 'disable') await adminApi.disableUser(user.id)
      if (action === 'enable') await adminApi.enableUser(user.id)
      const state = await adminApi.getModerationState(user.id)
      setUserStates((current) => ({ ...current, [user.id]: state }))
    } catch (requestError) { setError(requestError instanceof ApiError ? requestError.message : t('unableUpdateAccount')) } finally { setActionId(null) }
  }

  const tabs: { value: Tab; label: string }[] = [
    { value: 'overview', label: t('overview') }, { value: 'reports', label: t('reports') }, { value: 'users', label: t('users') },
  ]

  return (
    <main className="min-h-screen bg-bg p-4 sm:p-6" style={{ animation: 'fade-in .2s ease both' }}>
      <div className="mx-auto max-w-7xl">
        <header className="mb-6 flex flex-wrap items-center justify-between gap-4">
          <div><p className="text-xs font-bold tracking-[0.18em] text-primary">FOOKBASE ADMIN</p><h1 className="mt-1 font-heading text-2xl font-bold text-text">{t('adminCenter')}</h1><p className="mt-1 text-sm text-text-muted">{t('greeting')}, @{username}</p></div>
          <div className="flex flex-wrap items-center gap-2"><PreferenceControls />{newReportsCount > 0 && <button type="button" onClick={showPendingReports} title={t('viewPendingReports')} className="relative rounded-lg border border-warning/50 bg-warning/10 px-3 py-2 text-sm font-semibold text-warning hover:bg-warning/20">🔔 <span>{newReportsCount}</span></button>}<button type="button" onClick={() => void refresh()} className="rounded-lg border border-border bg-surface px-3 py-2 text-sm font-semibold text-text hover:bg-surface-hover">↻ {t('refresh')}</button><button type="button" onClick={() => void onSignOut()} className="rounded-lg border border-border px-3 py-2 text-sm font-semibold text-text-muted hover:bg-surface">{t('signOut')}</button></div>
        </header>
        <nav className="mb-6 flex gap-1 overflow-x-auto rounded-xl border border-border bg-surface p-1">{tabs.map(({ value, label }) => <button key={value} type="button" onClick={() => setTab(value)} className={`min-w-max flex-1 rounded-lg px-4 py-2 text-sm font-semibold ${tab === value ? 'bg-primary text-white' : 'text-text-muted hover:bg-surface-2 hover:text-text'}`}>{label}{value === 'reports' && dashboard?.pendingReports ? ` (${dashboard.pendingReports})` : ''}</button>)}</nav>
        {error && <div className="mb-5 flex justify-between gap-4 rounded-xl border border-danger/40 bg-danger/10 p-4 text-sm text-danger"><span>{error}</span><button type="button" onClick={() => setError(null)}>✕</button></div>}
        {loading && !dashboard ? <p className="text-center text-sm text-text-muted">{t('adminCenter')}...</p> : <>
          {tab === 'overview' && <section><div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4"><Metric label={t('totalAccounts')} value={dashboard?.totalUsers ?? 0} tone="text-text" locale={locale} /><Metric label={t('activeAccounts')} value={dashboard?.activeUsers ?? 0} tone="text-secondary" locale={locale} /><Metric label={t('visiblePosts')} value={dashboard?.activePosts ?? 0} tone="text-warning" locale={locale} /><Metric label={t('pendingReports')} value={dashboard?.pendingReports ?? 0} tone="text-danger" locale={locale} /></div><div className="mt-6 rounded-2xl border border-border bg-surface p-6"><h2 className="font-heading text-lg font-bold">{t('moderationPriority')}</h2><p className="mt-2 text-sm text-text-muted"><strong className="text-warning">{dashboard?.pendingReports ?? 0}</strong> {t('pendingSummary')}</p><button type="button" onClick={() => setTab('reports')} className="mt-5 rounded-lg bg-primary px-4 py-2 text-sm font-bold text-white hover:bg-primary-dark">{t('openQueue')}</button></div></section>}
          {tab === 'reports' && <section className="overflow-hidden rounded-2xl border border-border bg-surface"><div className="flex flex-wrap items-center justify-between gap-3 border-b border-border p-4"><div><h2 className="font-heading text-lg font-bold">{t('reportQueue')}</h2><p className="text-sm text-text-muted">{reportTotal.toLocaleString(locale)} {t('results')}</p></div><select value={reportFilter} onChange={(event) => changeReportFilter(event.target.value as ReportFilter)} disabled={isLoadingReports} className="rounded-lg border border-border bg-surface-2 px-3 py-2 text-sm text-text outline-none disabled:opacity-60">{filters.map((filter) => <option key={filter.value} value={filter.value}>{filter.label}</option>)}</select></div>{reportsPageError && <div className="flex items-center justify-between gap-3 border-b border-danger/30 bg-danger/10 px-4 py-3 text-sm text-danger"><span>{reportsPageError}</span><button type="button" onClick={() => void loadMoreReports()} className="font-semibold underline">{t('retry')}</button></div>}<div className="divide-y divide-border">{isLoadingReports && reports.length === 0 ? <p className="p-8 text-center text-sm text-text-muted">{t('loadingReports')}</p> : reports.length === 0 ? <p className="p-8 text-center text-sm text-text-muted">{t('noReports')}</p> : reports.map((report) => <article key={report.id} className="flex flex-col justify-between gap-4 p-4 lg:flex-row"><div><div className="flex flex-wrap items-center gap-2"><span className="rounded bg-surface-2 px-2 py-0.5 text-[11px] font-bold uppercase text-text-muted">{report.targetType === 'post' ? t('post') : t('user')}</span><span className="rounded bg-warning/15 px-2 py-0.5 text-[11px] font-bold text-warning">{t(report.status)}</span><span className="text-xs text-text-light">{formatDate(report.createdAtUtc)}</span></div><p className="mt-2 text-sm font-semibold">{t('reason')}: <span className="capitalize">{report.reason}</span></p>{report.details && <p className="mt-1 text-sm text-text-muted">{report.details}</p>}<p className="mt-2 text-xs text-text-light">{t('target')}: <span title={report.targetId}>{shortId(report.targetId)}</span> · {t('reporter')}: <span title={report.reporterUserId}>{shortId(report.reporterUserId)}</span></p></div><div className="flex flex-wrap content-start gap-2">{report.status === 'pending' && <><button type="button" onClick={() => void warnReportUser(report)} disabled={actionId === `warn-${report.id}`} className="rounded-lg border border-border bg-surface-2 px-3 py-1.5 text-xs font-semibold">Warn</button><button type="button" onClick={() => void suspendReportUser(report)} disabled={actionId === `suspend-${report.id}`} className="rounded-lg bg-warning/15 px-3 py-1.5 text-xs font-semibold text-warning">Suspend</button></>}{report.targetType === 'post' && report.status === 'pending' && <button type="button" onClick={() => void removePost(report)} disabled={actionId === `post-${report.id}`} className="rounded-lg bg-danger/15 px-3 py-1.5 text-xs font-semibold text-danger">{t('removePost')}</button>}{report.status === 'pending' && <button type="button" onClick={() => void dismissReport(report)} disabled={actionId === `dismiss-${report.id}`} className="rounded-lg bg-surface-2 px-3 py-1.5 text-xs font-semibold text-text-muted">{t('dismiss')}</button>}</div></article>)}</div>{reportOffset < reportTotal && <div className="border-t border-border p-4"><button type="button" onClick={() => void loadMoreReports()} disabled={isLoadingReports} className="w-full rounded-lg border border-border bg-surface-2 py-2 text-sm font-semibold disabled:opacity-60">{isLoadingReports ? t('loadingReports') : t('loadMore')}</button></div>}</section>}
          {tab === 'users' && <section className="overflow-hidden rounded-2xl border border-border bg-surface"><div className="flex flex-wrap items-center justify-between gap-3 border-b border-border p-4"><div><h2 className="font-heading text-lg font-bold">{t('users')}</h2><p className="text-sm text-text-muted">{userTotal.toLocaleString(locale)} {t('users').toLowerCase()}</p></div><form onSubmit={(event) => { event.preventDefault(); void searchUsers() }} className="flex gap-2"><input value={userQuery} onChange={(event) => setUserQuery(event.target.value)} className="rounded-lg border border-border bg-surface-2 px-3 py-2 text-sm text-text outline-none" placeholder={t('searchAccounts')} /><button disabled={isLoadingUsers} className="rounded-lg bg-primary px-3 py-2 text-sm font-semibold text-white disabled:opacity-60">{isLoadingUsers ? t('loadingAccounts') : t('search')}</button></form></div>{usersPageError && <div className="flex items-center justify-between gap-3 border-b border-danger/30 bg-danger/10 px-4 py-3 text-sm text-danger"><span>{usersPageError}</span><button type="button" onClick={() => void searchUsers()} className="font-semibold underline">{t('retry')}</button></div>}<div className="divide-y divide-border">{isLoadingUsers && users.length === 0 ? <p className="p-8 text-center text-sm text-text-muted">{t('loadingAccounts')}</p> : users.length === 0 ? <p className="p-8 text-center text-sm text-text-muted">{t('noAccounts')}</p> : users.map((user) => { const state = userStates[user.id]; const suspendedUntil = state?.suspendedUntilUtc ?? user.suspendedUntilUtc; const disabledAt = state?.disabledAtUtc ?? user.moderationDisabledAtUtc; const warnings = state?.warningCount ?? user.warningCount; return <article key={user.id} className="flex flex-wrap items-center justify-between gap-4 p-4"><div><p className="text-sm font-bold">@{user.username}</p><p className="text-xs text-text-muted">{user.email}</p><div className="mt-1 flex flex-wrap gap-2 text-[11px]"><span className={user.isActive ? 'text-secondary' : 'text-danger'}>{user.isActive ? t('active') : t('disabled')}</span>{disabledAt && <span className="text-danger">Moderation disabled</span>}{suspendedUntil && <span className="text-warning">Suspended until {formatDate(suspendedUntil)}</span>}<span className="text-warning">{warnings} warnings</span>{user.roles.map((role) => <span key={role} className="text-primary">{role}</span>)}<span className="text-text-light">{t('joined')} {formatDate(user.createdAt)}</span></div></div>{!user.roles.includes('Admin') && <div className="flex flex-wrap gap-2"><button type="button" onClick={() => void moderateUser(user, 'warn')} className="rounded-lg border border-border bg-surface-2 px-3 py-1.5 text-xs font-semibold">Warn</button>{suspendedUntil ? <button type="button" onClick={() => void moderateUser(user, 'unsuspend')} className="rounded-lg bg-secondary/15 px-3 py-1.5 text-xs font-semibold text-secondary">Unsuspend</button> : <button type="button" onClick={() => void moderateUser(user, 'suspend')} className="rounded-lg bg-warning/15 px-3 py-1.5 text-xs font-semibold text-warning">Suspend</button>}{disabledAt ? <button type="button" onClick={() => void moderateUser(user, 'enable')} className="rounded-lg bg-secondary/15 px-3 py-1.5 text-xs font-semibold text-secondary">Enable</button> : <button type="button" onClick={() => void moderateUser(user, 'disable')} className="rounded-lg bg-danger/15 px-3 py-1.5 text-xs font-semibold text-danger">Disable</button>}</div>}</article>})}</div>{userOffset < userTotal && <div className="border-t border-border p-4"><button type="button" onClick={() => void searchUsers(true)} disabled={isLoadingUsers} className="w-full rounded-lg border border-border bg-surface-2 py-2 text-sm font-semibold disabled:opacity-60">{isLoadingUsers ? t('loadingAccounts') : t('loadMore')}</button></div>}</section>}
        </>}
      </div>
    </main>
  )
}
