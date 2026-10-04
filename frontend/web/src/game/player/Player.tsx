import { useEffect, useMemo, useRef, type RefObject } from 'react'
import { useFrame, useThree } from '@react-three/fiber'
import { Euler, Group, Object3D, SpotLight, Vector3 } from 'three'
import type { GameDiagnostics, GameProgress, GameRuntime, InteractionId, PlayerInput } from '../types'
import type { KeyboardState } from '../hooks/useKeyboard'
import { EYE_HEIGHT, stepPlayer } from './PlayerController'
import { getColliders, SPAWN } from '../world/worldData'
import { findInteraction } from '../interaction/InteractionSystem'
import { interact } from '../gameplay/ObjectiveSystem'
import { enterStrangeRoom } from '../gameplay/TriggerSystem'

interface PlayerProps {
  runtimeRef: RefObject<GameRuntime>
  keyboard: KeyboardState
  runId: number
  onProgress: (progress: GameProgress) => void
  onPrompt: (id: InteractionId | null) => void
  onComplete: () => void
  onDiagnostics: (diagnostics: GameDiagnostics) => void
}

export function Player({ runtimeRef, keyboard, runId, onProgress, onPrompt, onComplete, onDiagnostics }: PlayerProps) {
  const { keys: keysRef, actions: actionsRef } = keyboard
  const { camera, gl } = useThree()
  const body = useRef<Group>(null)
  const hand = useRef<Group>(null)
  const torch = useRef<SpotLight>(null)
  const torchTarget = useMemo(() => new Object3D(), [])
  const direction = useMemo(() => new Vector3(), [])
  const rotation = useMemo(() => new Euler(0, 0, 0, 'YXZ'), [])
  const inputRef = useRef<PlayerInput>({ forward: 0, strafe: 0, sprint: false, jump: false, active: true })
  const started = useRef(false)
  const prompt = useRef<InteractionId | null>(null)
  const lastCheck = useRef(0)
  const reportTime = useRef(0)
  const frames = useRef(0)
  const reportDelta = useRef(0)

  useEffect(() => {
    started.current = false
    prompt.current = null
    lastCheck.current = 0
    reportTime.current = 0
    frames.current = 0
    reportDelta.current = 0
    camera.position.set(15, 9, 23)
    camera.lookAt(0, 1.3, -3)
    if (body.current) body.current.position.set(...SPAWN)
  }, [runId, camera])

  useFrame((_, delta) => {
    const current = runtimeRef.current
    if (current.phase !== 'playing') return
    const player = current.player
    if (!started.current) {
      camera.rotation.set(0, 0, 0, 'YXZ')
      camera.position.set(player.position.x, player.position.y + EYE_HEIGHT, player.position.z)
      started.current = true
    }
    current.elapsed += Math.min(delta, 0.25)
    rotation.setFromQuaternion(camera.quaternion, 'YXZ')
    const keys = keysRef.current
    const input = inputRef.current
    input.forward = Number(keys.has('KeyW')) - Number(keys.has('KeyS'))
    input.strafe = Number(keys.has('KeyD')) - Number(keys.has('KeyA'))
    input.sprint = keys.has('ShiftLeft') || keys.has('ShiftRight')
    input.jump = actionsRef.current.jump
    actionsRef.current.jump = false
    stepPlayer(player, input, rotation.y, delta, getColliders(current.progress))
    player.rotation.x = rotation.x
    const speed = Math.hypot(player.velocity.x, player.velocity.z)
    const bob = player.grounded ? Math.sin(current.elapsed * (input.sprint ? 13 : 9)) * 0.018 * Math.min(speed / 3.2, 1) : 0
    camera.position.set(player.position.x, player.position.y + EYE_HEIGHT + bob, player.position.z)
    if (body.current) { body.current.position.set(player.position.x, player.position.y, player.position.z); body.current.rotation.y = rotation.y }
    camera.getWorldDirection(direction)
    if (hand.current) { hand.current.position.copy(camera.position); hand.current.quaternion.copy(camera.quaternion) }
    if (torch.current) {
      torch.current.position.copy(camera.position)
      torchTarget.position.copy(direction).multiplyScalar(12).add(camera.position)
      torch.current.target = torchTarget
    }

    if (current.elapsed - lastCheck.current >= 0.08 || actionsRef.current.interact) {
      lastCheck.current = current.elapsed
      const nextPrompt = findInteraction(player.position, direction, current.progress, getColliders(current.progress))
      if (nextPrompt !== prompt.current) { prompt.current = nextPrompt; onPrompt(nextPrompt) }
      if (actionsRef.current.interact && nextPrompt) onProgress(interact(current.progress, nextPrompt))
      actionsRef.current.interact = false
    }
    const nextProgress = enterStrangeRoom(current.progress, player.position)
    if (nextProgress !== current.progress) { current.eventTime = current.elapsed; onProgress(nextProgress) }
    if (current.progress.stage === 'complete') { onPrompt(null); onComplete(); return }

    frames.current++
    reportDelta.current += delta
    if (current.elapsed - reportTime.current >= (import.meta.env.DEV ? 0.3 : 1)) {
      reportTime.current = current.elapsed
      onDiagnostics({ elapsed: current.elapsed, fps: Math.round(frames.current / reportDelta.current), calls: gl.info.render.calls, position: [player.position.x, player.position.y, player.position.z] })
      frames.current = 0; reportDelta.current = 0
    }
  })

  return <>
    <group ref={body} name="PlayerBody" position={[0, 0, 19]}>
      <mesh position={[0, 0.83, 0.13]} castShadow><capsuleGeometry args={[0.25, 1.05, 4, 8]} /><meshStandardMaterial color="#253a35" roughness={0.95} /></mesh>
      <mesh position={[0, 1.3, 0.12]} castShadow><boxGeometry args={[0.48, 0.2, 0.25]} /><meshStandardMaterial color="#2e423b" /></mesh>
    </group>
    <group ref={hand} name="PlayerFlashlight">
      <mesh position={[0.24, -0.28, -0.43]} rotation={[0.18, -0.12, 0]}><boxGeometry args={[0.16, 0.16, 0.28]} /><meshStandardMaterial color="#49564b" roughness={0.95} /></mesh>
      <mesh position={[0.24, -0.24, -0.54]} rotation={[Math.PI / 2, 0, 0]}><cylinderGeometry args={[0.065, 0.085, 0.32, 12]} /><meshStandardMaterial color="#262f30" metalness={0.65} roughness={0.4} /></mesh>
      <mesh position={[0.24, -0.24, -0.715]} rotation={[Math.PI / 2, 0, 0]}><cylinderGeometry args={[0.064, 0.064, 0.014, 12]} /><meshStandardMaterial color="#fff0c4" emissive="#eacc87" emissiveIntensity={0.6} /></mesh>
    </group>
    <spotLight ref={torch} name="FlashlightBeam" color="#fff1c9" intensity={38} distance={20} angle={0.48} penumbra={0.55} decay={2} castShadow shadow-mapSize={[512, 512]} shadow-bias={-0.0002} shadow-normalBias={0.025} />
    <primitive object={torchTarget} />
  </>
}
