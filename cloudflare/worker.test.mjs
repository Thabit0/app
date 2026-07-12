import test from 'node:test';
import assert from 'node:assert/strict';
import {
  computeOverallStatus,
  isValidFutureSchedule,
  parsePlatformStates,
  retentionDeadline
} from './worker.js';

test('platform statuses remain independent and produce an overall status', () => {
  assert.equal(computeOverallStatus(['published', 'scheduled', 'scheduled']), 'partially_published');
  assert.equal(computeOverallStatus(['published', 'failed', 'published']), 'partial_failed');
  assert.equal(computeOverallStatus(['published', 'published', 'published']), 'published');
  assert.equal(computeOverallStatus(['cancelled', 'cancelled']), 'cancelled');
});

test('platform state aggregation is converted to a clear object', () => {
  assert.deepEqual(parsePlatformStates('instagram:published,tiktok:failed,snapchat:scheduled'), {
    instagram: 'published',
    tiktok: 'failed',
    snapchat: 'scheduled'
  });
});

test('rescheduling only accepts a valid future date', () => {
  const now = Date.parse('2026-07-12T00:00:00.000Z');
  assert.equal(isValidFutureSchedule('2026-07-12T01:00:00.000Z', now), true);
  assert.equal(isValidFutureSchedule('2026-07-11T23:00:00.000Z', now), false);
  assert.equal(isValidFutureSchedule('invalid', now), false);
});

test('R2 retention is thirty days after completion', () => {
  assert.equal(retentionDeadline('2026-07-12T00:00:00.000Z'), '2026-08-11T00:00:00.000Z');
});
