import assert from 'node:assert/strict';
import test from 'node:test';
import { Position } from '@vue-flow/core';
import { CONNECTION_ARROW_GAP, smoothStepConnectionPath } from '../src/flow/connectionPaths.ts';
test('segment arrow ends six pixels before the target card', () => {
  const points = {
    sourceX: 381,
    sourceY: 276,
    sourcePosition: Position.Right,
    targetX: 475,
    targetY: 140,
    targetPosition: Position.Left,
  };
  assert.equal(CONNECTION_ARROW_GAP, 6);
  const smoothStepConnectionPathResult = smoothStepConnectionPath(points);
  assert.match(smoothStepConnectionPathResult, /L469 140$/);
  const smoothStepConnectionPathResult1 = smoothStepConnectionPath(points, 0);
  assert.match(smoothStepConnectionPathResult1, /L475 140$/);
});
