import { useRef, useState } from 'react'
import { canMove, createTwenty48, moveTiles, type Direction } from './twenty48Engine'

const tileColors: Record<number, string> = {
  2: 'bg-[#eee4da] text-[#665b50]', 4: 'bg-[#ede0c8] text-[#665b50]',
  8: 'bg-[#f2b179] text-[#54341f]', 16: 'bg-[#f59563] text-[#54270f]',
  32: 'bg-[#f67c5f] text-[#481c12]', 64: 'bg-[#f65e3b] text-[#40150b]',
  128: 'bg-[#edcf72] text-[#594809]', 256: 'bg-[#edcc61] text-[#594809]',
  512: 'bg-[#edc850] text-[#594809]', 1024: 'bg-[#edc53f] text-[#594809]',
  2048: 'bg-[#edc22e] text-[#594809]',
}
const controls: { direction: Direction; label: string; symbol: string }[] = [
  { direction: 'left', label: 'Trượt trái', symbol: '←' }, { direction: 'up', label: 'Trượt lên', symbol: '↑' },
  { direction: 'down', label: 'Trượt xuống', symbol: '↓' }, { direction: 'right', label: 'Trượt phải', symbol: '→' },
]
const keyDirections: Record<string, Direction> = { ArrowLeft: 'left', ArrowRight: 'right', ArrowUp: 'up', ArrowDown: 'down' }

export default function Twenty48Game() {
  const [game, setGame] = useState(() => createTwenty48())
  const stageRef = useRef<HTMLDivElement>(null)
  const swipeRef = useRef<{ x: number; y: number; id: number } | null>(null)
  const over = !canMove(game.board)
  const largest = Math.max(...game.board)
  const move = (direction: Direction) => {
    setGame(moveTiles(game, direction))
    stageRef.current?.focus({ preventScroll: true })
  }

  return <section aria-label="2048" className="game-detail-panel rounded-3xl border border-border bg-surface p-5 card-shadow sm:p-8">
    <div className="mb-6 flex flex-wrap items-center justify-between gap-4">
      <div><h2 className="font-heading text-2xl font-bold text-text">Nhỏ cộng nhỏ, thành điều lớn</h2><p className="mt-1 text-sm text-text-muted">Ghép những con số. Chinh phục ô 2048.</p></div>
      <div className="game-stat-strip flex gap-3 text-center"><div className="rounded-xl bg-primary/15 px-4 py-2"><p className="text-xs text-text-muted">Điểm</p><p className="text-2xl font-bold text-primary-light">{game.score}</p></div><div className="rounded-xl bg-surface-2 px-4 py-2"><p className="text-xs text-text-muted">Ô lớn nhất</p><p className="text-2xl font-bold text-text">{largest}</p></div></div>
    </div>
    <div className="game-play-grid grid items-center gap-8">
      <div>
        <div ref={stageRef} tabIndex={0} role="group" aria-label="Bàn 2048, dùng phím mũi tên hoặc vuốt để di chuyển" aria-describedby="twenty48-instructions"
          className="grid touch-none grid-cols-4 gap-2 rounded-2xl bg-[#a99c8e] p-2 focus-visible:outline-4 focus-visible:outline-offset-4 focus-visible:outline-primary sm:gap-3 sm:p-3"
          onKeyDown={(event) => { const direction = keyDirections[event.key]; if (direction) { event.preventDefault(); move(direction) } }}
          onPointerDown={(event) => { if (!event.isPrimary || event.button !== 0) return; swipeRef.current = { x: event.clientX, y: event.clientY, id: event.pointerId }; event.currentTarget.setPointerCapture(event.pointerId); event.currentTarget.focus({ preventScroll: true }) }}
          onPointerCancel={() => { swipeRef.current = null }}
          onLostPointerCapture={() => { swipeRef.current = null }}
          onPointerUp={(event) => {
            const start = swipeRef.current
            swipeRef.current = null
            if (!start || start.id !== event.pointerId) return
            const dx = event.clientX - start.x
            const dy = event.clientY - start.y
            if (Math.max(Math.abs(dx), Math.abs(dy)) < 24) return
            move(Math.abs(dx) > Math.abs(dy) ? dx > 0 ? 'right' : 'left' : dy > 0 ? 'down' : 'up')
          }}>
          {game.board.map((value, index) => <div key={index} aria-label={`Ô ${index + 1}: ${value || 'trống'}`} className={`grid aspect-square select-none place-items-center rounded-lg font-black transition-colors motion-reduce:transition-none ${value >= 1024 ? 'text-xl sm:text-2xl' : 'text-2xl sm:text-3xl'} ${value ? tileColors[value] ?? 'bg-[#6d5b9b] text-white' : 'bg-[#c5b9ac]'}`}>{value || ''}</div>)}
        </div>
        <div className="mt-4 flex justify-center gap-2">{controls.map(({ direction, label, symbol }) => <button key={direction} type="button" aria-label={label} disabled={over} onClick={() => move(direction)} className="h-12 w-12 rounded-xl border border-border bg-surface-2 text-xl font-bold text-text hover:bg-surface-hover disabled:opacity-40">{symbol}</button>)}</div>
      </div>
      <div id="twenty48-instructions" className="space-y-5 text-sm leading-6 text-text-muted">
        <div><h3 className="mb-2 font-heading text-lg font-bold text-text">Một nước đi, nhiều khả năng</h3><p>Chạm vào bàn rồi dùng phím mũi tên, vuốt trên điện thoại hoặc bấm các nút hướng bên dưới. Hai ô bằng nhau chạm nhau sẽ hợp thành một ô gấp đôi.</p></div>
        <p>Mỗi nước đi hợp lệ sinh thêm một ô 2 hoặc 4. Hết chỗ trống và không còn cặp nào ghép được thì kết thúc.</p>
        <div role="status" className={`rounded-xl border p-4 ${over || largest >= 2048 ? 'border-primary/40 bg-primary/10 text-text' : 'border-border bg-bg'}`}>{over ? `Hết nước đi! Bạn đạt ${game.score} điểm${largest >= 2048 ? ' và đã chinh phục 2048' : ''}. Thử lại nhé?` : largest >= 2048 ? '🎉 Bạn đã chinh phục 2048! Tiếp tục ghép để phá giới hạn nhé.' : 'Mẹo: giữ ô lớn nhất ở một góc và xây dần các ô xung quanh.'}</div>
        <button type="button" onClick={() => { setGame(createTwenty48()); stageRef.current?.focus({ preventScroll: true }) }} className="rounded-xl bg-primary px-5 py-2.5 font-semibold text-white hover:bg-primary-dark">Ván mới</button>
      </div>
    </div>
  </section>
}
