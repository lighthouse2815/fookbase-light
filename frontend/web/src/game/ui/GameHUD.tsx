import type { GameDiagnostics, GamePhase, GameProgress, InteractionId } from '../types'
import { getObjective } from '../gameplay/ObjectiveSystem'
import { INTERACTIONS } from '../world/worldData'
import { formatTime } from './formatTime'

interface HUDProps {
  phase: GamePhase
  progress: GameProgress
  prompt: InteractionId | null
  diagnostics: GameDiagnostics
  muted: boolean
  onPause: () => void
  onMute: () => void
}

export function GameHUD({ phase, progress, prompt, diagnostics, muted, onPause, onMute }: HUDProps) {
  const objective = getObjective(progress)
  const interaction = INTERACTIONS.find((item) => item.id === prompt)
  if (phase === 'ready') return null
  return <div className="signal-hud" data-phase={phase}>
    <section className="signal-objective" aria-label="Mục tiêu" aria-live="polite">
      <div className="signal-eyebrow"><span className="signal-live-dot" /> MỤC TIÊU <span>{String(objective.step).padStart(2, '0')} / 05</span></div>
      <h2>{objective.title}</h2><p>{objective.hint}</p>
      <div className="signal-progress">{[1, 2, 3, 4, 5].map((step) => <span key={step} className={step <= objective.step ? 'is-active' : ''} />)}</div>
    </section>
    <div className="signal-inventory"><span>TRẠM 07</span><strong>{formatTime(diagnostics.elapsed)}</strong><div className={progress.hasKey ? 'signal-item is-found' : 'signal-item'}>{progress.hasKey ? '⚿ Chìa khóa nhà trạm' : '⚿ Chưa có chìa khóa'}</div></div>
    {phase === 'playing' && <>
      <div className={`signal-crosshair${prompt ? ' is-interactive' : ''}`} aria-hidden="true"><span /><span /></div>
      <div className="signal-interaction" aria-live="polite">{interaction && <><kbd>E</kbd><span>Interact<strong>{interaction.label}</strong></span></>}</div>
      {progress.message && <p className="signal-message" key={progress.message}>{progress.message}</p>}
    </>}
    <div className="signal-hud-bottom"><span><i /> ĐÈN PIN ĐANG BẬT</span><div><button type="button" onClick={onMute} aria-pressed={muted}>{muted ? 'Âm thanh: tắt' : 'Âm thanh: bật'}</button><button type="button" onClick={onPause}><kbd>ESC</kbd> Tạm dừng</button></div></div>
    {import.meta.env.DEV && <output className="signal-debug" aria-label="Thông tin development">{diagnostics.fps} FPS · {diagnostics.calls} draw calls · XYZ {diagnostics.position.map((value) => value.toFixed(2)).join(' / ')}</output>}
  </div>
}
