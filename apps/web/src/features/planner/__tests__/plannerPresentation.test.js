import assert from 'node:assert/strict'
import { test } from 'node:test'
import { utcTime, wallTime, validatePlanningWindow } from '../plannerPresentation.ts'

test('WEB-PLANNER-TIME-001 destination clocks use Colombo time independently of browser location', () => {
  assert.equal(utcTime('2026-10-06T09:30', 'Asia/Colombo'), '2026-10-06T04:00:00.000Z')
  assert.equal(wallTime('2026-10-06T04:00:00.000Z', 'Asia/Colombo'), '2026-10-06T09:30')
})
test('WEB-PLANNER-TIME-002 skipped and ambiguous daylight saving clocks require a different time', () => {
  assert.throws(() => utcTime('2026-03-08T02:30', 'America/New_York'), /skipped or occurs twice/)
  assert.throws(() => utcTime('2026-11-01T01:30', 'America/New_York'), /skipped or occurs twice/)
  assert.equal(utcTime('2026-11-01T03:30', 'America/New_York'), '2026-11-01T08:30:00.000Z')
})
test('WEB-PLANNER-TIME-003 malformed clocks and invalid zones cannot become trip timestamps', () => {
  for (const value of ['', '2026-02-30T12:00', '2026-10-06T25:00', 'tomorrow']) assert.throws(() => utcTime(value, 'Asia/Colombo'))
  assert.throws(() => utcTime('2026-10-06T12:00', 'Unknown/Place'))
})
test('WEB-PLANNER-TIME-004 planning windows reject past dates, reversed dates, excessive horizon and fractional duration', () => {
  const start = new Date(Date.now() + 86400000).toISOString()
  const end = new Date(Date.parse(start) + 7200000).toISOString()
  assert.equal(validatePlanningWindow(start, end, 2), null)
  for (const hours of [0, 1.5, 3]) assert.match(validatePlanningWindow(start, end, hours), /whole number/)
  assert.match(validatePlanningWindow(end, start, 1), /after the start/)
  assert.match(validatePlanningWindow(new Date(0).toISOString(), end, 1), /future dates/)
  assert.match(validatePlanningWindow(start, new Date(Date.now() + 31 * 86400000).toISOString(), 1), /30 days/)
})
