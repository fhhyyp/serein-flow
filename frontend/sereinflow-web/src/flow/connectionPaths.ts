import { getBezierPath, getSmoothStepPath, Position } from '@vue-flow/core';
interface ConnectionPoints {
  sourceX: number;
  sourceY: number;
  sourcePosition: Position;
  targetX: number;
  targetY: number;
  targetPosition: Position;
}
/** Distance from the arrow tip to the target card border, in canvas pixels. */
/** 箭头尖端到目标卡片边框的距离，以画布像素计。 */
export const CONNECTION_ARROW_GAP = 6;
const approachLength = 16;
function withTargetGap(points: ConnectionPoints, gap: number): ConnectionPoints {
  const bindingSource = points;
  const { targetX } = bindingSource;
  const { targetY } = bindingSource;
  const { targetPosition } = bindingSource;
  switch (targetPosition) {
    case Position.Left:
      return { ...points, targetX: targetX - gap };
    case Position.Right:
      return { ...points, targetX: targetX + gap };
    case Position.Top:
      return { ...points, targetY: targetY - gap };
    case Position.Bottom:
      return { ...points, targetY: targetY + gap };
  }
}
/** Keep the arrow clear of the input seat and give it a horizontal approach. */
/** 使箭头避开输入连接位，并水平接近目标。 */
export function bezierConnectionPath(points: ConnectionPoints, gap = CONNECTION_ARROW_GAP): string {
  const bindingSource1 = points;
  const { sourceX } = bindingSource1;
  const { sourceY } = bindingSource1;
  const { sourcePosition } = bindingSource1;
  const { targetX } = bindingSource1;
  const { targetY } = bindingSource1;
  const { targetPosition } = bindingSource1;
  if (sourcePosition !== Position.Right || targetPosition !== Position.Left) {
    const withTargetGapResult = withTargetGap(points, gap);
    const evaluatedValue: string = getBezierPath(withTargetGapResult)[0];
    return evaluatedValue;
  }
  const endX = targetX - gap;
  const available = endX - sourceX;
  const approach = available > 0 ? Math.min(approachLength, available / 3) : approachLength;
  const curveEndX = endX - approach;
  const curveWidth = curveEndX - sourceX;
  const resolveMinResult = () => {
    const evaluatedValue2: number = Math.abs(targetY - sourceY) * 0.35;
    const maxResult: number = Math.max(36, evaluatedValue2);
    const minResult = Math.min(90, maxResult);
    return minResult;
  };
  const control = curveWidth > 0
    ? Math.min(90, curveWidth * 0.4)
    : resolveMinResult();
  const path = `M${sourceX},${sourceY} C${sourceX + control},${sourceY} ${curveEndX - control},${targetY} ${curveEndX},${targetY}`;
  return approach > 0 ? `${path} L${endX},${targetY}` : path;
}
export function smoothStepConnectionPath(points: ConnectionPoints, gap = CONNECTION_ARROW_GAP): string {
  const withTargetGapResult1 = withTargetGap(points, gap);
  const evaluatedValue1: string = getSmoothStepPath(withTargetGapResult1)[0];
  return evaluatedValue1;
}
