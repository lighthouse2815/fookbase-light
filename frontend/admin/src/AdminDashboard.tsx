import { useEffect, useState } from 'react'
import { adminApi } from './api/admin'
import type { AdminDashboard, AdminUser, ModerationReport, ReportStatus } from './api/admin'
import { ApiError } from './api/client'

type Tab = 'overview' | 'reports' | 'users'
type ReportFilter = ReportStatus | 'all'

const filters: { value: ReportFilter; label: string }[] = [
  { value: 'pending', label: 'Chờ xử lý' }, { value: 'all', label: 'Tất cả' },
  { value: 'reviewed', label: 'Đã xem' }, { value: 'resolved', label: 'Đã xử lý' }, { value: 'dismissed', label: 'Đã bỏ qua' },
]

const formatDate = (value: string) => new Intl.DateTimeFormat('vi-VN', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value))
const shortId = (value: string) => `${value.slice(0, 8)}…${value.slice(-4)}`
const statusText = (status: ReportStatus) => ({ pending: 'Chờ xử lý', reviewed: 'Đã xem', resolved: 'Đã xử lý', dismissed: 'Đã bỏ qua' })[status]

function Metric({ label, value, tone }: { label: string; value: number; tone: string }) {
  return <div className="rounded-2xl border border-border bg-surface p-5"><p className="text-sm text-text-muted">{label}</p><p className={`mt-2 font-heading text-3xl font-bold ${tone}`}>{value.toLocaleString('vi-VN')}</p></div>
}

export default function AdminDashboard({ username, onSignOut }: { username: string; onSignOut: () => Promise<void> }) {
  const [tab, setTab] = useState<Tab>('overview')
  const [dashboard, setDashboard] = useState<AdminDashboard | null>(null)
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

  const loadDashboard = async () => {
    setDashboard(await adminApi.getDashboard())
  }

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
      setError(requestError instanceof ApiError ? requestError.message : 'Không thể tải dữ liệu quản trị.')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    void Promise.resolve().then(refresh)
  }, [])

  const changeReportFilter = (filter: ReportFilter) => {
    setReportFilter(filter)
    setLoading(true)
    setError(null)
    void loadReports(0, false, filter)
      .catch((requestError: unknown) => setError(requestError instanceof ApiError ? requestError.message : 'Không thể tải báo cáo.'))
      .finally(() => setLoading(false))
  }

  const updateReport = async (report: ModerationReport, status: Exclude<ReportStatus, 'pending'>) => {
    setActionId(report.id)
    try {
      const updated = await adminApi.updateReportStatus(report.id, status)
      setReports((current) => current.map((item) => item.id === report.id ? updated : item))
      await loadDashboard()
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : 'Không thể cập nhật báo cáo.')
    } finally {
      setActionId(null)
    }
  }

  const removePost = async (report: ModerationReport) => {
    if (!window.confirm('Gỡ bài viết này? Thao tác không thể hoàn tác.')) return
    setActionId(`post-${report.id}`)
    try {
      await adminApi.deletePost(report.targetId)
      await loadDashboard()
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : 'Không thể gỡ bài viết.')
    } finally {
      setActionId(null)
    }
  }

  const updateUser = async (user: AdminUser) => {
    if (!window.confirm(`${user.isActive ? 'Vô hiệu hóa' : 'Kích hoạt'} @${user.username}?`)) return
    setActionId(`user-${user.id}`)
    try {
      const updated = await adminApi.updateUserStatus(user.id, !user.isActive)
      setUsers((current) => current.map((item) => item.id === user.id ? updated : item))
      await loadDashboard()
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : 'Không thể cập nhật tài khoản.')
    } finally {
      setActionId(null)
    }
  }

  return (
    <main className="min-h-screen bg-bg p-4 sm:p-6" style={{ animation: 'fade-in .2s ease both' }}>
      <div className="mx-auto max-w-7xl">
        <header className="mb-6 flex flex-wrap items-center justify-between gap-4"><div><p className="text-xs font-bold tracking-[0.18em] text-primary">FOOKBASE ADMIN</p><h1 className="mt-1 font-heading text-2xl font-bold text-text">Admin Center</h1><p className="mt-1 text-sm text-text-muted">Xin chào, @{username}</p></div><div className="flex gap-2"><button type="button" onClick={() => void refresh()} className="rounded-lg border border-border bg-surface px-3 py-2 text-sm font-semibold text-text hover:bg-surface-hover">↻ Làm mới</button><button type="button" onClick={() => void onSignOut()} className="rounded-lg border border-border px-3 py-2 text-sm font-semibold text-text-muted hover:bg-surface">Đăng xuất</button></div></header>
        <nav className="mb-6 flex gap-1 overflow-x-auto rounded-xl border border-border bg-surface p-1">{([['overview', 'Tổng quan'], ['reports', 'Báo cáo'], ['users', 'Tài khoản']] as const).map(([value, label]) => <button key={value} type="button" onClick={() => setTab(value)} className={`min-w-max flex-1 rounded-lg px-4 py-2 text-sm font-semibold ${tab === value ? 'bg-primary text-white' : 'text-text-muted hover:bg-surface-2 hover:text-text'}`}>{label}{value === 'reports' && dashboard?.pendingReports ? ` (${dashboard.pendingReports})` : ''}</button>)}</nav>
        {error && <div className="mb-5 flex justify-between gap-4 rounded-xl border border-danger/40 bg-danger/10 p-4 text-sm text-danger"><span>{error}</span><button type="button" onClick={() => setError(null)}>✕</button></div>}
        {loading && !dashboard ? <p className="text-center text-sm text-text-muted">Đang tải Admin Center...</p> : <>
          {tab === 'overview' && <section><div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4"><Metric label="Tổng tài khoản" value={dashboard?.totalUsers ?? 0} tone="text-text" /><Metric label="Đang hoạt động" value={dashboard?.activeUsers ?? 0} tone="text-secondary" /><Metric label="Bài viết hiển thị" value={dashboard?.activePosts ?? 0} tone="text-warning" /><Metric label="Report chờ xử lý" value={dashboard?.pendingReports ?? 0} tone="text-danger" /></div><div className="mt-6 rounded-2xl border border-border bg-surface p-6"><h2 className="font-heading text-lg font-bold">Ưu tiên moderation</h2><p className="mt-2 text-sm text-text-muted">Có <strong className="text-warning">{dashboard?.pendingReports ?? 0}</strong> báo cáo đang chờ xử lý. Chuyển tới tab Báo cáo để xem chi tiết và đưa ra quyết định.</p><button type="button" onClick={() => setTab('reports')} className="mt-5 rounded-lg bg-primary px-4 py-2 text-sm font-bold text-white hover:bg-primary-dark">Mở hàng đợi</button></div></section>}
          {tab === 'reports' && <section className="overflow-hidden rounded-2xl border border-border bg-surface"><div className="flex flex-wrap items-center justify-between gap-3 border-b border-border p-4"><div><h2 className="font-heading text-lg font-bold">Hàng đợi báo cáo</h2><p className="text-sm text-text-muted">{reportTotal.toLocaleString('vi-VN')} kết quả</p></div><select value={reportFilter} onChange={(event) => changeReportFilter(event.target.value as ReportFilter)} className="rounded-lg border border-border bg-surface-2 px-3 py-2 text-sm text-text outline-none"><>{filters.map((filter) => <option key={filter.value} value={filter.value}>{filter.label}</option>)}</></select></div><div className="divide-y divide-border">{reports.length === 0 ? <p className="p-8 text-center text-sm text-text-muted">Không có báo cáo nào.</p> : reports.map((report) => <article key={report.id} className="flex flex-col justify-between gap-4 p-4 lg:flex-row"><div><div className="flex flex-wrap items-center gap-2"><span className="rounded bg-surface-2 px-2 py-0.5 text-[11px] font-bold uppercase text-text-muted">{report.targetType === 'post' ? 'Bài viết' : 'Người dùng'}</span><span className="rounded bg-warning/15 px-2 py-0.5 text-[11px] font-bold text-warning">{statusText(report.status)}</span><span className="text-xs text-text-light">{formatDate(report.createdAtUtc)}</span></div><p className="mt-2 text-sm font-semibold">Lý do: <span className="capitalize">{report.reason}</span></p>{report.details && <p className="mt-1 text-sm text-text-muted">{report.details}</p>}<p className="mt-2 text-xs text-text-light">Mục tiêu: <span title={report.targetId}>{shortId(report.targetId)}</span> · Người báo cáo: <span title={report.reporterUserId}>{shortId(report.reporterUserId)}</span></p></div><div className="flex flex-wrap content-start gap-2">{report.status === 'pending' && <button type="button" onClick={() => void updateReport(report, 'reviewed')} disabled={actionId === report.id} className="rounded-lg border border-border bg-surface-2 px-3 py-1.5 text-xs font-semibold">Đã xem</button>}{report.targetType === 'post' && <button type="button" onClick={() => void removePost(report)} disabled={actionId === `post-${report.id}`} className="rounded-lg bg-danger/15 px-3 py-1.5 text-xs font-semibold text-danger">Gỡ bài</button>}{report.status !== 'dismissed' && <button type="button" onClick={() => void updateReport(report, 'dismissed')} disabled={actionId === report.id} className="rounded-lg bg-surface-2 px-3 py-1.5 text-xs font-semibold text-text-muted">Bỏ qua</button>}{report.status !== 'resolved' && <button type="button" onClick={() => void updateReport(report, 'resolved')} disabled={actionId === report.id} className="rounded-lg bg-primary px-3 py-1.5 text-xs font-semibold text-white">Đã xử lý</button>}</div></article>)}</div>{reportOffset < reportTotal && <div className="border-t border-border p-4"><button type="button" onClick={() => void loadReports(reportOffset, true).catch(() => setError('Không thể tải thêm báo cáo.'))} className="w-full rounded-lg border border-border bg-surface-2 py-2 text-sm font-semibold">Tải thêm</button></div>}</section>}
          {tab === 'users' && <section className="overflow-hidden rounded-2xl border border-border bg-surface"><div className="flex flex-wrap items-center justify-between gap-3 border-b border-border p-4"><div><h2 className="font-heading text-lg font-bold">Tài khoản</h2><p className="text-sm text-text-muted">{userTotal.toLocaleString('vi-VN')} tài khoản</p></div><form onSubmit={(event) => { event.preventDefault(); void loadUsers().catch(() => setError('Không thể tìm tài khoản.')) }} className="flex gap-2"><input value={userQuery} onChange={(event) => setUserQuery(event.target.value)} className="rounded-lg border border-border bg-surface-2 px-3 py-2 text-sm text-text outline-none" placeholder="Email hoặc username" /><button className="rounded-lg bg-primary px-3 py-2 text-sm font-semibold text-white">Tìm</button></form></div><div className="divide-y divide-border">{users.length === 0 ? <p className="p-8 text-center text-sm text-text-muted">Không tìm thấy tài khoản.</p> : users.map((user) => <article key={user.id} className="flex flex-wrap items-center justify-between gap-4 p-4"><div><p className="text-sm font-bold">@{user.username}</p><p className="text-xs text-text-muted">{user.email}</p><div className="mt-1 flex flex-wrap gap-2 text-[11px]"><span className={user.isActive ? 'text-secondary' : 'text-danger'}>{user.isActive ? 'Đang hoạt động' : 'Đã vô hiệu hóa'}</span>{user.roles.map((role) => <span key={role} className="text-primary">{role}</span>)}<span className="text-text-light">Tham gia {formatDate(user.createdAt)}</span></div></div>{!user.roles.includes('Admin') && <button type="button" onClick={() => void updateUser(user)} disabled={actionId === `user-${user.id}`} className={`rounded-lg px-3 py-1.5 text-xs font-semibold ${user.isActive ? 'bg-danger/15 text-danger' : 'bg-secondary/15 text-secondary'}`}>{user.isActive ? 'Vô hiệu hóa' : 'Kích hoạt'}</button>}</article>)}</div>{userOffset < userTotal && <div className="border-t border-border p-4"><button type="button" onClick={() => void loadUsers(userOffset, true).catch(() => setError('Không thể tải thêm tài khoản.'))} className="w-full rounded-lg border border-border bg-surface-2 py-2 text-sm font-semibold">Tải thêm</button></div>}</section>}
        </>}
      </div>
    </main>
  )
}
