import type { GameProgress, Vec3 } from '../types.ts'

export function enterStrangeRoom(progress: GameProgress, position: Vec3): GameProgress {
  if (!progress.powerOn || progress.eventTriggered) return progress
  if (Math.abs(position.x) >= 5.5 || position.y >= 3 || position.z >= -10.4 || position.z <= -13.8) return progress
  return { ...progress, stage: 'signal', eventTriggered: true, doorOpen: false, message: 'Cánh cửa vừa đóng lại. Tín hiệu không còn là tiếng nhiễu…' }
}
