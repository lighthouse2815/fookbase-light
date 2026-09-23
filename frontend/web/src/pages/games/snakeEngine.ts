export const SNAKE_SIZE = 16
export type SnakeDirection = 'up' | 'down' | 'left' | 'right'
interface Point { x: number; y: number }
export interface SnakeState {
  body: Point[]
  direction: SnakeDirection
  nextDirection: SnakeDirection
  food: Point | null
  score: number
  phase: 'ready' | 'playing' | 'paused' | 'over' | 'won'
}
const vectors: Record<SnakeDirection, Point> = { up: { x: 0, y: -1 }, down: { x: 0, y: 1 }, left: { x: -1, y: 0 }, right: { x: 1, y: 0 } }
const samePoint = (first: Point, second: Point) => first.x === second.x && first.y === second.y

function spawnFood(body: Point[], random: () => number): Point | null {
  const occupied = new Set(body.map(({ x, y }) => y * SNAKE_SIZE + x))
  const empty = Array.from({ length: SNAKE_SIZE * SNAKE_SIZE }, (_, index) => index).filter((index) => !occupied.has(index))
  if (!empty.length) return null
  const index = empty[Math.floor(random() * empty.length)]
  return { x: index % SNAKE_SIZE, y: Math.floor(index / SNAKE_SIZE) }
}

export function createSnake(random = Math.random): SnakeState {
  const body = [{ x: 7, y: 8 }, { x: 6, y: 8 }, { x: 5, y: 8 }]
  return { body, direction: 'right', nextDirection: 'right', food: spawnFood(body, random), score: 0, phase: 'ready' }
}

export function turnSnake(state: SnakeState, direction: SnakeDirection): SnakeState {
  if (state.phase !== 'playing' || state.nextDirection !== state.direction) return state
  const current = vectors[state.direction]
  const next = vectors[direction]
  if (current.x + next.x === 0 && current.y + next.y === 0) return state
  return { ...state, nextDirection: direction }
}

export function stepSnake(state: SnakeState, random = Math.random): SnakeState {
  if (state.phase !== 'playing') return state
  const direction = state.nextDirection
  const vector = vectors[direction]
  const head = { x: state.body[0].x + vector.x, y: state.body[0].y + vector.y }
  const eating = state.food !== null && samePoint(head, state.food)
  const remaining = eating ? state.body : state.body.slice(0, -1)
  if (head.x < 0 || head.x >= SNAKE_SIZE || head.y < 0 || head.y >= SNAKE_SIZE || remaining.some((point) => samePoint(point, head))) {
    return { ...state, direction, phase: 'over' }
  }
  const body = [head, ...remaining]
  const food = eating ? spawnFood(body, random) : state.food
  return { ...state, body, direction, food, score: state.score + (eating ? 1 : 0), phase: food ? 'playing' : 'won' }
}
