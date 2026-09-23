export const STAR_CATCH_DURATION = 30

export interface CatchItem {
  id: number
  x: number
  y: number
  kind: 'star' | 'meteor'
  speed: number
}

export interface StarCatchState {
  phase: 'ready' | 'playing' | 'paused' | 'over'
  shipX: number
  items: CatchItem[]
  score: number
  lives: number
  elapsed: number
  nextId: number
  event: 'star' | 'meteor' | 'miss' | null
}

const clamp = (value: number, min: number, max: number) => Math.max(min, Math.min(max, value))

function createItem(id: number, random: () => number, y = -8): CatchItem {
  return { id, x: 8 + random() * 84, y, kind: random() < 0.74 ? 'star' : 'meteor', speed: 17 + random() * 15 }
}

export function createStarCatch(random = Math.random): StarCatchState {
  const items = Array.from({ length: 6 }, (_, index) => createItem(index, random, -index * 16 - 8))
  return { phase: 'ready', shipX: 50, items, score: 0, lives: 3, elapsed: 0, nextId: items.length, event: null }
}

export function moveShip(state: StarCatchState, direction: -1 | 1, amount = 10): StarCatchState {
  if (state.phase !== 'playing') return state
  return { ...state, shipX: clamp(state.shipX + direction * amount, 10, 90) }
}

export function startStarCatch(state: StarCatchState): StarCatchState {
  return state.phase === 'ready' || state.phase === 'paused' ? { ...state, phase: 'playing', event: null } : state
}

export function advanceStarCatch(state: StarCatchState, seconds: number, random = Math.random): StarCatchState {
  if (state.phase !== 'playing' || !Number.isFinite(seconds) || seconds <= 0) return state
  const elapsed = Math.min(STAR_CATCH_DURATION, state.elapsed + Math.min(seconds, 0.1))
  let score = state.score
  let lives = state.lives
  let event: StarCatchState['event'] = null
  const items: CatchItem[] = []
  for (const item of state.items) {
    const y = item.y + item.speed * Math.min(seconds, 0.1)
    const caught = y >= 78 && y <= 94 && Math.abs(item.x - state.shipX) <= 11
    if (caught) {
      if (item.kind === 'star') { score += 10; event = 'star' }
      else { lives -= 1; event = 'meteor' }
      continue
    }
    if (y <= 106) items.push({ ...item, y })
    else if (item.kind === 'star') event = event ?? 'miss'
  }
  let nextId = state.nextId
  while (items.length < 6) items.push(createItem(nextId++, random))
  return { ...state, phase: lives <= 0 || elapsed >= STAR_CATCH_DURATION ? 'over' : 'playing', items, score, lives, elapsed, nextId, event }
}
