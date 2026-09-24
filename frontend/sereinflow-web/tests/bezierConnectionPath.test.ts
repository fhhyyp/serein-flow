import assert from 'node:assert/strict'
import test from 'node:test'
import { Position } from '@vue-flow/core'
import { CONNECTION_ARROW_GAP, bezierConnectionPath } from '../src/flow/connectionPaths.ts'

const points = {
  sourceX: 381,
  sourceY: 276,
  sourcePosition: Position.Right,
  targetX: 475,
  targetY: 140,
  targetPosition: Position.Left,
}

test('bezier arrow stops clear of the card after a straight horizontal approach', () => {
  const path = bezierConnectionPath(points)
  const match = path.match(/^M([\d.]+),([\d.]+) C([\d.]+),([\d.]+) ([\d.]+),([\d.]+) ([\d.]+),([\d.]+) L([\d.]+),([\d.]+)$/)

  assert.ok(match)
  assert.equal(Number(match[9]), points.targetX - CONNECTION_ARROW_GAP)
  assert.equal(Number(match[6]), points.targetY)
  assert.equal(Number(match[8]), points.targetY)
  assert.equal(Number(match[10]), points.targetY)
  assert.ok(Number(match[3]) < Number(match[5]), 'control points must not cross in a narrow gap')
  assert.ok(Number(match[7]) < Number(match[9]), 'the curve should finish before the arrow')
})

test('a nearby target keeps forward control points and the drag preview reaches the cursor', () => {
  const closePoints = { ...points, targetX: 421 }
  const path = bezierConnectionPath(closePoints)
  const match = path.match(/^M[\d.]+,[\d.]+ C([\d.]+),[\d.]+ ([\d.]+),[\d.]+ ([\d.]+),[\d.]+ L([\d.]+),[\d.]+$/)

  assert.ok(match)
  assert.ok(Number(match[1]) <= Number(match[2]))
  assert.ok(Number(match[2]) <= Number(match[3]))
  assert.equal(Number(match[4]), closePoints.targetX - CONNECTION_ARROW_GAP)
  assert.match(bezierConnectionPath(closePoints, 0), /L421,140$/)
})

test('a target behind the source still has a straight approach to its arrow', () => {
  const path = bezierConnectionPath({ ...points, targetX: 330 })
  assert.match(path, /L324,140$/)
  assert.match(path, /308,140 L324,140$/)
})
