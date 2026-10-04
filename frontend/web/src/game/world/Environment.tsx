import { useEffect, useMemo, useRef } from 'react'
import { DataTexture, InstancedMesh, Object3D, RepeatWrapping, RGBAFormat, SRGBColorSpace } from 'three'
import { TREES } from './worldData'

function Forest() {
  const trunks = useRef<InstancedMesh>(null)
  const lower = useRef<InstancedMesh>(null)
  const upper = useRef<InstancedMesh>(null)
  useEffect(() => {
    const transform = new Object3D()
    TREES.forEach(([x, y, z], index) => {
      transform.position.set(x, y + 2.5, z)
      transform.scale.set(1, 1, 1)
      transform.rotation.set(0, index * 1.7, 0)
      transform.updateMatrix()
      trunks.current?.setMatrixAt(index, transform.matrix)
      transform.position.y = y + 5.5
      transform.scale.set(1 + index % 3 * 0.1, 1, 1 + index % 3 * 0.1)
      transform.updateMatrix()
      lower.current?.setMatrixAt(index, transform.matrix)
      transform.position.y = y + 7.8
      transform.scale.set(0.72, 0.86, 0.72)
      transform.updateMatrix()
      upper.current?.setMatrixAt(index, transform.matrix)
    })
    for (const mesh of [trunks.current, lower.current, upper.current]) {
      if (mesh) { mesh.instanceMatrix.needsUpdate = true; mesh.computeBoundingSphere() }
    }
  }, [])
  return <group name="Forest">
    <instancedMesh ref={trunks} args={[undefined, undefined, TREES.length]} castShadow receiveShadow><cylinderGeometry args={[0.24, 0.37, 5, 7]} /><meshStandardMaterial color="#4a493a" roughness={1} /></instancedMesh>
    <instancedMesh ref={lower} args={[undefined, undefined, TREES.length]} castShadow receiveShadow><coneGeometry args={[2.4, 5.5, 7]} /><meshStandardMaterial color="#1d3a31" roughness={1} /></instancedMesh>
    <instancedMesh ref={upper} args={[undefined, undefined, TREES.length]} castShadow><coneGeometry args={[2.4, 5.5, 7]} /><meshStandardMaterial color="#284538" roughness={1} /></instancedMesh>
  </group>
}

export function Environment() {
  const groundTexture = useMemo(() => {
    const size = 64
    const pixels = new Uint8Array(size * size * 4)
    let seed = 17
    for (let i = 0; i < pixels.length; i += 4) {
      seed = (seed * 16807) % 2147483647
      const value = 155 + seed % 65
      pixels[i] = value; pixels[i + 1] = value; pixels[i + 2] = value; pixels[i + 3] = 255
    }
    const texture = new DataTexture(pixels, size, size, RGBAFormat)
    texture.wrapS = RepeatWrapping; texture.wrapT = RepeatWrapping
    texture.repeat.set(38, 38)
    texture.colorSpace = SRGBColorSpace
    texture.needsUpdate = true
    return texture
  }, [])
  useEffect(() => () => groundTexture.dispose(), [groundTexture])

  return <>
    <color attach="background" args={['#101e28']} />
    <fog attach="fog" args={['#101e28', 18, 64]} />
    <hemisphereLight args={['#adc4d2', '#2c3527', 0.85]} />
    <directionalLight position={[-10, 19, 12]} color="#b4d6ed" intensity={2.1} castShadow shadow-mapSize={[1024, 1024]} shadow-camera-left={-25} shadow-camera-right={25} shadow-camera-top={26} shadow-camera-bottom={-26} shadow-camera-near={1} shadow-camera-far={65} shadow-bias={-0.0005} shadow-normalBias={0.04} />
    <mesh name="Ground" rotation={[-Math.PI / 2, 0, 0]} receiveShadow><planeGeometry args={[120, 120]} /><meshStandardMaterial color="#53624d" map={groundTexture} roughness={1} /></mesh>
    <mesh position={[0, 0.016, 12]} receiveShadow><boxGeometry args={[3.8, 0.03, 24]} /><meshStandardMaterial color="#5c6159" roughness={1} /></mesh>
    <mesh position={[-5.3, 0.02, 9.5]} rotation={[0, 0.12, 0]} receiveShadow><boxGeometry args={[11, 0.035, 2.2]} /><meshStandardMaterial color="#555b50" roughness={1} /></mesh>
    <Forest />
    <mesh position={[-18, 21, -42]}><sphereGeometry args={[1.3, 20, 20]} /><meshBasicMaterial color="#c6d8d5" fog={false} /></mesh>
  </>
}
