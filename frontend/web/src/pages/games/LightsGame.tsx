import { useEffect, useRef, useState } from 'react'
import GameSoundToggle from './GameSoundToggle'
import { useGameSound } from './useGameSound'
import { createLights, LIGHTS_SIZE, pressLights, startLights, type LightsState } from './lightsEngine'
import './arcade.css'

export default function LightsGame() {
  const [game, setGame] = useState(createLights)
  const gameRef = useRef(game)
  const previousPhase = useRef(game.phase)
  const { enabled, toggle, play } = useGameSound()
  const commit = (next: LightsState) => {
    if (next.phase === 'won' && previousPhase.current !== 'won') {
      play(783.99, 0.16, 'triangle')
      window.setTimeout(() => play(1046.5, 0.26, 'triangle'), 110)
    }
    previousPhase.current = next.phase
    gameRef.current = next
    setGame(next)
  }
  useEffect(() => { previousPhase.current = game.phase }, [game.phase])

  const start = () => { play(392, 0.12); commit(startLights(gameRef.current)); }
  const reset = () => commit(createLights(Math.random, gameRef.current.level))
  const nextLevel = () => commit(createLights(Math.random, gameRef.current.level + 1))
  const press = (index: number) => {
    if (gameRef.current.phase !== 'playing') return
    const next = pressLights(gameRef.current, index)
    play(next.board[index] ? 440 : 246.94, 0.09, 'sine')
    commit(next)
  }

  return <section aria-label="Thắp sáng ngân hà" className="arcade-shell lights-game">
    <header className="arcade-heading"><div><p className="arcade-eyebrow">NEBULA GRID / 03</p><h2>Thắp sáng ngân hà</h2><p>Tắt tất cả đèn để mở cổng đến vì sao tiếp theo.</p></div><GameSoundToggle enabled={enabled} onToggle={toggle} /></header>
    <div className="arcade-layout">
      <div className="lights-stage arcade-stage" role="group" aria-label={`Lưới đèn ${LIGHTS_SIZE} x ${LIGHTS_SIZE}, cấp độ ${game.level}`}>
        <div className="lights-orbit lights-orbit-one" /><div className="lights-orbit lights-orbit-two" />
        <div className="lights-grid">{game.board.map((light, index) => <button key={index} type="button" aria-label={`Ô ${index + 1}: ${light ? 'đang sáng' : 'đã tắt'}`} aria-pressed={light} disabled={game.phase !== 'playing'} onClick={() => press(index)} className={`light-cell ${light ? 'is-on' : ''}`}><span aria-hidden="true">{light ? '✦' : ''}</span></button>)}</div>
        <div className={`lights-overlay ${game.phase === 'playing' ? 'is-hidden' : ''}`}><span aria-hidden="true">{game.phase === 'won' ? '✹' : '◎'}</span><strong>{game.phase === 'won' ? 'Cổng đã mở!' : 'Bản đồ sao đã sẵn sàng'}</strong><small>{game.phase === 'won' ? `Bạn tắt hết đèn trong ${game.moves} nước.` : 'Mỗi lần chạm đổi trạng thái ô và bốn ô kề cạnh.'}</small></div>
      </div>
      <aside className="arcade-aside"><span className="arcade-tag">GIẢI ĐỐ + KHÔNG GIAN</span><h3>Một chạm.<br />Năm tia sáng.</h3><p>Mỗi ô bạn chạm sẽ đổi trạng thái của chính nó và bốn ô bên cạnh. Hãy tính trước một nhịp để đưa cả ngân hà về màu tối.</p><p>Chạm ô bất kỳ. Khi tất cả 25 ô tắt, cánh cổng sao sẽ mở. Cấp độ sau sẽ xáo trộn nhiều bước hơn.</p>
        <div className="arcade-stats"><div><span>CẤP ĐỘ</span><strong>{String(game.level).padStart(2, '0')}</strong></div><div><span>NƯỚC ĐI</span><strong>{game.moves}<small> lần</small></strong></div><div><span>CÒN SÁNG</span><strong>{game.board.filter(Boolean).length}<small> ô</small></strong></div></div>
        <div className="arcade-status" role="status"><strong>{game.phase === 'won' ? 'Ngân hà đã yên giấc.' : game.phase === 'playing' ? 'Nhìn mẫu hình, tìm điểm bẻ.' : 'Sẵn sàng bật công tắc?'}</strong><p>{game.phase === 'won' ? 'Bạn có thể đi tiếp lên cấp độ khó hơn hoặc luyện lại bản đồ này.' : game.phase === 'playing' ? 'Mỗi ô sáng còn lại là một gợi ý. Thử góc và cạnh trước khi chạm giữa.' : 'Bấm bắt đầu để kích hoạt lưới đèn.'}</p></div>
        <div className="arcade-actions">{game.phase === 'ready' && <button type="button" className="arcade-button arcade-primary" onClick={start}>Mở bản đồ sao <span aria-hidden="true">↗</span></button>}{game.phase === 'playing' && <button type="button" className="arcade-button" onClick={reset}>Xáo trộn lại</button>}{game.phase === 'won' && <><button type="button" className="arcade-button arcade-primary" onClick={nextLevel}>Cấp độ tiếp theo <span aria-hidden="true">↗</span></button><button type="button" className="arcade-button" onClick={reset}>Chơi lại cấp này</button></>}</div>
        <p className="arcade-footnote">Mẹo: trạng thái bắt đầu được tạo từ một chuỗi bước hợp lệ nên luôn có lời giải.</p>
      </aside>
    </div>
  </section>
}
