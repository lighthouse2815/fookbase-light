import { Link } from 'react-router-dom'
import type { GamePhase } from '../types'
import { formatTime } from './formatTime'

function Controls() {
  return <dl className="signal-controls">
    <div><dt><kbd>W</kbd><kbd>A</kbd><kbd>S</kbd><kbd>D</kbd></dt><dd>Di chuyển</dd></div>
    <div><dt>↔ Mouse</dt><dd>Nhìn xung quanh</dd></div>
    <div><dt><kbd>Shift</kbd> / <kbd>Space</kbd></dt><dd>Chạy / Nhảy</dd></div>
    <div><dt><kbd>E</kbd> / <kbd>ESC</kbd></dt><dd>Tương tác / Tạm dừng</dd></div>
  </dl>
}

interface MenuProps {
  phase: GamePhase
  ready: boolean
  error: string | null
  fatal: boolean
  elapsed: number
  muted: boolean
  onMute: () => void
  onStart: () => void
  onReplay: () => void
}

export function GameMenus({ phase, ready, error, fatal, elapsed, muted, onMute, onStart, onReplay }: MenuProps) {
  if (phase === 'playing') return null
  const complete = phase === 'complete'
  const paused = phase === 'paused'
  return <div className={`signal-menu${paused ? ' signal-menu--paused' : ''}${complete ? ' signal-menu--complete' : ''}`}>
    <button type="button" className="signal-menu-audio" onClick={onMute} aria-pressed={muted}>{muted ? 'Âm thanh: tắt' : 'Âm thanh: bật'}</button>
    <section className="signal-menu-content" aria-labelledby="signal-menu-title">
      <p className="signal-eyebrow"><span className="signal-line" /> {complete ? 'TRANSMISSION RECEIVED' : paused ? 'TRANSMISSION ON HOLD' : 'MỘT CHUYẾN ĐI SAU NỬA ĐÊM'}</p>
      <h1 id="signal-menu-title">{paused ? 'Tạm dừng.' : complete ? 'Có ai ở đó?' : <>TẦN SỐ <em>0</em></>}</h1>
      <p className="signal-subtitle">{complete ? 'BẠN ĐÃ TÌM THẤY TÍN HIỆU' : paused ? 'Trạm phát sóng vẫn đang chờ bạn.' : 'THE LAST SIGNAL'}</p>
      <p className="signal-story">{complete ? '“Nếu nghe thấy tín hiệu này… đừng quay lại.” Bạn đã khôi phục nguồn điện và tìm ra thông điệp từ căn phòng cuối. Nhưng người gác trạm đã biến mất từ ba mươi năm trước.' : paused ? 'Tiến trình được giữ trong lượt chơi này. Nhấn tiếp tục để quay lại và điều khiển chuột.' : 'Trạm 07 đã im lặng suốt ba mươi năm. Đêm nay, một tín hiệu lại xuất hiện. Theo ánh đèn. Tìm đường vào. Và khám phá điều còn ở lại.'}</p>
      {complete && <div className="signal-result"><span>05 / 05<strong>Mục tiêu hoàn tất</strong></span><span>{formatTime(elapsed)}<strong>Thời gian khám phá</strong></span></div>}
      {error && <p className="signal-error" role="alert">{error}</p>}
      <div className="signal-menu-actions">
        <button className="signal-primary-button" type="button" disabled={!ready && !fatal} onClick={fatal ? () => window.location.reload() : complete ? onReplay : onStart}>
          <span>{fatal ? 'Tải lại trang' : complete ? 'Chơi lại' : paused ? 'Tiếp tục' : ready ? 'Start Game' : 'Đang dựng thế giới…'}</span><span aria-hidden="true">↗</span>
        </button>
        <Link className="signal-secondary-link" to="/games">{complete || paused ? 'Về thư viện game' : '← Thư viện game'}</Link>
      </div>
      {!complete && <Controls />}
      <p className="signal-desktop-notice">Desktop controls recommended<span>Dùng bàn phím và chuột để trải nghiệm đầy đủ.</span></p>
    </section>
    {!paused && !complete && <div className="signal-scene-caption"><span className="signal-live-dot" /><div>TRẠM PHÁT SÓNG 07<strong>23° 17′ N · 105° 42′ E</strong></div><span className="signal-caption-line" /></div>}
    <div className="signal-menu-footnote">{complete ? 'KẾT THÚC CHUYẾN KHÁM PHÁ' : 'EXPLORATION / MYSTERY / 3D'}<span>FOOKBASE ORIGINAL · PROTOTYPE 01</span></div>
  </div>
}
