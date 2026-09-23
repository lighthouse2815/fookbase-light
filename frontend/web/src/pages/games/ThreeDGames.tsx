import { useCallback, useEffect, useRef, useState, type PointerEvent as ReactPointerEvent } from 'react'
import { useGameSound } from './useGameSound'
import { advanceGravityFlip, createGravityFlip, flipGravity, GRAVITY_FLIP_DURATION, startGravityFlip, type GravityFlipState } from './gravityFlipEngine'
import { advanceNeonDrift, createNeonDrift, moveNeonDrift, NEON_DRIFT_DURATION, phaseShift, startNeonDrift, type NeonDriftState } from './neonDriftEngine'
import './arcade.css'

function useStagePause(pause: () => void) {
  useEffect(() => {
    const onVisibilityChange = () => { if (document.hidden) pause() }
    window.addEventListener('blur', pause)
    document.addEventListener('visibilitychange', onVisibilityChange)
    return () => { window.removeEventListener('blur', pause); document.removeEventListener('visibilitychange', onVisibilityChange) }
  }, [pause])
}

function NeonWorld({ game }: { game: NeonDriftState }) {
  return <div className={`neon-world ${game.phaseTime > 0 ? 'is-phased' : ''}`} aria-hidden="true">
    <div className="neon-tunnel neon-tunnel--one" /><div className="neon-tunnel neon-tunnel--two" /><div className="neon-tunnel neon-tunnel--three" />
    <div className="neon-grid-floor" />
    {game.obstacles.map((obstacle) => <span key={obstacle.id} className={`neon-obstacle neon-obstacle--${obstacle.kind}`} style={{ transform: `translate(-50%, -50%) translateX(${obstacle.lane * 142}px) translateZ(${obstacle.depth * 540}px) scale(${1 + obstacle.depth * 1.7})` }} />)}
    <span className="neon-ship" style={{ transform: `translate(-50%, -50%) translateX(${game.lane * 142}px)` }}><i /><b /></span>
  </div>
}

export function NeonDriftGame() {
  const [game, setGame] = useState(createNeonDrift)
  const gameRef = useRef(game)
  const stageRef = useRef<HTMLDivElement>(null)
  const pointerStart = useRef<number | null>(null)
  const { enabled, toggle, play, stop } = useGameSound()
  const playing = game.phase === 'playing'
  const commit = useCallback((next: NeonDriftState) => {
    if (next.event === 'hit') play(118, 0.2, 'sawtooth')
    else if (next.event === 'phase') play(740, 0.09, 'triangle')
    else if (next.event === 'dodge') play(420, 0.04, 'sine')
    gameRef.current = next
    setGame(next)
  }, [play])
  const pause = useCallback(() => {
    if (gameRef.current.phase === 'playing') { commit({ ...gameRef.current, phase: 'paused', event: null }); stop() }
  }, [commit, stop])
  useStagePause(pause)

  useEffect(() => {
    if (!playing) return
    let frame = 0
    let previous = performance.now()
    const tick = (now: number) => {
      if (gameRef.current.phase !== 'playing') return
      const next = advanceNeonDrift(gameRef.current, (now - previous) / 1000)
      previous = now
      commit(next)
      if (next.phase === 'playing') frame = requestAnimationFrame(tick)
    }
    frame = requestAnimationFrame(tick)
    return () => cancelAnimationFrame(frame)
  }, [commit, playing])

  const start = () => { play(440, 0.12); commit(startNeonDrift(gameRef.current.phase === 'over' ? createNeonDrift() : gameRef.current)); stageRef.current?.focus({ preventScroll: true }) }
  const reset = () => { stop(); commit(createNeonDrift()); stageRef.current?.focus({ preventScroll: true }) }
  const nudge = (direction: -1 | 1) => commit(moveNeonDrift(gameRef.current, direction))
  const triggerPhase = () => commit(phaseShift(gameRef.current))
  const steerWithPointer = (event: ReactPointerEvent<HTMLDivElement>) => {
    if (!playing) return
    const bounds = event.currentTarget.getBoundingClientRect()
    const ratio = (event.clientX - bounds.left) / bounds.width
    const target = ratio < 0.34 ? -1 : ratio > 0.66 ? 1 : 0
    const direction = target - gameRef.current.lane
    if (direction !== 0) commit(moveNeonDrift(gameRef.current, direction < 0 ? -1 : 1))
  }
  const onPointerDown = (event: ReactPointerEvent<HTMLDivElement>) => { pointerStart.current = event.clientX; event.currentTarget.setPointerCapture(event.pointerId) }
  const onPointerUp = (event: ReactPointerEvent<HTMLDivElement>) => {
    if (pointerStart.current === null) return
    const distance = event.clientX - pointerStart.current
    pointerStart.current = null
    if (Math.abs(distance) > 35) nudge(distance < 0 ? -1 : 1)
  }
  const status = game.phase === 'over' ? game.lives ? 'Hết giờ trong đường hầm!' : 'Va chạm! Tàu cần sửa rồi.' : game.phase === 'paused' ? 'Đã tạm dừng' : playing ? game.phaseTime > 0 ? 'PHASE SHIFT — xuyên vật thể!' : 'Lách qua các khối neon!' : 'Sẵn sàng vào đường hầm?'

  return <section aria-label="Neon Drift 3D" className="arcade-shell neon-drift-game" onBlur={(event) => { if (!event.currentTarget.contains(event.relatedTarget)) pause() }} onKeyDown={(event) => {
    const key = event.key.toLowerCase()
    if (event.key === 'ArrowLeft' || key === 'a') { event.preventDefault(); if (!event.repeat) nudge(-1) }
    if (event.key === 'ArrowRight' || key === 'd') { event.preventDefault(); if (!event.repeat) nudge(1) }
    if (event.key === ' ' || key === 'f') { event.preventDefault(); if (!event.repeat) { if (playing) triggerPhase(); else start() } }
    if (event.key === 'Escape' || key === 'p') { event.preventDefault(); if (!event.repeat) { if (playing) pause(); else if (game.phase === 'paused') start() } }
  }}>
    <header className="arcade-heading"><div><p className="arcade-eyebrow">NEON TUNNEL / 04</p><h2>Neon Drift 3D</h2><p>Lái một con tàu phát sáng qua đường hầm đang co giãn.</p></div><button type="button" className="arcade-button arcade-sound" onClick={toggle}>{enabled ? 'Âm thanh: bật' : 'Âm thanh: tắt'}</button></header>
    <div className="arcade-layout">
      <div ref={stageRef} tabIndex={0} role="group" aria-label="Đường hầm neon, dùng A D hoặc mũi tên để lái" className="neon-stage arcade-stage" onPointerMove={steerWithPointer} onPointerDown={onPointerDown} onPointerUp={onPointerUp} onDoubleClick={triggerPhase}>
        <NeonWorld game={game} />
        <div className={`neon-overlay ${playing ? 'is-hidden' : ''}`}><span aria-hidden="true">{game.phase === 'over' ? '✦' : game.phase === 'paused' ? 'Ⅱ' : '◈'}</span><strong>{game.phase === 'over' ? 'Đường hầm đã khép' : game.phase === 'paused' ? 'Tạm dừng giữa chiều không gian' : 'Sẵn sàng drift?'}</strong><small>{game.phase === 'over' ? `Bạn ghi ${game.score} điểm.` : game.phase === 'paused' ? 'Bấm tiếp tục để bay tiếp.' : 'Bấm bắt đầu hoặc Space để vào hầm.'}</small></div>
        <div className="neon-hud"><span>SECTOR 04</span><span>{Math.ceil(game.elapsed)}s / {NEON_DRIFT_DURATION}s</span></div>
        <div className="neon-controls"><button type="button" aria-label="Lái sang trái" disabled={!playing} onClick={() => nudge(-1)}>←</button><button type="button" aria-label="Phase shift" disabled={!playing || game.phaseTime > 0} onClick={triggerPhase}>PHASE</button><button type="button" aria-label="Lái sang phải" disabled={!playing} onClick={() => nudge(1)}>→</button></div>
      </div>
      <aside className="arcade-aside"><span className="arcade-tag">3D + PHẢN XẠ</span><h3>Rê để lái.<br />Nhấn đúp để xuyên.</h3><p>Khối cổng tím và orb hồng trôi tới từ chiều sâu. Trượt sang ba làn để né chúng trước khi chúng chạm tàu.</p><p>Điều khiển bất ngờ: rê chuột hoặc ngón tay trên màn chơi để lái trực tiếp. Nhấn <kbd>Space</kbd>, <kbd>F</kbd> hoặc nhấn đúp để bật <strong>Phase Shift</strong>, xuyên vật thể trong chớp mắt.</p>
        <div className="arcade-stats"><div><span>ĐIỂM</span><strong>{game.score}</strong></div><div><span>MẠNG</span><strong>{'◆'.repeat(game.lives)}<small> / 3</small></strong></div><div><span>PHASE</span><strong>{game.phaseTime > 0 ? 'ON' : '—'}</strong></div></div>
        <div className="arcade-status" role="status"><strong>{status}</strong><p>{game.phase === 'over' ? 'Mỗi khối vượt qua được cộng điểm. Một lần phase đúng lúc có thể cứu cả chuyến bay.' : playing ? 'Nhìn vào độ sâu của khối: càng phóng to, càng gần tàu.' : 'Ba mạng, bốn mươi lăm giây và một đường hầm không đứng yên.'}</p></div>
        <div className="arcade-actions">{playing ? <><button type="button" className="arcade-button arcade-primary" onClick={pause}>Tạm dừng</button><button type="button" className="arcade-button" onClick={reset}>Ván mới</button></> : <><button type="button" className="arcade-button arcade-primary" onClick={start}>{game.phase === 'ready' ? 'Vào đường hầm' : game.phase === 'paused' ? 'Tiếp tục' : 'Drift lại'} <span aria-hidden="true">↗</span></button>{game.phase !== 'ready' && <button type="button" className="arcade-button" onClick={reset}>Đổi đường hầm</button>}</>}</div>
        <p className="arcade-footnote">Rời tab sẽ tự tạm dừng. Hãy thử nhấn đúp màn chơi để khám phá điều bất ngờ.</p>
      </aside>
    </div>
  </section>
}

function GravityWorld({ game }: { game: GravityFlipState }) {
  return <div className={`gravity-world gravity-world--${game.gravity === 1 ? 'down' : 'up'}`} aria-hidden="true">
    <div className="gravity-grid gravity-grid--back" /><div className="gravity-grid gravity-grid--floor" />
    {game.gates.map((gate) => <div key={gate.id} className="gravity-gate" style={{ left: `${gate.x * 100}%` }}><i style={{ height: `${Math.max(0, gate.gap - gate.size / 2)}%` }} /><b style={{ top: `${gate.gap + gate.size / 2}%` }} /></div>)}
    <span className="gravity-pilot" style={{ top: `${game.playerY}%` }}><i /><b /></span>
  </div>
}

export function GravityFlipGame() {
  const [game, setGame] = useState(createGravityFlip)
  const gameRef = useRef(game)
  const stageRef = useRef<HTMLDivElement>(null)
  const { enabled, toggle, play, stop } = useGameSound()
  const playing = game.phase === 'playing'
  const commit = useCallback((next: GravityFlipState) => {
    if (next.event === 'flip') play(next.gravity === 1 ? 280 : 560, 0.1, 'triangle')
    else if (next.event === 'hit') play(100, 0.35, 'sawtooth')
    else if (next.event === 'score') play(720, 0.08, 'sine')
    gameRef.current = next
    setGame(next)
  }, [play])
  const pause = useCallback(() => {
    if (gameRef.current.phase === 'playing') { commit({ ...gameRef.current, phase: 'paused', event: null }); stop() }
  }, [commit, stop])
  useStagePause(pause)

  useEffect(() => {
    if (!playing) return
    let frame = 0
    let previous = performance.now()
    const tick = (now: number) => {
      if (gameRef.current.phase !== 'playing') return
      const next = advanceGravityFlip(gameRef.current, (now - previous) / 1000)
      previous = now
      commit(next)
      if (next.phase === 'playing') frame = requestAnimationFrame(tick)
    }
    frame = requestAnimationFrame(tick)
    return () => cancelAnimationFrame(frame)
  }, [commit, playing])

  const start = () => { play(392, 0.12); commit(startGravityFlip(gameRef.current.phase === 'over' ? createGravityFlip() : gameRef.current)); stageRef.current?.focus({ preventScroll: true }) }
  const reset = () => { stop(); commit(createGravityFlip()); stageRef.current?.focus({ preventScroll: true }) }
  const flip = () => commit(flipGravity(gameRef.current))
  const status = game.phase === 'over' ? game.event === 'hit' ? 'Va vào thành ống!' : 'Bạn đã xuyên qua cả đường chân trời.' : game.phase === 'paused' ? 'Đã tạm dừng' : playing ? game.gravity === 1 ? 'Trọng lực kéo xuống' : 'Trọng lực kéo lên' : 'Sẵn sàng lộn ngược?'

  return <section aria-label="Gravity Flip 3D" className="arcade-shell gravity-flip-game" onBlur={(event) => { if (!event.currentTarget.contains(event.relatedTarget)) pause() }} onKeyDown={(event) => {
    if (event.key === ' ' || event.key === 'ArrowUp' || event.key === 'ArrowDown' || event.key.toLowerCase() === 'g') { event.preventDefault(); if (!event.repeat) { if (playing) flip(); else start() } }
    if (event.key === 'Escape' || event.key.toLowerCase() === 'p') { event.preventDefault(); if (!event.repeat) { if (playing) pause(); else if (game.phase === 'paused') start() } }
  }}>
    <header className="arcade-heading"><div><p className="arcade-eyebrow">CUBE FLIGHT / 05</p><h2>Gravity Flip</h2><p>Bay trong một ống lập phương, nơi sàn và trần đổi chỗ liên tục.</p></div><button type="button" className="arcade-button arcade-sound" onClick={toggle}>{enabled ? 'Âm thanh: bật' : 'Âm thanh: tắt'}</button></header>
    <div className="arcade-layout">
      <div ref={stageRef} tabIndex={0} role="group" aria-label="Ống trọng lực, chạm để đảo chiều" className="gravity-stage arcade-stage" onPointerDown={() => { if (playing) flip(); else start() }}>
        <GravityWorld game={game} />
        <div className={`gravity-overlay ${playing ? 'is-hidden' : ''}`}><span aria-hidden="true">{game.phase === 'over' ? '◒' : game.phase === 'paused' ? 'Ⅱ' : '↕'}</span><strong>{game.phase === 'over' ? 'Quỹ đạo kết thúc' : game.phase === 'paused' ? 'Tạm dừng giữa hai cực' : 'Chạm để đảo trọng lực'}</strong><small>{game.phase === 'over' ? `Bạn vượt qua ${game.score} cổng.` : game.phase === 'paused' ? 'Bấm tiếp tục để bay lại.' : 'Mỗi lần chạm là một cú lộn 180°.'}</small></div>
        <div className="gravity-hud"><span>GRAVITY {game.gravity === 1 ? '↓' : '↑'}</span><span>{Math.ceil(game.elapsed)}s / {GRAVITY_FLIP_DURATION}s</span></div>
        <button type="button" className="gravity-flip-button" onPointerDown={(event) => event.stopPropagation()} onClick={flip} disabled={!playing} aria-label="Đảo trọng lực">FLIP <span>↕</span></button>
      </div>
      <aside className="arcade-aside"><span className="arcade-tag">3D + NHỊP ĐỘ</span><h3>Chạm là lộn.<br />Đừng chạm quá muộn.</h3><p>Phi công phải lọt qua khoảng trống của từng cổng. Trọng lực luôn tăng tốc, nên bạn cần lộn ngược để giữ mình ở giữa.</p><p>Điều khiển bất ngờ: chạm hoặc click <strong>bất kỳ vị trí nào</strong> trên ống để đảo trọng lực. Phím <kbd>Space</kbd>, <kbd>↑</kbd>, <kbd>↓</kbd> và nút FLIP cũng làm được.</p>
        <div className="arcade-stats"><div><span>CỔNG ĐÃ QUA</span><strong>{game.score}</strong></div><div><span>LẦN LỘN</span><strong>{game.flips}</strong></div><div><span>HƯỚNG</span><strong>{game.gravity === 1 ? '↓' : '↑'}</strong></div></div>
        <div className="arcade-status" role="status"><strong>{status}</strong><p>{game.phase === 'over' ? 'Mỗi cổng là một nhịp. Quan sát khoảng trống kế tiếp rồi chạm sớm hơn một chút.' : playing ? 'Giữ phi công gần tâm ống. Cổng càng về sau càng chạy nhanh.' : 'Bốn mươi giây, vô hạn cú lộn, một đường bay không trọng lượng.'}</p></div>
        <div className="arcade-actions">{playing ? <><button type="button" className="arcade-button arcade-primary" onClick={pause}>Tạm dừng</button><button type="button" className="arcade-button" onClick={reset}>Ván mới</button></> : <><button type="button" className="arcade-button arcade-primary" onClick={start}>{game.phase === 'ready' ? 'Cất cánh' : game.phase === 'paused' ? 'Tiếp tục' : 'Bay lại'} <span aria-hidden="true">↗</span></button>{game.phase !== 'ready' && <button type="button" className="arcade-button" onClick={reset}>Đổi quỹ đạo</button>}</>}</div>
        <p className="arcade-footnote">Màn chơi tự tạm dừng khi rời tab. Trên điện thoại, chạm vào bất kỳ vùng nào trong ống.</p>
      </aside>
    </div>
  </section>
}
