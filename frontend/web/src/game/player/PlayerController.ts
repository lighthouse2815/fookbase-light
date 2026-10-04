import type { PlayerInput, PlayerState, WorldBox, Vec3 } from '../types.ts'
import { SPAWN } from '../world/worldData.ts'

export const PLAYER_RADIUS = 0.32
export const PLAYER_HEIGHT = 1.75
export const EYE_HEIGHT = 1.62
const WALK_SPEED = 3.2
const RUN_SPEED = 5.8
const GRAVITY = 20

export function createPlayer(): PlayerState {
  return {
    position: { x: SPAWN[0], y: SPAWN[1], z: SPAWN[2] },
    rotation: { x: 0, y: 0, z: 0 }, velocity: { x: 0, y: 0, z: 0 },
    grounded: true, movementSpeed: WALK_SPEED,
  }
}

function overlaps(position: Vec3, box: WorldBox): boolean {
  return position.x + PLAYER_RADIUS > box.position[0] - box.size[0] / 2 + 1e-7
    && position.x - PLAYER_RADIUS < box.position[0] + box.size[0] / 2 - 1e-7
    && position.y + PLAYER_HEIGHT > box.position[1] - box.size[1] / 2 + 1e-7
    && position.y < box.position[1] + box.size[1] / 2 - 1e-7
    && position.z + PLAYER_RADIUS > box.position[2] - box.size[2] / 2 + 1e-7
    && position.z - PLAYER_RADIUS < box.position[2] + box.size[2] / 2 - 1e-7
}

function moveAxis(player: PlayerState, axis: 'x' | 'y' | 'z', dt: number, colliders: readonly WorldBox[]) {
  const velocity = player.velocity[axis]
  if (velocity === 0) return
  player.position[axis] += velocity * dt
  const index = axis === 'x' ? 0 : axis === 'y' ? 1 : 2
  for (const box of colliders) {
    if (!overlaps(player.position, box)) continue
    const edge = box.position[index] + (velocity > 0 ? -1 : 1) * box.size[index] / 2
    player.position[axis] = edge + (axis === 'y' ? (velocity > 0 ? -PLAYER_HEIGHT : 0) : (velocity > 0 ? -PLAYER_RADIUS : PLAYER_RADIUS))
    player.velocity[axis] = 0
    if (axis === 'y' && velocity < 0) player.grounded = true
  }
}

/** Mutates the reusable player state; bounded fixed substeps prevent tunneling. */
export function stepPlayer(player: PlayerState, input: PlayerInput, yaw: number, delta: number, colliders: readonly WorldBox[]): void {
  if (!input.active || !Number.isFinite(delta) || delta <= 0) return
  player.rotation.y = yaw
  player.movementSpeed = input.sprint ? RUN_SPEED : WALK_SPEED
  const length = Math.max(1, Math.hypot(input.forward, input.strafe))
  const forward = input.forward / length
  const strafe = input.strafe / length
  const targetX = (strafe * Math.cos(yaw) - forward * Math.sin(yaw)) * player.movementSpeed
  const targetZ = (-strafe * Math.sin(yaw) - forward * Math.cos(yaw)) * player.movementSpeed
  if (input.jump && player.grounded) {
    player.velocity.y = 6
    player.grounded = false
  }
  const duration = Math.min(delta, 0.25)
  const count = Math.ceil(duration / (1 / 120))
  const dt = duration / count
  const damping = 1 - Math.exp(-14 * dt)
  for (let i = 0; i < count; i++) {
    player.velocity.x += (targetX - player.velocity.x) * damping
    player.velocity.z += (targetZ - player.velocity.z) * damping
    moveAxis(player, 'x', dt, colliders)
    moveAxis(player, 'z', dt, colliders)
    player.grounded = false
    player.velocity.y -= GRAVITY * dt
    moveAxis(player, 'y', dt, colliders)
    if (player.position.y <= 0) {
      player.position.y = 0
      player.velocity.y = 0
      player.grounded = true
    }
  }
}
