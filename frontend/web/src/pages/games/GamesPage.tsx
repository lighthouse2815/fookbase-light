import { useEffect, useMemo, useState, type ComponentType, type ReactNode } from 'react'
import { Mascot } from 'page-mascot'
import { usePreferences } from '../../preferences'
import FlappyBirdGame from './FlappyBirdGame'
import MemoryGame from './MemoryGame'
import Twenty48Game from './Twenty48Game'
import SnakeGame from './SnakeGame'
import MinesweeperGame from './MinesweeperGame'
import WhackMoleGame from './WhackMoleGame'
import SlidingPuzzleGame from './SlidingPuzzleGame'
import MelodyGame from './MelodyGame'
import StarCatchGame from './StarCatchGame'
import LightsGame from './LightsGame'
import { GravityFlipGame, NeonDriftGame } from './ThreeDGames'
import './games.css'

type GameId = 'tic-tac-toe' | 'flappy-bird' | 'memory' | '2048' | 'snake' | 'minesweeper' | 'whack-mole' | 'sliding-puzzle' | 'melody' | 'star-catch' | 'lights' | 'neon-drift' | 'gravity-flip'
type Mark = 'X' | 'O'
type Cell = Mark | null

interface GameDefinition {
  id: GameId
  title: string
  description: string
  category: string
  badge: string
  accent: string
  icon: ReactNode
  component: ComponentType
}

const games: GameDefinition[] = [
  {
    id: 'neon-drift', title: 'Neon Drift 3D', category: '3D Arcade', badge: 'Điều khiển lạ', component: NeonDriftGame,
    description: 'Rê để lái tàu qua đường hầm neon và nhấn đúp để xuyên vật thể bằng Phase Shift.',
    accent: 'from-[#073f5e] via-[#0a8bad] to-[#8c49cf]',
    icon: <svg viewBox="0 0 96 96" className="h-full w-full" aria-hidden="true"><rect x="4" y="4" width="88" height="88" rx="22" fill="#071d35" /><path d="M14 73 48 14l34 59" fill="none" stroke="#64ebff" strokeWidth="3" opacity=".8" /><path d="M23 72h50M31 58h34M39 44h18" stroke="#ff70e6" strokeWidth="3" strokeLinecap="round" /><path d="m48 30 12 27-12-5-12 5z" fill="#bafcff" stroke="#56dfff" strokeWidth="2" /></svg>,
  },
  {
    id: 'gravity-flip', title: 'Gravity Flip', category: '3D Arcade', badge: 'Chạm để lộn', component: GravityFlipGame,
    description: 'Bay qua ống lập phương và chạm bất kỳ đâu để đảo sàn thành trần.',
    accent: 'from-[#52253d] via-[#a85b42] to-[#f0b45e]',
    icon: <svg viewBox="0 0 96 96" className="h-full w-full" aria-hidden="true"><rect x="4" y="4" width="88" height="88" rx="22" fill="#32182d" /><path d="M19 26h58M19 70h58" stroke="#ffce80" strokeWidth="4" opacity=".65" /><path d="M29 26v44M67 26v44" stroke="#ff8d66" strokeWidth="3" strokeDasharray="5 5" /><circle cx="48" cy="48" r="11" fill="#ffd66e" stroke="#fff4c1" strokeWidth="2" /><path d="M48 15v18m0 30v18M41 23l7-8 7 8m-14 50 7 8 7-8" fill="none" stroke="#fff0ad" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round" /></svg>,
  },
  {
    id: 'melody', title: 'Giai điệu sắc màu', category: 'Trí nhớ & âm nhạc', badge: 'Có âm thanh', component: MelodyGame,
    description: 'Bốn phím đàn, mười hai vòng nhớ. Lắng nghe và chơi lại giai điệu của bạn.',
    accent: 'from-[#284b43] via-[#397666] to-[#79b99f]',
    icon: <svg viewBox="0 0 96 96" className="h-full w-full" aria-hidden="true"><rect x="4" y="4" width="88" height="88" rx="20" fill="#1c342e" /><rect x="15" y="15" width="66" height="14" rx="4" fill="#b2dfb3" /><rect x="15" y="37" width="29" height="20" rx="5" fill="#ffab8a" /><rect x="52" y="37" width="29" height="20" rx="5" fill="#ffe394" /><rect x="15" y="65" width="29" height="20" rx="5" fill="#a3e2d2" /><rect x="52" y="65" width="29" height="20" rx="5" fill="#a9caff" /><path d="M22 22h17m5 0h7m5 0h18" stroke="#305748" strokeWidth="3" strokeLinecap="round" /></svg>,
  },
  {
    id: 'star-catch', title: 'Hứng sao', category: 'Phản xạ', badge: 'Mới', component: StarCatchGame,
    description: 'Lái tàu qua cơn mưa vũ trụ, hứng sao để ghi điểm và né thiên thạch.',
    accent: 'from-[#162b54] via-[#31538e] to-[#5c8bc9]',
    icon: <svg viewBox="0 0 96 96" className="h-full w-full" aria-hidden="true"><rect x="4" y="4" width="88" height="88" rx="22" fill="#132644" /><circle cx="18" cy="22" r="2" fill="#fff" /><circle cx="73" cy="18" r="2" fill="#bde5ff" /><circle cx="81" cy="62" r="2" fill="#fff" /><path d="m48 16 4 9 10 1-8 7 2 10-8-5-8 5 2-10-8-7 10-1z" fill="#ffe394" /><path d="m68 34 5 10 10 2-8 7 2 11-9-5-9 5 2-11-8-7 10-2z" fill="#ff997f" opacity=".9" /><path d="m47 76 8-17 8 17 10 4-18 2-18-2z" fill="#a3e2d2" stroke="#e2fff6" strokeWidth="2" /></svg>,
  },
  {
    id: 'lights', title: 'Thắp sáng ngân hà', category: 'Giải đố', badge: 'Mới', component: LightsGame,
    description: 'Tắt mọi ô sáng trên lưới 5 × 5 và mở cổng đến vì sao tiếp theo.',
    accent: 'from-[#3a236d] via-[#6d4ab2] to-[#9e83e3]',
    icon: <svg viewBox="0 0 96 96" className="h-full w-full" aria-hidden="true"><rect x="4" y="4" width="88" height="88" rx="22" fill="#251a48" /><g fill="#c9b8ff"><circle cx="25" cy="25" r="7" /><circle cx="48" cy="25" r="7" /><circle cx="71" cy="25" r="7" /><circle cx="25" cy="48" r="7" /><circle cx="48" cy="48" r="7" /><circle cx="71" cy="48" r="7" /><circle cx="25" cy="71" r="7" /><circle cx="48" cy="71" r="7" /><circle cx="71" cy="71" r="7" /></g><path d="M48 13v24m0 22v24M13 48h24m22 0h24" stroke="#f8edff" strokeWidth="2" opacity=".8" /><circle cx="48" cy="48" r="13" fill="#f7ddff" opacity=".9" /></svg>,
  },
  {
    id: 'tic-tac-toe', title: 'Cờ ca-rô', category: 'Chiến thuật', badge: 'Chơi đơn',
    component: TicTacToeGame,
    description: 'Đấu trí cùng máy, tạo ba dấu liên tiếp để giành chiến thắng.',
    accent: 'from-[#6558e8] via-[#7c6ff2] to-[#9b8cff]',
    icon: <svg viewBox="0 0 96 96" className="h-full w-full" aria-hidden="true"><rect x="5" y="5" width="86" height="86" rx="24" fill="white" fillOpacity=".16" /><path d="M35 20v56M61 20v56M20 35h56M20 61h56" stroke="white" strokeWidth="5" strokeLinecap="round" opacity=".85" /><path d="m24 23 9 9m0-9-9 9m39 33 10 10m0-10L63 75" stroke="white" strokeWidth="5" strokeLinecap="round" /><circle cx="48" cy="48" r="8" fill="none" stroke="#ffdf72" strokeWidth="5" /></svg>,
  },
  {
    id: 'flappy-bird', title: 'Flappy Bird', category: 'Phản xạ', badge: 'Có online',
    component: FlappyBirdGame,
    description: 'Giữ nhịp bay qua những chiếc ống hoặc lập phòng chơi cùng bạn bè.',
    accent: 'from-[#159f91] via-[#38b8a4] to-[#72d2a8]',
    icon: <svg viewBox="0 0 96 96" className="h-full w-full" aria-hidden="true"><rect x="5" y="5" width="86" height="86" rx="24" fill="white" fillOpacity=".15" /><path d="M15 73h66" stroke="#dff8bd" strokeWidth="6" strokeLinecap="round" /><path d="M68 16h17v21H68zm-3 21h23v8H65zM68 59h17v14H68zm-3-8h23v8H65z" fill="#b9ef72" stroke="#397956" strokeWidth="2" /><g transform="translate(38 48)"><circle r="17" fill="#ffdc4f" stroke="#73532c" strokeWidth="3" /><ellipse cx="-10" cy="5" rx="10" ry="7" fill="#fff0a0" /><circle cx="7" cy="-6" r="6" fill="white" /><circle cx="9" cy="-6" r="2" fill="#263238" /><path d="M13 1h15l-8 7h-7z" fill="#ef7e42" stroke="#8d492b" strokeWidth="2" /></g></svg>,
  },
  {
    id: 'memory', title: 'Lật thẻ trí nhớ', category: 'Trí nhớ', badge: 'Mới', component: MemoryGame,
    description: 'Khám phá vườn trái cây, lật thẻ và tìm đủ tám cặp giống nhau.',
    accent: 'from-[#db2777] via-[#c044b7] to-[#8b5cf6]',
    icon: <svg viewBox="0 0 96 96" className="h-full w-full" aria-hidden="true"><rect x="10" y="18" width="49" height="65" rx="10" fill="white" fillOpacity=".35" transform="rotate(-12 35 50)" /><rect x="36" y="12" width="49" height="65" rx="10" fill="white" /><path d="m60 25 17 17-17 20-17-20z" fill="#ec4899" /><path d="m24 43 7 8-7 8-7-8z" fill="white" /></svg>,
  },
  {
    id: '2048', title: '2048', category: 'Giải đố', badge: 'Mới', component: Twenty48Game,
    description: 'Trượt những ô số, ghép đôi và chinh phục cột mốc 2048.',
    accent: 'from-[#d97706] via-[#e9a125] to-[#f2ca58]',
    icon: <svg viewBox="0 0 96 96" className="h-full w-full" aria-hidden="true"><rect x="7" y="7" width="82" height="82" rx="22" fill="white" fillOpacity=".18" /><rect x="17" y="17" width="28" height="28" rx="7" fill="#fff2d4" /><rect x="51" y="17" width="28" height="28" rx="7" fill="#ffe2a0" /><rect x="17" y="51" width="62" height="28" rx="7" fill="white" /><g fill="#996019" fontFamily="sans-serif" fontWeight="900" textAnchor="middle"><text x="31" y="38" fontSize="20">2</text><text x="65" y="38" fontSize="20">4</text><text x="48" y="72" fontSize="22">2048</text></g></svg>,
  },
  {
    id: 'snake', title: 'Rắn săn mồi', category: 'Arcade', badge: 'Mới', component: SnakeGame,
    description: 'Dẫn chú rắn săn táo, lớn dần và thử thách phản xạ của bạn.',
    accent: 'from-[#166534] via-[#399339] to-[#84b83b]',
    icon: <svg viewBox="0 0 96 96" className="h-full w-full" aria-hidden="true"><rect x="5" y="5" width="86" height="86" rx="24" fill="white" fillOpacity=".15" /><path d="M23 69h35q12 0 12-12T58 45H37q-12 0-12-12t12-12h17" fill="none" stroke="#bef264" strokeWidth="15" strokeLinecap="round" strokeLinejoin="round" /><rect x="48" y="11" width="24" height="20" rx="9" fill="#ecfccb" /><circle cx="65" cy="17" r="2" fill="#24462d" /><circle cx="65" cy="25" r="2" fill="#24462d" /><circle cx="26" cy="72" r="8" fill="#fb7185" /><path d="m26 64 4-6" stroke="#ecfccb" strokeWidth="3" strokeLinecap="round" /></svg>,
  },
  {
    id: 'minesweeper', title: 'Dò mìn', category: 'Suy luận', badge: 'Mới', component: MinesweeperGame,
    description: 'Đọc những con số, cắm cờ và mở đường qua bãi mìn bí ẩn.',
    accent: 'from-[#0369a1] via-[#0891b2] to-[#67c9d5]',
    icon: <svg viewBox="0 0 96 96" className="h-full w-full" aria-hidden="true"><rect x="8" y="8" width="80" height="80" rx="20" fill="white" fillOpacity=".2" /><path d="M30 74V21" stroke="white" strokeWidth="6" strokeLinecap="round" /><path d="M33 20h37L55 35l15 15H33z" fill="#fde047" /><path d="M19 76h30" stroke="white" strokeWidth="6" strokeLinecap="round" /><circle cx="69" cy="70" r="10" fill="#164e63" /><path d="M69 55v30M54 70h30" stroke="#164e63" strokeWidth="3" /><circle cx="66" cy="67" r="3" fill="white" /></svg>,
  },
  {
    id: 'whack-mole', title: 'Đập chuột', category: 'Phản xạ', badge: 'Mới', component: WhackMoleGame,
    description: 'Nhanh tay bắt những chú chuột tinh nghịch trong thử thách 30 giây.',
    accent: 'from-[#b45309] via-[#da8b30] to-[#edbd71]',
    icon: <svg viewBox="0 0 96 96" className="h-full w-full" aria-hidden="true"><rect x="5" y="5" width="86" height="86" rx="24" fill="white" fillOpacity=".15" /><ellipse cx="48" cy="73" rx="33" ry="12" fill="#713f12" /><circle cx="28" cy="34" r="12" fill="#f5d2a0" /><circle cx="68" cy="34" r="12" fill="#f5d2a0" /><ellipse cx="48" cy="51" rx="27" ry="29" fill="#e9b575" /><ellipse cx="48" cy="62" rx="17" ry="13" fill="#fff1d6" /><circle cx="37" cy="46" r="4" fill="#713f12" /><circle cx="59" cy="46" r="4" fill="#713f12" /><ellipse cx="48" cy="58" rx="5" ry="4" fill="#9a583c" /></svg>,
  },
  {
    id: 'sliding-puzzle', title: 'Xếp hình 15 ô', category: 'Giải đố', badge: 'Mới', component: SlidingPuzzleGame,
    description: 'Trượt những ô số về đúng vị trí và giải mã bàn cờ đầy màu sắc.',
    accent: 'from-[#6d28d9] via-[#8b5cf6] to-[#c084fc]',
    icon: <svg viewBox="0 0 96 96" className="h-full w-full" aria-hidden="true"><rect x="7" y="7" width="82" height="82" rx="22" fill="white" fillOpacity=".18" /><g fill="white"><rect x="18" y="18" width="27" height="27" rx="6" /><rect x="51" y="18" width="27" height="27" rx="6" /><rect x="18" y="51" width="27" height="27" rx="6" /></g><rect x="52" y="52" width="25" height="25" rx="6" fill="none" stroke="white" strokeWidth="2" strokeDasharray="4 3" /><g fill="#7c3aed" fontSize="21" fontFamily="sans-serif" fontWeight="900" textAnchor="middle"><text x="31" y="39">1</text><text x="65" y="39">2</text><text x="31" y="72">3</text></g></svg>,
  },
]

const WINNING_LINES = [[0, 1, 2], [3, 4, 5], [6, 7, 8], [0, 3, 6], [1, 4, 7], [2, 5, 8], [0, 4, 8], [2, 4, 6]] as const
const createBoard = (): Cell[] => Array<Cell>(9).fill(null)
const getWinner = (board: Cell[]): Mark | null => {
  for (const [first, second, third] of WINNING_LINES) {
    const mark = board[first]
    if (mark && mark === board[second] && mark === board[third]) return mark
  }
  return null
}
const availableMoves = (board: Cell[]) => board.flatMap((cell, index) => cell ? [] : [index])
const findMove = (board: Cell[], mark: Mark) => availableMoves(board).find((move) => {
  const trial = [...board]
  trial[move] = mark
  return getWinner(trial) === mark
}) ?? null
const chooseComputerMove = (board: Cell[]) => {
  const win = findMove(board, 'O')
  if (win !== null) return win
  const block = findMove(board, 'X')
  if (block !== null) return block
  if (!board[4]) return 4
  const moves = availableMoves(board)
  return [0, 2, 6, 8].find((move) => moves.includes(move)) ?? moves[0] ?? null
}

function GameLibrary({ onSelect, activeCategory, onCategoryChange }: {
  onSelect: (gameId: GameId) => void
  activeCategory: string
  onCategoryChange: (category: string) => void
}) {
  const categories = ['Tất cả', ...Array.from(new Set(games.map((game) => game.category)))]
  const visibleGames = activeCategory === 'Tất cả' ? games : games.filter((game) => game.category === activeCategory)

  return <section className="games-library" aria-labelledby="game-library-title">
    <div className="games-library__heading">
      <div>
        <p className="games-kicker">BỘ SƯU TẬP FOOKBASE</p>
        <h2 id="game-library-title">Chọn mood của bạn</h2>
        <p>Những ván chơi ngắn, dễ bắt đầu và vừa vặn cho cả màn hình lớn lẫn điện thoại.</p>
      </div>
      <span className="games-count">{visibleGames.length} / {games.length} game</span>
    </div>
    <div className="games-filters" role="tablist" aria-label="Lọc game theo thể loại">
      {categories.map((category) => <button key={category} type="button" role="tab" aria-selected={activeCategory === category} onClick={() => onCategoryChange(category)} className={activeCategory === category ? 'is-active' : ''}>{category}</button>)}
    </div>
    <div className="games-library__grid">
      {visibleGames.map((game) => <button key={game.id} type="button" onClick={() => onSelect(game.id)} className="game-card group">
        <div className={`game-card__art bg-linear-to-br ${game.accent}`}>
          <div className="game-card__orb game-card__orb--top" /><div className="game-card__orb game-card__orb--bottom" />
          <div className="game-card__icon">{game.icon}</div>
          <span className="game-card__badge">{game.badge}</span>
          <span className="game-card__open" aria-hidden="true">↗</span>
        </div>
        <div className="game-card__body">
          <p className="game-card__category">{game.category}</p>
          <h3>{game.title}</h3>
          <p>{game.description}</p>
          <span className="game-card__cta">Chơi ngay <span aria-hidden="true">→</span></span>
        </div>
      </button>)}
    </div>
  </section>
}

function TicTacToeGame() {
  const { t } = usePreferences()
  const [board, setBoard] = useState<Cell[]>(createBoard)
  const [turn, setTurn] = useState<Mark>('X')
  const [winner, setWinner] = useState<Mark | null>(null)
  const [isDraw, setIsDraw] = useState(false)
  const [score, setScore] = useState({ player: 0, computer: 0, draw: 0 })
  const gameIsOver = Boolean(winner) || isDraw
  const status = useMemo(() => winner === 'X' ? t('youWonRound') : winner === 'O' ? t('computerWonRound') : isDraw ? t('drawRound') : turn === 'X' ? t('yourTurn') : t('computerThinking'), [isDraw, t, turn, winner])
  const finishMove = (next: Cell[], mark: Mark) => {
    const won = getWinner(next)
    if (won) { setWinner(won); const key = won === 'X' ? 'player' : 'computer'; setScore((current) => ({ ...current, [key]: current[key] + 1 })); return }
    if (next.every(Boolean)) { setIsDraw(true); setScore((current) => ({ ...current, draw: current.draw + 1 })); return }
    setTurn(mark === 'X' ? 'O' : 'X')
  }
  const playMove = (index: number) => { if (turn !== 'X' || board[index] || gameIsOver) return; const next = [...board]; next[index] = 'X'; setBoard(next); finishMove(next, 'X') }
  const resetRound = () => { setBoard(createBoard()); setTurn('X'); setWinner(null); setIsDraw(false) }
  const resetGame = () => { setScore({ player: 0, computer: 0, draw: 0 }); resetRound() }
  useEffect(() => {
    if (turn !== 'O' || gameIsOver) return
    const timer = window.setTimeout(() => { const move = chooseComputerMove(board); if (move === null) return; const next = [...board]; next[move] = 'O'; setBoard(next); finishMove(next, 'O') }, 450)
    return () => window.clearTimeout(timer)
  }, [board, gameIsOver, turn])

  return <section className="grid items-start gap-6">
    <div className="game-detail-panel rounded-3xl border border-border bg-surface p-5 card-shadow sm:p-8">
      <div className="mb-6 flex flex-wrap items-start justify-between gap-4"><div><h2 className="font-heading text-xl font-bold text-text">{t('ticTacToe')}</h2><p className="mt-1 text-sm text-text-muted">{t('youPlayX')}</p></div><span className={`rounded-full px-3 py-1.5 text-sm font-semibold ${gameIsOver ? 'bg-primary/15 text-primary-light' : turn === 'X' ? 'bg-secondary/15 text-secondary' : 'bg-surface-2 text-text-muted'}`}>{status}</span></div>
      <div className="mx-auto grid w-full max-w-lg grid-cols-3 gap-2 rounded-3xl bg-bg p-2 sm:gap-3 sm:p-3">{board.map((cell, index) => <button key={index} type="button" aria-label={`${t('square')} ${index + 1}${cell ? `: ${cell}` : ''}`} disabled={turn !== 'X' || gameIsOver || Boolean(cell)} onClick={() => playMove(index)} className={`aspect-square rounded-2xl border border-border bg-surface text-4xl font-black shadow-sm transition sm:text-5xl ${cell === 'X' ? 'text-primary-light' : cell === 'O' ? 'text-secondary' : 'hover:bg-surface-2 focus-visible:bg-surface-2'} disabled:cursor-default`}>{cell}</button>)}</div>
      <div className="mt-6 flex flex-wrap justify-center gap-3"><button type="button" onClick={resetRound} className="rounded-lg bg-primary px-4 py-2 text-sm font-semibold text-white hover:bg-primary-dark">{t('newRound')}</button><button type="button" onClick={resetGame} className="rounded-lg border border-border bg-surface-2 px-4 py-2 text-sm font-semibold text-text hover:bg-surface-hover">{t('resetScore')}</button></div>
    </div>
    <aside className="space-y-4">
      <div className="rounded-3xl border border-border bg-surface p-5 card-shadow"><h2 className="font-heading text-lg font-bold text-text">{t('scoreboard')}</h2><dl className="mt-4 space-y-3"><Score label={`${t('you')} (X)`} value={score.player} style="bg-primary/15 text-primary-light" /><Score label={t('draws')} value={score.draw} style="bg-bg text-text" /><Score label={`${t('computer')} (O)`} value={score.computer} style="bg-secondary/15 text-secondary" /></dl></div>
      <div className="rounded-3xl border border-border bg-surface p-5 card-shadow"><h2 className="font-heading text-lg font-bold text-text">{t('howToPlay')}</h2><ol className="mt-3 space-y-2 text-sm leading-6 text-text-muted"><li>1. {t('gameRuleOne')}</li><li>2. {t('gameRuleTwo')}</li><li>3. {t('gameRuleThree')}</li></ol></div>
    </aside>
  </section>
}

function Score({ label, value, style }: { label: string; value: number; style: string }) {
  return <div className={`flex items-center justify-between rounded-2xl px-4 py-3 ${style}`}><dt className="font-medium">{label}</dt><dd className="text-xl font-bold">{value}</dd></div>
}

export default function GamesPage() {
  const { t } = usePreferences()
  const [selectedGame, setSelectedGame] = useState<GameId | null>(null)
  const game = games.find((item) => item.id === selectedGame)
  const ActiveGame = game?.component
  const [activeCategory, setActiveCategory] = useState('Tất cả')
  return <main className="games-page" style={{ animation: 'fade-in 0.25s ease both' }}><div className="games-page__inner">
    {ActiveGame && game ? <>
      <section className="games-detail-hero" aria-labelledby="games-detail-title">
        <div className={`games-detail-hero__art bg-linear-to-br ${game.accent}`}><div className="games-detail-hero__icon">{game.icon}</div></div>
        <div className="games-detail-hero__copy"><p className="games-kicker">{game.category} · FOOKBASE ARCADE</p><h1 id="games-detail-title">{game.title}</h1><p>{game.description}</p></div>
        <span className="games-detail-hero__badge">{game.badge}</span>
      </section>
      <button type="button" onClick={() => setSelectedGame(null)} className="games-back-button"><span aria-hidden="true">←</span> Tất cả trò chơi</button>
      <ActiveGame />
    </> : <>
      <section className="games-hero" aria-labelledby="games-page-title">
        <div className="games-hero__glow" />
        <div className="games-hero__copy"><p className="games-kicker">FOOKBASE PLAYROOM</p><h1 id="games-page-title">{t('quickBreak')}<br /><em>the fun way.</em></h1><p>Chọn một ván ngắn để đổi nhịp, luyện phản xạ hoặc rủ bạn bè cùng bay.</p><div className="games-hero__stats"><span><strong>{games.length}</strong> game</span><span><strong>{new Set(games.map((item) => item.category)).size}</strong> thể loại</span><span><strong>∞</strong> lượt vui</span></div></div>
        <div className="games-hero__mascot"><Mascot directions="/mascots/gearbot-directions.webp" reactions="/mascots/gearbot-reactions.webp" size={148} className="shrink-0" label="Game Robot Mascot" /><span>Chơi một ván nhé?</span></div>
      </section>
      <GameLibrary activeCategory={activeCategory} onCategoryChange={setActiveCategory} onSelect={(gameId) => { setSelectedGame(gameId); window.scrollTo({ top: 0, behavior: 'instant' }) }} />
    </>}
  </div></main>
}
