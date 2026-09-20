import { lazy, Suspense, useEffect, useRef, useState } from 'react'
import { Bell, CalendarDays, ChevronRight, Flag, LayoutDashboard, LoaderCircle, LogOut, RefreshCw, ShieldCheck, Users, X } from 'lucide-react'
import { adminApi } from './api/admin'
import type { AdminDashboard as DashboardData, AdminUser, ModerationReport, ReportStatus, UserModerationState } from './api/admin'
import { ApiError } from './api/client'
import { PreferenceControls, usePreferences } from './preferences'

const DashboardOverview = lazy(() => import('./DashboardOverview'))

type Tab = 'overview' | 'reports' | 'users'
type ReportFilter = ReportStatus | 'all'

const shortId = (value: string) => `${value.slice(0, 8)}…${value.slice(-4)}`

export default function AdminDashboard({ username, onSignOut }: { username: string; onSignOut: () => Promise<void> }) {
  const { language, t } = usePreferences()
  const locale = language === 'vi' ? 'vi-VN' : 'en-US'
  const formatDate = (value: string) => new Intl.DateTimeFormat(locale, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value))
  const filters: { value: ReportFilter; label: string }[] = [
    { value: 'pending', label: t('pending') }, { value: 'all', label: t('all') },
    { value: 'reviewed', label: t('reviewed') }, { value: 'resolved', label: t('resolved') }, { value: 'dismissed', label: t('dismissed') },
  ]
  const [currentTime, setCurrentTime] = useState(() => Date.now())
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
    setCurrentTime(Date.now())
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
        setDashboard((current) => current ? { ...current, pendingReports: page.total, reportStatuses: current.reportStatuses?.map(item => item.status === 'pending' ? { ...item, count: page.total } : item) } : current)
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
      await Promise.all([loadReports(), loadDashboard()])
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : t('unableRemovePost'))
    } finally {
      setActionId(null)
    }
  }

  const dismissReport = async (report: ModerationReport) => {
    if (!window.confirm(t('dismissConfirm'))) return
    setActionId(`dismiss-${report.id}`)
    try {
      await adminApi.dismissReport(report.id)
      await Promise.all([loadReports(), loadDashboard()])
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : t('unableUpdateReport'))
    } finally { setActionId(null) }
  }

  const warnReportUser = async (report: ModerationReport) => {
    if (!window.confirm(t('warnConfirm'))) return
    setActionId(`warn-${report.id}`)
    try {
      await adminApi.warnReportedUser(report.id)
      await Promise.all([loadReports(), loadDashboard()])
    } catch (requestError) { setError(requestError instanceof ApiError ? requestError.message : t('unableUpdateReport')) } finally { setActionId(null) }
  }

  const suspendReportUser = async (report: ModerationReport) => {
    const value = window.prompt(t('suspensionDuration'), '24')
    const durationHours = Number(value)
    if (!Number.isInteger(durationHours) || durationHours < 1 || durationHours > 8760 || !window.confirm(t('suspendConfirm'))) return
    setActionId(`suspend-${report.id}`)
    try {
      await adminApi.suspendReportedUser(report.id, durationHours)
      await Promise.all([loadReports(), loadDashboard()])
    } catch (requestError) { setError(requestError instanceof ApiError ? requestError.message : t('unableUpdateAccount')) } finally { setActionId(null) }
  }

  const moderateUser = async (user: AdminUser, action: 'warn' | 'suspend' | 'unsuspend' | 'disable' | 'enable') => {
    if ((action === 'suspend' || action === 'disable') && !window.confirm(`${action === 'suspend' ? t('suspend') : t('disable')} @${user.username}?`)) return
    let durationHours = 0
    if (action === 'suspend') {
      durationHours = Number(window.prompt(t('suspensionDuration'), '24'))
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
      await Promise.all([loadUsers(), loadDashboard()])
    } catch (requestError) { setError(requestError instanceof ApiError ? requestError.message : t('unableUpdateAccount')) } finally { setActionId(null) }
  }

  const tabs = [
    { value: 'overview' as const, label: t('overview'), icon: LayoutDashboard },
    { value: 'reports' as const, label: t('reports'), icon: Flag },
    { value: 'users' as const, label: t('users'), icon: Users },
  ]
  const description = tab === 'overview' ? 'overviewDescription' : tab === 'reports' ? 'reportsDescription' : 'usersDescription'
  const initial = username.slice(0, 1)

  return (
    <div className="admin-shell">
      <aside className="admin-sidebar">
        <div className="brand"><span className="brand-mark">f</span><div><div className="brand-name">fookbase<span>.</span></div><p className="brand-caption">Admin center</p></div></div>
        <p className="nav-label">{t('workspace')}</p>
        <nav className="sidebar-nav" aria-label={t('mainNavigation')}>{tabs.map(({ value, label, icon: Icon }) => <button key={value} type="button" onClick={() => setTab(value)} aria-current={tab === value ? 'page' : undefined}><Icon size={18} /><span>{label}</span>{value === 'reports' && !!dashboard?.pendingReports && <span className="nav-count">{dashboard.pendingReports.toLocaleString(locale)}</span>}</button>)}</nav>
        <div className="sidebar-note"><ShieldCheck size={23} /><strong>{t('adminWorkspace')}</strong><p>{t('protectedWorkspace')}</p></div>
        <div className="sidebar-profile"><span className="avatar">{initial}</span><div className="profile-text"><strong title={username}>{username}</strong><span>{t('administrator')}</span></div><button type="button" onClick={() => void onSignOut()} className="icon-button" aria-label={t('signOut')} title={t('signOut')}><LogOut size={17} /></button></div>
      </aside>
      <div className="admin-main">
        <header className="admin-topbar">
          <div className="breadcrumb"><span>{t('administration')}</span><ChevronRight size={13} /><strong>{t(tab)}</strong></div>
          <div className="brand mobile-brand"><span className="brand-mark">f</span><span className="brand-name">fookbase<span>.</span></span></div>
          <div className="topbar-actions"><PreferenceControls /><span className="topbar-divider" /><button type="button" onClick={showPendingReports} title={t('viewPendingReports')} aria-label={`${t('viewPendingReports')}${newReportsCount ? ` · ${newReportsCount} ${t('newReports')}` : ''}`} className="icon-button"><Bell size={18} />{newReportsCount > 0 && <span className="notification-dot">{newReportsCount}</span>}</button><span className="avatar" title={username}>{initial}</span><button type="button" onClick={() => void onSignOut()} className="icon-button mobile-signout" aria-label={t('signOut')}><LogOut size={17} /></button></div>
        </header>
        <main className="admin-content" id="main-content">
          <div className="page-heading"><div><div className="page-eyebrow">{t('greeting')}, {username}</div><h1>{t(tab)}</h1><p>{t(description)}</p></div><div className="heading-actions"><span className="date-chip"><CalendarDays size={14} />{new Intl.DateTimeFormat(locale, { day: 'numeric', month: 'long', year: 'numeric' }).format(new Date())}</span><button type="button" onClick={() => void refresh()} disabled={loading} className="refresh-button"><RefreshCw size={14} className={loading ? 'spin' : ''} />{t('refresh')}</button></div></div>
          {error && <div role="alert" className="mb-5 flex items-center justify-between gap-4 rounded-xl border border-danger/30 bg-danger/10 p-4 text-sm text-danger"><span>{error}</span><button type="button" aria-label={t('close')} onClick={() => setError(null)}><X size={16} /></button></div>}
          {loading && !dashboard ? <div className="workspace-state" role="status"><LoaderCircle size={28} className="spin" />{t('loadingDashboard')}</div> : !dashboard ? <div className="workspace-state"><ShieldCheck size={30} /><p>{t('unableLoad')}</p><button className="refresh-button" type="button" onClick={() => void refresh()}>{t('retry')}</button></div> : <>
          {tab === 'overview' && <Suspense fallback={<div className="workspace-state" role="status">{t('loadingDashboard')}</div>}><DashboardOverview data={dashboard} onOpenReports={showPendingReports} /></Suspense>}
          {tab === 'reports' && <section className="management-panel overflow-hidden rounded-xl border border-border bg-surface"><div className="flex flex-wrap items-center justify-between gap-3 border-b border-border p-4"><div><h2 className="font-heading text-lg font-bold">{t('reportQueue')}</h2><p className="text-sm text-text-muted">{reportTotal.toLocaleString(locale)} {t('results')}</p></div><select aria-label={t('reportQueue')} value={reportFilter} onChange={(event) => changeReportFilter(event.target.value as ReportFilter)} disabled={isLoadingReports} className="rounded-lg border border-border bg-surface-2 px-3 py-2 text-sm text-text outline-none disabled:opacity-60">{filters.map((filter) => <option key={filter.value} value={filter.value}>{filter.label}</option>)}</select></div>{reportsPageError && <div className="flex items-center justify-between gap-3 border-b border-danger/30 bg-danger/10 px-4 py-3 text-sm text-danger"><span>{reportsPageError}</span><button type="button" onClick={() => changeReportFilter(reportFilter)} className="font-semibold underline">{t('retry')}</button></div>}<div className="divide-y divide-border">{isLoadingReports && reports.length === 0 ? <p className="p-8 text-center text-sm text-text-muted">{t('loadingReports')}</p> : reports.length === 0 ? <p className="p-8 text-center text-sm text-text-muted">{t('noReports')}</p> : reports.map((report) => <article key={report.id} className="flex flex-col justify-between gap-4 p-4 lg:flex-row"><div><div className="flex flex-wrap items-center gap-2"><span className="rounded bg-surface-2 px-2 py-0.5 text-[11px] font-bold uppercase text-text-muted">{report.targetType === 'post' ? t('post') : t('user')}</span><span className="rounded bg-warning/15 px-2 py-0.5 text-[11px] font-bold text-warning">{t(report.status)}</span><span className="text-xs text-text-light">{formatDate(report.createdAtUtc)}</span></div><p className="mt-2 text-sm font-semibold">{t('reason')}: <span className="capitalize">{report.reason}</span></p>{report.details && <p className="mt-1 text-sm text-text-muted">{report.details}</p>}<p className="mt-2 text-xs text-text-light">{t('target')}: <span title={report.targetId}>{shortId(report.targetId)}</span> · {t('reporter')}: <span title={report.reporterUserId}>{shortId(report.reporterUserId)}</span></p></div><div className="flex flex-wrap content-start gap-2">{report.status === 'pending' && <><button type="button" onClick={() => void warnReportUser(report)} disabled={actionId !== null} className="rounded-lg border border-border bg-surface-2 px-3 py-1.5 text-xs font-semibold">{t('warn')}</button><button type="button" onClick={() => void suspendReportUser(report)} disabled={actionId !== null} className="rounded-lg bg-warning/15 px-3 py-1.5 text-xs font-semibold text-warning">{t('suspend')}</button></>}{report.targetType === 'post' && report.status === 'pending' && <button type="button" onClick={() => void removePost(report)} disabled={actionId !== null} className="rounded-lg bg-danger/15 px-3 py-1.5 text-xs font-semibold text-danger">{t('removePost')}</button>}{report.status === 'pending' && <button type="button" onClick={() => void dismissReport(report)} disabled={actionId !== null} className="rounded-lg bg-surface-2 px-3 py-1.5 text-xs font-semibold text-text-muted">{t('dismiss')}</button>}</div></article>)}</div>{reportOffset < reportTotal && <div className="border-t border-border p-4"><button type="button" onClick={() => void loadMoreReports()} disabled={isLoadingReports} className="w-full rounded-lg border border-border bg-surface-2 py-2 text-sm font-semibold disabled:opacity-60">{isLoadingReports ? t('loadingReports') : t('loadMore')}</button></div>}</section>}
          {tab === 'users' && <section className="management-panel overflow-hidden rounded-xl border border-border bg-surface"><div className="flex flex-wrap items-center justify-between gap-3 border-b border-border p-4"><div><h2 className="font-heading text-lg font-bold">{t('users')}</h2><p className="text-sm text-text-muted">{userTotal.toLocaleString(locale)} {t('users').toLowerCase()}</p></div><form onSubmit={(event) => { event.preventDefault(); void searchUsers() }} className="flex gap-2"><input aria-label={t('searchAccounts')} value={userQuery} onChange={(event) => setUserQuery(event.target.value)} className="rounded-lg border border-border bg-surface-2 px-3 py-2 text-sm text-text outline-none" placeholder={t('searchAccounts')} /><button disabled={isLoadingUsers} className="rounded-lg bg-primary px-3 py-2 text-sm font-semibold text-white disabled:opacity-60">{isLoadingUsers ? t('loadingAccounts') : t('search')}</button></form></div>{usersPageError && <div className="flex items-center justify-between gap-3 border-b border-danger/30 bg-danger/10 px-4 py-3 text-sm text-danger"><span>{usersPageError}</span><button type="button" onClick={() => void searchUsers()} className="font-semibold underline">{t('retry')}</button></div>}<div className="divide-y divide-border">{isLoadingUsers && users.length === 0 ? <p className="p-8 text-center text-sm text-text-muted">{t('loadingAccounts')}</p> : users.length === 0 ? <p className="p-8 text-center text-sm text-text-muted">{t('noAccounts')}</p> : users.map((user) => { const state = userStates[user.id]; const suspension = state ? state.suspendedUntilUtc : user.suspendedUntilUtc; const suspendedUntil = suspension && new Date(suspension).getTime() > currentTime ? suspension : null; const disabledAt = state ? state.disabledAtUtc : user.moderationDisabledAtUtc; const warnings = state?.warningCount ?? user.warningCount; return <article key={user.id} className="flex flex-wrap items-center justify-between gap-4 p-4"><div><p className="text-sm font-bold">@{user.username}</p><p className="text-xs text-text-muted">{user.email}</p><div className="mt-1 flex flex-wrap gap-2 text-[11px]"><span className={user.isActive ? 'text-secondary' : 'text-danger'}>{user.isActive ? t('active') : t('disabled')}</span>{disabledAt && <span className="text-danger">{t('moderationDisabled')}</span>}{suspendedUntil && <span className="text-warning">{t('suspendedUntil')} {formatDate(suspendedUntil)}</span>}<span className="text-warning">{warnings} {t('warnings')}</span>{user.roles.map((role) => <span key={role} className="text-primary">{role}</span>)}<span className="text-text-light">{t('joined')} {formatDate(user.createdAt)}</span></div></div>{!user.roles.includes('Admin') && <div className="flex flex-wrap gap-2"><button type="button" disabled={actionId !== null} onClick={() => void moderateUser(user, 'warn')} className="rounded-lg border border-border bg-surface-2 px-3 py-1.5 text-xs font-semibold">{t('warn')}</button>{suspendedUntil ? <button type="button" disabled={actionId !== null} onClick={() => void moderateUser(user, 'unsuspend')} className="rounded-lg bg-secondary/15 px-3 py-1.5 text-xs font-semibold text-secondary">{t('unsuspend')}</button> : <button type="button" disabled={actionId !== null} onClick={() => void moderateUser(user, 'suspend')} className="rounded-lg bg-warning/15 px-3 py-1.5 text-xs font-semibold text-warning">{t('suspend')}</button>}{disabledAt ? <button type="button" disabled={actionId !== null} onClick={() => void moderateUser(user, 'enable')} className="rounded-lg bg-secondary/15 px-3 py-1.5 text-xs font-semibold text-secondary">{t('enable')}</button> : <button type="button" disabled={actionId !== null} onClick={() => void moderateUser(user, 'disable')} className="rounded-lg bg-danger/15 px-3 py-1.5 text-xs font-semibold text-danger">{t('disable')}</button>}</div>}</article>})}</div>{userOffset < userTotal && <div className="border-t border-border p-4"><button type="button" onClick={() => void searchUsers(true)} disabled={isLoadingUsers} className="w-full rounded-lg border border-border bg-surface-2 py-2 text-sm font-semibold disabled:opacity-60">{isLoadingUsers ? t('loadingAccounts') : t('loadMore')}</button></div>}</section>}
        </>}
          <footer className="admin-footer"><span>© {new Date().getFullYear()} Fookbase</span><span>{t('workspaceDescription')}</span></footer>
        </main>
      </div>
    </div>
  )
}
