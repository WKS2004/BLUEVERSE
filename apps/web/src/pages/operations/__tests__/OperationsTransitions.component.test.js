import assert from 'node:assert/strict'
import { after, afterEach, test } from 'node:test'
import { cleanup, closeWebTestServer, createElement, fireEvent, jsonResponse, loadWebModule, makeAuthSessionValue, makeAuthUser, renderInApp, resetTestBrowser, screen, userEvent, within } from '../../../testSupport/reactTestHarness.js'

const { default: Routes } = await loadWebModule('/src/app/routes.tsx')
const originalFetch = globalThis.fetch
const auth = makeAuthSessionValue({ status: 'signed-in', user: makeAuthUser({ permissions: ['operations.assessment.read', 'operations.alert.read', 'operations.audit.read'] }) })
afterEach(() => { cleanup(); resetTestBrowser(); globalThis.fetch = originalFetch; delete document.documentElement.dataset.coastalDirection })
after(() => closeWebTestServer())

test('WEB-OPS-MOTION-001 repeated Assessment/Alert navigation animates the complete workspace in each direction (ui-integration: coastal-operations-assessment, coastal-operations-alerts)', async () => {
  globalThis.fetch = async () => jsonResponse({ items: [], nextCursor: null })
  delete document.documentElement.dataset.coastalDirection
  renderInApp(createElement(Routes), { path: '/operations/assessments', auth })
  await screen.findByRole('heading', { name: 'No assessments to show yet' })
  let previousPanel = document.querySelector('.coastal-section-panel')
  assert.equal(previousPanel.dataset.coastalDirection, undefined)
  for (const [tab, title, direction] of [
    ['Alerts', 'Clear updates. Safer coastal days.', 'forward'],
    ['Assessments', 'Look after the places we share.', 'back'],
    ['Alerts', 'Clear updates. Safer coastal days.', 'forward'],
    ['Assessments', 'Look after the places we share.', 'back'],
  ]) {
    const tabs = screen.getByRole('navigation', { name: 'Coastal record sections' })
    await userEvent.click(within(tabs).getByRole('link', { name: tab, exact: true }))
    await screen.findByRole('heading', { name: tab === 'Alerts' ? 'No advisories to show' : 'No assessments to show yet' })
    const panel = document.querySelector('.coastal-section-panel')
    assert.ok(panel !== previousPanel, 'Each destination gets a new content panel so the entry transition can replay')
    assert.equal(panel.dataset.coastalDirection, direction)
    assert.ok(panel.contains(screen.getByRole('heading', { name: title })))
    assert.ok(panel.contains(screen.getByRole('searchbox')))
    assert.equal(panel.contains(document.querySelector('header')), false)
    assert.equal(panel.contains(screen.getByRole('navigation', { name: 'Coastal record sections' })), false)
    fireEvent.animationEnd(panel)
    if (!window.AnimationEvent) fireEvent(panel, new Event('webkitAnimationEnd', { bubbles: true }))
    assert.equal(document.documentElement.dataset.coastalDirection, undefined)
    previousPanel = panel
  }
  await userEvent.click(within(screen.getByRole('navigation', { name: 'Coastal record sections' })).getByRole('link', { name: 'Assessments', exact: true }))
  assert.ok(document.querySelector('.coastal-section-panel') === previousPanel, 'Clicking the active section keeps the current panel')
  assert.equal(document.documentElement.dataset.coastalDirection, undefined)
})

test('WEB-OPS-MOTION-002 Logs category changes animate hero and records together in both directions; the active tab does not restart (ui-integration: coastal-operations-logs)', async () => {
  globalThis.fetch = async () => jsonResponse({ items: [], nextCursor: null })
  renderInApp(createElement(Routes), { path: '/operations/logs?kind=assessments', auth })
  await screen.findByRole('heading', { name: 'No assessments to show yet' })
  let previousPanel = document.querySelector('.coastal-log-panel')
  assert.equal(previousPanel.dataset.coastalDirection, undefined)
  const tabs = screen.getByLabelText('Log record category')
  for (const [tab, label, direction] of [
    ['Alert logs', 'COASTAL ALERT LOGS', 'forward'],
    ['Assessment logs', 'COASTAL ASSESSMENT LOGS', 'back'],
    ['Alert logs', 'COASTAL ALERT LOGS', 'forward'],
    ['Assessment logs', 'COASTAL ASSESSMENT LOGS', 'back'],
  ]) {
    const previousDirection = previousPanel.dataset.coastalDirection
    await userEvent.click(screen.getByRole('button', { name: tab, exact: true }))
    await screen.findByRole('heading', { name: tab === 'Alert logs' ? 'No advisories to show' : 'No assessments to show yet' })
    const panel = document.querySelector('.coastal-log-panel')
    assert.notEqual(panel.dataset.coastalDirection, previousDirection)
    assert.equal(panel.dataset.coastalDirection, direction)
    assert.ok(panel.contains(screen.getByText(label)))
    assert.ok(panel.contains(screen.getByRole('heading', { name: 'Every coastal decision, accounted for.' })))
    assert.ok(panel.contains(screen.getByRole('region', { name: 'Coastal Operations logs' })))
    assert.ok(screen.getByLabelText('Log record category') === tabs, 'Logs tabs remain outside the transitioning content')
    previousPanel = panel
  }
  await userEvent.click(screen.getByRole('button', { name: 'Assessment logs', exact: true }))
  assert.ok(document.querySelector('.coastal-log-panel') === previousPanel, 'Clicking the active Logs category keeps the current panel')
})
