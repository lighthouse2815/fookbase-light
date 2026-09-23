import { useCallback, useEffect, useRef, useState, type CSSProperties } from 'react'
import GameSoundToggle from './GameSoundToggle'
import { useGameSound } from './useGameSound'
import { answerMelody, createMelody, finishMelodyPlayback, MELODY_ROUNDS, nextMelody, type MelodyState } from './melodyEngine'
import './arcade.css'

const notes = [
  { name: 'Đô', frequency: 261.63, color: '#ffab8a', symbol: '●' },
  { name: 'Mi', frequency: 329.63, color: '#ffe394', symbol: '✦' },
  { name: 'Sol', frequency: 392, color: '#a3e2d2', symbol: '◆' },
  { name: 'Si', frequency: 493.88, color: '#a9caff', symbol: '■' },
]

export default function MelodyGame() {
  const [game, setGame] = useState(createMelody)
  const [activeNote, setActiveNote] = useState<number | null>(null)
  const [paused, setPaused] = useState(false)
  const gameRef = useRef(game)
  const stageRef = useRef<HTMLDivElement>(null)
  const flashRef = useRef<number | undefined>(undefined)
  const { enabled, toggle, play, stop } = useGameSound()
  const commit = useCallback((next: MelodyState) => { gameRef.current = next; setGame(next) }, [])
  const running = ['listening', 'input', 'between'].includes(game.phase)

  const pause = useCallback(() => {
    if (['listening', 'input', 'between'].includes(gameRef.current.phase)) {
      setPaused(true)
      setActiveNote(null)
      stop()
    }
  }, [stop])

  useEffect(() => {
    if (paused || game.phase !== 'listening') return
    const spacing = Math.max(430, 760 - game.completed * 25)
    const timers: number[] = []
    game.sequence.forEach((note, index) => {
      timers.push(window.setTimeout(() => { setActiveNote(note); play(notes[note].frequency, 0.3) }, 450 + index * spacing))
      timers.push(window.setTimeout(() => setActiveNote(null), 450 + index * spacing + 300))
    })
    timers.push(window.setTimeout(() => commit(finishMelodyPlayback(gameRef.current)), 450 + game.sequence.length * spacing))
    return () => { timers.forEach(window.clearTimeout); stop() }
  }, [commit, game.completed, game.phase, game.sequence, paused, play, stop])

  useEffect(() => {
    if (paused || game.phase !== 'between') return
    const timer = window.setTimeout(() => commit(nextMelody(gameRef.current)), 800)
    return () => window.clearTimeout(timer)
  }, [commit, game.phase, paused])

  useEffect(() => {
    const visibility = () => { if (document.hidden) pause() }
    window.addEventListener('blur', pause)
    document.addEventListener('visibilitychange', visibility)
    return () => { window.removeEventListener('blur', pause); document.removeEventListener('visibilitychange', visibility); window.clearTimeout(flashRef.current) }
  }, [pause])

  const start = () => {
    window.clearTimeout(flashRef.current)
    stop()
    play(523.25, 0.1)
    setPaused(false)
    setActiveNote(null)
    commit(nextMelody(createMelody()))
    stageRef.current?.focus({ preventScroll: true })
  }
  const answer = (note: number) => {
    if (paused || gameRef.current.phase !== 'input') return
    const next = answerMelody(gameRef.current, note)
    play(next.phase === 'over' ? 130.81 : notes[note].frequency, next.phase === 'over' ? 0.5 : 0.22)
    setActiveNote(note)
    window.clearTimeout(flashRef.current)
    flashRef.current = window.setTimeout(() => setActiveNote(null), 200)
    commit(next)
  }
  const status = paused ? 'Đã tạm dừng' : game.phase === 'listening' ? 'Lắng nghe & ghi nhớ' : game.phase === 'input' ? 'Đến lượt bạn!' : game.phase === 'between' ? 'Chính xác. Thêm một nốt nhé!' : game.phase === 'over' ? 'Lệch một nhịp rồi!' : game.phase === 'won' ? 'Bạn là nhạc trưởng!' : 'Một chút nhạc, một chút trí nhớ'

  return <section aria-label="Giai điệu sắc màu" className="arcade-shell melody-game" onBlur={(event) => { if (!event.currentTarget.contains(event.relatedTarget)) pause() }} onKeyDown={(event) => {
    if (/^[1-4]$/.test(event.key)) { event.preventDefault(); if (!event.repeat) answer(Number(event.key) - 1) }
    if (event.key === 'Escape') { event.preventDefault(); pause() }
  }}>
    <header className="arcade-heading"><div><p className="arcade-eyebrow">SOUND LAB / 01</p><h2>Giai điệu sắc màu</h2><p>Lắng nghe. Ghi nhớ. Chạm đúng nhịp.</p></div><GameSoundToggle enabled={enabled} onToggle={toggle} /></header>
    <div className="arcade-layout">
      <div ref={stageRef} tabIndex={0} role="group" aria-label="Bàn nhạc, dùng phím 1 đến 4" aria-describedby="melody-instructions" className="melody-stage arcade-stage">
        <div className="melody-stage-top"><span>FOOKBASE · SOUND MACHINE</span><span className={running && !paused ? 'melody-led is-on' : 'melody-led'} /></div>
        <div className="melody-display"><p>{paused ? 'PAUSED' : game.phase === 'input' ? 'YOUR TURN' : game.phase === 'listening' ? 'LISTEN' : game.phase === 'won' ? 'BRAVO!' : 'PLAY A LITTLE'}</p><strong>{String(game.sequence.length).padStart(2, '0')}<small> / 12</small></strong><div className="melody-equalizer" aria-hidden="true">{[0, 1, 2, 3, 4, 5, 6].map((bar) => <i key={bar} style={{ height: activeNote !== null && !paused ? `${14 + (bar * 17 + activeNote * 13) % 32}px` : '5px' }} />)}</div></div>
        <div className="melody-pads">{notes.map((note, index) => <button key={note.name} type="button" aria-label={`Nốt ${note.name}, phím ${index + 1}`} aria-disabled={game.phase !== 'input' || paused} onClick={() => answer(index)} style={{ '--note-color': note.color } as CSSProperties} className={`melody-pad ${activeNote === index && !paused ? 'is-lit' : ''}`}><span className="melody-pad-number">0{index + 1}</span><span className="melody-pad-symbol" aria-hidden="true">{note.symbol}</span><span className="melody-pad-name">{note.name}</span></button>)}</div>
        <div className="melody-sequence" aria-label={`Đã nhập ${game.cursor}/${game.sequence.length} nốt`}>{Array.from({ length: Math.max(1, game.sequence.length) }, (_, index) => <i key={index} className={game.phase !== 'listening' && index < game.cursor ? 'is-complete' : ''} />)}</div>
      </div>
      <aside id="melody-instructions" className="arcade-aside"><span className="arcade-tag">TRÍ NHỚ + ÂM NHẠC</span><h3>Nhớ bằng tai.<br />Chơi bằng cảm giác.</h3><p>Máy phát một chuỗi nốt nhạc. Chạm lại các ô theo đúng thứ tự. Mỗi vòng thêm một nốt, cùng bạn đi tới vòng 12.</p><p>Dùng phím <kbd>1</kbd> – <kbd>4</kbd> hoặc chạm các ô. Tắt tiếng vẫn chơi được bằng ánh sáng và biểu tượng.</p>
        <div className="arcade-stats"><div><span>ĐÃ VƯỢT QUA</span><strong>{game.completed}<small> / {MELODY_ROUNDS}</small></strong></div><div><span>CHUỖI HIỆN TẠI</span><strong>{game.sequence.length}<small> nốt</small></strong></div></div>
        <div className="arcade-status" role="status"><strong>{status}</strong><p>{paused ? 'Tiếp tục để trở lại. Chuỗi đang phát sẽ được nghe lại từ đầu.' : game.phase === 'over' ? `Bạn vượt qua ${game.completed} vòng. Thử lại để tiến xa hơn nhé.` : game.phase === 'won' ? '12 vòng trọn vẹn. Một trí nhớ thật đáng nể!' : game.phase === 'input' ? `Còn ${game.sequence.length - game.cursor} nốt nữa. Không cần vội.` : 'Những ô sáng chính là gợi ý cho giai điệu của bạn.'}</p></div>
        <div className="arcade-actions">{running ? <><button type="button" className="arcade-button arcade-primary" onClick={() => { if (paused) { play(523.25, 0.1); setPaused(false); stageRef.current?.focus({ preventScroll: true }) } else pause() }}>{paused ? 'Tiếp tục' : 'Tạm dừng'}</button><button type="button" className="arcade-button" onClick={start}>Chơi lại</button></> : <button type="button" className="arcade-button arcade-primary" onClick={start}>{game.phase === 'ready' ? 'Bắt đầu giai điệu' : 'Thử giai điệu mới'} <span aria-hidden="true">↗</span></button>}</div>
        <p className="arcade-footnote">Tự tạm dừng khi bạn chuyển tab hoặc rời vùng chơi.</p>
      </aside>
    </div>
  </section>
}
