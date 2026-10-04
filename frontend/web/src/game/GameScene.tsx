import { Canvas, useThree } from '@react-three/fiber'
import { PerformanceMonitor, PointerLockControls } from '@react-three/drei'
import type { ComponentRef, RefObject } from 'react'
import { PCFShadowMap } from 'three'
import type { GameDiagnostics, GamePhase, GameProgress, GameRuntime, InteractionId } from './types'
import type { KeyboardState } from './hooks/useKeyboard'
import { Environment } from './world/Environment'
import { World } from './world/World'
import { Player } from './player/Player'

export type GameControls = ComponentRef<typeof PointerLockControls>

function AdaptiveQuality() {
  const setDpr = useThree((state) => state.setDpr)
  return <PerformanceMonitor iterations={4} bounds={() => [24, 50]} flipflops={1} onDecline={() => setDpr(0.75)} onFallback={() => setDpr(0.75)} />
}

interface GameSceneProps {
  runtime: RefObject<GameRuntime>
  progress: GameProgress
  phase: GamePhase
  keyboard: KeyboardState
  controls: RefObject<GameControls | null>
  runId: number
  onReady: () => void
  onLock: () => void
  onUnlock: () => void
  onProgress: (progress: GameProgress) => void
  onPrompt: (id: InteractionId | null) => void
  onComplete: () => void
  onDiagnostics: (diagnostics: GameDiagnostics) => void
}

export function GameScene({ runtime, progress, phase, keyboard, controls, runId, onReady, onLock, onUnlock, onProgress, onPrompt, onComplete, onDiagnostics }: GameSceneProps) {
  return <Canvas id="last-signal-canvas" shadows={{ type: PCFShadowMap }} dpr={[1, 1.5]} frameloop={phase === 'playing' ? 'always' : 'demand'} camera={{ position: [15, 9, 23], fov: 68, near: 0.08, far: 100 }} gl={{ antialias: true, powerPreference: 'high-performance' }} onCreated={({ camera, scene }) => {
    camera.name = 'PlayerCamera'
    camera.lookAt(0, 1.3, -3)
    scene.name = 'LastSignalWorld'
    onReady()
  }} fallback={<p className="signal-fallback">Trình duyệt chưa hỗ trợ WebGL. Hãy bật tăng tốc phần cứng và tải lại trang.</p>}>
    <Environment />
    {phase === 'playing' && <AdaptiveQuality />}
    <World runtime={runtime} progress={progress} />
    <Player runtimeRef={runtime} keyboard={keyboard} runId={runId} onProgress={onProgress} onPrompt={onPrompt} onComplete={onComplete} onDiagnostics={onDiagnostics} />
    {/* Capture is invoked through the ref by Start/Resume user gestures only. */}
    <PointerLockControls ref={controls} makeDefault selector="#last-signal-mouse-capture" minPolarAngle={0.18} maxPolarAngle={Math.PI - 0.18} onLock={onLock} onUnlock={onUnlock} />
  </Canvas>
}
