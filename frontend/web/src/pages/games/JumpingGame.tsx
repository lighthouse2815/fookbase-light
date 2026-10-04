import { useCallback, useEffect, useRef, useState } from 'react'
import GameSoundToggle from './GameSoundToggle'
import { useGameSound } from './useGameSound'
import { useJumpingRoom } from './useJumpingRoom'
import { advanceJumping, createJumping, jump, startJumping, JUMPING_WORLD as WORLD, type JumpingState } from './jumpingEngine'
import './arcade.css'
import './jumping.css'

const bestKey = 'fookbase.jumping.best'
function readBest() {
  try { const value = Number(localStorage.getItem(bestKey)); return Number.isSafeInteger(value) && value >= 0 ? value : 0 }
  catch { return 0 }
}

export default function JumpingGame() {
  const [game, setGame] = useState(createJumping)
  const [best, setBest] = useState(readBest)
  const [copiedCode, setCopiedCode] = useState<string | null>(null)
  const gameRef = useRef(game)
  const bestRef = useRef(best)
  const stageRef = useRef<HTMLButtonElement>(null)
  const endedAtRef = useRef(0)
  const { enabled, toggle, play, stop } = useGameSound()
  const commit = useCallback((next: JumpingState) => {
    if (next.score > gameRef.current.score) play(659.25, 0.09, 'triangle')
    if (next.phase === 'over' && gameRef.current.phase !== 'over') { endedAtRef.current = performance.now(); play(130.81, 0.2, 'triangle') }
    if (next.score > bestRef.current) {
      bestRef.current = next.score
      setBest(next.score)
      try { localStorage.setItem(bestKey, String(next.score)) } catch { /* A record is optional when storage is unavailable. */ }
    }
    gameRef.current = next
    setGame(next)
  }, [play])
  const online = useJumpingRoom(gameRef, commit)
  const playing = game.phase === 'playing'
  const inRoom = Boolean(online.room)
  const { finishRound } = online

  useEffect(() => {
    if (playing && !document.hidden) stageRef.current?.focus({ preventScroll: true })
  }, [playing])

  const pause = useCallback(() => {
    if (!inRoom && gameRef.current.phase === 'playing') { commit({ ...gameRef.current, phase: 'paused' }); stop() }
  }, [commit, inRoom, stop])

  const act = (protectRestart = false) => {
    const current = gameRef.current
    if (online.roomRef.current && current.phase !== 'playing') return
    if (protectRestart && current.phase === 'over' && performance.now() - endedAtRef.current < 400) return
    stageRef.current?.focus({ preventScroll: true })
    if (current.phase === 'playing') {
      const next = jump(current)
      if (next !== current) { play(392, 0.1, 'triangle'); commit(next) }
    } else commit(startJumping(current.phase === 'over' ? createJumping() : current))
  }

  useEffect(() => {
    if (!playing) return
    let frame = 0
    let previous = performance.now()
    const animate = (now: number) => {
      if (gameRef.current.phase !== 'playing') return
      const round = online.roundRef.current
      let seconds = round ? Math.max(0, (Date.now() - Date.parse(round.startsAtUtc)) / 1000 - gameRef.current.elapsed - gameRef.current.remainder) : (now - previous) / 1000
      previous = now
      let next = gameRef.current
      // Online rounds use the shared start time, including delayed animation frames.
      while (seconds > 0 && next.phase === 'playing') {
        const step = Math.min(seconds, 0.1)
        next = advanceJumping(next, step)
        seconds -= round ? step : seconds
      }
      commit(next)
      if (next.phase === 'playing') frame = requestAnimationFrame(animate)
    }
    frame = requestAnimationFrame(animate)
    return () => cancelAnimationFrame(frame)
  }, [commit, online.roundRef, playing])

  useEffect(() => {
    const visibility = () => {
      if (!document.hidden) return
      if (online.roundRef.current) finishRound()
      else pause()
    }
    window.addEventListener('blur', pause)
    document.addEventListener('visibilitychange', visibility)
    return () => { window.removeEventListener('blur', pause); document.removeEventListener('visibilitychange', visibility) }
  }, [finishRound, online.roundRef, pause])

  const reset = () => { stop(); commit(createJumping()); stageRef.current?.focus({ preventScroll: true }) }
  const copyCode = async () => {
    try { await navigator.clipboard.writeText(online.room?.code ?? ''); setCopiedCode(online.room?.code ?? null) }
    catch { setCopiedCode(null) }
  }
  const remote = [...online.players.values()]
  const leaderboard = [{ connectionId: 'local', username: 'Bạn', phase: game.phase, score: game.score }, ...remote].sort((a, b) => b.score - a.score)
  const canStartRound = !online.busy && online.countdown === 0 && !playing && remote.every(player => player.phase === 'over')
  const waiting = Boolean(online.room) && !online.roundRef.current
  const overlayTitle = online.countdown ? `Bắt đầu sau ${online.countdown}` : game.phase === 'over' ? 'Thêm một lần nhảy?' : game.phase === 'paused' ? 'Nghỉ một nhịp.' : waiting ? 'Bạn bè đã sẵn sàng?' : 'Chạy. Nhảy. Đi xa hơn.'
  const overlayHint = online.countdown ? 'Mọi người chạy cùng lúc, trên cùng một đường.' : online.room ? game.phase === 'over' ? 'Xem bảng điểm và chờ vòng tiếp theo.' : 'Chủ phòng bấm Bắt đầu vòng để cùng chạy.' : game.phase === 'paused' ? 'Bấm Tiếp tục chạy hoặc Space.' : game.phase === 'over' ? `Bạn đã vượt ${game.score} chướng ngại vật.` : 'Nhấn Space hoặc chạm vào đường chạy để bắt đầu.'

  return <section className="arcade-shell jumping-game" aria-label="Jumping" onBlur={event => { if (!event.currentTarget.contains(event.relatedTarget)) pause() }}>
    <header className="arcade-heading"><div><p className="arcade-eyebrow">JUMPING / CÙNG NHAU ĐI XA</p><h2>Một cú nhảy. Một nhịp vui.</h2><p>Vượt chướng ngại vật, giữ nhịp và thử thách bạn bè.</p></div><GameSoundToggle enabled={enabled} onToggle={toggle} /></header>
    <div className="jumping-score-strip"><div><span>ĐIỂM</span><strong>{game.score}</strong></div><div><span>KỶ LỤC</span><strong>{best}</strong></div><p>{online.room ? 'CHƠI CÙNG BẠN BÈ' : 'CHƠI ĐƠN'}<span>{Math.floor(game.distance / 10)} m đã chạy</span></p></div>
    <button ref={stageRef} type="button" className="jumping-stage arcade-stage" data-phase={game.phase} data-score={game.score}
      aria-label={playing ? 'Đường chạy Jumping, nhấn để nhảy' : 'Đường chạy Jumping, nhấn để bắt đầu hoặc tiếp tục'} aria-describedby="jumping-instructions" onClick={() => act(true)}
      onKeyDown={event => {
        const key = event.key.toLowerCase()
        if ([' ', 'enter', 'arrowup', 'w'].includes(key)) { event.preventDefault(); if (!event.repeat) act(true) }
        if (key === 'escape' || key === 'p') { event.preventDefault(); if (!event.repeat) { if (playing) pause(); else if (game.phase === 'paused') act() } }
      }}>
      <svg viewBox={`0 0 ${WORLD.width} ${WORLD.height}`} preserveAspectRatio="xMinYMid slice" aria-hidden="true">
        <circle cx="562" cy="90" r="47" fill="#ffce92" /><circle cx="562" cy="90" r="64" fill="none" stroke="#ffe2b6" strokeWidth="10" />
        {[0, 720].map(offset => <g key={offset} transform={`translate(${offset - game.distance * 0.12 % 720} 0)`}><path d="M-60 275 96 111l126 135 119-75 153 115 153-152 133 142v110H-60Z" fill="#c5c6b7" /><path d="m66 144 30-33 31 33-20-9-12 7-14-6Z" fill="#f9f0df" /><path d="M-40 299q120-102 251-10t242-8 295-6v100H-40Z" fill="#8bafa3" /></g>)}
        <g fill="#fff8ec" opacity=".85"><path d="M135 69c-5-20 16-37 33-24 14-28 47-21 49 5 26-4 36 30 12 33h-82c-11 0-17-8-12-14Z" /><path d="M345 116c-3-13 12-24 23-15 9-18 30-13 32 3 17-2 24 20 8 22h-54c-7 0-11-5-9-10Z" /></g>
        <path d="M0 315q95-28 183-8t174-12 168 6 195-2v101H0Z" fill="#5a8878" />
        <rect y={WORLD.groundY} width={WORLD.width} height="84" fill="#2e5353" /><path d="M0 316h720" stroke="#f6dc9f" strokeWidth="4" />
        <path d="M0 355h720" stroke="#699487" strokeWidth="2" strokeDasharray="24 38" strokeDashoffset={game.distance % 62} />
        {game.obstacles.map(block => <g key={block.id}><rect className="jumping-block" x={block.x} y={WORLD.groundY - block.height} width={block.width} height={block.height} rx="3" fill="#975965" /><path d={`M${block.x + 4} ${WORLD.groundY - block.height + 5}h${block.width - 8}`} stroke="#eaaa95" strokeWidth="3" /><path d={`m${block.x + 9} ${WORLD.groundY - 12} 8-13 8 13`} stroke="#cf928d" fill="none" strokeWidth="2" /></g>)}
        {remote.filter(player => player.phase === 'playing').map(player => <g key={player.connectionId} opacity=".38" transform={`translate(${WORLD.playerX + 5} ${WORLD.groundY - WORLD.radius - player.height})`}><circle r={WORLD.radius} fill="#8b77c9" stroke="#4c3c66" strokeWidth="2" /><circle cx="6" cy="-3" r="2" fill="#fff" /></g>)}
        <ellipse cx={WORLD.playerX} cy={WORLD.groundY + 7} rx={Math.max(8, 19 - game.height * 0.07)} ry="4" fill="#132f38" opacity=".28" />
        <circle className="jumping-runner" cx={WORLD.playerX} cy={WORLD.groundY - WORLD.radius - game.height} r={WORLD.radius} fill="#ffd07a" stroke="#654c50" strokeWidth="2" />
        <g transform={`translate(${WORLD.playerX} ${WORLD.groundY - WORLD.radius - game.height})`}><ellipse cx="-8" cy="5" rx="4" ry="2.5" fill="#ef967c" /><circle cx="0" cy="-3" r="2" fill="#654c50" /><circle cx="9" cy="-3" r="2" fill="#654c50" /><path d="M1 4q4 5 8 0" fill="none" stroke="#654c50" strokeWidth="1.6" strokeLinecap="round" /><path d="m-14-13-8-8" stroke="#e89b68" strokeWidth="4" strokeLinecap="round" /></g>
      </svg>
      {!playing && <span className="jumping-overlay"><span className="jumping-overlay__icon" aria-hidden="true">{online.countdown || (game.phase === 'paused' ? 'Ⅱ' : '↗')}</span><strong>{overlayTitle}</strong><small>{overlayHint}</small></span>}
      <span className="jumping-stage-hint" aria-hidden="true">{playing ? 'SPACE / CHẠM ĐỂ NHẢY' : 'MỘT ĐƯỜNG CHẠY. VÔ HẠN LƯỢT VUI.'}</span>
    </button>
    <div className="jumping-controls"><button type="button" className="arcade-button arcade-primary" onClick={() => act()} disabled={Boolean(online.room) && !playing}>{playing ? 'Nhảy' : game.phase === 'paused' ? 'Tiếp tục chạy' : game.phase === 'over' ? 'Chạy lại' : 'Bắt đầu chạy'} <span aria-hidden="true">↑</span></button>{!online.room && <>{playing && <button type="button" className="arcade-button" onClick={pause}>Tạm dừng</button>}{game.phase !== 'ready' && <button type="button" className="arcade-button" onClick={reset}>Ván mới</button>}</>}<p id="jumping-instructions"><kbd>Space</kbd> / <kbd>↑</kbd> nhảy · <kbd>P</kbd> tạm dừng khi chơi đơn</p></div>
    <div className="jumping-bottom"><aside className="jumping-online" aria-labelledby="jumping-online-title"><div className="jumping-online__heading"><h3 id="jumping-online-title">Chơi cùng bạn bè</h3><span className={`jumping-connection ${online.status}`} role="status">{online.status === 'connected' ? 'Đã kết nối' : online.status === 'connecting' ? 'Đang kết nối…' : 'Chưa kết nối'}</span></div>
      {online.room ? <><div className="jumping-room-code"><div><span>MÃ PHÒNG</span><strong data-room-code>{online.room.code}</strong></div><button type="button" className="arcade-button" onClick={copyCode}>{copiedCode === online.room.code ? 'Đã sao chép' : 'Sao chép mã'}</button></div><p>{online.room.playerCount} người trong phòng</p><div className="arcade-actions">{online.room.isHost && <button type="button" className="arcade-button arcade-primary" disabled={!canStartRound || online.status !== 'connected'} onClick={() => void online.run('StartRound')}>{online.roundRef.current ? 'Chơi vòng mới' : 'Bắt đầu vòng'}</button>}<button type="button" className="arcade-button" disabled={online.busy || online.status !== 'connected'} onClick={() => void online.run('LeaveRoom')}>Rời phòng</button></div></> : <><p>Tạo phòng, gửi mã cho bạn bè và cùng chinh phục một đường chạy.</p><div className="arcade-actions"><button type="button" className="arcade-button arcade-primary" disabled={online.status !== 'connected' || online.busy} onClick={() => void online.run('CreateRoom')}>Tạo phòng</button>{online.status === 'disconnected' && <button type="button" className="arcade-button" onClick={() => void online.reconnect()}>Kết nối lại</button>}</div><form className="jumping-join" onSubmit={event => { event.preventDefault(); void online.run('JoinRoom') }}><label htmlFor="jumping-room-code">Mã phòng</label><div><input id="jumping-room-code" type="text" autoComplete="off" autoCapitalize="characters" spellCheck={false} maxLength={6} placeholder="VD: AB23CD" value={online.code} onChange={event => online.setCode(event.target.value.toUpperCase())} /><button type="submit" className="arcade-button" disabled={online.status !== 'connected' || online.busy}>Vào phòng</button></div></form></>}
      {online.notice && <p className="jumping-notice" role="status">{online.notice}</p>}{online.error && <p className="jumping-error" role="alert">{online.error}</p>}
      <p className="jumping-footnote">Phòng online bắt đầu cùng lúc. Giữ tab mở đến hết vòng; chuyển tab sẽ kết thúc lượt của bạn.</p>
    </aside><aside className="jumping-results"><h3>{online.room ? 'Bảng điểm trực tiếp' : 'Giữ nhịp, đi xa.'}</h3>{online.room ? <ol className="jumping-leaderboard">{leaderboard.map((player, index) => <li key={player.connectionId} data-local={player.connectionId === 'local'} data-score={player.score}><span>{index + 1}</span><div><strong>{player.username}</strong><small>{player.phase === 'playing' ? 'Đang chạy' : player.phase === 'over' ? 'Đã về đích' : 'Sẵn sàng'}</small></div><b>{player.score}</b></li>)}</ol> : <><p>Nhảy qua mỗi khối đá để ghi một điểm. Tốc độ tăng dần, nên hãy nhảy khi chướng ngại vật đến gần.</p><p>Chỉ nhảy tiếp sau khi chạm đất. Chơi đơn tự tạm dừng khi bạn chuyển tab.</p><div className="jumping-tip"><span aria-hidden="true">↗</span><p>Mẹo nhỏ: một cú nhảy đúng lúc tốt hơn nhiều cú nhấn liên tục.</p></div></>}</aside></div>
    <p className="sr-only" role="status">{game.phase === 'over' ? `Hết lượt. Bạn đạt ${game.score} điểm.` : game.phase === 'paused' ? 'Jumping đang tạm dừng.' : game.phase === 'ready' ? 'Jumping sẵn sàng.' : ''}</p>
  </section>
}
