import { useEffect, useState } from 'react'
import { authApi, type AuthSessionInfo, type RecoveryCodes, type SecurityState, type TwoFactorSetup } from '../../api/auth'
import { useAuth } from '../../auth/useAuth'
import { Mascot } from 'page-mascot'
import { usePreferences } from '../../preferences'
import { getSessionDeviceType } from './sessionDevice'

const revokeButtonClass = 'shrink-0 rounded-lg border-2 border-danger/50 px-3 py-2 text-sm font-semibold text-danger transition-colors hover:border-danger hover:bg-danger/10 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-danger focus-visible:ring-offset-2 focus-visible:ring-offset-surface'

function SessionDeviceIcon({ deviceType }: { deviceType: ReturnType<typeof getSessionDeviceType> }) {
  const icons = {
    computerDevice: <><rect x="4" y="3" width="16" height="12" rx="2" /><path d="m4 15-2 5h20l-2-5M9 20h6" /></>,
    phoneDevice: <><rect x="7" y="2" width="10" height="20" rx="2" /><path d="M10 5h4M11 19h2" /></>,
    tabletDevice: <><rect x="4" y="2" width="16" height="20" rx="2" /><path d="M11 18h2" /></>,
    unknownDevice: <><rect x="3" y="3" width="18" height="18" rx="4" /><path d="M9.5 9a2.5 2.5 0 1 1 4.2 1.8c-.9.8-1.7 1.2-1.7 2.7M12 17h.01" /></>,
  }
  return <svg viewBox="0 0 24 24" className="h-6 w-6" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">{icons[deviceType]}</svg>
}

export default function SecuritySettingsPage() {
  const { language, t } = usePreferences()
  const sessionDateFormat = new Intl.DateTimeFormat(language === 'vi' ? 'vi-VN' : 'en-US', { dateStyle: 'short', timeStyle: 'short' })
  const { changePassword } = useAuth(); const [security, setSecurity] = useState<SecurityState | null>(null); const [sessions, setSessions] = useState<AuthSessionInfo[]>([]); const [setup, setSetup] = useState<TwoFactorSetup | null>(null); const [code, setCode] = useState(''); const [codes, setCodes] = useState<RecoveryCodes | null>(null); const [currentPassword, setCurrentPassword] = useState(''); const [newPassword, setNewPassword] = useState('')
  const load = () => { void authApi.security().then(setSecurity); void authApi.sessions().then(setSessions) }; useEffect(load, [])
  return <main className="mx-auto max-w-2xl space-y-6 p-6 text-text"><div className="flex items-center gap-4"><Mascot directions="/mascots/knight-directions.webp" reactions="/mascots/knight-reactions.webp" size={64} label="Security Knight Mascot" /><h1 className="text-2xl font-bold">Bảo mật</h1></div><section className="rounded-xl border border-border bg-surface p-4"><h2 className="font-bold">Đổi mật khẩu</h2><input className="m-1 rounded bg-surface-2 p-2" type="password" placeholder="Mật khẩu hiện tại" value={currentPassword} onChange={e => setCurrentPassword(e.target.value)} /><input className="m-1 rounded bg-surface-2 p-2" type="password" placeholder="Mật khẩu mới" value={newPassword} onChange={e => setNewPassword(e.target.value)} /><button className="rounded bg-primary px-3 py-2 text-white" onClick={() => void changePassword({ currentPassword, newPassword, confirmPassword: newPassword })}>Đổi</button></section>
    <section className="rounded-xl border border-border bg-surface p-4 sm:p-5" aria-labelledby="active-sessions-title">
      <div className="mb-4">
        <div className="flex items-center gap-2">
          <h2 id="active-sessions-title" className="text-lg font-bold">{t('activeSessions')}</h2>
          <span className="rounded-full bg-surface-2 px-2.5 py-0.5 text-sm font-semibold text-text-muted">{security?.activeSessionCount ?? 0}</span>
        </div>
        <p className="mt-1 text-sm text-text-muted">{t('activeSessionsDescription')}</p>
      </div>
      <ul className="max-h-[32rem] space-y-3 overflow-y-auto p-1">
        {sessions.map(session => {
          const deviceType = getSessionDeviceType(session.device)
          return <li key={session.sessionId} className={`flex flex-wrap items-center gap-3 rounded-xl border p-3 sm:p-4 ${session.isCurrent ? 'border-primary/40 bg-primary/5' : 'border-border bg-surface'}`}>
            <span className={`flex h-12 w-12 shrink-0 items-center justify-center rounded-xl ${session.isCurrent ? 'bg-primary/15 text-primary' : 'bg-surface-2 text-text-muted'}`}>
              <SessionDeviceIcon deviceType={deviceType} />
            </span>
            <div className="min-w-0 flex-1 basis-40">
              <div className="flex flex-wrap items-center gap-2">
                <p className="font-semibold" title={session.device ?? undefined}>{t(deviceType)}</p>
                {session.isCurrent && <span className="rounded-full bg-primary/15 px-2 py-0.5 text-xs font-semibold text-primary">{t('currentSession')}</span>}
              </div>
              <p className="mt-1 text-xs text-text-muted">{t('sessionLastActive')}: <time dateTime={session.lastSeenAtUtc}>{sessionDateFormat.format(new Date(session.lastSeenAtUtc))}</time></p>
            </div>
            {!session.isCurrent && <button type="button" className={`ml-auto ${revokeButtonClass}`} aria-label={`${t('revokeSession')}: ${t(deviceType)}`} onClick={() => void authApi.revokeSession(session.sessionId).then(load)}>{t('revokeSession')}</button>}
          </li>
        })}
      </ul>
      <div className="mt-4 flex justify-end border-t border-border pt-4">
        <button type="button" className={revokeButtonClass} onClick={() => void authApi.revokeOtherSessions().then(load)}>{t('revokeOtherSessions')}</button>
      </div>
    </section>
    <section className="rounded-xl border border-border bg-surface p-4"><h2 className="font-bold">Xác thực hai bước</h2>{!security?.twoFactorEnabled && !setup && <button onClick={() => void authApi.setupTwoFactor().then(setSetup)}>Thiết lập</button>}{setup && <><p className="break-all text-sm">{setup.otpauthUri}</p><code>{setup.sharedKey}</code><input className="m-2 rounded bg-surface-2 p-2" placeholder="Mã xác thực" value={code} onChange={e => setCode(e.target.value)} /><button onClick={() => void authApi.enableTwoFactor(code).then(setCodes).then(load)}>Bật 2FA</button></>}{security?.twoFactorEnabled && <button onClick={() => void authApi.regenerateRecoveryCodes().then(setCodes)}>Tạo lại recovery codes</button>}{codes && <p className="mt-3 rounded bg-yellow-100 p-3 text-black">Lưu an toàn, chỉ hiển thị một lần: {codes.recoveryCodes.join(', ')}</p>}</section></main>
}
