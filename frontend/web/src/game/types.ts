export type Point3 = [number, number, number]
export interface Vec3 { x: number; y: number; z: number }

export interface WorldBox {
  id: string
  position: Point3
  size: Point3
  color: string
  solid?: boolean
  metalness?: number
}

export interface PlayerState {
  position: Vec3
  rotation: Vec3
  velocity: Vec3
  grounded: boolean
  movementSpeed: number
}

export interface PlayerInput {
  forward: number
  strafe: number
  sprint: boolean
  jump: boolean
  active: boolean
}

export type GamePhase = 'ready' | 'playing' | 'paused' | 'complete'
export type ObjectiveStage = 'key' | 'door' | 'power' | 'room' | 'signal' | 'complete'
export type InteractionId = 'key' | 'door' | 'generator' | 'receiver' | 'note' | 'lamp'

export interface GameProgress {
  stage: ObjectiveStage
  hasKey: boolean
  doorOpen: boolean
  powerOn: boolean
  eventTriggered: boolean
  noteRead: boolean
  lampOn: boolean
  message: string
}

export interface Interaction {
  id: InteractionId
  position: Point3
  label: string
}

export interface GameRuntime {
  player: PlayerState
  progress: GameProgress
  phase: GamePhase
  elapsed: number
  eventTime: number
}
