import { useEffect, useState } from 'react'
import { adminApi } from './api/admin'
import type { AdminDashboard as DashboardData, AdminUser, ModerationReport, ReportStatus } from './api/admin'
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
  const [actionId, setActionId] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

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
    setError(null)
    try {
      await Promise.all([loadDashboard(), loadReports(), loadUsers()])
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : t('unableLoad'))
    } finally {
      setLoading(false)
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

  const changeReportFilter = (filter: ReportFilter) => {
    setReportFilter(filter)
    setLoading(true)
    setError(null)
    void loadReports(0, false, filter)
      .catch((requestError: unknown) => setError(requestError instanceof ApiError ? requestError.message : t('unableReports')))
      .finally(() => setLoading(false))
  }

  const updateReport = async (report: ModerationReport, status: Exclude<ReportStatus, 'pending'>) => {
    setActionId(report.id)
    try {
      const updated = await adminApi.updateReportStatus(report.id, status)
      setReports((current) => current.map((item) => item.id === report.id ? updated : item))
      await loadDashboard()
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : t('unableUpdateReport'))
    } finally {
      setActionId(null)
    }
  }

  const removePost = async (report: ModerationReport) => {
    if (!window.confirm(t('removePostConfirm'))) return
    setActionId(`post-${report.id}`)
    try {
      await adminApi.deletePost(report.targetId)
      await loadDashboard()
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : t('unableRemovePost'))
    } finally {
      setActionId(null)
    }
  }

  const updateUser = async (user: AdminUser) => {
    if (!window.confirm(`${user.isActive ? t('disable') : t('enable')} @${user.username} ${t('accountConfirm')}`)) return
    setActionId(`user-${user.id}`)
    try {
      const updated = await adminApi.updateUserStatus(user.id, !user.isActive)
      setUsers((current) => current.map((item) => item.id === user.id ? updated : item))
      await loadDashboard()
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : t('unableUpdateAccount'))
    } finally {
      setActionId(null)
    }
  }

  const tabs: { value: Tab; label: string }[] = [
    { value: 'overview', label: t('overview') }, { value: 'reports', label: t('reports') }, { value: 'users', label: t('users') },
  ]

  return (
    <main className="min-h-screen bg-bg p-4 sm:p-6" style={{ animation: 'fade-in .2s ease both' }}>
      <div className="mx-auto max-w-7xl">
        <header className="mb-6 flex flex-wrap items-center justify-between gap-4">
          <div><p className="text-xs font-bold tracking-[0.18em] text-primary">FOOKBASE ADMIN</p><h1 className="mt-1 font-heading text-2xl font-bold text-text">{t('adminCenter')}</h1><p className="mt-1 text-sm text-text-muted">{t('greeting')}, @{username}</p></div>
          <div className="flex flex-wrap items-center gap-2"><PreferenceControls /><button type="button" onClick={() => void refresh()} className="rounded-lg border border-border bg-surface px-3 py-2 text-sm font-semibold text-text hover:bg-surface-hover">↻ {t('refresh')}</button><button type="button" onClick={() => void onSignOut()} className="rounded-lg border border-border px-3 py-2 text-sm font-semibold text-text-muted hover:bg-surface">{t('signOut')}</button></div>
        </header>
        <nav className="mb-6 flex gap-1 overflow-x-auto rounded-xl border border-border bg-surface p-1">{tabs.map(({ value, label }) => <button key={value} type="button" onClick={() => setTab(value)} className={`min-w-max flex-1 rounded-lg px-4 py-2 text-sm font-semibold ${tab === value ? 'bg-primary text-white' : 'text-text-muted hover:bg-surface-2 hover:text-text'}`}>{label}{value === 'reports' && dashboard?.pendingReports ? ` (${dashboard.pendingReports})` : ''}</button>)}</nav>
        {error && <div className="mb-5 flex justify-between gap-4 rounded-xl border border-danger/40 bg-danger/10 p-4 text-sm text-danger"><span>{error}</span><button type="button" onClick={() => setError(null)}>✕</button></div>}
        {loading && !dashboard ? <p className="text-center text-sm text-text-muted">{t('adminCenter')}...</p> : <>
          {tab === 'overview' && <section><div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4"><Metric label={t('totalAccounts')} value={dashboard?.totalUsers ?? 0} tone="text-text" locale={locale} /><Metric label={t('activeAccounts')} value={dashboard?.activeUsers ?? 0} tone="text-secondary" locale={locale} /><Metric label={t('visiblePosts')} value={dashboard?.activePosts ?? 0} tone="text-warning" locale={locale} /><Metric label={t('pendingReports')} value={dashboard?.pendingReports ?? 0} tone="text-danger" locale={locale} /></div><div className="mt-6 rounded-2xl border border-border bg-surface p-6"><h2 className="font-heading text-lg font-bold">{t('moderationPriority')}</h2><p className="mt-2 text-sm text-text-muted"><strong className="text-warning">{dashboard?.pendingReports ?? 0}</strong> {t('pendingSummary')}</p><button type="button" onClick={() => setTab('reports')} className="mt-5 rounded-lg bg-primary px-4 py-2 text-sm font-bold text-white hover:bg-primary-dark">{t('openQueue')}</button></div></section>}
          {tab === 'reports' && <section className="overflow-hidden rounded-2xl border border-border bg-surface"><div className="flex flex-wrap items-center justify-between gap-3 border-b border-border p-4"><div><h2 className="font-heading text-lg font-bold">{t('reportQueue')}</h2><p className="text-sm text-text-muted">{reportTotal.toLocaleString(locale)} {t('results')}</p></div><select value={reportFilter} onChange={(event) => changeReportFilter(event.target.value as ReportFilter)} className="rounded-lg border border-border bg-surface-2 px-3 py-2 text-sm text-text outline-none">{filters.map((filter) => <option key={filter.value} value={filter.value}>{filter.label}</option>)}</select></div><div className="divide-y divide-border">{reports.length === 0 ? <p className="p-8 text-center text-sm text-text-muted">{t('noReports')}</p> : reports.map((report) => <article key={report.id} className="flex flex-col justify-between gap-4 p-4 lg:flex-row"><div><div className="flex flex-wrap items-center gap-2"><span className="rounded bg-surface-2 px-2 py-0.5 text-[11px] font-bold uppercase text-text-muted">{report.targetType === 'post' ? t('post') : t('user')}</span><span className="rounded bg-warning/15 px-2 py-0.5 text-[11px] font-bold text-warning">{t(report.status)}</span><span className="text-xs text-text-light">{formatDate(report.createdAtUtc)}</span></div><p className="mt-2 text-sm font-semibold">{t('reason')}: <span className="capitalize">{report.reason}</span></p>{report.details && <p className="mt-1 text-sm text-text-muted">{report.details}</p>}<p className="mt-2 text-xs text-text-light">{t('target')}: <span title={report.targetId}>{shortId(report.targetId)}</span> · {t('reporter')}: <span title={report.reporterUserId}>{shortId(report.reporterUserId)}</span></p></div><div className="flex flex-wrap content-start gap-2">{report.status === 'pending' && <button type="button" onClick={() => void updateReport(report, 'reviewed')} disabled={actionId === report.id} className="rounded-lg border border-border bg-surface-2 px-3 py-1.5 text-xs font-semibold">{t('markReviewed')}</button>}{report.targetType === 'post' && <button type="button" onClick={() => void removePost(report)} disabled={actionId === `post-${report.id}`} className="rounded-lg bg-danger/15 px-3 py-1.5 text-xs font-semibold text-danger">{t('removePost')}</button>}{report.status !== 'dismissed' && <button type="button" onClick={() => void updateReport(report, 'dismissed')} disabled={actionId === report.id} className="rounded-lg bg-surface-2 px-3 py-1.5 text-xs font-semibold text-text-muted">{t('dismiss')}</button>}{report.status !== 'resolved' && <button type="button" onClick={() => void updateReport(report, 'resolved')} disabled={actionId === report.id} className="rounded-lg bg-primary px-3 py-1.5 text-xs font-semibold text-white">{t('resolve')}</button>}</div></article>)}</div>{reportOffset < reportTotal && <div className="border-t border-border p-4"><button type="button" onClick={() => void loadReports(reportOffset, true).catch(() => setError(t('unableReports')))} className="w-full rounded-lg border border-border bg-surface-2 py-2 text-sm font-semibold">{t('loadMore')}</button></div>}</section>}
          {tab === 'users' && <section className="overflow-hidden rounded-2xl border border-border bg-surface"><div className="flex flex-wrap items-center justify-between gap-3 border-b border-border p-4"><div><h2 className="font-heading text-lg font-bold">{t('users')}</h2><p className="text-sm text-text-muted">{userTotal.toLocaleString(locale)} {t('users').toLowerCase()}</p></div><form onSubmit={(event) => { event.preventDefault(); void loadUsers().catch(() => setError(t('unableSearch'))) }} className="flex gap-2"><input value={userQuery} onChange={(event) => setUserQuery(event.target.value)} className="rounded-lg border border-border bg-surface-2 px-3 py-2 text-sm text-text outline-none" placeholder={t('searchAccounts')} /><button className="rounded-lg bg-primary px-3 py-2 text-sm font-semibold text-white">{t('search')}</button></form></div><div className="divide-y divide-border">{users.length === 0 ? <p className="p-8 text-center text-sm text-text-muted">{t('noAccounts')}</p> : users.map((user) => <article key={user.id} className="flex flex-wrap items-center justify-between gap-4 p-4"><div><p className="text-sm font-bold">@{user.username}</p><p className="text-xs text-text-muted">{user.email}</p><div className="mt-1 flex flex-wrap gap-2 text-[11px]"><span className={user.isActive ? 'text-secondary' : 'text-danger'}>{user.isActive ? t('active') : t('disabled')}</span>{user.roles.map((role) => <span key={role} className="text-primary">{role}</span>)}<span className="text-text-light">{t('joined')} {formatDate(user.createdAt)}</span></div></div>{!user.roles.includes('Admin') && <button type="button" onClick={() => void updateUser(user)} disabled={actionId === `user-${user.id}`} className={`rounded-lg px-3 py-1.5 text-xs font-semibold ${user.isActive ? 'bg-danger/15 text-danger' : 'bg-secondary/15 text-secondary'}`}>{user.isActive ? t('disable') : t('enable')}</button>}</article>)}</div>{userOffset < userTotal && <div className="border-t border-border p-4"><button type="button" onClick={() => void loadUsers(userOffset, true).catch(() => setError(t('unableSearch')))} className="w-full rounded-lg border border-border bg-surface-2 py-2 text-sm font-semibold">{t('loadMore')}</button></div>}</section>}
        </>}
      </div>
    </main>
  )
}
