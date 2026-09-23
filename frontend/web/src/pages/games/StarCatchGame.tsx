import { useCallback, useEffect, useRef, useState, type CSSProperties } from 'react'
import GameSoundToggle from './GameSoundToggle'
import { useGameSound } from './useGameSound'
import { advanceStarCatch, createStarCatch, moveShip, startStarCatch, STAR_CATCH_DURATION, type StarCatchState } from './starCatchEngine'
import './arcade.css'

const stars = Array.from({ length: 22 }, (_, index) => ({ left: `${(index * 37) % 100}%`, top: `${(index * 61) % 90}%`, delay: `${(index % 5) * 0.7}s`, size: `${2 + index % 3}px` }))

export default function StarCatchGame() {
  const [game, setGame] = useState(createStarCatch)
  const gameRef = useRef(game)
  const stageRef = useRef<HTMLDivElement>(null)
  const { enabled, toggle, play, stop } = useGameSound()
  const playing = game.phase === 'playing'
  const commit = useCallback((next: StarCatchState) => {
    if (next.event === 'star') play(659.25, 0.16, 'triangle')
    else if (next.event === 'meteor') play(110, 0.35, 'sawtooth')
    else if (next.event === 'miss') play(196, 0.08, 'sine')
    gameRef.current = next
    setGame(next)
  }, [play])

  const pause = useCallback(() => {
    if (gameRef.current.phase === 'playing') { commit({ ...gameRef.current, phase: 'paused', event: null }); stop() }
  }, [commit, stop])

  useEffect(() => {
    if (!playing) return
    let frame = 0
    let previous = performance.now()
    const animate = (now: number) => {
      if (gameRef.current.phase !== 'playing') return
      const next = advanceStarCatch(gameRef.current, (now - previous) / 1000)
      previous = now
      commit(next)
      if (next.phase === 'playing') frame = requestAnimationFrame(animate)
    }
    frame = requestAnimationFrame(animate)
    return () => cancelAnimationFrame(frame)
  }, [commit, playing])

  useEffect(() => {
    const visibility = () => { if (document.hidden) pause() }
    window.addEventListener('blur', pause)
    document.addEventListener('visibilitychange', visibility)
    return () => { window.removeEventListener('blur', pause); document.removeEventListener('visibilitychange', visibility) }
  }, [pause])

  const start = () => {
    play(523.25, 0.12)
    commit(startStarCatch(gameRef.current.phase === 'over' ? createStarCatch() : gameRef.current))
    stageRef.current?.focus({ preventScroll: true })
  }
  const reset = () => { stop(); commit(createStarCatch()); stageRef.current?.focus({ preventScroll: true }) }
  const nudge = (direction: -1 | 1) => { if (gameRef.current.phase === 'playing') commit(moveShip(gameRef.current, direction)) }
  const status = game.phase === 'over' ? game.lives === 0 ? 'Thiên thạch đã chạm tàu!' : 'Hết giờ bay!' : game.phase === 'paused' ? 'Đã tạm dừng' : playing ? 'Bắt sao, né đá!' : 'Sẵn sàng cất cánh?'

  return <section aria-label="Hứng sao" className="arcade-shell star-catch-game" onBlur={(event) => { if (!event.currentTarget.contains(event.relatedTarget)) pause() }} onKeyDown={(event) => {
    const key = event.key.toLowerCase()
    if (event.key === 'ArrowLeft' || key === 'a') { event.preventDefault(); if (!event.repeat) nudge(-1) }
    if (event.key === 'ArrowRight' || key === 'd') { event.preventDefault(); if (!event.repeat) nudge(1) }
    if (event.key === ' ' || event.key === 'Enter') { event.preventDefault(); if (!event.repeat && !playing) start() }
    if (event.key === 'Escape' || key === 'p') { event.preventDefault(); if (!event.repeat) { if (playing) pause(); else if (game.phase === 'paused') start() } }
  }}>
    <header className="arcade-heading"><div><p className="arcade-eyebrow">ORBIT RUN / 02</p><h2>Hứng sao</h2><p>Đưa con tàu nhỏ qua một cơn mưa vũ trụ.</p></div><GameSoundToggle enabled={enabled} onToggle={toggle} /></header>
    <div className="arcade-layout">
      <div ref={stageRef} tabIndex={0} role="group" aria-label="Bầu trời hứng sao, dùng phím mũi tên hoặc A D" className="star-stage arcade-stage">
        <div className="star-sky" aria-hidden="true">{stars.map((star, index) => <i key={index} style={{ '--star-left': star.left, '--star-top': star.top, '--star-delay': star.delay, '--star-size': star.size } as CSSProperties} />)}
          {game.items.map((item) => item.kind === 'star' ? <span key={item.id} className="falling-star" style={{ left: `${item.x}%`, top: `${item.y}%` }}>✦</span> : <span key={item.id} className="falling-meteor" style={{ left: `${item.x}%`, top: `${item.y}%` }}>◆</span>)}
          <div className={`space-overlay ${!playing ? 'is-visible' : ''}`}><span aria-hidden="true">{game.phase === 'over' ? '✦' : game.phase === 'paused' ? 'Ⅱ' : '✧'}</span><strong>{game.phase === 'over' ? 'Chuyến bay kết thúc' : game.phase === 'paused' ? 'Nghỉ giữa các vì sao' : 'Sẵn sàng bay?'}</strong><small>{game.phase === 'over' ? `Bạn bắt được ${game.score / 10} sao.` : game.phase === 'paused' ? 'Bấm tiếp tục để bay tiếp.' : 'Bấm Bắt đầu hoặc phím Space.'}</small></div>
          <div className="space-horizon" /><div className="space-ship" style={{ left: `${game.shipX}%` }}><span>⌃</span><i /><b /></div>
        </div>
        <div className="star-controls"><button type="button" aria-label="Di chuyển tàu sang trái" disabled={!playing} onClick={() => nudge(-1)}>←</button><div className="star-progress"><span style={{ width: `${game.elapsed / STAR_CATCH_DURATION * 100}%` }} /></div><button type="button" aria-label="Di chuyển tàu sang phải" disabled={!playing} onClick={() => nudge(1)}>→</button></div>
      </div>
      <aside className="arcade-aside"><span className="arcade-tag">PHẢN XẠ + NHỊP ĐỘ</span><h3>Sao là điểm.<br />Đá là bài học.</h3><p>Điều khiển tàu sang trái và phải để hứng những ngôi sao lấp lánh. Thiên thạch màu cam làm mất một mạng, còn sao lướt qua sẽ không quay lại.</p><p>Dùng <kbd>←</kbd> <kbd>→</kbd> hoặc <kbd>A</kbd> <kbd>D</kbd>. Trên điện thoại, dùng hai nút bên dưới bầu trời.</p>
        <div className="arcade-stats"><div><span>ĐIỂM SỐ</span><strong>{game.score}<small> điểm</small></strong></div><div><span>MẠNG CÒN</span><strong>{'♥'.repeat(game.lives)}<small> / 3</small></strong></div></div>
        <div className="arcade-status" role="status"><strong>{status}</strong><p>{game.phase === 'over' ? `Bạn đạt ${game.score} điểm. ${game.lives ? 'Một chuyến bay rất đẹp!' : 'Lần sau hãy né thiên thạch sớm hơn nhé.'}` : playing ? 'Giữ mắt trên quỹ đạo. Sao và đá rơi nhanh dần theo từng giây.' : 'Ba mạng, ba mươi giây, một bầu trời đầy bất ngờ.'}</p></div>
        <div className="arcade-actions">{playing ? <><button type="button" className="arcade-button arcade-primary" onClick={pause}>Tạm dừng</button><button type="button" className="arcade-button" onClick={reset}>Ván mới</button></> : <><button type="button" className="arcade-button arcade-primary" onClick={start}>{game.phase === 'ready' ? 'Bắt đầu bay' : game.phase === 'paused' ? 'Tiếp tục' : 'Bay lại'} <span aria-hidden="true">↗</span></button>{game.phase !== 'ready' && <button type="button" className="arcade-button" onClick={reset}>Đổi bầu trời</button>}</>}</div>
        <p className="arcade-footnote">Game tự tạm dừng khi bạn chuyển tab hoặc rời vùng chơi.</p>
      </aside>
    </div>
  </section>
}
