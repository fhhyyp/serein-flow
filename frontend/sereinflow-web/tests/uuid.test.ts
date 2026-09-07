import assert from 'node:assert/strict'
import test from 'node:test'
import { createUuid } from '../src/utils/uuid.ts'

test('createUuid returns a version 4 UUID without depending on randomUUID', () => {
  const value = createUuid()

  assert.match(value, /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/)
})
