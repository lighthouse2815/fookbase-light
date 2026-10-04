import type { GameProgress, InteractionId, Vec3, WorldBox } from '../types.ts'
import { INTERACTIONS } from '../world/worldData.ts'
import { EYE_HEIGHT } from '../player/PlayerController.ts'
import { isInteractionEnabled } from '../gameplay/ObjectiveSystem.ts'

/** Segment/AABB slab test: nearby objects still cannot be used through walls. */
function occludes(x: number, y: number, z: number, dx: number, dy: number, dz: number, box: WorldBox) {
  let near = 0
  let far = 1
  for (let axis = 0; axis < 3; axis++) {
    const origin = axis === 0 ? x : axis === 1 ? y : z
    const direction = axis === 0 ? dx : axis === 1 ? dy : dz
    const min = box.position[axis] - box.size[axis] / 2
    const max = box.position[axis] + box.size[axis] / 2
    if (Math.abs(direction) < 1e-8) {
      if (origin < min || origin > max) return false
    } else {
      const one = (min - origin) / direction
      const two = (max - origin) / direction
      near = Math.max(near, Math.min(one, two))
      far = Math.min(far, Math.max(one, two))
      if (near > far) return false
    }
  }
  return near < 0.98 && far > 0.001
}

export function findInteraction(position: Vec3, direction: Vec3, progress: GameProgress, colliders: readonly WorldBox[]): InteractionId | null {
  let nearest: InteractionId | null = null
  let closest = 2.6
  const eyeY = position.y + EYE_HEIGHT
  const directionLength = Math.hypot(direction.x, direction.y, direction.z)
  if (directionLength < 1e-8) return null
  for (const item of INTERACTIONS) {
    if (!isInteractionEnabled(progress, item.id)) continue
    const dx = item.position[0] - position.x
    const dy = item.position[1] - eyeY
    const dz = item.position[2] - position.z
    const distance = Math.hypot(dx, dy, dz)
    if (distance > closest || distance < 1e-6) continue
    const alignment = (dx * direction.x + dy * direction.y + dz * direction.z) / (distance * directionLength)
    if (alignment < 0.55) continue
    if (colliders.some((box) => occludes(position.x, eyeY, position.z, dx, dy, dz, box))) continue
    closest = distance
    nearest = item.id
  }
  return nearest
}
