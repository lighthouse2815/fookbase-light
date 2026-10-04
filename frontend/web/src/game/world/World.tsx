import { useRef, type RefObject } from 'react'
import { useFrame } from '@react-three/fiber'
import { Group, MathUtils, PointLight } from 'three'
import type { GameProgress, GameRuntime, Point3 } from '../types'
import { INTERACTIONS, ROCKS, WORLD_BOXES } from './worldData'

function Lamp({ position, color, intensity }: { position: Point3; color: string; intensity: number }) {
  return <group position={position}>
    <mesh><boxGeometry args={[0.34, 0.18, 0.24]} /><meshStandardMaterial color={color} emissive={color} emissiveIntensity={intensity > 0 ? 2 : 0} /></mesh>
    <pointLight position={[0, -0.1, 0.2]} color={color} intensity={intensity} distance={11} decay={2} />
  </group>
}

function StationSign() {
  return <group position={[0, 3.42, 0.34]}>
    <mesh><boxGeometry args={[1.9, 0.8, 0.08]} /><meshStandardMaterial color="#1b3339" /></mesh>
    <mesh position={[-0.29, 0, 0.06]}><torusGeometry args={[0.22, 0.035, 6, 18]} /><meshStandardMaterial color="#e9c983" emissive="#dcb666" emissiveIntensity={0.25} /></mesh>
    <mesh position={[0.28, 0.22, 0.06]}><boxGeometry args={[0.42, 0.06, 0.06]} /><meshStandardMaterial color="#e9c983" /></mesh>
    <mesh position={[0.32, -0.025, 0.06]} rotation={[0, 0, -0.42]}><boxGeometry args={[0.06, 0.52, 0.06]} /><meshStandardMaterial color="#e9c983" /></mesh>
  </group>
}

export function World({ runtime, progress }: { runtime: RefObject<GameRuntime>; progress: GameProgress }) {
  const door = useRef<Group>(null)
  const key = useRef<Group>(null)
  const roomLight = useRef<PointLight>(null)
  const apparition = useRef<Group>(null)

  useFrame((_, delta) => {
    const current = runtime.current
    if (door.current) {
      if (current.phase === 'ready') door.current.rotation.y = 0
      else if (current.phase === 'playing') door.current.rotation.y = MathUtils.damp(door.current.rotation.y, current.progress.doorOpen ? -1.5 : 0, 9, Math.min(delta, 0.25))
    }
    if (key.current && current.phase === 'playing') key.current.rotation.y = Math.sin(current.elapsed * 1.4) * 0.12
    if (roomLight.current) {
      const age = current.elapsed - current.eventTime
      const flicker = current.progress.eventTriggered && age < 3.5 ? (Math.sin(age * 31) > -0.1 ? 1 : 0.06) : 1
      roomLight.current.intensity = current.progress.powerOn ? 18 * flicker : 0
    }
    if (apparition.current) apparition.current.visible = current.progress.eventTriggered && current.elapsed - current.eventTime < 4
  })

  return <group name="TransmissionStation">
    {WORLD_BOXES.map((box) => <mesh key={box.id} name={box.id} position={box.position} castShadow receiveShadow>
      <boxGeometry args={box.size} /><meshStandardMaterial color={box.color} roughness={box.metalness ? 0.6 : 0.93} metalness={box.metalness ?? 0} />
    </mesh>)}
    {ROCKS.map((rock, index) => <mesh key={rock.id} name={rock.id} position={rock.position} scale={rock.size.map((value) => value / 2) as Point3} rotation={[0, index * 1.9, 0]} castShadow receiveShadow>
      <dodecahedronGeometry args={[1, 0]} /><meshStandardMaterial color={rock.color} roughness={1} />
    </mesh>)}

    <group ref={door} name="StationDoor" position={[-1.325, 0, 0]}>
      <mesh position={[1.325, 1.45, 0]} castShadow receiveShadow><boxGeometry args={[2.65, 2.9, 0.22]} /><meshStandardMaterial color="#5e6253" metalness={0.55} roughness={0.65} /></mesh>
      <mesh position={[2.4, 1.35, 0.16]}><boxGeometry args={[0.08, 0.26, 0.1]} /><meshStandardMaterial color="#c7ac70" metalness={0.8} roughness={0.35} /></mesh>
      <mesh position={[1.325, 2.05, 0.13]}><boxGeometry args={[1.55, 0.13, 0.02]} /><meshStandardMaterial color="#b1a776" /></mesh>
    </group>
    <StationSign />
    <Lamp position={[0, 3.25, 0.8]} color="#e4bc78" intensity={16} />
    <Lamp position={[-10, 2.75, 7.4]} color="#ffbe6a" intensity={progress.lampOn ? 28 : 0} />
    <Lamp position={[1, 3.5, -5.5]} color="#ddc998" intensity={progress.powerOn ? 23 : 0} />
    <Lamp position={[-1, 3.2, -9]} color="#69c6b0" intensity={progress.powerOn ? 9 : 0} />
    <pointLight ref={roomLight} position={[1.8, 2.7, -12]} color="#65d2c2" distance={8} decay={2} intensity={progress.powerOn ? 18 : 0} />

    {!progress.hasKey && <group ref={key} name="StationKey" position={INTERACTIONS[0].position} rotation={[0, 0, Math.PI / 2]}>
      <mesh><torusGeometry args={[0.11, 0.028, 8, 16]} /><meshStandardMaterial color="#ffe19a" emissive="#d7a93b" emissiveIntensity={0.6} metalness={0.8} roughness={0.3} /></mesh>
      <mesh position={[0.2, 0, 0]}><boxGeometry args={[0.3, 0.045, 0.05]} /><meshStandardMaterial color="#eac777" metalness={0.8} roughness={0.3} /></mesh>
      <mesh position={[0.3, -0.045, 0]}><boxGeometry args={[0.05, 0.1, 0.05]} /><meshStandardMaterial color="#eac777" /></mesh>
      <pointLight color="#ffc866" intensity={1.6} distance={2.5} />
    </group>}
    <mesh name="CaretakerNote" position={INTERACTIONS[1].position} rotation={[-Math.PI / 2, 0, 0.16]}><boxGeometry args={[0.5, 0.34, 0.015]} /><meshStandardMaterial color="#d5c9a5" roughness={0.9} /></mesh>
    <group name="LampSwitch" position={INTERACTIONS[5].position}>
      <mesh><boxGeometry args={[0.25, 0.34, 0.15]} /><meshStandardMaterial color="#625e48" /></mesh>
      <mesh position={[0, 0, 0.12]} rotation={[progress.lampOn ? -0.4 : 0.4, 0, 0]}><boxGeometry args={[0.07, 0.2, 0.08]} /><meshStandardMaterial color="#eac381" /></mesh>
      <mesh position={[0, -0.8, -0.05]}><cylinderGeometry args={[0.055, 0.055, 1.6, 6]} /><meshStandardMaterial color="#454b42" /></mesh>
    </group>
    <group name="GeneratorControls" position={INTERACTIONS[3].position}>
      <mesh><boxGeometry args={[0.58, 0.3, 0.1]} /><meshStandardMaterial color="#222d2a" /></mesh>
      <mesh position={[0.18, 0, 0.07]}><sphereGeometry args={[0.06, 8, 8]} /><meshStandardMaterial color={progress.powerOn ? '#8bd5a1' : '#f0ae59'} emissive={progress.powerOn ? '#8bd5a1' : '#f0ae59'} emissiveIntensity={2} /></mesh>
      <mesh position={[-0.15, 0, 0.07]}><cylinderGeometry args={[0.07, 0.07, 0.07, 8]} /><meshStandardMaterial color="#9b8770" metalness={0.6} /></mesh>
    </group>
    <group name="SignalReceiver" position={[1.8, 1.5, -12.2]}>
      <mesh castShadow><boxGeometry args={[1.65, 0.65, 0.75]} /><meshStandardMaterial color="#233d3b" metalness={0.55} roughness={0.65} /></mesh>
      <mesh position={[-0.2, 0.08, 0.39]}><boxGeometry args={[0.85, 0.28, 0.025]} /><meshStandardMaterial color="#8ed6b5" emissive="#55c2aa" emissiveIntensity={progress.powerOn ? 2 : 0} /></mesh>
      {[0.33, 0.59].map((x) => <mesh key={x} position={[x, -0.08, 0.43]} rotation={[Math.PI / 2, 0, 0]}><cylinderGeometry args={[0.09, 0.09, 0.08, 10]} /><meshStandardMaterial color="#b5aa86" metalness={0.5} /></mesh>)}
      <mesh position={[0.62, 0.9, 0]} rotation={[0, 0, -0.12]}><cylinderGeometry args={[0.012, 0.02, 1.3, 6]} /><meshStandardMaterial color="#a6ada5" metalness={0.9} /></mesh>
    </group>
    <group name="Antenna" position={[3.7, 4.42, -7.5]}>
      <mesh castShadow><cylinderGeometry args={[0.08, 0.16, 9, 8]} /><meshStandardMaterial color="#71807d" metalness={0.8} /></mesh>
      {[1, 2, 3].map((y) => <mesh key={y} position={[0, y, 0]}><boxGeometry args={[3.2 - y * 0.5, 0.07, 0.08]} /><meshStandardMaterial color="#80918c" metalness={0.7} /></mesh>)}
      <mesh position={[0, 4.6, 0]}><sphereGeometry args={[0.09, 8, 8]} /><meshStandardMaterial color="#eb7661" emissive="#eb4836" emissiveIntensity={3} /></mesh>
    </group>
    <group ref={apparition} name="Apparition" position={[4.65, 0, -12.7]} visible={false}>
      <mesh position={[0, 0.92, 0]}><capsuleGeometry args={[0.24, 1.2, 4, 8]} /><meshStandardMaterial color="#0b1517" roughness={1} /></mesh>
      <mesh position={[0, 1.75, 0]}><sphereGeometry args={[0.18, 10, 8]} /><meshStandardMaterial color="#0b1517" /></mesh>
      {[-0.055, 0.055].map((x) => <mesh key={x} position={[x, 1.78, 0.165]}><sphereGeometry args={[0.018, 6, 6]} /><meshBasicMaterial color="#ffbb86" /></mesh>)}
    </group>
  </group>
}
