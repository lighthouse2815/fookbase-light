import { useEffect, useState } from 'react'
import { privacyApi, type PrivacySettings } from '../../api/privacy'
import { Mascot } from 'page-mascot'

const options = { defaultPostPrivacy: ['public', 'friends', 'onlyMe'], friendRequestPolicy: ['everyone', 'friendsOfFriends'], friendListVisibility: ['public', 'friends', 'onlyMe'], followListVisibility: ['public', 'friends', 'onlyMe'] } as const
export default function PrivacySettingsPage() {
  const [settings, setSettings] = useState<PrivacySettings | null>(null)
  const [notice, setNotice] = useState('')
  useEffect(() => { void privacyApi.get().then(setSettings) }, [])
  if (!settings) return <main className="mx-auto max-w-2xl p-8 text-text-muted">Đang tải…</main>
  const save = async () => { setSettings(await privacyApi.update(settings)); setNotice('Đã lưu thiết lập riêng tư.') }
  return <main className="mx-auto max-w-2xl space-y-6 p-6"><div className="flex items-center gap-4"><Mascot directions="/mascots/raccoon-directions.webp" reactions="/mascots/raccoon-reactions.webp" size={64} label="Privacy Raccoon Mascot" /><h1 className="text-2xl font-bold text-text">Quyền riêng tư</h1></div>{(Object.keys(options) as (keyof typeof options)[]).map((key) => <label key={key} className="block rounded-xl border border-border bg-surface p-4 text-text"><span className="mb-2 block font-semibold">{key === 'defaultPostPrivacy' ? 'Đối tượng mặc định cho bài viết' : key === 'friendRequestPolicy' ? 'Ai có thể gửi lời mời kết bạn' : key === 'friendListVisibility' ? 'Ai xem danh sách bạn bè' : 'Ai xem danh sách theo dõi'}</span><select className="w-full rounded-lg bg-surface-2 p-2" value={settings[key]} onChange={(event) => setSettings({ ...settings, [key]: event.target.value })}>{options[key].map((value) => <option key={value}>{value}</option>)}</select></label>)}<button className="rounded-xl bg-primary px-4 py-2 font-bold text-white" onClick={() => void save()}>Lưu</button>{notice && <p className="text-primary-light">{notice}</p>}</main>
}
