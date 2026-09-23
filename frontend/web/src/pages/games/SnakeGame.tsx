import { useCallback, useEffect, useId, useRef, useState } from 'react'
import { createSnake, SNAKE_SIZE, stepSnake, turnSnake, type SnakeDirection, type SnakeState } from './snakeEngine'

const keyDirections: Record<string, SnakeDirection> = { ArrowUp: 'up', ArrowDown: 'down', ArrowLeft: 'left', ArrowRight: 'right', w: 'up', s: 'down', a: 'left', d: 'right' }
const controls: { direction: SnakeDirection; label: string; symbol: string }[] = [
  { direction: 'up', label: 'Đi lên', symbol: '↑' }, { direction: 'left', label: 'Sang trái', symbol: '←' },
  { direction: 'down', label: 'Đi xuống', symbol: '↓' }, { direction: 'right', label: 'Sang phải', symbol: '→' },
]
const angles: Record<SnakeDirection, number> = { right: 0, down: 90, left: 180, up: 270 }

export default function SnakeGame() {
  const [game, setGame] = useState(() => createSnake())
  const gameRef = useRef(game)
  const stageRef = useRef<HTMLDivElement>(null)
  const id = useId()
  const playing = game.phase === 'playing'
  const paused = game.phase === 'paused'
  const ended = game.phase === 'over' || game.phase === 'won'
  const speed = Math.max(85, 180 - game.score * 5)
  const commitGame = useCallback((next: SnakeState) => { gameRef.current = next; setGame(next) }, [])
  const pause = useCallback(() => {
    if (gameRef.current.phase === 'playing') commitGame({ ...gameRef.current, phase: 'paused' })
  }, [commitGame])
  const start = () => {
    commitGame({ ...(gameRef.current.phase === 'paused' ? gameRef.current : createSnake()), phase: 'playing' })
    stageRef.current?.focus({ preventScroll: true })
  }
  const turn = (direction: SnakeDirection) => commitGame(turnSnake(gameRef.current, direction))

  useEffect(() => {
    if (!playing) return
    const timer = window.setInterval(() => commitGame(stepSnake(gameRef.current)), speed)
    return () => window.clearInterval(timer)
  }, [commitGame, playing, speed])

  useEffect(() => {
    const visibilityChange = () => { if (document.hidden) pause() }
    window.addEventListener('blur', pause)
    document.addEventListener('visibilitychange', visibilityChange)
    return () => { window.removeEventListener('blur', pause); document.removeEventListener('visibilitychange', visibilityChange) }
  }, [pause])

  return <section aria-label="Rắn săn mồi" className="game-detail-panel rounded-3xl border border-border bg-surface p-5 card-shadow sm:p-8"
    onBlur={(event) => { if (!event.currentTarget.contains(event.relatedTarget)) pause() }}
    onKeyDown={(event) => {
      const direction = keyDirections[event.key] ?? keyDirections[event.key.toLowerCase()]
      if (direction) { event.preventDefault(); if (!event.repeat) turn(direction) }
      else if (event.key === 'Escape' || event.key.toLowerCase() === 'p') { event.preventDefault(); if (!event.repeat) { if (gameRef.current.phase === 'paused') start(); else pause() } }
      else if (event.code === 'Space' && event.target === stageRef.current) { event.preventDefault(); if (!event.repeat) { if (playing) pause(); else start() } }
    }}>
    <div className="mb-6 flex flex-wrap items-center justify-between gap-4">
      <div><h2 className="font-heading text-2xl font-bold text-text">Ăn thêm một miếng!</h2><p className="mt-1 text-sm text-text-muted">Chú rắn nhỏ, chiếc bụng không đáy.</p></div>
      <div className="game-stat-strip flex gap-3 text-center"><div className="rounded-xl bg-primary/15 px-4 py-2"><p className="text-xs text-text-muted">Táo đã ăn</p><p className="text-2xl font-bold text-primary-light">{game.score}</p></div><div className="rounded-xl bg-surface-2 px-4 py-2"><p className="text-xs text-text-muted">Độ dài</p><p className="text-2xl font-bold text-text">{game.body.length}</p></div></div>
    </div>
    <div className="game-play-grid grid items-center gap-8">
      <div>
        <div ref={stageRef} tabIndex={0} role="group" aria-label="Bàn rắn săn mồi, điều khiển bằng phím mũi tên hoặc WASD" aria-describedby={`${id}-instructions`}
          className="relative overflow-hidden rounded-2xl border-4 border-[#284937] bg-[#10271f] focus-visible:outline-4 focus-visible:outline-offset-4 focus-visible:outline-primary">
          <svg viewBox={`0 0 ${SNAKE_SIZE * 20} ${SNAKE_SIZE * 20}`} className="block aspect-square w-full" aria-hidden="true">
            <defs><pattern id={`${id}-grid`} width="20" height="20" patternUnits="userSpaceOnUse"><circle cx="10" cy="10" r="1" fill="#2b493b" /></pattern></defs>
            <rect width="320" height="320" fill={`url(#${id}-grid)`} />
            {game.food && <g transform={`translate(${game.food.x * 20 + 10} ${game.food.y * 20 + 10})`}><circle r="7" cy="1" fill="#fb7185" /><circle cx="-2" cy="-1" r="2" fill="#fecdd3" /><path d="M0-5Q0-11 6-9Q6-5 0-5" fill="#a3e635" /></g>}
            {game.body.map((point, index) => <g key={index} transform={`translate(${point.x * 20 + 10} ${point.y * 20 + 10}) rotate(${angles[game.direction]})`}><rect x="-9" y="-9" width="18" height="18" rx={index === 0 ? 7 : 5} fill={index === 0 ? '#d9f99d' : index % 2 ? '#84cc16' : '#a3e635'} />{index === 0 && <g fill="#1a3922"><circle cx="4" cy="-4" r="2" /><circle cx="4" cy="4" r="2" /></g>}</g>)}
          </svg>
          {!playing && <div className="pointer-events-none absolute inset-0 flex flex-col items-center justify-center bg-[#10271f]/75 p-5 text-center text-white"><span className="text-4xl" aria-hidden="true">{game.phase === 'won' ? '🏆' : ended ? '🍎' : paused ? '⏸' : '🐍'}</span><h3 className="mt-3 font-heading text-2xl font-bold">{game.phase === 'won' ? 'Kín vườn rồi!' : ended ? 'Hết lượt!' : paused ? 'Nghỉ một nhịp' : 'Sẵn sàng săn mồi?'}</h3><p className="mt-2 text-sm text-[#d9e8d7]">{ended ? `Bạn đã ăn ${game.score} quả táo.` : paused ? 'Bấm Tiếp tục để trở lại.' : 'Bấm Bắt đầu hoặc Space để chơi.'}</p></div>}
        </div>
        <div className="mx-auto mt-4 grid w-fit grid-cols-3 gap-2">{controls.map(({ direction, label, symbol }, index) => <button key={direction} type="button" aria-label={label} disabled={!playing} onClick={() => turn(direction)} className={`h-12 w-14 touch-manipulation rounded-xl border border-border bg-surface-2 text-xl font-bold text-text hover:bg-surface-hover disabled:opacity-40 ${index === 0 ? 'col-start-2' : index === 1 ? 'col-start-1 row-start-2' : 'row-start-2'}`}>{symbol}</button>)}</div>
      </div>
      <div id={`${id}-instructions`} className="space-y-5 text-sm leading-6 text-text-muted">
        <div><h3 className="mb-2 font-heading text-lg font-bold text-text">Càng ăn, càng nhanh</h3><p>Dùng phím mũi tên / WASD hoặc các nút hướng để dẫn rắn tới quả táo. Mỗi quả giúp rắn dài hơn và tăng tốc một chút.</p></div>
        <p>Đừng đâm vào tường hay thân mình. Rắn không thể quay ngược 180°; mỗi nhịp chỉ đổi hướng một lần.</p>
        <p>Nhấn P / Esc để tạm dừng hoặc tiếp tục. Game tự dừng khi bạn chuyển tab hay rời vùng chơi.</p>
        <div role="status" className="rounded-xl border border-border bg-bg p-4">{game.phase === 'won' ? '🏆 Bạn đã lấp đầy bàn chơi. Chiến thắng tuyệt đối!' : ended ? `Hết lượt với ${game.score} quả táo. Thử phá kỷ lục của chính mình nào!` : paused ? 'Game đang tạm dừng.' : playing ? 'Đang săn mồi. Nhìn trước một bước để tránh bị kẹt nhé!' : 'Một chút hoài niệm, một chút thử thách. Bắt đầu nào!'}</div>
        <div className="flex flex-wrap gap-3"><button type="button" onClick={() => { if (playing) pause(); else start() }} className="rounded-xl bg-primary px-5 py-2.5 font-semibold text-white hover:bg-primary-dark">{playing ? 'Tạm dừng' : paused ? 'Tiếp tục' : ended ? 'Chơi lại' : 'Bắt đầu'}</button>{(playing || paused) && <button type="button" onClick={() => { commitGame(createSnake()); stageRef.current?.focus({ preventScroll: true }) }} className="rounded-xl border border-border bg-surface-2 px-5 py-2.5 font-semibold text-text hover:bg-surface-hover">Ván mới</button>}</div>
      </div>
    </div>
  </section>
}
