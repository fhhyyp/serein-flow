import { getBezierPath, getSmoothStepPath, Position } from '@vue-flow/core'

interface ConnectionPoints {
  sourceX: number
  sourceY: number
  sourcePosition: Position
  targetX: number
  targetY: number
  targetPosition: Position
}

/** Distance from the arrow tip to the target card border, in canvas pixels. */
export const CONNECTION_ARROW_GAP = 6
const approachLength = 16

function withTargetGap(points: ConnectionPoints, gap: number): ConnectionPoints {
  const { targetX, targetY, targetPosition } = points
  switch (targetPosition) {
    case Position.Left:
      return { ...points, targetX: targetX - gap }
    case Position.Right:
      return { ...points, targetX: targetX + gap }
    case Position.Top:
      return { ...points, targetY: targetY - gap }
    case Position.Bottom:
      return { ...points, targetY: targetY + gap }
  }
}

/** Keep the arrow clear of the input seat and give it a horizontal approach. */
export function bezierConnectionPath(points: ConnectionPoints, gap = CONNECTION_ARROW_GAP): string {
  const { sourceX, sourceY, sourcePosition, targetX, targetY, targetPosition } = points

  if (sourcePosition !== Position.Right || targetPosition !== Position.Left) {
    return getBezierPath(withTargetGap(points, gap))[0]
  }

  const endX = targetX - gap
  const available = endX - sourceX
  const approach = available > 0 ? Math.min(approachLength, available / 3) : approachLength
  const curveEndX = endX - approach
  const curveWidth = curveEndX - sourceX
  const control = curveWidth > 0
    ? Math.min(90, curveWidth * 0.4)
    : Math.min(90, Math.max(36, Math.abs(targetY - sourceY) * 0.35))

  const path = `M${sourceX},${sourceY} C${sourceX + control},${sourceY} ${curveEndX - control},${targetY} ${curveEndX},${targetY}`
  return approach > 0 ? `${path} L${endX},${targetY}` : path
}

export function smoothStepConnectionPath(points: ConnectionPoints, gap = CONNECTION_ARROW_GAP): string {
  return getSmoothStepPath(withTargetGap(points, gap))[0]
}
