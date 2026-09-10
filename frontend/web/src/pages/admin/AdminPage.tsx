import { useEffect, useState } from 'react'
import { ApiError } from '../../api/client'
import { adminApi } from '../../api/admin'
import type { AdminDashboard, AdminUser, ModerationReport, ReportStatus } from '../../api/admin'
import { useAuth } from '../../auth/useAuth'

type AdminTab = 'overview' | 'reports' | 'users'
type ReportFilter = ReportStatus | 'all'

const reportFilters: { value: ReportFilter; label: string }[] = [
  { value: 'pending', label: 'Chờ xử lý' },
  { value: 'all', label: 'Tất cả' },
  { value: 'reviewed', label: 'Đã xem' },
  { value: 'resolved', label: 'Đã xử lý' },
  { value: 'dismissed', label: 'Đã bỏ qua' },
]

function formatDate(value: string) {
  return new Intl.DateTimeFormat('vi-VN', {
    dateStyle: 'medium',
    timeStyle: 'short',
  }).format(new Date(value))
}

function shortId(value: string) {
  return `${value.slice(0, 8)}…${value.slice(-4)}`
}

function statusLabel(status: ReportStatus) {
  return {
    pending: 'Chờ xử lý',
    reviewed: 'Đã xem',
    resolved: 'Đã xử lý',
    dismissed: 'Đã bỏ qua',
  }[status]
}

function statusClass(status: ReportStatus) {
  return {
    pending: 'bg-warning/15 text-warning',
    reviewed: 'bg-primary/15 text-primary-light',
    resolved: 'bg-secondary/15 text-secondary',
    dismissed: 'bg-surface-2 text-text-muted',
  }[status]
}

function StatCard({ label, value, icon, tone }: { label: string; value: number; icon: string; tone: string }) {
  return (
    <div className="rounded-2xl border border-border bg-surface p-4 card-shadow">
      <div className="flex items-start justify-between gap-3">
        <div>
          <p className="text-[12px] font-medium text-text-muted">{label}</p>
          <p className="mt-1 font-heading text-3xl font-bold tracking-tight text-text">{value.toLocaleString('vi-VN')}</p>
        </div>
        <span className={`flex h-10 w-10 items-center justify-center rounded-xl text-lg ${tone}`}>{icon}</span>
      </div>
    </div>
  )
}

export default function AdminPage() {
  const { session } = useAuth()
  const isAdmin = (session!.user.roles ?? []).includes('Admin')
  const [activeTab, setActiveTab] = useState<AdminTab>('overview')
  const [dashboard, setDashboard] = useState<AdminDashboard | null>(null)
  const [reports, setReports] = useState<ModerationReport[]>([])
  const [users, setUsers] = useState<AdminUser[]>([])
  const [reportFilter, setReportFilter] = useState<ReportFilter>('pending')
  const [userQuery, setUserQuery] = useState('')
  const [reportTotal, setReportTotal] = useState(0)
  const [userTotal, setUserTotal] = useState(0)
  const [nextReportOffset, setNextReportOffset] = useState(0)
  const [nextUserOffset, setNextUserOffset] = useState(0)
  const [isLoadingDashboard, setIsLoadingDashboard] = useState(true)
  const [isLoadingReports, setIsLoadingReports] = useState(true)
  const [isLoadingUsers, setIsLoadingUsers] = useState(true)
  const [isLoadingMoreReports, setIsLoadingMoreReports] = useState(false)
  const [isLoadingMoreUsers, setIsLoadingMoreUsers] = useState(false)
  const [actionId, setActionId] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  const loadDashboard = async () => {
    setIsLoadingDashboard(true)
    try {
      setDashboard(await adminApi.getDashboard())
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : 'Không thể tải thống kê quản trị.')
    } finally {
      setIsLoadingDashboard(false)
    }
  }

  const loadReports = async (offset = 0, append = false, filter = reportFilter) => {
    if (append) setIsLoadingMoreReports(true)
    else setIsLoadingReports(true)

    try {
      const page = await adminApi.getReports(filter === 'all' ? undefined : filter, offset)
      setReports((currentReports) => append ? [...currentReports, ...page.items] : page.items)
      setReportTotal(page.total)
      setNextReportOffset(page.offset + page.items.length)
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : 'Không thể tải danh sách báo cáo.')
    } finally {
      setIsLoadingReports(false)
      setIsLoadingMoreReports(false)
    }
  }

  const loadUsers = async (offset = 0, append = false, query = userQuery) => {
    if (append) setIsLoadingMoreUsers(true)
    else setIsLoadingUsers(true)

    try {
      const page = await adminApi.getUsers(query, offset)
      setUsers((currentUsers) => append ? [...currentUsers, ...page.items] : page.items)
      setUserTotal(page.total)
      setNextUserOffset(page.offset + page.items.length)
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : 'Không thể tải danh sách tài khoản.')
    } finally {
      setIsLoadingUsers(false)
      setIsLoadingMoreUsers(false)
    }
  }

  useEffect(() => {
    if (!isAdmin) return
    void Promise.resolve().then(async () => {
      await Promise.all([loadDashboard(), loadUsers()])
    })
  }, [isAdmin])

  useEffect(() => {
    if (!isAdmin) return
    void Promise.resolve().then(() => loadReports(0, false, reportFilter))
  }, [isAdmin, reportFilter])

  const updateReport = async (report: ModerationReport, status: Exclude<ReportStatus, 'pending'>) => {
    setActionId(report.id)
    setError(null)
    try {
      const updated = await adminApi.updateReportStatus(report.id, status)
      setReports((currentReports) => currentReports.map((item) => item.id === report.id ? updated : item))
      void loadDashboard()
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : 'Không thể cập nhật báo cáo.')
    } finally {
      setActionId(null)
    }
  }

  const removePost = async (report: ModerationReport) => {
    if (!window.confirm('Gỡ bài viết này khỏi Fookbase? Thao tác này không thể hoàn tác.')) return

    setActionId(`post-${report.id}`)
    setError(null)
    try {
      await adminApi.deletePost(report.targetId)
      void loadDashboard()
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : 'Không thể gỡ bài viết.')
    } finally {
      setActionId(null)
    }
  }

  const updateUserStatus = async (user: AdminUser) => {
    const nextStatus = !user.isActive
    const message = nextStatus
      ? `Mở lại tài khoản @${user.username}?`
      : `Vô hiệu hóa tài khoản @${user.username}? Người này sẽ không thể đăng nhập lại.`
    if (!window.confirm(message)) return

    setActionId(`user-${user.id}`)
    setError(null)
    try {
      const updated = await adminApi.updateUserStatus(user.id, nextStatus)
      setUsers((currentUsers) => currentUsers.map((item) => item.id === user.id ? updated : item))
      void loadDashboard()
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : 'Không thể cập nhật trạng thái tài khoản.')
    } finally {
      setActionId(null)
    }
  }

  if (!isAdmin) {
    return (
      <div className="flex min-h-screen items-center justify-center p-6 bg-bg">
        <section className="max-w-md rounded-2xl border border-border bg-surface p-7 text-center card-shadow">
          <div className="mx-auto flex h-12 w-12 items-center justify-center rounded-full bg-danger/15 text-xl">🔒</div>
          <h1 className="mt-4 font-heading text-xl font-bold text-text">Khu vực quản trị</h1>
          <p className="mt-2 text-sm text-text-muted">Tài khoản của bạn chưa có quyền truy cập Admin Center.</p>
        </section>
      </div>
    )
  }

  const tabs: { value: AdminTab; label: string; icon: string }[] = [
    { value: 'overview', label: 'Tổng quan', icon: '▦' },
    { value: 'reports', label: 'Báo cáo', icon: '⚑' },
    { value: 'users', label: 'Tài khoản', icon: '♙' },
  ]

  return (
    <div className="min-h-screen bg-bg p-4 xl:p-6" style={{ animation: 'fade-in 0.25s ease both' }}>
      <div className="mx-auto max-w-7xl">
        <header className="mb-6 flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <div className="mb-1 flex items-center gap-2 text-xs font-semibold uppercase tracking-[0.16em] text-primary-light"><span>✦</span> Fookbase</div>
            <h1 className="font-heading text-2xl font-bold text-text">Admin Center</h1>
            <p className="mt-1 text-sm text-text-muted">Quản lý an toàn cộng đồng và nội dung.</p>
          </div>
          <button type="button" onClick={() => { void loadDashboard(); void loadReports(); void loadUsers() }} className="rounded-lg border border-border bg-surface px-3.5 py-2 text-sm font-semibold text-text transition-colors hover:bg-surface-hover">↻ Làm mới</button>
        </header>

        <nav className="mb-6 flex w-full gap-1 overflow-x-auto rounded-xl border border-border bg-surface p-1" aria-label="Điều hướng quản trị">
          {tabs.map((tab) => <button key={tab.value} type="button" onClick={() => setActiveTab(tab.value)} className={`flex min-w-max flex-1 items-center justify-center gap-2 rounded-lg px-4 py-2 text-sm font-semibold transition-colors ${activeTab === tab.value ? 'bg-primary text-white' : 'text-text-muted hover:bg-surface-2 hover:text-text'}`}><span>{tab.icon}</span>{tab.label}{tab.value === 'reports' && dashboard && dashboard.pendingReports > 0 && <span className={`rounded-full px-1.5 py-0.5 text-[10px] ${activeTab === tab.value ? 'bg-white/20 text-white' : 'bg-danger/15 text-danger'}`}>{dashboard.pendingReports}</span>}</button>)}
        </nav>

        {error && <div className="mb-5 flex items-start justify-between gap-4 rounded-xl border border-danger/40 bg-danger/10 p-4 text-sm text-[#ff9baa]"><span>{error}</span><button type="button" onClick={() => setError(null)} className="cursor-pointer border-0 bg-transparent text-inherit">✕</button></div>}

        {activeTab === 'overview' && <section>
          <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
            <StatCard label="Tổng tài khoản" value={dashboard?.totalUsers ?? 0} icon="♙" tone="bg-primary/15 text-primary-light" />
            <StatCard label="Tài khoản hoạt động" value={dashboard?.activeUsers ?? 0} icon="✓" tone="bg-secondary/15 text-secondary" />
            <StatCard label="Bài viết đang hiển thị" value={dashboard?.activePosts ?? 0} icon="▤" tone="bg-accent/15 text-accent" />
            <StatCard label="Báo cáo chờ xử lý" value={dashboard?.pendingReports ?? 0} icon="⚑" tone="bg-danger/15 text-danger" />
          </div>
          {isLoadingDashboard && <p className="mt-4 text-sm text-text-muted">Đang đồng bộ số liệu...</p>}
          <section className="mt-6 rounded-2xl border border-border bg-surface p-5 card-shadow">
            <div className="flex items-start justify-between gap-4"><div><h2 className="font-heading text-lg font-bold text-text">Ưu tiên hôm nay</h2><p className="mt-1 text-sm text-text-muted">Xem các báo cáo đang chờ để giữ cộng đồng an toàn.</p></div><button type="button" onClick={() => setActiveTab('reports')} className="rounded-lg bg-primary px-3 py-2 text-sm font-semibold text-white hover:bg-primary-dark">Mở hàng đợi</button></div>
            <div className="mt-5 grid gap-3 md:grid-cols-3"><div className="rounded-xl bg-surface-3 p-4"><p className="text-xs text-text-muted">Báo cáo cần xem</p><p className="mt-1 text-xl font-bold text-text">{dashboard?.pendingReports ?? 0}</p></div><div className="rounded-xl bg-surface-3 p-4"><p className="text-xs text-text-muted">Tỷ lệ tài khoản hoạt động</p><p className="mt-1 text-xl font-bold text-text">{dashboard && dashboard.totalUsers ? Math.round((dashboard.activeUsers / dashboard.totalUsers) * 100) : 0}%</p></div><div className="rounded-xl bg-surface-3 p-4"><p className="text-xs text-text-muted">Nội dung đang hiển thị</p><p className="mt-1 text-xl font-bold text-text">{dashboard?.activePosts ?? 0}</p></div></div>
          </section>
        </section>}

        {activeTab === 'reports' && <section className="rounded-2xl border border-border bg-surface card-shadow">
          <div className="flex flex-col gap-4 border-b border-border p-4 sm:flex-row sm:items-center sm:justify-between"><div><h2 className="font-heading text-lg font-bold text-text">Hàng đợi báo cáo</h2><p className="text-sm text-text-muted">{reportTotal.toLocaleString('vi-VN')} báo cáo phù hợp</p></div><select value={reportFilter} onChange={(event) => setReportFilter(event.target.value as ReportFilter)} className="rounded-lg border border-border bg-surface-2 px-3 py-2 text-sm text-text outline-none focus:input-focus"><>{reportFilters.map((filter) => <option key={filter.value} value={filter.value}>{filter.label}</option>)}</></select></div>
          <div className="divide-y divide-border">{isLoadingReports ? <p className="p-8 text-center text-sm text-text-muted">Đang tải báo cáo...</p> : reports.length === 0 ? <p className="p-8 text-center text-sm text-text-muted">Không có báo cáo nào trong mục này.</p> : reports.map((report) => <article key={report.id} className="flex flex-col gap-4 p-4 lg:flex-row lg:items-start lg:justify-between"><div className="min-w-0"><div className="flex flex-wrap items-center gap-2"><span className="rounded-md bg-surface-2 px-2 py-0.5 text-[11px] font-bold uppercase text-text-muted">{report.targetType === 'post' ? 'Bài viết' : 'Người dùng'}</span><span className={`rounded-full px-2 py-0.5 text-[11px] font-bold ${statusClass(report.status)}`}>{statusLabel(report.status)}</span><span className="text-xs text-text-light">{formatDate(report.createdAtUtc)}</span></div><p className="mt-2 text-sm font-semibold text-text">Lý do: <span className="capitalize">{report.reason}</span></p>{report.details && <p className="mt-1 max-w-2xl text-sm leading-relaxed text-text-muted">{report.details}</p>}<p className="mt-2 text-xs text-text-light">Mục tiêu: <span title={report.targetId}>{shortId(report.targetId)}</span> · Người báo cáo: <span title={report.reporterUserId}>{shortId(report.reporterUserId)}</span></p></div><div className="flex shrink-0 flex-wrap gap-2">{report.status === 'pending' && <button type="button" onClick={() => void updateReport(report, 'reviewed')} disabled={actionId === report.id} className="rounded-lg border border-border bg-surface-2 px-3 py-1.5 text-xs font-semibold text-text hover:bg-surface-hover disabled:opacity-60">Đánh dấu đã xem</button>}{report.targetType === 'post' && <button type="button" onClick={() => void removePost(report)} disabled={actionId === `post-${report.id}`} className="rounded-lg border border-danger/40 bg-danger/10 px-3 py-1.5 text-xs font-semibold text-danger hover:bg-danger/20 disabled:opacity-60">{actionId === `post-${report.id}` ? 'Đang gỡ...' : 'Gỡ bài viết'}</button>}{report.status !== 'dismissed' && <button type="button" onClick={() => void updateReport(report, 'dismissed')} disabled={actionId === report.id} className="rounded-lg bg-surface-2 px-3 py-1.5 text-xs font-semibold text-text-muted hover:bg-surface-hover disabled:opacity-60">Bỏ qua</button>}{report.status !== 'resolved' && <button type="button" onClick={() => void updateReport(report, 'resolved')} disabled={actionId === report.id} className="rounded-lg bg-primary px-3 py-1.5 text-xs font-semibold text-white hover:bg-primary-dark disabled:opacity-60">Đã xử lý</button>}</div></article>)}</div>
          {nextReportOffset < reportTotal && <div className="border-t border-border p-4"><button type="button" onClick={() => void loadReports(nextReportOffset, true)} disabled={isLoadingMoreReports} className="w-full rounded-lg border border-border bg-surface-2 py-2 text-sm font-semibold text-text hover:bg-surface-hover disabled:opacity-60">{isLoadingMoreReports ? 'Đang tải...' : 'Tải thêm báo cáo'}</button></div>}
        </section>}

        {activeTab === 'users' && <section className="rounded-2xl border border-border bg-surface card-shadow">
          <div className="flex flex-col gap-4 border-b border-border p-4 sm:flex-row sm:items-center sm:justify-between"><div><h2 className="font-heading text-lg font-bold text-text">Tài khoản</h2><p className="text-sm text-text-muted">{userTotal.toLocaleString('vi-VN')} tài khoản</p></div><form onSubmit={(event) => { event.preventDefault(); void loadUsers(0, false) }} className="flex w-full gap-2 sm:w-auto"><input value={userQuery} onChange={(event) => setUserQuery(event.target.value)} placeholder="Email hoặc username" className="min-w-0 flex-1 rounded-lg border border-border bg-surface-2 px-3 py-2 text-sm text-text outline-none focus:input-focus sm:w-64" /><button type="submit" className="rounded-lg bg-primary px-3 py-2 text-sm font-semibold text-white hover:bg-primary-dark">Tìm</button></form></div>
          <div className="divide-y divide-border">{isLoadingUsers ? <p className="p-8 text-center text-sm text-text-muted">Đang tải tài khoản...</p> : users.length === 0 ? <p className="p-8 text-center text-sm text-text-muted">Không tìm thấy tài khoản phù hợp.</p> : users.map((user) => <article key={user.id} className="flex flex-col gap-3 p-4 sm:flex-row sm:items-center sm:justify-between"><div className="flex min-w-0 items-center gap-3"><div className={`flex h-10 w-10 shrink-0 items-center justify-center rounded-full text-xs font-bold text-white ${user.isActive ? 'bg-primary' : 'bg-surface-2 text-text-muted'}`}>{user.username.slice(0, 2).toUpperCase()}</div><div className="min-w-0"><p className="truncate text-sm font-semibold text-text">@{user.username}</p><p className="truncate text-xs text-text-muted">{user.email}</p><div className="mt-1 flex flex-wrap items-center gap-2"><span className={`rounded-full px-2 py-0.5 text-[10px] font-bold ${user.isActive ? 'bg-secondary/15 text-secondary' : 'bg-danger/15 text-danger'}`}>{user.isActive ? 'Đang hoạt động' : 'Đã vô hiệu hóa'}</span>{user.roles.map((role) => <span key={role} className="rounded-full bg-primary/15 px-2 py-0.5 text-[10px] font-bold text-primary-light">{role}</span>)}<span className="text-[11px] text-text-light">Tham gia {formatDate(user.createdAt)}</span></div></div></div>{!user.roles.includes('Admin') && <button type="button" onClick={() => void updateUserStatus(user)} disabled={actionId === `user-${user.id}`} className={`rounded-lg px-3 py-1.5 text-xs font-semibold disabled:opacity-60 ${user.isActive ? 'border border-danger/40 bg-danger/10 text-danger hover:bg-danger/20' : 'bg-secondary text-white hover:brightness-110'}`}>{actionId === `user-${user.id}` ? 'Đang cập nhật...' : user.isActive ? 'Vô hiệu hóa' : 'Kích hoạt'}</button>}</article>)}</div>
          {nextUserOffset < userTotal && <div className="border-t border-border p-4"><button type="button" onClick={() => void loadUsers(nextUserOffset, true)} disabled={isLoadingMoreUsers} className="w-full rounded-lg border border-border bg-surface-2 py-2 text-sm font-semibold text-text hover:bg-surface-hover disabled:opacity-60">{isLoadingMoreUsers ? 'Đang tải...' : 'Tải thêm tài khoản'}</button></div>}
        </section>}
      </div>
    </div>
  )
}
