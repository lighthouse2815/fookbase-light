import type { GameProgress, Interaction, WorldBox, Point3 } from '../types.ts'

export const SPAWN: Point3 = [0, 0, 19]
export const WORLD_BOXES: WorldBox[] = [
  { id: 'station-west', position: [-6, 2, -7], size: [0.55, 4, 14], color: '#556264' },
  { id: 'station-east', position: [6, 2, -7], size: [0.55, 4, 14], color: '#556264' },
  { id: 'station-back', position: [0, 2, -14], size: [12.5, 4, 0.55], color: '#4b5759' },
  { id: 'station-front-left', position: [-3.8, 2, 0], size: [4.8, 4, 0.55], color: '#566563' },
  { id: 'station-front-right', position: [3.8, 2, 0], size: [4.8, 4, 0.55], color: '#566563' },
  { id: 'door-lintel', position: [0, 3.5, 0], size: [2.8, 1, 0.55], color: '#4a5757' },
  { id: 'station-roof', position: [0, 4.2, -7], size: [13, 0.45, 15], color: '#283b40', metalness: 0.35 },
  { id: 'station-floor', position: [0, -0.1, -7], size: [12, 0.2, 14], color: '#4c5351', solid: false },
  { id: 'partition-left', position: [-4, 2, -9], size: [4, 4, 0.35], color: '#425352' },
  { id: 'partition-right', position: [3, 2, -9], size: [6, 4, 0.35], color: '#425352' },
  { id: 'partition-lintel', position: [-1, 3.5, -9], size: [2, 1, 0.35], color: '#425352' },
  { id: 'shelter-back', position: [-10, 1.6, 5.7], size: [4.6, 3.2, 0.25], color: '#45453b' },
  { id: 'shelter-west', position: [-12.2, 1.6, 7.7], size: [0.25, 3.2, 4.2], color: '#45453b' },
  { id: 'shelter-east', position: [-7.8, 1.6, 6.7], size: [0.25, 3.2, 2.2], color: '#45453b' },
  { id: 'shelter-roof', position: [-10, 3.25, 7.7], size: [4.9, 0.2, 4.7], color: '#303632', metalness: 0.3 },
  { id: 'workbench', position: [-10, 0.5, 7.8], size: [2.6, 1, 1.1], color: '#655342' },
  { id: 'generator', position: [-4, 0.65, -5], size: [1.6, 1.3, 1.3], color: '#6a5e38', metalness: 0.65 },
  { id: 'receiver-desk', position: [1.8, 0.6, -12.4], size: [2.5, 1.2, 1.1], color: '#3f4946' },
  { id: 'crate-outside', position: [3.5, 0.5, 5.5], size: [1.4, 1, 1.4], color: '#65553f' },
  { id: 'crate-station', position: [4.3, 0.45, -4], size: [1.3, 0.9, 1.3], color: '#5f503b' },
  { id: 'cabinet', position: [5.2, 1.25, -7.2], size: [0.8, 2.5, 1.5], color: '#394b4b', metalness: 0.5 },
  { id: 'boundary-west', position: [-24, 1.6, 0], size: [0.4, 3.2, 56], color: '#2b3433' },
  { id: 'boundary-east', position: [24, 1.6, 0], size: [0.4, 3.2, 56], color: '#2b3433' },
  { id: 'boundary-north', position: [0, 1.6, -28], size: [48, 3.2, 0.4], color: '#2b3433' },
  { id: 'boundary-south', position: [0, 1.6, 28], size: [48, 3.2, 0.4], color: '#2b3433' },
]

export const ROCKS: WorldBox[] = [
  { id: 'rock-1', position: [7.8, 1, 10], size: [3, 2, 2.4], color: '#4e5855' },
  { id: 'rock-2', position: [-5.2, 0.9, 15], size: [2.2, 1.8, 2.6], color: '#465551' },
  { id: 'rock-3', position: [10, 1.2, -2], size: [3.5, 2.4, 3], color: '#4d5757' },
  { id: 'rock-4', position: [-15, 0.9, -7], size: [3, 1.8, 2.6], color: '#4b5755' },
  { id: 'rock-5', position: [14, 0.7, 19], size: [2.2, 1.4, 2.2], color: '#4d5955' },
]

export const TREES: Point3[] = [
  [-16, 0, 19], [-13, 0, 21], [-20, 0, 11], [-16, 0, 2], [-18, 0, -12],
  [-14, 0, -19], [-8, 0, -21], [0, 0, -23], [8, 0, -21], [17, 0, -17],
  [20, 0, -8], [16, 0, 1], [13, 0, 8], [19, 0, 15], [20, 0, 23],
  [-20, 0, 24], [8, 0, 23], [-8, 0, 24], [-20, 0, -23], [21, 0, -23],
]

export const DOOR_COLLIDER: WorldBox = { id: 'station-door', position: [0, 1.45, 0], size: [2.65, 2.9, 0.22], color: '#4d4b3e' }
const staticColliders = [
  ...WORLD_BOXES.filter((box) => box.solid !== false), ...ROCKS,
  ...TREES.map((position, index): WorldBox => ({ id: `tree-${index}`, position: [position[0], 2.5, position[2]], size: [0.75, 5, 0.75], color: '#383b30' })),
]
const closedColliders = [...staticColliders, DOOR_COLLIDER]
const openColliders = [...staticColliders, {
  id: 'open-door-leaf', position: [-1.23, 1.45, 1.32], size: [0.41, 2.9, 2.66], color: DOOR_COLLIDER.color,
} satisfies WorldBox]
export function getColliders(progress: GameProgress): readonly WorldBox[] {
  return progress.doorOpen ? openColliders : closedColliders
}

export const INTERACTIONS: Interaction[] = [
  { id: 'key', position: [-9.4, 1.14, 8], label: 'Nhặt chìa khóa nhà trạm' },
  { id: 'note', position: [-10.7, 1.09, 8], label: 'Đọc ghi chú của người gác trạm' },
  { id: 'door', position: [0, 1.35, 0.3], label: 'Mở cửa nhà trạm' },
  { id: 'generator', position: [-4, 1.1, -4.3], label: 'Khởi động máy phát điện' },
  { id: 'receiver', position: [1.8, 1.6, -11.78], label: 'Điều tra máy thu tín hiệu' },
  { id: 'lamp', position: [-11.8, 1.6, 9.5], label: 'Bật / tắt đèn lán' },
]
