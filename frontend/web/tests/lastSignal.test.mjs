import assert from 'node:assert/strict'
import test from 'node:test'
import { createPlayer, stepPlayer, PLAYER_RADIUS, PLAYER_HEIGHT } from '../src/game/player/PlayerController.ts'
import { createProgress, interact, getObjective } from '../src/game/gameplay/ObjectiveSystem.ts'
import { enterStrangeRoom } from '../src/game/gameplay/TriggerSystem.ts'
import { findInteraction } from '../src/game/interaction/InteractionSystem.ts'
import { getColliders, INTERACTIONS, DOOR_COLLIDER } from '../src/game/world/worldData.ts'

const idle = { forward: 0, strafe: 0, sprint: false, jump: false, active: true }
const forward = { ...idle, forward: 1 }
const wall = { id: 'wall', position: [0, 2, -2], size: [6, 4, 0.4], color: '#888' }
function walk(player, input, seconds, colliders = [], yaw = 0, fps = 60) {
  for (let i = 0; i < seconds * fps; i++) stepPlayer(player, input, yaw, 1 / fps, colliders)
  return player
}
function atOrigin() {
  const player = createPlayer()
  Object.assign(player.position, { x: 0, y: 0, z: 0 })
  return player
}

test('movement follows camera yaw and stays normalized on diagonals', () => {
  const straight = walk(atOrigin(), forward, 1)
  const turned = walk(atOrigin(), forward, 1, [], Math.PI / 2)
  const diagonal = walk(atOrigin(), { ...forward, strafe: 1 }, 1)
  assert.ok(straight.position.z < -2)
  assert.ok(Math.abs(straight.position.x) < 0.001)
  assert.ok(turned.position.x < -2)
  assert.ok(Math.abs(turned.position.z) < 0.001)
  assert.ok(Math.abs(Math.hypot(diagonal.position.x, diagonal.position.z) + straight.position.z) < 0.01)
  assert.equal(turned.rotation.y, Math.PI / 2)
})

test('running is faster and movement is independent of rendering frame rate', () => {
  const slow = walk(atOrigin(), forward, 2, [], 0, 30)
  const fast = walk(atOrigin(), forward, 2, [], 0, 144)
  const run = walk(atOrigin(), { ...forward, sprint: true }, 2)
  assert.ok(Math.abs(slow.position.z - fast.position.z) < 0.04)
  assert.ok(run.position.z < slow.position.z * 1.6)
})

test('walls block the player and allow sliding along their faces', () => {
  const player = walk(atOrigin(), { ...forward, strafe: 0.3 }, 2, [wall])
  assert.ok(player.position.z >= -1.8 + PLAYER_RADIUS - 0.00001)
  assert.ok(player.position.x > 0.5)
})

test('fast movement and long frames cannot tunnel through thin walls', () => {
  const player = atOrigin()
  stepPlayer(player, { ...forward, sprint: true }, 0, 10, [wall])
  assert.ok(player.position.z >= -1.8 + PLAYER_RADIUS - 0.00001)
  walk(player, { ...forward, sprint: true }, 2, [wall], 0, 15)
  assert.ok(player.position.z >= -1.8 + PLAYER_RADIUS - 0.00001)
})

test('gravity lands on the ground and low 3D props', () => {
  const player = atOrigin()
  player.position.y = 4
  walk(player, idle, 2)
  assert.equal(player.position.y, 0)
  assert.equal(player.grounded, true)
  player.position.y = 4
  walk(player, idle, 2, [{ ...wall, position: [0, 0.4, 0], size: [2, 0.8, 2] }])
  assert.ok(Math.abs(player.position.y - 0.8) < 0.001)
  assert.equal(player.grounded, true)
})

test('jump rises in Y and ceiling collision stops upward velocity', () => {
  const player = atOrigin()
  stepPlayer(player, { ...idle, jump: true }, 0, 0.05, [])
  assert.ok(player.position.y > 0)
  assert.ok(player.velocity.y > 0)
  const ceiling = { ...wall, position: [0, PLAYER_HEIGHT + 0.15, 0], size: [6, 0.2, 6] }
  walk(player, idle, 0.2, [ceiling])
  assert.ok(player.position.y + PLAYER_HEIGHT <= PLAYER_HEIGHT + 0.05 + 0.00001)
  assert.ok(player.velocity.y <= 0)
})

test('pause freezes position, velocity and rotation', () => {
  const player = walk(atOrigin(), forward, 1)
  const before = structuredClone(player)
  stepPlayer(player, { ...forward, active: false, jump: true }, 1, 1, [])
  assert.deepEqual(player, before)
})

test('the key opens the door collider and the room event closes it again', () => {
  const ready = createProgress()
  assert.ok(getColliders(ready).includes(DOOR_COLLIDER))
  const opened = interact(interact(ready, 'key'), 'door')
  assert.ok(!getColliders(opened).includes(DOOR_COLLIDER))
  const event = enterStrangeRoom(interact(opened, 'generator'), { x: -1, y: 0, z: -11 })
  assert.ok(getColliders(event).includes(DOOR_COLLIDER))
})

test('objectives follow the complete playable loop and reject skipping ahead', () => {
  let progress = createProgress()
  assert.equal(progress.stage, 'key')
  assert.equal(interact(progress, 'door').stage, 'key')
  assert.equal(interact(progress, 'generator').stage, 'key')
  assert.equal(interact(progress, 'receiver').stage, 'key')
  progress = interact(progress, 'key')
  assert.equal(progress.stage, 'door')
  assert.equal(interact(progress, 'key'), progress)
  progress = interact(progress, 'door')
  assert.equal(progress.stage, 'power')
  progress = interact(progress, 'generator')
  assert.equal(progress.stage, 'room')
  assert.equal(interact(progress, 'receiver').stage, 'room')
  progress = enterStrangeRoom(progress, { x: -1, y: 0, z: -11 })
  assert.equal(progress.stage, 'signal')
  progress = interact(progress, 'receiver')
  assert.equal(progress.stage, 'complete')
  assert.match(getObjective(progress).title, /Hoàn tất/)
})

test('opening the door frees the entrance while its parked 3D leaf still blocks movement', () => {
  const opened = interact(interact(createProgress(), 'key'), 'door')
  const entering = atOrigin()
  entering.position.z = 2
  walk(entering, forward, 1, getColliders(opened))
  assert.ok(entering.position.z < -0.5)
  const byHinge = atOrigin()
  byHinge.position.x = -1.23
  byHinge.position.z = 4
  walk(byHinge, forward, 1, getColliders(opened))
  assert.ok(byHinge.position.z >= 2.6, 'the opened door must still occupy physical space')
})

test('the horror trigger checks X/Y/Z, requires power and runs only once', () => {
  let progress = createProgress()
  assert.equal(enterStrangeRoom(progress, { x: 0, y: 0, z: -12 }), progress)
  progress = interact(interact(interact(progress, 'key'), 'door'), 'generator')
  assert.equal(enterStrangeRoom(progress, { x: 10, y: 0, z: -12 }), progress)
  assert.equal(enterStrangeRoom(progress, { x: 0, y: 8, z: -12 }), progress)
  assert.equal(enterStrangeRoom(progress, { x: 0, y: 0, z: -7 }), progress)
  const triggered = enterStrangeRoom(progress, { x: 0, y: 0, z: -12 })
  assert.equal(triggered.eventTriggered, true)
  assert.equal(enterStrangeRoom(triggered, { x: 0, y: 0, z: -12 }), triggered)
})

test('interaction requires proximity and looking at the object', () => {
  const key = INTERACTIONS.find((item) => item.id === 'key')
  const position = { x: key.position[0], y: 0, z: key.position[2] + 1.5 }
  assert.equal(findInteraction(position, { x: 0, y: -0.2, z: -1 }, createProgress(), []), 'key')
  assert.equal(findInteraction(position, { x: 0, y: 0, z: 1 }, createProgress(), []), null)
  assert.equal(findInteraction({ ...position, z: position.z + 10 }, { x: 0, y: 0, z: -1 }, createProgress(), []), null)
})

test('a 3D wall occludes interactions, including when the item is nearby', () => {
  const key = INTERACTIONS.find((item) => item.id === 'key')
  const position = { x: key.position[0], y: 0, z: key.position[2] + 1.5 }
  const blocker = { ...wall, position: [position.x, 2, position.z - 0.6] }
  assert.equal(findInteraction(position, { x: 0, y: -0.2, z: -1 }, createProgress(), [blocker]), null)
})

test('completed interactions are unavailable and replay starts with fresh progress/player', () => {
  const key = INTERACTIONS.find((item) => item.id === 'key')
  const picked = interact(createProgress(), 'key')
  assert.notEqual(findInteraction({ x: key.position[0], y: 0, z: key.position[2] + 1.5 }, { x: 0, y: -0.2, z: -1 }, picked, []), 'key')
  const one = createProgress()
  const two = createProgress()
  assert.notEqual(one, two)
  assert.equal(two.hasKey, false)
  assert.equal(two.eventTriggered, false)
  assert.notEqual(createPlayer().position, createPlayer().position)
})
