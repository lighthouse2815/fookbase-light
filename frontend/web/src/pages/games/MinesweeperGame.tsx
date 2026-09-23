import { useState } from 'react'
import { adjacentMines, createMinesweeper, MINE_COUNT, revealMineCell, toggleFlag } from './minesweeperEngine'

const colors = ['text-text-muted', 'text-blue-500', 'text-emerald-500', 'text-rose-500', 'text-violet-500', 'text-orange-500', 'text-cyan-500', 'text-pink-500', 'text-text']

export default function MinesweeperGame() {
  const [game, setGame] = useState(createMinesweeper)
  const [flagMode, setFlagMode] = useState(false)
  const ended = game.phase === 'won' || game.phase === 'lost'
  const flags = game.flags.filter(Boolean).length
  const opened = game.revealed.filter(Boolean).length
  return <section aria-label="Dò mìn" className="game-detail-panel rounded-3xl border border-border bg-surface p-5 card-shadow sm:p-8">
    <div className="mb-6 flex flex-wrap items-center justify-between gap-4">
      <div><h2 className="font-heading text-2xl font-bold text-text">Một chút suy luận, một chút hồi hộp</h2><p className="mt-1 text-sm text-text-muted">Bàn 8 × 8, 10 quả mìn. Lượt mở đầu luôn an toàn.</p></div>
      <div className="flex gap-3 text-center"><div className="rounded-xl bg-primary/15 px-4 py-2"><p className="text-xs text-text-muted">Cờ còn lại</p><p className="text-2xl font-bold text-primary-light">{MINE_COUNT - flags}</p></div><div className="rounded-xl bg-surface-2 px-4 py-2"><p className="text-xs text-text-muted">Ô an toàn</p><p className="text-2xl font-bold text-text">{opened}/54</p></div></div>
    </div>
    <div className="grid items-center gap-8 md:grid-cols-[minmax(0,420px)_1fr]">
      <div>
        <div className="mb-4 flex gap-2" role="group" aria-label="Chế độ thao tác">{[false, true].map((flag) => <button key={String(flag)} type="button" aria-pressed={flagMode === flag} onClick={() => setFlagMode(flag)} className={`flex-1 rounded-xl border px-3 py-2.5 text-sm font-semibold ${flagMode === flag ? 'border-primary bg-primary text-white' : 'border-border bg-surface-2 text-text'}`}>{flag ? '🚩 Cắm cờ' : '🔍 Mở ô'}</button>)}</div>
        <div className="grid grid-cols-8 gap-1 rounded-2xl bg-bg p-2" aria-label="Bàn dò mìn">
          {game.revealed.map((revealed, index) => {
            const mine = game.mines?.[index] && ended
            const count = revealed && game.mines ? adjacentMines(game.mines, index) : 0
            const wrongFlag = game.phase === 'lost' && game.flags[index] && !game.mines?.[index]
            const label = mine ? 'mìn' : wrongFlag ? 'cắm cờ sai' : game.flags[index] ? 'đã cắm cờ' : revealed ? `${count} mìn xung quanh` : 'chưa mở'
            return <button key={index} type="button" aria-label={`Ô ${index + 1}: ${label}`} disabled={ended || revealed}
              onClick={() => setGame(flagMode ? toggleFlag(game, index) : revealMineCell(game, index))}
              onContextMenu={(event) => { event.preventDefault(); setGame((current) => toggleFlag(current, index)) }}
              className={`aspect-square min-w-0 touch-manipulation rounded-md border text-sm font-black transition-colors sm:text-lg focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary ${index === game.detonated ? 'border-danger bg-danger/25' : revealed ? 'border-border/40 bg-surface/50' : 'border-border bg-surface-2 enabled:hover:border-primary enabled:hover:bg-primary/15'} ${colors[count]}`}>
              <span aria-hidden="true">{mine ? '💣' : wrongFlag ? '✕' : game.flags[index] ? '🚩' : revealed ? count || '·' : ''}</span>
            </button>
          })}
        </div>
      </div>
      <div className="space-y-5 text-sm leading-6 text-text-muted">
        <div><h3 className="mb-2 font-heading text-lg font-bold text-text">Đọc số, tìm đường an toàn</h3><p>Mỗi con số cho biết số mìn trong 8 ô xung quanh. Mở hết 54 ô an toàn để chiến thắng; chạm phải mìn là hết lượt.</p></div>
        <p>Nhấp chuột phải để đánh dấu ô nghi có mìn. Trên điện thoại, chọn chế độ Cắm cờ rồi chạm ô; chạm lại để bỏ cờ. Chuyển về Mở ô để tiếp tục khám phá.</p>
        <div role="status" className={`rounded-xl border p-4 ${ended ? 'border-primary/40 bg-primary/10 text-text' : 'border-border bg-bg'}`}>{game.phase === 'won' ? '🎉 Bạn đã tìm đủ ô an toàn! Bãi mìn được chinh phục.' : game.phase === 'lost' ? '💥 Trúng mìn rồi! Xem lại vị trí mìn và thử ván mới nhé.' : game.phase === 'ready' ? 'Chọn ô bất kỳ để bắt đầu. Cả vùng quanh ô đầu tiên đều không có mìn.' : `Đã mở ${opened}/54 ô an toàn. ${flagMode ? 'Đang cắm cờ.' : 'Đang mở ô.'}`}</div>
        <button type="button" onClick={() => { setGame(createMinesweeper()); setFlagMode(false) }} className="rounded-xl bg-primary px-5 py-2.5 font-semibold text-white hover:bg-primary-dark">Ván mới</button>
      </div>
    </div>
  </section>
}
