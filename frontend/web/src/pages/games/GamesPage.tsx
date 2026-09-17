import { useEffect, useMemo, useRef, useState } from 'react'
import { usePreferences } from '../../preferences'

type Mark = 'X' | 'O'
type Cell = Mark | null

const WINNING_LINES = [
  [0, 1, 2],
  [3, 4, 5],
  [6, 7, 8],
  [0, 3, 6],
  [1, 4, 7],
  [2, 5, 8],
  [0, 4, 8],
  [2, 4, 6],
] as const

const createBoard = (): Cell[] => Array<Cell>(9).fill(null)

type FlappyPipe = { id: number; x: number; gapCenter: number; scored: boolean }

const FLAPPY_BIRD_X = 27
const FLAPPY_BIRD_SIZE = 8
const FLAPPY_PIPE_WIDTH = 15
const FLAPPY_GAP_SIZE = 28

function FlappyBirdGame() {
  const [status, setStatus] = useState<'ready' | 'playing' | 'gameover'>('ready')
  const [birdY, setBirdY] = useState(45)
  const [pipes, setPipes] = useState<FlappyPipe[]>([])
  const [score, setScore] = useState(0)
  const [bestScore, setBestScore] = useState(0)
  const birdYRef = useRef(45)
  const nextPipeIdRef = useRef(1)
  const tickRef = useRef(0)

  const createPipe = (x = 100): FlappyPipe => ({
    id: nextPipeIdRef.current++,
    x,
    gapCenter: 26 + Math.random() * 48,
    scored: false,
  })

  const reset = () => {
    birdYRef.current = 45
    tickRef.current = 0
    setBirdY(45)
    setPipes([])
    setScore(0)
    setStatus('ready')
  }

  const flap = () => {
    if (status === 'gameover') {
      reset()
      return
    }
    if (status === 'ready') {
      setStatus('playing')
      setPipes([createPipe(88)])
    }
    const nextY = Math.max(2, birdYRef.current - 9)
    birdYRef.current = nextY
    setBirdY(nextY)
  }

  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.code !== 'Space' && event.code !== 'ArrowUp') return
      event.preventDefault()
      flap()
    }
    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  })

  useEffect(() => {
    if (status !== 'playing') return
    const timer = window.setInterval(() => {
      tickRef.current += 1
      const nextBirdY = birdYRef.current + 1.25
      birdYRef.current = nextBirdY
      setBirdY(nextBirdY)
      if (nextBirdY < 0 || nextBirdY + FLAPPY_BIRD_SIZE > 100) {
        setBestScore((current) => Math.max(current, score))
        setStatus('gameover')
        return
      }

      setPipes((current) => {
        const moved = current
          .map((pipe) => ({ ...pipe, x: pipe.x - 1.15 }))
          .filter((pipe) => pipe.x + FLAPPY_PIPE_WIDTH > -2)
        if (tickRef.current % 105 === 0) moved.push(createPipe())

        let gainedPoint = false
        const next = moved.map((pipe) => {
          if (!pipe.scored && pipe.x + FLAPPY_PIPE_WIDTH < FLAPPY_BIRD_X) {
            gainedPoint = true
            return { ...pipe, scored: true }
          }
          return pipe
        })
        if (gainedPoint) setScore((current) => current + 1)

        const hitPipe = next.some((pipe) => {
          const overlapsPipe = pipe.x < FLAPPY_BIRD_X + FLAPPY_BIRD_SIZE && pipe.x + FLAPPY_PIPE_WIDTH > FLAPPY_BIRD_X
          const gapTop = pipe.gapCenter - FLAPPY_GAP_SIZE / 2
          const gapBottom = pipe.gapCenter + FLAPPY_GAP_SIZE / 2
          return overlapsPipe && (nextBirdY < gapTop || nextBirdY + FLAPPY_BIRD_SIZE > gapBottom)
        })
        if (hitPipe) {
          setBestScore((current) => Math.max(current, score))
          setStatus('gameover')
        }
        return next
      })
    }, 25)
    return () => window.clearInterval(timer)
  }, [score, status])

  const message = status === 'ready' ? 'Nhấn để bắt đầu' : status === 'gameover' ? 'Chạm để chơi lại' : null

  return <section className="rounded-3xl border border-border bg-surface p-5 card-shadow sm:p-8">
    <div className="mb-5 flex flex-wrap items-start justify-between gap-3">
      <div><h2 className="font-heading text-xl font-bold text-text">Flappy Bird</h2><p className="mt-1 text-sm text-text-muted">Nhấp chuột, chạm màn hình hoặc nhấn phím cách để bay.</p></div>
      <div className="flex gap-2 text-sm font-semibold"><span className="rounded-full bg-primary/15 px-3 py-1.5 text-primary-light">Điểm: {score}</span><span className="rounded-full bg-surface-2 px-3 py-1.5 text-text-muted">Kỷ lục: {bestScore}</span></div>
    </div>
    <button type="button" onClick={flap} className="relative mx-auto block aspect-[9/14] w-full max-w-[320px] cursor-pointer overflow-hidden rounded-3xl border-4 border-surface-3 bg-gradient-to-b from-sky-400 via-sky-300 to-emerald-200 shadow-inner focus-visible:ring-4 focus-visible:ring-primary" aria-label={message ?? 'Làm chim bay'}>
      <span className="absolute inset-x-0 bottom-0 h-[12%] bg-emerald-500/80" />
      <span className="absolute inset-x-0 bottom-[12%] h-2 bg-emerald-700/60" />
      {pipes.map((pipe) => {
        const gapTop = pipe.gapCenter - FLAPPY_GAP_SIZE / 2
        const gapBottom = pipe.gapCenter + FLAPPY_GAP_SIZE / 2
        return <span key={pipe.id} className="absolute inset-y-0" style={{ left: `${pipe.x}%`, width: `${FLAPPY_PIPE_WIDTH}%` }}>
          <span className="absolute inset-x-0 top-0 rounded-b-md border-x-2 border-b-2 border-emerald-800 bg-emerald-500" style={{ height: `${gapTop}%` }} />
          <span className="absolute inset-x-0 bottom-0 rounded-t-md border-x-2 border-t-2 border-emerald-800 bg-emerald-500" style={{ top: `${gapBottom}%` }} />
        </span>
      })}
      <span className="absolute z-10 grid place-items-center rounded-full border-2 border-amber-500 bg-yellow-300 text-lg shadow-md transition-transform" style={{ left: `${FLAPPY_BIRD_X}%`, top: `${birdY}%`, width: `${FLAPPY_BIRD_SIZE}%`, height: `${FLAPPY_BIRD_SIZE}%`, transform: 'translateY(-50%)' }}>🐤</span>
      {message && <span className="absolute inset-0 z-20 grid place-items-center bg-black/15 px-8 text-center text-xl font-bold text-white drop-shadow-md">{message}</span>}
    </button>
    <div className="mt-4 flex justify-center"><button type="button" onClick={(event) => { event.stopPropagation(); reset() }} className="rounded-lg border border-border bg-surface-2 px-4 py-2 text-sm font-semibold text-text transition hover:bg-surface-hover">Chơi lại từ đầu</button></div>
  </section>
}

const getWinner = (board: Cell[]): Mark | null => {
  for (const [first, second, third] of WINNING_LINES) {
    const mark = board[first]
    if (mark && mark === board[second] && mark === board[third]) {
      return mark
    }
  }

  return null
}

const getAvailableMoves = (board: Cell[]) => board.flatMap((cell, index) => (cell ? [] : [index]))

const findMoveFor = (board: Cell[], mark: Mark) => {
  for (const move of getAvailableMoves(board)) {
    const trialBoard = [...board]
    trialBoard[move] = mark

    if (getWinner(trialBoard) === mark) {
      return move
    }
  }

  return null
}

const chooseComputerMove = (board: Cell[]) => {
  const winningMove = findMoveFor(board, 'O')
  if (winningMove !== null) return winningMove

  const blockingMove = findMoveFor(board, 'X')
  if (blockingMove !== null) return blockingMove

  if (!board[4]) return 4

  const availableMoves = getAvailableMoves(board)
  const corner = [0, 2, 6, 8].find((move) => availableMoves.includes(move))
  return corner ?? availableMoves[0] ?? null
}

export default function GamesPage() {
  const { t } = usePreferences()
  const [board, setBoard] = useState<Cell[]>(createBoard)
  const [turn, setTurn] = useState<Mark>('X')
  const [winner, setWinner] = useState<Mark | null>(null)
  const [isDraw, setIsDraw] = useState(false)
  const [score, setScore] = useState({ player: 0, computer: 0, draw: 0 })

  const gameIsOver = Boolean(winner) || isDraw
  const status = useMemo(() => {
    if (winner === 'X') return t('youWonRound')
    if (winner === 'O') return t('computerWonRound')
    if (isDraw) return t('drawRound')
    return turn === 'X' ? t('yourTurn') : t('computerThinking')
  }, [isDraw, t, turn, winner])

  const finishMove = (nextBoard: Cell[], mark: Mark) => {
    const roundWinner = getWinner(nextBoard)
    if (roundWinner === 'X') {
      setWinner(roundWinner)
      setScore((currentScore) => ({ ...currentScore, player: currentScore.player + 1 }))
      return
    }

    if (roundWinner === 'O') {
      setWinner(roundWinner)
      setScore((currentScore) => ({ ...currentScore, computer: currentScore.computer + 1 }))
      return
    }

    if (nextBoard.every(Boolean)) {
      setIsDraw(true)
      setScore((currentScore) => ({ ...currentScore, draw: currentScore.draw + 1 }))
      return
    }

    setTurn(mark === 'X' ? 'O' : 'X')
  }

  const playMove = (index: number) => {
    if (turn !== 'X' || board[index] || gameIsOver) return

    const nextBoard = [...board]
    nextBoard[index] = 'X'
    setBoard(nextBoard)
    finishMove(nextBoard, 'X')
  }

  const resetRound = () => {
    setBoard(createBoard())
    setTurn('X')
    setWinner(null)
    setIsDraw(false)
  }

  const resetGame = () => {
    setScore({ player: 0, computer: 0, draw: 0 })
    resetRound()
  }

  useEffect(() => {
    if (turn !== 'O' || gameIsOver) return

    const timer = window.setTimeout(() => {
      const move = chooseComputerMove(board)
      if (move === null) return

      const nextBoard = [...board]
      nextBoard[move] = 'O'
      setBoard(nextBoard)
      finishMove(nextBoard, 'O')
    }, 450)

    return () => window.clearTimeout(timer)
  }, [board, gameIsOver, turn])

  return (
    <main className="min-h-screen bg-bg p-4 xl:p-6" style={{ animation: 'fade-in 0.25s ease both' }}>
      <div className="mx-auto max-w-5xl">
        <header className="mb-6">
          <p className="mb-1 text-sm font-semibold uppercase tracking-wider text-primary">{t('games')}</p>
          <h1 className="font-heading text-3xl font-bold text-text">{t('quickBreak')}</h1>
          <p className="mt-2 text-text-muted">{t('gamesDescription')}</p>
        </header>

        <section className="grid items-start gap-6 lg:grid-cols-[minmax(0,1fr)_300px]">
          <div className="rounded-3xl border border-border bg-surface p-5 card-shadow sm:p-8">
            <div className="mb-6 flex flex-wrap items-start justify-between gap-4">
              <div>
                <h2 className="font-heading text-xl font-bold text-text">{t('ticTacToe')}</h2>
                <p className="mt-1 text-sm text-text-muted">{t('youPlayX')}</p>
              </div>
              <span
                className={`rounded-full px-3 py-1.5 text-sm font-semibold ${
                  gameIsOver ? 'bg-primary/15 text-primary-light' : turn === 'X' ? 'bg-secondary/15 text-secondary' : 'bg-surface-2 text-text-muted'
                }`}
              >
                {status}
              </span>
            </div>

            <div className="mx-auto grid w-full max-w-sm grid-cols-3 gap-2 rounded-3xl bg-bg p-2 sm:gap-3 sm:p-3">
              {board.map((cell, index) => (
                <button
                  key={index}
                  type="button"
                  aria-label={`${t('square')} ${index + 1}${cell ? `: ${cell}` : ''}`}
                  disabled={turn !== 'X' || gameIsOver || Boolean(cell)}
                  onClick={() => playMove(index)}
                  className={`aspect-square rounded-2xl border border-border bg-surface text-4xl font-black shadow-sm transition sm:text-5xl ${
                    cell === 'X' ? 'text-primary-light' : cell === 'O' ? 'text-secondary' : 'hover:bg-surface-2 focus-visible:bg-surface-2'
                  } disabled:cursor-default`}
                >
                  {cell}
                </button>
              ))}
            </div>

            <div className="mt-6 flex flex-wrap justify-center gap-3">
              <button type="button" onClick={resetRound} className="rounded-lg bg-primary px-4 py-2 text-sm font-semibold text-white transition hover:bg-primary-dark">
                {t('newRound')}
              </button>
              <button type="button" onClick={resetGame} className="rounded-lg border border-border bg-surface-2 px-4 py-2 text-sm font-semibold text-text transition hover:bg-surface-hover">
                {t('resetScore')}
              </button>
            </div>
          </div>

          <aside className="space-y-4">
            <div className="rounded-3xl border border-border bg-surface p-5 card-shadow">
              <h2 className="font-heading text-lg font-bold text-text">{t('scoreboard')}</h2>
              <dl className="mt-4 space-y-3">
                <div className="flex items-center justify-between rounded-2xl bg-primary/15 px-4 py-3">
                  <dt className="font-medium text-primary-light">{t('you')} (X)</dt>
                  <dd className="text-xl font-bold text-primary-light">{score.player}</dd>
                </div>
                <div className="flex items-center justify-between rounded-2xl bg-bg px-4 py-3">
                  <dt className="font-medium text-text-muted">{t('draws')}</dt>
                  <dd className="text-xl font-bold text-text">{score.draw}</dd>
                </div>
                <div className="flex items-center justify-between rounded-2xl bg-secondary/15 px-4 py-3">
                  <dt className="font-medium text-secondary">{t('computer')} (O)</dt>
                  <dd className="text-xl font-bold text-secondary">{score.computer}</dd>
                </div>
              </dl>
            </div>

            <div className="rounded-3xl border border-border bg-surface p-5 card-shadow">
              <h2 className="font-heading text-lg font-bold text-text">{t('howToPlay')}</h2>
              <ol className="mt-3 space-y-2 text-sm leading-6 text-text-muted">
                <li>1. {t('gameRuleOne')}</li>
                <li>2. {t('gameRuleTwo')}</li>
                <li>3. {t('gameRuleThree')}</li>
              </ol>
            </div>
          </aside>
        </section>

        <div className="mt-6"><FlappyBirdGame /></div>
      </div>
    </main>
  )
}
