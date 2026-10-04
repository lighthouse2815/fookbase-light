import { Component, useCallback, useEffect, useRef, useState, type ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { GameScene, type GameControls } from './GameScene'
import type { GameDiagnostics, GamePhase, GameProgress, GameRuntime, InteractionId } from './types'
import { createPlayer } from './player/PlayerController'
import { createProgress } from './gameplay/ObjectiveSystem'
import { GameAudio } from './gameplay/audio'
import { useKeyboard } from './hooks/useKeyboard'
import { GameHUD } from './ui/GameHUD'
import { GameMenus } from './ui/GameMenus'
import './game.css'

class SceneBoundary extends Component<{ children: ReactNode; onError: () => void }, { failed: boolean }> {
  state = { failed: false }
  static getDerivedStateFromError() { return { failed: true } }
  componentDidCatch() { this.props.onError() }
  render() { return this.state.failed ? null : this.props.children }
}

const initialDiagnostics: GameDiagnostics = { elapsed: 0, fps: 0, calls: 0, position: [0, 0, 19] }

export default function Game() {
  const [phase, setPhase] = useState<GamePhase>('ready')
  const [progress, setProgress] = useState(createProgress)
  const [prompt, setPrompt] = useState<InteractionId | null>(null)
  const [diagnostics, setDiagnostics] = useState(initialDiagnostics)
  const [ready, setReady] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [fatal, setFatal] = useState(false)
  const [muted, setMuted] = useState(false)
  const [runId, setRunId] = useState(0)
  const viewport = useRef<HTMLDivElement>(null)
  const controls = useRef<GameControls>(null)
  const audio = useRef<GameAudio | null>(null)
  const runtime = useRef<GameRuntime>({ player: createPlayer(), progress, phase: 'ready', elapsed: 0, eventTime: 0 })
  const keyboard = useKeyboard(phase === 'playing')

  const pause = useCallback(() => {
    if (runtime.current.phase !== 'playing') return
    runtime.current.phase = 'paused'
    setPhase('paused')
    setPrompt(null)
    audio.current?.pause()
    controls.current?.unlock()
  }, [])

  const start = () => {
    if (!ready || fatal) return
    if (!controls.current || typeof viewport.current?.querySelector('canvas')?.requestPointerLock !== 'function') {
      setError('Trình duyệt chưa hỗ trợ điều khiển chuột. Hãy dùng Chrome, Edge hoặc Firefox trên máy tính.')
      return
    }
    setError(null)
    try {
      audio.current ??= new GameAudio()
      audio.current.setMuted(muted)
      void audio.current.start().catch(() => undefined)
      controls.current.lock()
    } catch {
      setError('Chưa thể giữ chuột. Hãy bấm Start Game hoặc Tiếp tục một lần nữa.')
      audio.current?.pause()
    }
  }

  const onLock = useCallback(() => { runtime.current.phase = 'playing'; setPhase('playing') }, [])
  const onReady = useCallback(() => setReady(true), [])
  const onProgress = useCallback((next: GameProgress) => {
    const previous = runtime.current.progress
    if (next === previous) return
    runtime.current.progress = next
    setProgress(next)
    if (next.hasKey && !previous.hasKey) audio.current?.cue('item')
    else if (next.eventTriggered && !previous.eventTriggered) audio.current?.cue('event')
    else if (next.powerOn && !previous.powerOn) audio.current?.cue('power')
    else if (next.doorOpen !== previous.doorOpen) audio.current?.cue('door')
    else if (next.stage === 'complete') audio.current?.cue('signal')
  }, [])

  const complete = useCallback(() => {
    runtime.current.phase = 'complete'
    setPhase('complete')
    setDiagnostics((value) => ({ ...value, elapsed: runtime.current.elapsed }))
    controls.current?.unlock()
    audio.current?.pause()
  }, [])

  const replay = () => {
    controls.current?.unlock()
    audio.current?.pause()
    const next = createProgress()
    runtime.current = { player: createPlayer(), progress: next, phase: 'ready', elapsed: 0, eventTime: 0 }
    setProgress(next); setPhase('ready'); setPrompt(null); setDiagnostics(initialDiagnostics); setError(null)
    setRunId((value) => value + 1)
  }

  const webglError = useCallback(() => {
    pause()
    setFatal(true)
    setError('Không thể khởi tạo đồ họa WebGL. Hãy bật tăng tốc phần cứng trong trình duyệt rồi tải lại trang.')
  }, [pause])

  useEffect(() => {
    const container = viewport.current
    const escape = (event: KeyboardEvent) => { if (event.code === 'Escape') pause() }
    const hidden = () => { if (document.hidden) pause() }
    const lockError = () => {
      pause()
      audio.current?.pause()
      setError('Trình duyệt chưa cho phép giữ chuột. Bấm Start Game hoặc Tiếp tục để thử lại.')
    }
    window.addEventListener('keydown', escape)
    window.addEventListener('blur', pause)
    document.addEventListener('visibilitychange', hidden)
    document.addEventListener('pointerlockerror', lockError)
    return () => {
      runtime.current.phase = 'paused'
      if (container?.contains(document.pointerLockElement)) document.exitPointerLock()
      audio.current?.dispose()
      window.removeEventListener('keydown', escape)
      window.removeEventListener('blur', pause)
      document.removeEventListener('visibilitychange', hidden)
      document.removeEventListener('pointerlockerror', lockError)
    }
  }, [pause])

  useEffect(() => {
    const canvas = viewport.current?.querySelector('canvas')
    if (!canvas) return
    const lost = (event: Event) => { event.preventDefault(); pause(); setFatal(true); setError('Kết nối WebGL bị gián đoạn. Chờ đồ họa phục hồi hoặc tải lại trang.') }
    const restored = () => { setFatal(false); setError(null) }
    canvas.addEventListener('webglcontextlost', lost)
    canvas.addEventListener('webglcontextrestored', restored)
    return () => { canvas.removeEventListener('webglcontextlost', lost); canvas.removeEventListener('webglcontextrestored', restored) }
  }, [ready, pause])

  const toggleMute = () => { const next = !muted; setMuted(next); audio.current?.setMuted(next) }

  return <div className="signal-page">
    <header className="signal-page-header"><Link to="/games">← Chơi game</Link><div><span>FOOKBASE PLAY</span><i>/</i><strong>Tần số 0</strong></div><span className="signal-page-badge"><span /> KHÁM PHÁ 3D</span></header>
    <div ref={viewport} className="signal-viewport" aria-label="Game Tần số 0">
      <SceneBoundary onError={webglError}><GameScene runtime={runtime} progress={progress} phase={phase} keyboard={keyboard} controls={controls} runId={runId} onReady={onReady} onLock={onLock} onUnlock={pause} onProgress={onProgress} onPrompt={setPrompt} onComplete={complete} onDiagnostics={setDiagnostics} /></SceneBoundary>
      <div className="signal-vignette" aria-hidden="true" />
      <GameHUD phase={phase} progress={progress} prompt={prompt} diagnostics={diagnostics} muted={muted} onPause={pause} onMute={toggleMute} />
      <GameMenus phase={phase} ready={ready} error={error} fatal={fatal} elapsed={diagnostics.elapsed} muted={muted} onMute={toggleMute} onStart={start} onReplay={replay} />
    </div>
    <footer className="signal-page-footer"><span><span className="signal-live-dot" /> Trải nghiệm khám phá góc nhìn thứ nhất</span><span>Tai nghe & bàn phím được khuyên dùng <span aria-hidden="true">↗</span></span></footer>
  </div>
}
