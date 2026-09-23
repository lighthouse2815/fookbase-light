import { useCallback, useEffect, useId, useRef, useState } from 'react'
import { HubConnectionBuilder, HubConnectionState, LogLevel, type HubConnection } from '@microsoft/signalr'
import { apiBaseUrl } from '../../api/client'
import { useAuth } from '../../auth/useAuth'
import { getAuthSession } from '../../auth/session'
import { advanceGame, createGame, flap, WORLD, type FlappyState } from './flappyBirdEngine'

const bestScoreKey = 'fookbase.flappy-bird.best'
function readBestScore() {
  try {
    const value = Number(localStorage.getItem(bestScoreKey))
    return Number.isSafeInteger(value) && value >= 0 ? value : 0
  } catch { return 0 }
}

interface OnlineRound {
  id: string
  seed: number
  startsAtUtc: string
}

interface RemotePlayer {
  birdY: number
  velocity: number
  phase: 'playing' | 'over'
  score: number
}

interface RoomMembership {
  code: string
  isHost: boolean
  playerCount: number
  round: OnlineRound | null
}

export default function FlappyBirdGame() {
  const { session } = useAuth()
  const [game, setGame] = useState(createGame)
  const [bestScore, setBestScore] = useState(readBestScore)
  const [room, setRoom] = useState<RoomMembership | null>(null)
  const [roomCodeDraft, setRoomCodeDraft] = useState('')
  const [onlineRound, setOnlineRound] = useState<OnlineRound | null>(null)
  const [remotePlayers, setRemotePlayers] = useState<ReadonlyMap<string, RemotePlayer>>(new Map())
  const [onlineError, setOnlineError] = useState<string | null>(null)
  const [isOnlineConnected, setIsOnlineConnected] = useState(false)
  const gameRef = useRef(game)
  const bestRef = useRef(bestScore)
  const onlineRoundRef = useRef<OnlineRound | null>(null)
  const roomRef = useRef<RoomMembership | null>(null)
  const onlineConnectionRef = useRef<HubConnection | null>(null)
  const onlineStartTimerRef = useRef<number | null>(null)
  const stageRef = useRef<HTMLButtonElement>(null)
  const endedAtRef = useRef(0)
  const id = useId()
  const playing = game.phase === 'playing'
  const paused = game.phase === 'paused'
  const ended = game.phase === 'over'

  const commitGame = useCallback((next: FlappyState) => {
    if (next.phase === 'over' && gameRef.current.phase !== 'over') {
      endedAtRef.current = performance.now()
      if (next.score > bestRef.current) {
        bestRef.current = next.score
        setBestScore(next.score)
        try { localStorage.setItem(bestScoreKey, String(next.score)) } catch { /* Playing also works with storage disabled. */ }
      }
    }
    gameRef.current = next
    setGame(next)
  }, [])

  const scheduleOnlineRound = useCallback((round: OnlineRound) => {
    if (onlineStartTimerRef.current) window.clearTimeout(onlineStartTimerRef.current)
    onlineRoundRef.current = round
    setOnlineRound(round)
    setRemotePlayers(new Map())
    setOnlineError(null)
    commitGame(createGame(round.seed))
    const delay = Math.max(0, Date.parse(round.startsAtUtc) - Date.now())
    onlineStartTimerRef.current = window.setTimeout(() => {
      commitGame(flap(createGame(round.seed)))
      onlineStartTimerRef.current = null
    }, delay)
  }, [commitGame])

  const applyRoomMembership = useCallback((membership: RoomMembership) => {
    roomRef.current = membership
    setRoom(membership)
    setRoomCodeDraft(membership.code)
    if (membership.round && Date.parse(membership.round.startsAtUtc) > Date.now()) {
      scheduleOnlineRound(membership.round)
    } else if (membership.round) {
      setOnlineError('Vòng này đã bắt đầu. Chờ chủ phòng mở vòng tiếp theo.')
    }
  }, [scheduleOnlineRound])

  const handleRoundStarted = useCallback((round: OnlineRound) => {
    if (roomRef.current) {
      const membership = { ...roomRef.current, round }
      roomRef.current = membership
      setRoom(membership)
    }
    scheduleOnlineRound(round)
  }, [scheduleOnlineRound])

  const pause = useCallback(() => {
    if (!onlineRoundRef.current && gameRef.current.phase === 'playing') {
      commitGame({ ...gameRef.current, phase: 'paused' })
    }
  }, [commitGame])

  const play = () => {
    const current = gameRef.current
    if (onlineRoundRef.current && current.phase !== 'playing') return
    if (current.phase === 'over' && performance.now() - endedAtRef.current < 400) return
    stageRef.current?.focus({ preventScroll: true })
    if (current.phase === 'paused') commitGame({ ...current, phase: 'playing', remainder: 0 })
    else commitGame(flap(current.phase === 'over' ? createGame() : current))
  }

  useEffect(() => {
    if (!playing) return
    let frame: number
    let previous = performance.now()
    const animate = (now: number) => {
      if (gameRef.current.phase !== 'playing') return
      const next = advanceGame(gameRef.current, (now - previous) / 1000)
      previous = now
      commitGame(next)
      if (next.phase === 'playing') frame = requestAnimationFrame(animate)
    }
    frame = requestAnimationFrame(animate)
    return () => cancelAnimationFrame(frame)
  }, [commitGame, playing])

  useEffect(() => {
    const onVisibilityChange = () => { if (document.hidden) pause() }
    window.addEventListener('blur', pause)
    document.addEventListener('visibilitychange', onVisibilityChange)
    return () => {
      window.removeEventListener('blur', pause)
      document.removeEventListener('visibilitychange', onVisibilityChange)
    }
  }, [pause])

  useEffect(() => {
    if (!session) return

    const connection = new HubConnectionBuilder()
      .withUrl(`${apiBaseUrl}/hubs/flappy-bird`, {
        accessTokenFactory: () => getAuthSession()?.accessToken ?? '',
      })
      .withAutomaticReconnect()
      .configureLogging(import.meta.env.DEV ? LogLevel.Warning : LogLevel.Error)
      .build()
    onlineConnectionRef.current = connection
    connection.on('RoundStarted', handleRoundStarted)
    connection.on('PlayerUpdated', (player: RemotePlayer & { connectionId: string }) => {
      setRemotePlayers((current) => new Map(current).set(player.connectionId, player))
    })
    connection.on('PlayerLeft', ({ connectionId }: { connectionId: string }) => {
      setRemotePlayers((current) => {
        const next = new Map(current)
        next.delete(connectionId)
        return next
      })
    })
    connection.on('RoomUpdated', ({ playerCount }: { playerCount: number }) => {
      if (!roomRef.current) return
      const membership = { ...roomRef.current, playerCount }
      roomRef.current = membership
      setRoom(membership)
    })
    connection.onreconnected(() => {
      const previousRoom = roomRef.current
      if (!previousRoom) return
      void connection.invoke<RoomMembership>('JoinRoom', previousRoom.code)
        .then(applyRoomMembership)
        .catch(() => setOnlineError('Không thể kết nối lại phòng chơi.'))
    })
    void connection.start()
      .then(() => setIsOnlineConnected(true))
      .catch(() => setOnlineError('Không thể kết nối chế độ chơi cùng.'))

    return () => {
      if (onlineStartTimerRef.current) window.clearTimeout(onlineStartTimerRef.current)
      onlineStartTimerRef.current = null
      onlineRoundRef.current = null
      roomRef.current = null
      if (onlineConnectionRef.current === connection) onlineConnectionRef.current = null
      setIsOnlineConnected(false)
      void connection.stop()
    }
  }, [applyRoomMembership, handleRoundStarted, session])

  useEffect(() => {
    if (!onlineRound) return
    const timer = window.setInterval(() => {
      const connection = onlineConnectionRef.current
      const current = gameRef.current
      if (connection?.state !== HubConnectionState.Connected ||
          current.phase !== 'playing' && current.phase !== 'over') return
      void connection.invoke('UpdatePlayer', {
        roundId: onlineRound.id,
        birdY: current.birdY,
        velocity: current.velocity,
        phase: current.phase,
        score: current.score,
      }).catch(() => undefined)
    }, 80)
    return () => window.clearInterval(timer)
  }, [onlineRound])

  const createRoom = () => {
    const connection = onlineConnectionRef.current
    if (connection?.state !== HubConnectionState.Connected) {
      setOnlineError('Chưa kết nối được với máy chủ game. Hãy thử lại sau ít giây.')
      return
    }
    setOnlineError(null)
    void connection.invoke<RoomMembership>('CreateRoom')
      .then(applyRoomMembership)
      .catch(() => setOnlineError('Không thể tạo phòng.'))
  }

  const joinRoom = () => {
    const connection = onlineConnectionRef.current
    const code = roomCodeDraft.trim().toUpperCase()
    if (connection?.state !== HubConnectionState.Connected) {
      setOnlineError('Chưa kết nối được với máy chủ game. Hãy thử lại sau ít giây.')
      return
    }
    if (code.length !== 6) {
      setOnlineError('Nhập mã phòng gồm 6 ký tự.')
      return
    }
    setOnlineError(null)
    void connection.invoke<RoomMembership>('JoinRoom', code)
      .then(applyRoomMembership)
      .catch((error: Error) => setOnlineError(error.message || 'Không thể vào phòng.'))
  }

  const startHostedRound = () => {
    const connection = onlineConnectionRef.current
    if (!roomRef.current?.isHost || connection?.state !== HubConnectionState.Connected) return
    setOnlineError(null)
    void connection.invoke('StartRound').catch((error: Error) => setOnlineError(error.message || 'Không thể bắt đầu vòng chơi.'))
  }

  const leaveOnlineRound = () => {
    if (onlineStartTimerRef.current) window.clearTimeout(onlineStartTimerRef.current)
    onlineStartTimerRef.current = null
    onlineRoundRef.current = null
    roomRef.current = null
    const connection = onlineConnectionRef.current
    if (connection?.state === HubConnectionState.Connected) {
      void connection.invoke('LeaveRoom').catch(() => undefined)
    }
    setRoom(null)
    setRoomCodeDraft('')
    setOnlineRound(null)
    setRemotePlayers(new Map())
    setOnlineError(null)
    commitGame(createGame())
  }

  const angle = ended ? 65 : Math.max(-25, Math.min(85, game.velocity * 0.15))
  const wingY = playing ? Math.sin(game.distance * 0.22) * 4 : 0

  return (
    <section id="flappy-bird" aria-labelledby={`${id}-title`} className="game-detail-panel scroll-mt-20 rounded-3xl border border-border bg-surface p-4 card-shadow sm:p-8"
      onBlur={(event) => { if (!event.currentTarget.contains(event.relatedTarget)) pause() }}>
      <div className="mb-6 flex flex-wrap items-center justify-between gap-4">
        <div>
          <p className="mb-1 text-xs font-bold tracking-[0.2em] text-primary-light">FOOKBASE ARCADE</p>
          <h2 id={`${id}-title`} className="font-heading text-2xl font-bold text-text">Flappy Bird</h2>
          <p className="mt-1 text-sm text-text-muted">Một cú chạm. Thêm một lần bay.</p>
        </div>
        <div className="game-stat-strip flex gap-3 text-center">
          <div className="min-w-20 rounded-xl bg-primary/15 px-4 py-2"><p className="text-xs text-text-muted">Điểm</p><p className="text-2xl font-bold text-primary-light">{game.score}</p></div>
          <div className="min-w-20 rounded-xl bg-surface-2 px-4 py-2"><p className="text-xs text-text-muted">Kỷ lục</p><p className="text-2xl font-bold text-text">{bestScore}</p></div>
        </div>
      </div>

      <div className="game-play-grid grid items-center gap-6 md:justify-center">
        <div className="game-stage-column mx-auto w-full max-w-[430px]">
          <button ref={stageRef} type="button" aria-label={playing ? 'Vỗ cánh' : paused ? 'Tiếp tục Flappy Bird' : ended ? 'Chơi lại Flappy Bird' : 'Bắt đầu Flappy Bird'} aria-describedby={`${id}-instructions`}
            className="relative block aspect-[2/3] w-full touch-none select-none overflow-hidden rounded-2xl border-2 border-[#315343] bg-[#71c5cf] shadow-xl focus-visible:outline-4 focus-visible:outline-offset-4 focus-visible:outline-primary"
            onPointerDown={(event) => { if (!event.isPrimary || event.button !== 0) return; event.preventDefault(); play() }}
            onClick={(event) => { if (event.detail === 0) play() }}
            onKeyDown={(event) => {
              if (['Space', 'ArrowUp', 'Enter'].includes(event.code)) {
                event.preventDefault()
                if (!event.repeat) play()
              } else if (event.code === 'Escape' || event.code === 'KeyP') {
                event.preventDefault()
                if (!event.repeat) { if (paused) play(); else pause() }
              }
            }}>
            <svg viewBox={`0 0 ${WORLD.width} ${WORLD.height}`} className="block h-full w-full" aria-hidden="true">
              <defs>
                <linearGradient id={`${id}-sky`} x2="0" y2="1"><stop stopColor="#66bdd0" /><stop offset="1" stopColor="#b1e4cb" /></linearGradient>
                <linearGradient id={`${id}-pipe`}><stop stopColor="#6b9e2e" /><stop offset="0.2" stopColor="#c1ed70" /><stop offset="0.45" stopColor="#92cb42" /><stop offset="1" stopColor="#4d8a29" /></linearGradient>
              </defs>
              <rect width="360" height="540" fill={`url(#${id}-sky)`} />
              <circle cx="284" cy="86" r="29" fill="#fff3bf" opacity="0.85" />
              <g fill="#eaf7df" opacity="0.85">
                {[[-35, 128], [156, 60], [305, 182]].map(([x, y]) => <g key={x} transform={`translate(${x} ${y})`}><ellipse cx="30" cy="8" rx="35" ry="15" /><ellipse cx="16" cy="0" rx="17" ry="19" /><ellipse cx="45" cy="-2" rx="24" ry="23" /></g>)}
              </g>
              <g fill="#9cd5c0" stroke="#84c6b4" strokeWidth="2">
                {[0, 45, 102, 155, 207, 263, 321].map((x, i) => <rect key={x} x={x} y={365 - i % 3 * 19} width="38" height="110" rx="2" />)}
              </g>
              <path d="M0 437 Q15 396 40 427 Q65 389 90 421 Q120 394 147 427 Q177 390 205 422 Q236 387 264 427 Q292 399 319 423 Q345 394 360 426 V470 H0Z" fill="#76ba77" stroke="#579d60" strokeWidth="3" />
              {game.pipes.map(pipe => {
                const top = pipe.gapCenter - WORLD.gap / 2
                const bottom = pipe.gapCenter + WORLD.gap / 2
                return <g key={pipe.id} data-pipe="" transform={`translate(${pipe.x} 0)`} fill={`url(#${id}-pipe)`} stroke="#365125" strokeWidth="2">
                  <rect x="0" y="-2" width={WORLD.pipeWidth} height={top - WORLD.pipeCapHeight + 2} />
                  <rect x={-WORLD.pipeLip} y={top - WORLD.pipeCapHeight} width={WORLD.pipeWidth + WORLD.pipeLip * 2} height={WORLD.pipeCapHeight} rx="2" />
                  <rect x="0" y={bottom + WORLD.pipeCapHeight - 2} width={WORLD.pipeWidth} height={WORLD.groundY - bottom} />
                  <rect x={-WORLD.pipeLip} y={bottom} width={WORLD.pipeWidth + WORLD.pipeLip * 2} height={WORLD.pipeCapHeight} rx="2" />
                </g>
              })}
              {[...remotePlayers.values()].map((player, index) => {
                const remoteAngle = player.phase === 'over' ? 65 : Math.max(-25, Math.min(85, player.velocity * 0.15))
                return <g key={index} transform={`translate(${WORLD.birdX} ${player.birdY}) rotate(${remoteAngle})`} opacity="0.35" stroke="#332751" strokeWidth="2" strokeLinejoin="round">
                  <circle r={WORLD.radius} fill="#cbbdff" />
                  <path d="M-10 5 Q0 15 9 5" fill="#aa94f0" stroke="none" />
                  <ellipse cx="-9" cy="3" rx="8" ry="5" fill="#eeeaff" />
                  <ellipse cx="6" cy="-5" rx="6" ry="7" fill="white" /><path d="M8-6v3" stroke="#292b2f" strokeWidth="3" />
                  <path d="M9 2H20V6H9Z" fill="#8667dc" />
                </g>
              })}
              <g data-bird="" transform={`translate(${WORLD.birdX} ${game.birdY}) rotate(${angle})`} stroke="#65432e" strokeWidth="2" strokeLinejoin="round">
                <circle r={WORLD.radius} fill="#f9d848" />
                <path d="M-10 5 Q0 15 9 5" fill="#f8b839" stroke="none" />
                <ellipse cx="-9" cy={wingY + 3} rx="8" ry="5" fill="#fff2a7" />
                <ellipse cx="6" cy="-5" rx="6" ry="7" fill="white" /><path d="M8-6v3" stroke="#292b2f" strokeWidth="3" />
                <path d="M9 2H20V6H9Z" fill="#ed783e" />
              </g>
              <g>
                <rect y={WORLD.groundY} width="360" height="76" fill="#ddda9f" />
                <rect y={WORLD.groundY} width="360" height="14" fill="#94cd45" />
                <g transform={`translate(${-game.distance % 24} 0)`} fill="#d8eb86">
                  {Array.from({ length: 17 }, (_, i) => <path key={i} d={`M${i * 24} 466h12l-8 10h-12z`} />)}
                </g>
                <path d="M0 464H360M0 479H360" stroke="#496430" strokeWidth="3" />
                <path d="M0 485H360" stroke="#c6b97f" strokeWidth="3" />
              </g>
              {playing && <text x="180" y="67" textAnchor="middle" fill="white" stroke="#37505b" strokeWidth="4" paintOrder="stroke" fontFamily="monospace" fontWeight="900" fontSize="46">{game.score}</text>}
            </svg>

            {!playing && <span className="absolute inset-0 flex flex-col items-center justify-center bg-[#17393b]/15 px-5">
              <span className="w-full rounded-xl border-2 border-[#584830] bg-[#f4e9bb] p-5 text-center text-[#584830] shadow-[0_5px_0_#584830]">
                <span className="block font-mono text-2xl font-black tracking-tight">{ended ? 'HẾT LƯỢT!' : paused ? 'TẠM DỪNG' : onlineRound ? 'CÙNG BAY!' : 'SẴN SÀNG?'}</span>
                <span className="mt-2 block text-sm">{ended ? `Bạn đã vượt qua ${game.score} ống` : paused ? 'Chuyến bay đang đợi bạn.' : onlineRound ? 'Vòng chơi chung sẽ tự bắt đầu.' : 'Giữ nhịp bay, vượt qua những chiếc ống.'}</span>
                {ended && <span className="mt-2 block text-sm font-bold">Kỷ lục: {bestScore}</span>}
                <span className="mx-auto mt-5 block w-fit rounded-md border-2 border-[#86472f] bg-[#e98442] px-5 py-2 text-sm font-extrabold text-white shadow-[0_3px_0_#86472f]">{onlineRound ? 'ĐANG ĐẾM NGƯỢC' : ended ? 'CHƠI LẠI' : paused ? 'TIẾP TỤC' : 'CHẠM ĐỂ BAY'}</span>
              </span>
              <span className="mt-5 text-xs font-semibold text-[#234a43]">CLICK / CHẠM / SPACE</span>
            </span>}
          </button>
          <div className="mt-4 flex justify-center gap-3">
            <button type="button" disabled={game.phase === 'ready' || ended || Boolean(onlineRound)} onClick={() => { if (paused) play(); else pause() }} className="rounded-lg border border-border bg-surface-2 px-4 py-2 text-sm font-semibold text-text hover:bg-surface-hover disabled:opacity-40">{paused ? 'Tiếp tục' : 'Tạm dừng'}</button>
            <button type="button" onClick={() => { leaveOnlineRound(); stageRef.current?.focus({ preventScroll: true }) }} className="rounded-lg border border-border bg-surface-2 px-4 py-2 text-sm font-semibold text-text hover:bg-surface-hover">Chơi đơn</button>
          </div>
        </div>
        <div id={`${id}-instructions`} className="space-y-5 text-sm leading-6 text-text-muted">
          <div><h3 className="mb-2 font-heading text-lg font-bold text-text">Giữ nhịp, bay xa</h3><p>Chạm vào khung game để bắt đầu. Mỗi lần chạm hoặc nhấn <kbd className="rounded border border-border bg-surface-2 px-1.5 py-0.5 font-mono text-xs text-text">Space</kbd> / <kbd className="rounded border border-border bg-surface-2 px-1.5 py-0.5 font-mono text-xs text-text">↑</kbd>, chim sẽ vỗ cánh một lần.</p></div>
          <p>Luồn qua khoảng trống giữa hai ống để được 1 điểm. Chạm ống, trần hoặc mặt đất là hết lượt.</p>
          <p>Nhấn <kbd className="rounded border border-border bg-surface-2 px-1.5 py-0.5 font-mono text-xs text-text">P</kbd> / <kbd className="rounded border border-border bg-surface-2 px-1.5 py-0.5 font-mono text-xs text-text">Esc</kbd> để tạm dừng. Game cũng tự dừng khi bạn chuyển tab hoặc rời vùng chơi.</p>
          <div className="rounded-xl border border-primary/30 bg-primary/10 p-4">
            <div><h3 className="font-heading text-base font-bold text-text">Chơi cùng online</h3><p className="mt-1">{isOnlineConnected ? 'Tạo phòng rồi gửi mã cho bạn bè để cùng vào một màn.' : 'Đang kết nối máy chủ game…'} Chim đối thủ có màu tím mờ.</p></div>
            {!room ? <div className="mt-3 flex flex-wrap gap-2">
              <button type="button" onClick={createRoom} disabled={!isOnlineConnected} className="rounded-lg bg-primary px-4 py-2 text-sm font-bold text-white transition hover:bg-primary-dark disabled:opacity-50">Tạo phòng</button>
              <label className="flex min-w-48 flex-1 items-center rounded-lg border border-border bg-bg px-3 focus-within:border-primary"><span className="sr-only">Mã phòng</span><input value={roomCodeDraft} maxLength={6} onChange={(event) => setRoomCodeDraft(event.target.value.toUpperCase())} placeholder="Nhập mã phòng" className="min-w-0 flex-1 bg-transparent font-mono font-bold tracking-[0.18em] text-text outline-none placeholder:font-sans placeholder:font-normal placeholder:tracking-normal placeholder:text-text-light" /><button type="button" onClick={joinRoom} disabled={!isOnlineConnected || roomCodeDraft.trim().length !== 6} className="text-sm font-bold text-primary disabled:opacity-50">Vào phòng</button></label>
            </div> : <div className="mt-3 rounded-lg border border-border bg-bg p-3">
              <div className="flex flex-wrap items-center justify-between gap-3"><div><p className="text-xs text-text-muted">Mã phòng</p><p className="font-mono text-xl font-black tracking-[0.2em] text-primary-light">{room.code}</p></div><div className="text-right text-sm text-text-muted"><p>{room.isHost ? 'Bạn là chủ phòng' : 'Đã vào phòng'}</p><p>{Math.max(0, room.playerCount - 1)} người chơi khác</p></div></div>
              <div className="mt-3 flex flex-wrap gap-2">{room.isHost && <button type="button" onClick={startHostedRound} className="rounded-lg bg-primary px-4 py-2 text-sm font-bold text-white transition hover:bg-primary-dark">Bắt đầu vòng chung</button>}<button type="button" onClick={leaveOnlineRound} className="rounded-lg border border-border bg-surface-2 px-4 py-2 text-sm font-semibold text-text transition hover:bg-surface-hover">Rời phòng</button></div>
            </div>}
            {onlineError && <p role="alert" className="mt-3 text-sm text-danger">{onlineError}</p>}
          </div>
          <p className="rounded-xl border border-border bg-bg p-4">Mẹo nhỏ: bấm nhẹ, đều tay và nhìn chiếc ống tiếp theo. Kỷ lục được lưu trên trình duyệt này.</p>
          <p role="status" className="sr-only">{ended ? `Hết lượt. Điểm ${game.score}. Kỷ lục ${bestScore}.` : paused ? 'Game đang tạm dừng.' : playing ? 'Đang chơi Flappy Bird.' : 'Flappy Bird sẵn sàng.'}</p>
        </div>
      </div>
    </section>
  )
}
