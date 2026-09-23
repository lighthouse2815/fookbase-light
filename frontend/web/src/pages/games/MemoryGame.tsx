import { useEffect, useState } from 'react'
import { createMemoryGame, flipCard } from './memoryEngine'

const symbols = ['🍉', '🍋', '🍇', '🍒', '🥝', '🍓', '🍍', '🥥']
const names = ['Dưa hấu', 'Chanh', 'Nho', 'Anh đào', 'Kiwi', 'Dâu tây', 'Dứa', 'Dừa']

export default function MemoryGame() {
  const [game, setGame] = useState(createMemoryGame)
  const won = game.matched.length === game.cards.length
  useEffect(() => {
    if (game.opened.length !== 2) return
    const timer = window.setTimeout(() => setGame((current) => ({ ...current, opened: [] })), 850)
    return () => window.clearTimeout(timer)
  }, [game.opened])

  return <section aria-label="Lật thẻ trí nhớ" className="game-detail-panel rounded-3xl border border-border bg-surface p-5 card-shadow sm:p-8">
    <div className="mb-6 flex flex-wrap items-center justify-between gap-4">
      <div><h2 className="font-heading text-2xl font-bold text-text">Vườn trái cây bí mật</h2><p className="mt-1 text-sm text-text-muted">16 chiếc thẻ, 8 cặp trái cây. Bạn nhớ được bao nhiêu?</p></div>
      <div className="flex gap-3 text-center"><div className="rounded-xl bg-primary/15 px-4 py-2"><p className="text-xs text-text-muted">Lượt lật</p><p className="text-2xl font-bold text-primary-light">{game.moves}</p></div><div className="rounded-xl bg-surface-2 px-4 py-2"><p className="text-xs text-text-muted">Đã ghép</p><p className="text-2xl font-bold text-text">{game.matched.length / 2}/8</p></div></div>
    </div>
    <div className="grid items-center gap-8 md:grid-cols-[minmax(0,420px)_1fr]">
      <div className="grid grid-cols-4 gap-2 rounded-3xl bg-bg p-3 sm:gap-3">
        {game.cards.map((symbol, index) => {
          const matched = game.matched.includes(index)
          const visible = matched || game.opened.includes(index)
          return <button key={index} type="button" onClick={() => setGame((current) => flipCard(current, index))}
            disabled={matched || game.opened.includes(index) || game.opened.length === 2}
            aria-label={`Thẻ ${index + 1}: ${visible ? names[symbol] : 'chưa lật'}${matched ? ', đã ghép' : ''}`}
            className={`relative aspect-square rounded-xl border text-3xl shadow-sm transition duration-200 motion-reduce:transition-none sm:text-4xl focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary ${matched ? 'border-secondary/40 bg-secondary/15' : visible ? 'border-primary/40 bg-surface' : 'border-white/20 bg-linear-to-br from-[#ec4899] to-[#8b5cf6] text-white hover:scale-105 disabled:hover:scale-100'}`}>
            <span aria-hidden="true">{visible ? symbols[symbol] : '✦'}</span>{matched && <span aria-hidden="true" className="absolute right-1 top-0.5 text-xs text-secondary">✓</span>}
          </button>
        })}
      </div>
      <div className="space-y-5 text-sm leading-6 text-text-muted">
        <div><h3 className="mb-2 font-heading text-lg font-bold text-text">Lật nhẹ, nhớ lâu</h3><p>Chọn hai thẻ để tìm một cặp giống nhau. Nếu chưa khớp, chúng sẽ úp lại sau một nhịp để bạn ghi nhớ vị trí.</p></div>
        <p>Ghép đủ 8 cặp để chiến thắng. Càng ít lượt lật, trí nhớ của bạn càng đỉnh! Có thể dùng Tab và Enter để chọn thẻ.</p>
        <div role="status" className={`rounded-xl border p-4 ${won ? 'border-secondary/40 bg-secondary/15 text-text' : 'border-border bg-bg'}`}>{won ? `🎉 Tuyệt vời! Bạn tìm đủ 8 cặp trong ${game.moves} lượt.` : `Đã tìm thấy ${game.matched.length / 2} cặp. ${game.opened.length === 2 ? 'Ghi nhớ hai thẻ này nhé!' : 'Tìm cặp tiếp theo nào!'}`}</div>
        <button type="button" onClick={() => setGame(createMemoryGame())} className="rounded-xl bg-primary px-5 py-2.5 font-semibold text-white hover:bg-primary-dark">Trộn thẻ, chơi lại</button>
      </div>
    </div>
  </section>
}
