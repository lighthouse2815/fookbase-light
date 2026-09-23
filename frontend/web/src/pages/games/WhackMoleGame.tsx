import { useCallback, useEffect, useRef, useState } from 'react'
import { advanceMole, createMoleGame, hitMole, MOLE_ROUND_MS, type MoleState } from './whackMoleEngine'

export default function WhackMoleGame() {
  const [game, setGame] = useState(createMoleGame)
  const gameRef = useRef(game)
  const previousRef = useRef(0)
  const stageRef = useRef<HTMLDivElement>(null)
  const playing = game.phase === 'playing'
  const commit = useCallback((next: MoleState) => { gameRef.current = next; setGame(next) }, [])
  const updateClock = useCallback(() => {
    const now = performance.now()
    const next = advanceMole(gameRef.current, now - previousRef.current)
    previousRef.current = now
    return next
  }, [])
  const pause = useCallback(() => {
    if (gameRef.current.phase !== 'playing') return
    const next = updateClock()
    commit(next.phase === 'over' ? next : { ...next, phase: 'paused' })
  }, [commit, updateClock])
  const start = useCallback(() => {
    previousRef.current = performance.now()
    const next = gameRef.current.phase === 'paused' ? gameRef.current : createMoleGame()
    commit(advanceMole({ ...next, phase: 'playing' }, 0))
    stageRef.current?.focus({ preventScroll: true })
  }, [commit])
  const hit = (index: number) => { if (gameRef.current.phase === 'playing') commit(hitMole(updateClock(), index)) }

  useEffect(() => {
    if (!playing) return
    const timer = window.setInterval(() => commit(updateClock()), 50)
    return () => window.clearInterval(timer)
  }, [commit, playing, updateClock])
  useEffect(() => {
    const visibilityChange = () => { if (document.hidden) pause() }
    window.addEventListener('blur', pause)
    document.addEventListener('visibilitychange', visibilityChange)
    return () => { window.removeEventListener('blur', pause); document.removeEventListener('visibilitychange', visibilityChange) }
  }, [pause])

  return <section aria-label="Đập chuột" className="game-detail-panel rounded-3xl border border-border bg-surface p-5 card-shadow sm:p-8"
    onBlur={(event) => { if (!event.currentTarget.contains(event.relatedTarget)) pause() }}
    onKeyDown={(event) => {
      if (/^[1-9]$/.test(event.key)) { event.preventDefault(); if (!event.repeat) hit(Number(event.key) - 1) }
      else if (event.key.toLowerCase() === 'p' || event.key === 'Escape') { event.preventDefault(); if (!event.repeat) { if (gameRef.current.phase === 'paused') start(); else pause() } }
    }}>
    <div className="mb-6 flex flex-wrap items-center justify-between gap-4">
      <div><h2 className="font-heading text-2xl font-bold text-text">Nhanh tay bắt chuột!</h2><p className="mt-1 text-sm text-text-muted">30 giây bảo vệ khu vườn, bạn bắt được bao nhiêu?</p></div>
      <div className="flex gap-3 text-center"><div className="rounded-xl bg-primary/15 px-4 py-2"><p className="text-xs text-text-muted">Điểm</p><p className="text-2xl font-bold text-primary-light">{game.score}</p></div><div className="rounded-xl bg-surface-2 px-4 py-2"><p className="text-xs text-text-muted">Còn lại</p><p className="text-2xl font-bold text-text">{Math.ceil(game.remaining / 1000)}s</p></div></div>
    </div>
    <div className="grid items-center gap-8 md:grid-cols-[minmax(0,420px)_1fr]">
      <div>
        <div className="mb-4 flex flex-wrap gap-3"><button type="button" onClick={() => { if (playing) pause(); else start() }} className="rounded-xl bg-primary px-5 py-2.5 text-sm font-semibold text-white hover:bg-primary-dark">{playing ? 'Tạm dừng' : game.phase === 'paused' ? 'Tiếp tục' : game.phase === 'over' ? 'Chơi lại' : 'Bắt đầu'}</button><button type="button" onClick={() => commit(createMoleGame())} className="rounded-xl border border-border bg-surface-2 px-5 py-2.5 text-sm font-semibold text-text hover:bg-surface-hover">Ván mới</button></div>
        <div ref={stageRef} tabIndex={0} role="group" aria-label="Vườn chuột, dùng phím 1 đến 9 tương ứng các hang" className="grid grid-cols-3 gap-3 rounded-3xl border-4 border-[#528238] bg-linear-to-br from-[#c5e99b] to-[#78ad55] p-3 focus-visible:outline-4 focus-visible:outline-offset-4 focus-visible:outline-primary sm:p-5">
          {Array.from({ length: 9 }, (_, index) => <button key={index} type="button" disabled={!playing} aria-label={`Hang ${index + 1}${playing && game.target === index ? ': có chuột' : ''}`} onClick={() => hit(index)} className="relative aspect-square touch-manipulation select-none rounded-2xl focus-visible:outline-4 focus-visible:outline-[#6d28d9] enabled:active:scale-95">
            <span className="absolute inset-x-0 bottom-1 h-1/2 rounded-[50%] border-b-4 border-[#d0aa71] bg-[#5c3b25] shadow-inner" />
            <span className={`relative block text-4xl transition-transform duration-100 motion-reduce:transition-none sm:text-5xl ${playing && game.target === index ? '-translate-y-1 scale-110' : 'scale-75'}`} aria-hidden="true">{playing && game.target === index ? '🐹' : game.lastHit === index && playing ? '✨' : ' '}</span>
            <span className="absolute bottom-1 left-1/2 -translate-x-1/2 text-xs font-bold text-[#ffedc7]" aria-hidden="true">{index + 1}</span>
          </button>)}
        </div>
        <div role="progressbar" aria-label="Thời gian còn lại" aria-valuemin={0} aria-valuemax={30} aria-valuenow={Math.ceil(game.remaining / 1000)} className="mt-4 h-2 overflow-hidden rounded-full bg-surface-2"><div className="h-full rounded-full bg-secondary" style={{ width: `${game.remaining / MOLE_ROUND_MS * 100}%` }} /></div>
      </div>
      <div className="space-y-5 text-sm leading-6 text-text-muted">
        <div><h3 className="mb-2 font-heading text-lg font-bold text-text">Nhìn nhanh, chạm đúng</h3><p>Chạm vào chú chuột vừa xuất hiện để được 10 điểm. Chuột càng lúc càng nhanh khi điểm tăng. Chạm hang trống được tính là một lần hụt.</p></div>
        <p>Dùng chuột, cảm ứng hoặc phím 1–9 theo số trên hang. Nhấn P / Esc để tạm dừng hoặc tiếp tục. Chuyển tab hay rời vùng chơi cũng sẽ tự tạm dừng.</p>
        <p className="rounded-xl border border-border bg-bg p-4">Bắt trúng: <strong className="text-text">{game.score / 10}</strong> · Chạm hụt: <strong className="text-text">{game.misses}</strong></p>
        <div role="status" className="rounded-xl border border-primary/30 bg-primary/10 p-4 text-text">{game.phase === 'over' ? `🎉 Hết giờ! Bạn bắt được ${game.score / 10} chú chuột, đạt ${game.score} điểm.` : game.phase === 'paused' ? 'Đã tạm dừng. Khu vườn đang đợi bạn quay lại.' : playing ? 'Chuột đang xuất hiện. Bắt nhanh nào!' : 'Bấm Bắt đầu để thử thách phản xạ trong 30 giây.'}</div>
      </div>
    </div>
  </section>
}
