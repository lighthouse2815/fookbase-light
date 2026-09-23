import { useRef, useState } from 'react'
import { createSlidingPuzzle, isPuzzleSolved, puzzleNeighbors, slideTile } from './slidingPuzzleEngine'

const offsets: Record<string, number> = { ArrowUp: -4, ArrowDown: 4, ArrowLeft: -1, ArrowRight: 1 }

export default function SlidingPuzzleGame() {
  const [game, setGame] = useState(() => createSlidingPuzzle())
  const stageRef = useRef<HTMLDivElement>(null)
  const won = isPuzzleSolved(game.board)
  const empty = game.board.indexOf(0)
  const neighbors = puzzleNeighbors(empty)
  const correct = game.board.filter((value, index) => value !== 0 && value === index + 1).length
  const move = (index: number) => { setGame((current) => slideTile(current, index)); stageRef.current?.focus({ preventScroll: true }) }
  return <section aria-label="Xếp hình 15 ô" className="game-detail-panel rounded-3xl border border-border bg-surface p-5 card-shadow sm:p-8">
    <div className="mb-6 flex flex-wrap items-center justify-between gap-4">
      <div><h2 className="font-heading text-2xl font-bold text-text">Đưa những con số về nhà</h2><p className="mt-1 text-sm text-text-muted">Một ô trống, hàng trăm cách xoay chuyển.</p></div>
      <div className="game-stat-strip flex gap-3 text-center"><div className="rounded-xl bg-primary/15 px-4 py-2"><p className="text-xs text-text-muted">Nước đi</p><p className="text-2xl font-bold text-primary-light">{game.moves}</p></div><div className="rounded-xl bg-surface-2 px-4 py-2"><p className="text-xs text-text-muted">Đúng vị trí</p><p className="text-2xl font-bold text-text">{correct}/15</p></div></div>
    </div>
    <div className="game-play-grid grid items-center gap-8">
      <div ref={stageRef} tabIndex={0} role="group" aria-label="Bàn xếp hình, phím mũi tên di chuyển ô trống" aria-describedby="sliding-puzzle-instructions"
        onKeyDown={(event) => { const offset = offsets[event.key]; if (offset !== undefined) { event.preventDefault(); move(empty + offset) } }}
        className="grid grid-cols-4 gap-2 rounded-3xl border border-border bg-bg p-3 focus-visible:outline-4 focus-visible:outline-offset-4 focus-visible:outline-primary sm:gap-3">
        {game.board.map((value, index) => value ? <button key={value} type="button" aria-label={`Số ${value}, hàng ${Math.floor(index / 4) + 1}, cột ${index % 4 + 1}`} disabled={won || !neighbors.includes(index)} onClick={() => move(index)}
          className={`aspect-square touch-manipulation rounded-xl border text-2xl font-black shadow-sm transition duration-150 motion-reduce:transition-none sm:text-3xl focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary ${value === index + 1 ? 'border-secondary/40 bg-secondary/15 text-secondary' : 'border-primary/30 bg-primary/15 text-primary-light'} ${neighbors.includes(index) && !won ? 'ring-2 ring-primary/40 enabled:hover:scale-105 enabled:hover:bg-primary/25' : ''}`}>{value}</button>
          : <div key="empty" aria-label="Ô trống" className="grid aspect-square place-items-center rounded-xl border-2 border-dashed border-border text-2xl text-text-muted" aria-hidden="true">✦</div>)}
      </div>
      <div id="sliding-puzzle-instructions" className="space-y-5 text-sm leading-6 text-text-muted">
        <div><h3 className="mb-2 font-heading text-lg font-bold text-text">Xếp lại từng chút một</h3><p>Chạm ô nằm sát ô trống để trượt nó vào khoảng trống. Các ô có viền sáng là những ô có thể di chuyển.</p></div>
        <p>Xếp số 1 đến 15 theo thứ tự từ trái sang phải, từ trên xuống dưới; ô trống ở góc dưới bên phải. Ô đã đúng vị trí sẽ có màu xanh.</p>
        <p>Cũng có thể chạm vào bàn và dùng phím mũi tên để di chuyển <strong className="text-text">ô trống</strong>. Mỗi bàn trộn đều có lời giải.</p>
        <div className="flex items-center gap-4 rounded-xl border border-border bg-bg p-4"><div className="grid w-28 shrink-0 grid-cols-4 gap-1" aria-hidden="true">{Array.from({ length: 16 }, (_, index) => <span key={index} className="grid aspect-square place-items-center rounded bg-surface-2 text-xs font-bold text-text">{index < 15 ? index + 1 : ''}</span>)}</div><p>Mục tiêu của bạn.<br />Mẹo: hoàn thành từng hàng, bắt đầu từ góc trên trái.</p></div>
        <div role="status" className="rounded-xl border border-primary/30 bg-primary/10 p-4 text-text">{won ? `🎉 Hoàn hảo! Bạn xếp đúng cả 15 ô trong ${game.moves} nước đi.` : `${correct}/15 ô đã về đúng vị trí. Cứ bình tĩnh, bạn làm được!`}</div>
        <button type="button" onClick={() => { setGame(createSlidingPuzzle()); stageRef.current?.focus({ preventScroll: true }) }} className="rounded-xl bg-primary px-5 py-2.5 font-semibold text-white hover:bg-primary-dark">Trộn bàn mới</button>
      </div>
    </div>
  </section>
}
