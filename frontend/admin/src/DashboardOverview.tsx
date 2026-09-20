import { useState } from 'react'
import { Activity, ArrowRight, CheckCheck, FileText, Flag, ShieldCheck, Users, UserCheck } from 'lucide-react'
import { Area, AreaChart, Bar, BarChart, CartesianGrid, Pie, PieChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import type { AdminDashboard } from './api/admin'
import { usePreferences } from './preferences'

const chartColors = { users: '#0d9488', posts: '#8b7ad8', pending: '#f0aa4e', reviewed: '#7c8ee6', resolved: '#28aa91', dismissed: '#a6adb9' }
const tooltipStyle = { background: 'var(--color-surface)', border: '1px solid var(--color-border)', borderRadius: 12, color: 'var(--color-text)', fontSize: 12 }

export default function DashboardOverview({ data, onOpenReports }: { data: AdminDashboard; onOpenReports: () => void }) {
  const { language, t } = usePreferences()
  const [days, setDays] = useState(30)
  const locale = language === 'vi' ? 'vi-VN' : 'en-US'
  const number = (value: number) => value.toLocaleString(locale)
  const dateLabel = (value: string) => new Intl.DateTimeFormat(locale, { day: 'numeric', month: 'short', timeZone: 'UTC' }).format(new Date(`${value}T00:00:00Z`))
  const activity = (data.activity ?? []).slice(-days)
  const statuses = (data.reportStatuses ?? []).map(item => ({ ...item, name: t(item.status), fill: chartColors[item.status] }))
  const totalReports = statuses.reduce((sum, item) => sum + item.count, 0)
  const newUsers = activity.reduce((sum, day) => sum + day.newUsers, 0)
  const newPosts = activity.reduce((sum, day) => sum + day.newPosts, 0)
  const newReports = activity.reduce((sum, day) => sum + day.newReports, 0)
  const inactiveUsers = Math.max(0, data.totalUsers - data.activeUsers)
  const activePercent = data.totalUsers ? Math.round(data.activeUsers / data.totalUsers * 100) : 0
  const cards = [
    { label: 'totalAccounts', value: data.totalUsers, hint: 'registeredAccounts', icon: Users, tone: 'teal' },
    { label: 'activeAccounts', value: data.activeUsers, hint: 'enabledAccounts', icon: UserCheck, tone: 'blue' },
    { label: 'visiblePosts', value: data.activePosts, hint: 'publishedContent', icon: FileText, tone: 'purple' },
    { label: 'pendingReports', value: data.pendingReports, hint: 'needsAttention', icon: Flag, tone: 'amber' },
  ]

  return <div className="overview-content">
    <div className="metrics-grid">{cards.map(({ label, value, hint, icon: Icon, tone }) => <article className={`metric-card metric-${tone}`} key={label}>
      <div className="metric-top"><span>{t(label)}</span><span className={`icon-tile ${tone}`}><Icon size={19} /></span></div>
      <strong className="metric-value">{number(value)}</strong>
      <p className="metric-hint"><span className={`status-dot ${tone}`} />{t(hint)}</p>
    </article>)}</div>

    <div className="analytics-grid">
      <section className="dashboard-panel activity-panel" aria-labelledby="activity-title">
        <div className="panel-heading"><div><h2 id="activity-title">{t('communityActivity')}</h2><p>{t('activityDescription')}</p></div>
          <div className="period-control" aria-label={t('dateRange')}>{[7, 30].map(value => <button key={value} type="button" aria-pressed={days === value} onClick={() => setDays(value)}>{value} {t('days')}</button>)}</div>
        </div>
        <div className="chart-summary"><div><span className="legend-dot" style={{ background: chartColors.users }} />{t('newAccounts')}<strong>{number(newUsers)}</strong></div><div><span className="legend-dot" style={{ background: chartColors.posts }} />{t('newPosts')}<strong>{number(newPosts)}</strong></div><span className="chart-period">{days} {t('recentDays')}</span></div>
        {activity.length > 0 ? <div className="activity-chart" role="group" aria-label={t('communityActivity')}>
          <ResponsiveContainer width="100%" height="100%" minWidth={0}>
            <AreaChart data={activity} margin={{ top: 12, right: 8, left: -22, bottom: 0 }} accessibilityLayer>
              <defs><linearGradient id="users-fill" x1="0" y1="0" x2="0" y2="1"><stop offset="0%" stopColor={chartColors.users} stopOpacity={0.2} /><stop offset="100%" stopColor={chartColors.users} stopOpacity={0.01} /></linearGradient><linearGradient id="posts-fill" x1="0" y1="0" x2="0" y2="1"><stop offset="0%" stopColor={chartColors.posts} stopOpacity={0.13} /><stop offset="100%" stopColor={chartColors.posts} stopOpacity={0} /></linearGradient></defs>
              <CartesianGrid strokeDasharray="4 5" vertical={false} stroke="var(--color-border)" />
              <XAxis dataKey="date" tickFormatter={dateLabel} tickLine={false} axisLine={false} tick={{ fill: 'var(--color-text-light)', fontSize: 11 }} minTickGap={36} tickMargin={12} />
              <YAxis allowDecimals={false} tickLine={false} axisLine={false} tick={{ fill: 'var(--color-text-light)', fontSize: 11 }} />
              <Tooltip contentStyle={tooltipStyle} labelFormatter={value => dateLabel(String(value))} formatter={(value) => number(Number(value))} />
              <Area type="monotone" dataKey="newPosts" name={t('newPosts')} stroke={chartColors.posts} fill="url(#posts-fill)" strokeWidth={2.5} isAnimationActive={false} />
              <Area type="monotone" dataKey="newUsers" name={t('newAccounts')} stroke={chartColors.users} fill="url(#users-fill)" strokeWidth={2.5} isAnimationActive={false} />
            </AreaChart>
          </ResponsiveContainer>
        </div> : <div className="chart-empty">{t('noAnalytics')}</div>}
        <p className="chart-footnote">{t('utcNote')}</p>
        {activity.length > 0 && <details className="chart-data"><summary>{t('viewData')}</summary><div className="data-table-scroll"><table><caption className="sr-only">{t('communityActivity')}</caption><thead><tr><th scope="col">{t('date')}</th><th scope="col">{t('newAccounts')}</th><th scope="col">{t('newPosts')}</th><th scope="col">{t('newReports')}</th></tr></thead><tbody>{activity.map(day => <tr key={day.date}><th scope="row">{dateLabel(day.date)}</th><td>{number(day.newUsers)}</td><td>{number(day.newPosts)}</td><td>{number(day.newReports)}</td></tr>)}</tbody></table></div></details>}
      </section>

      <section className="dashboard-panel reports-panel" aria-labelledby="reports-title">
        <div className="panel-heading"><div><h2 id="reports-title">{t('reportDistribution')}</h2><p>{t('allTime')}</p></div><span className="panel-symbol"><Flag size={18} /></span></div>
        <div className="donut-chart" role="img" aria-label={`${t('reports')}: ${number(totalReports)}`}>
          {totalReports > 0 ? <ResponsiveContainer width="100%" height="100%" minWidth={0}><PieChart><Pie data={statuses} dataKey="count" nameKey="name" innerRadius="73%" outerRadius="92%" paddingAngle={totalReports > 1 ? 4 : 0} cornerRadius={5} stroke="none" isAnimationActive={false} /><Tooltip contentStyle={tooltipStyle} formatter={value => number(Number(value))} /></PieChart></ResponsiveContainer> : <div className="empty-donut" />}
          <div className="donut-label"><strong>{number(totalReports)}</strong><span>{t('totalReports')}</span></div>
        </div>
        <ul className="report-legend">{statuses.map(item => <li key={item.status}><span className="legend-dot" style={{ background: item.fill }} /><span>{item.name}</span><strong>{number(item.count)}</strong><span className="legend-percent">{totalReports ? Math.round(item.count / totalReports * 100) : 0}%</span></li>)}</ul>
        <button className="panel-link" type="button" onClick={onOpenReports}>{t('viewPendingReports')}<ArrowRight size={16} /></button>
      </section>

      <section className="dashboard-panel report-activity-panel" aria-labelledby="report-activity-title">
        <div className="panel-heading"><div><h2 id="report-activity-title">{t('reportActivity')}</h2><p>{days} {t('recentDays')}</p></div><span className="summary-badge"><Flag size={14} />{number(newReports)} {t('reports').toLowerCase()}</span></div>
        {activity.length > 0 ? <div className="reports-chart" role="group" aria-label={t('reportActivity')}><ResponsiveContainer width="100%" height="100%" minWidth={0}><BarChart data={activity} margin={{ top: 12, right: 8, left: -22, bottom: 0 }} accessibilityLayer>
          <CartesianGrid strokeDasharray="4 5" vertical={false} stroke="var(--color-border)" />
          <XAxis dataKey="date" tickFormatter={dateLabel} tickLine={false} axisLine={false} tick={{ fill: 'var(--color-text-light)', fontSize: 11 }} minTickGap={36} tickMargin={10} />
          <YAxis allowDecimals={false} tickLine={false} axisLine={false} tick={{ fill: 'var(--color-text-light)', fontSize: 11 }} />
          <Tooltip contentStyle={tooltipStyle} cursor={{ fill: 'var(--color-surface-2)' }} labelFormatter={value => dateLabel(String(value))} formatter={value => number(Number(value))} />
          <Bar dataKey="newReports" name={t('newReports')} fill={chartColors.pending} radius={[4, 4, 0, 0]} maxBarSize={28} isAnimationActive={false} />
        </BarChart></ResponsiveContainer></div> : <div className="chart-empty">{t('noAnalytics')}</div>}
      </section>

      <section className="dashboard-panel health-panel" aria-labelledby="health-title">
        <div className="panel-heading"><div><h2 id="health-title">{t('accountHealth')}</h2><p>{t('accountHealthDescription')}</p></div><span className="panel-symbol"><ShieldCheck size={19} /></span></div>
        <div className="health-number"><strong>{activePercent}<span>%</span></strong><span>{t('accountsEnabled')}</span></div>
        <div className="health-bar" role="img" aria-label={`${t('activeAccounts')}: ${activePercent}%`}><span style={{ width: `${activePercent}%` }} /></div>
        <div className="health-legend"><span><i className="legend-dot" style={{ background: chartColors.users }} />{t('active')}<strong>{number(data.activeUsers)}</strong></span><span><i className="legend-dot" style={{ background: 'var(--color-border)' }} />{t('disabled')}<strong>{number(inactiveUsers)}</strong></span></div>
        <div className={`queue-summary ${data.pendingReports ? 'has-pending' : ''}`}>{data.pendingReports ? <Activity size={19} /> : <CheckCheck size={19} />}<div><strong>{t(data.pendingReports ? 'moderationPriority' : 'queueClear')}</strong><p>{data.pendingReports ? `${number(data.pendingReports)} ${t('pendingSummary')}` : t('queueClearDescription')}</p></div></div>
      </section>
    </div>
  </div>
}
