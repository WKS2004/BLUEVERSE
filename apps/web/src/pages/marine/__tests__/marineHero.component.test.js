import assert from 'node:assert/strict'
import { after, afterEach, test } from 'node:test'
import { cleanup, closeWebTestServer, createElement, jsonResponse, loadWebModule, makeAuthSessionValue, makeAuthUser, renderInApp, resetTestBrowser, screen, waitFor } from '../../../testSupport/reactTestHarness.js'

const [{ default: MarinePageHero }, { MarineConditionsPage, MarineHistoryPage, MarineSafetyProfilesPage }, { default: MarineWorkspaceLayout }] = await Promise.all([
  loadWebModule('/src/pages/marine/MarinePageHero.tsx'),
  loadWebModule('/src/pages/marine/MarinePages.tsx'),
  loadWebModule('/src/pages/marine/MarineWorkspaceLayout.tsx'),
])
const originalFetch = globalThis.fetch
afterEach(() => { cleanup(); resetTestBrowser(); globalThis.fetch = originalFetch })
after(closeWebTestServer)

const cases = [
  { area: 'conditions', Page: MarineConditionsPage, route: '/marine/conditions', workflow: 'marine-conditions', title: 'Marine conditions', image: 'coastal-conditions-hero.webp', eyebrow: 'READ THE COAST' },
  { area: 'history', Page: MarineHistoryPage, route: '/marine/history', workflow: 'marine-condition-history', title: 'Marine history', image: 'coastal-evidence-hero.webp', eyebrow: 'FOLLOW THE EVIDENCE' },
  { area: 'profiles', Page: MarineSafetyProfilesPage, route: '/marine/safety-profiles', workflow: 'marine-safety-profile-management', title: 'Safety profiles', image: 'activity-safety-hero.webp', eyebrow: 'PREPARE WITH PURPOSE' },
]

for (const { area, Page, route, workflow, title, image, eyebrow } of cases) {
  test(`WEB-MARINE-HERO-001 ${area} uses its own new decorative image with readable semantic copy (ui-integration: ${workflow})`, () => {
    renderInApp(createElement(MarineWorkspaceLayout, null, createElement(MarinePageHero, { area, title, intro: 'Coastal workflow guidance.' })), { path: route })
    const heading = screen.getByRole('heading', { level: 2, name: title })
    assert.equal(document.querySelectorAll('h1').length, 1)
    assert.ok(screen.getByRole('heading', { level: 1, name: 'Marine & safety' }))
    const header = heading.closest('header')
    assert.equal(header.getAttribute('aria-labelledby'), heading.id)
    assert.ok(screen.getByText(eyebrow))
    assert.ok(screen.getByText('Coastal workflow guidance.'))
    const photo = header.querySelector('img')
    assert.equal(header.firstElementChild, photo.parentElement)
    assert.equal(header.lastElementChild.contains(heading), true)
    assert.match(header.className, /\bgrid-cols-1\b/)
    assert.doesNotMatch(header.className, /\b(?:sm|md|lg|xl|2xl):grid-cols-/)
    for (const panel of header.children) assert.doesNotMatch(panel.className, /\border-/)
    assert.ok(photo.getAttribute('src').endsWith(`/marine/${image}`))
    assert.equal(photo.getAttribute('alt'), '')
    assert.equal(photo.getAttribute('aria-hidden'), 'true')
    assert.equal(photo.getAttribute('width'), '1536')
    assert.equal(photo.getAttribute('height'), '1024')
    assert.equal(photo.getAttribute('fetchpriority'), 'high')
    assert.equal(photo.getAttribute('loading'), 'eager')
    photo.dispatchEvent(new window.Event('error'))
    assert.equal(screen.getByRole('heading', { level: 2, name: title }), heading)
    assert.ok(screen.getByText('Coastal workflow guidance.'))
  })

  test(`WEB-MARINE-HERO-002 ${area} page retains its correct hero and workflow links during a dependency outage (ui-integration: ${workflow})`, async () => {
    globalThis.fetch = async () => jsonResponse({ detail: 'Activity data unavailable.' }, 503)
    renderInApp(createElement(MarineWorkspaceLayout, null, createElement(Page)), { path: route, auth: makeAuthSessionValue({ status: 'signed-in', user: makeAuthUser({ permissions: ['marine.profile.read'] }) }) })
    const hero = screen.getByRole('heading', { level: 2, name: title }).closest('header')
    assert.ok(hero.querySelector('img').getAttribute('src').endsWith(`/marine/${image}`))
    const links = [...screen.getByRole('navigation', { name: 'Marine workflows' }).querySelectorAll('a')]
    assert.deepEqual(links.map(link => link.textContent), ['Coastal conditions', 'Coastal evidence', 'Activity safety limits'])
    assert.equal(links.find(link => link.getAttribute('href') === route).getAttribute('aria-current'), 'page')
    await waitFor(() => assert.ok(screen.getAllByRole('alert').length > 0))
    assert.ok(screen.getByRole('heading', { level: 2, name: title }))
    assert.ok(hero.querySelector('img').getAttribute('src').endsWith(`/marine/${image}`))
  })
}
