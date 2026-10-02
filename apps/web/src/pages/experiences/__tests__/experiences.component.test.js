import assert from 'node:assert/strict'
import { after, afterEach, test } from 'node:test'
import {
  cleanup,
  closeWebTestServer,
  createElement,
  jsonResponse,
  loadWebModule,
  makeAuthSessionValue,
  makeAuthUser,
  renderInApp,
  resetTestBrowser,
  screen,
  userEvent,
  waitFor,
} from '../../../testSupport/reactTestHarness.js'

const originalFetch = globalThis.fetch

const [
  { default: ExperiencesPage },
  { default: DestinationDetailPage },
  { default: OfferingDetailPage },
  { default: FavouritesPage },
  { default: CatalogueManagementPage },
  { Routes, Route },
] = await Promise.all([
  loadWebModule('/src/pages/experiences/ExperiencesPage.tsx'),
  loadWebModule('/src/pages/experiences/DestinationDetailPage.tsx'),
  loadWebModule('/src/pages/experiences/OfferingDetailPage.tsx'),
  loadWebModule('/src/pages/experiences/FavouritesPage.tsx'),
  loadWebModule('/src/pages/experiences/CatalogueManagementPage.tsx'),
  import('react-router'),
])

afterEach(() => {
  cleanup()
  resetTestBrowser()
  globalThis.fetch = originalFetch
})

after(async () => closeWebTestServer())

const mockDestination = {
  id: 'd1111111-1111-1111-1111-111111111111',
  name: 'Mirissa Bay',
  slug: 'mirissa-bay',
  description: 'Southern coastal paradise renowned for cetacean observation.',
  region: 'Southern Province',
  country: 'Sri Lanka',
  latitude: 5.9482,
  longitude: 80.4578,
  publicationStatus: 'PUBLISHED',
  tags: ['Whales', 'Surfing'],
  createdAt: '2026-09-01T00:00:00Z',
  updatedAt: '2026-09-01T00:00:00Z',
  activitiesCount: 2,
  offeringsCount: 3,
}

const mockActivity = {
  id: 'a1111111-1111-1111-1111-111111111111',
  destinationId: 'd1111111-1111-1111-1111-111111111111',
  name: 'Responsible Whale Watching',
  slug: 'responsible-whale-watching',
  description: 'Ethical blue whale observation following maritime guidelines.',
  category: 'Marine Wildlife',
  publicationStatus: 'PUBLISHED',
  tags: ['Wildlife', 'Boat Tour'],
  seasonStartMonth: 11,
  seasonEndMonth: 4,
  createdAt: '2026-09-01T00:00:00Z',
  updatedAt: '2026-09-01T00:00:00Z',
  destinationName: 'Mirissa Bay',
  offeringsCount: 1,
}

const mockOffering = {
  id: 'o1111111-1111-1111-1111-111111111111',
  activityId: 'a1111111-1111-1111-1111-111111111111',
  destinationId: 'd1111111-1111-1111-1111-111111111111',
  title: 'Dawn Blue Whale Expedition',
  slug: 'dawn-blue-whale-expedition',
  description: 'Early morning sea excursion with qualified marine biologists.',
  price: 85,
  priceAmount: 85,
  currency: 'USD',
  priceCurrency: 'USD',
  durationMinutes: 240,
  maxCapacity: 20,
  status: 'PUBLISHED',
  publicationStatus: 'PUBLISHED',
  inclusions: ['Guide', 'Life vest', 'Breakfast'],
  exclusions: ['Transport to harbor'],
  termsAndConditions: 'Subject to safe sea conditions.',
  cancellationPolicy: 'Full refund up to 24 hours before departure.',
  createdAt: '2026-09-01T00:00:00Z',
  updatedAt: '2026-09-01T00:00:00Z',
  activityName: 'Responsible Whale Watching',
  activityCode: 'WHALES',
  destinationName: 'Mirissa Bay',
  destinationRegion: 'Southern Province',
  schedulesCount: 2,
}

const mockSchedule = {
  id: 's1111111-1111-1111-1111-111111111111',
  offeringId: 'o1111111-1111-1111-1111-111111111111',
  startsAt: '2026-10-01T06:00:00Z',
  endsAt: '2026-10-01T10:00:00Z',
  capacity: 20,
  bookedCount: 5,
  availableSpots: 15,
  isActive: true,
}

const mockMarineConditions = {
  destinationId: 'd1111111-1111-1111-1111-111111111111',
  destinationName: 'Mirissa Bay',
  latitude: 5.9482,
  longitude: 80.4578,
  responded: true,
  remoteStatus: 'Connected',
  safetyLevel: 'SAFE',
  waterCondition: 'Calm swell',
  waveHeightMeters: 0.8,
  windSpeedKnots: 9.2,
  tideStatus: 'Rising',
  advisoryMessage: 'Optimal morning observation conditions.',
  fallbackUsed: false,
  disclaimer: 'Member 2 Marine Safety telemetry updated hourly.',
}

const mockAdvisories = {
  destinationId: 'd1111111-1111-1111-1111-111111111111',
  destinationName: 'Mirissa Bay',
  responded: true,
  remoteStatus: 'Active',
  advisories: [
    {
      advisoryId: 'adv-001',
      title: 'Harbor Speed Limit Notice',
      severity: 'Info',
      description: 'Strict 5 knot speed limit within harbor limits to protect local cetaceans.',
      issuedAt: '2026-09-20T00:00:00Z',
    },
  ],
  fallbackUsed: false,
  message: 'Normal navigation in effect.',
}

const mockBiodiversity = {
  destinationId: 'd1111111-1111-1111-1111-111111111111',
  destinationName: 'Mirissa Bay',
  latitude: 5.9482,
  longitude: 80.4578,
  status: 'available',
  predictions: [
    {
      speciesName: 'Blue Whale',
      scientificName: 'Balaenoptera musculus',
      conservationStatus: 'Endangered',
      occurrenceProbability: 0.92,
      habitatSuitability: 'High - Continental shelf break',
      primaryThreats: 'Vessel strikes, acoustic pollution',
    },
  ],
  modelVersion: 'v1.4-coastal-marine-ensemble',
  modelSource: 'Member 3 Biodiversity ML Pipeline',
  uncertaintyNotes: 'High confidence based on recent sonar and temperature readings.',
  disclaimer: 'Machine learning prediction indicative only.',
  responded: true,
  attemptsCount: 1,
  latencyMs: 42,
}

const mockAvailabilityEvaluation = {
  offeringId: 'o1111111-1111-1111-1111-111111111111',
  startsAt: '2026-10-01T06:00:00Z',
  endsAt: '2026-10-01T10:00:00Z',
  status: 'AVAILABLE',
  reasonCodes: ['CAPACITY_AVAILABLE', 'OPERATIONAL_OK'],
  offering: {
    id: 'o1111111-1111-1111-1111-111111111111',
    title: 'Dawn Blue Whale Expedition',
    priceAmount: 85,
    priceCurrency: 'USD',
  },
  evaluatedAt: '2026-09-27T12:00:00Z',
}

const mockDependenciesStatus = {
  serviceName: 'experience-biodiversity',
  evaluatedAt: '2026-09-27T12:00:00Z',
  allHealthy: true,
  dependencies: [
    {
      serviceName: 'marine-safety',
      endpoint: 'http://marine-safety:5002/health',
      responded: true,
      latencyMs: 18,
      status: 'Healthy',
      checkedAt: '2026-09-27T12:00:00Z',
    },
    {
      serviceName: 'biodiversity-ml',
      endpoint: 'http://biodiversity-ml:5003/health',
      responded: true,
      latencyMs: 25,
      status: 'Healthy',
      checkedAt: '2026-09-27T12:00:00Z',
    },
  ],
}

// -------------------------------------------------------------
// WEB-EXP-001: Experiences Discovery Page
// -------------------------------------------------------------
test('WEB-EXP-001 discovery hub renders catalogue search, destination cards, and offerings (ui-integration: experience-discovery)', async () => {
  globalThis.fetch = async (input) => {
    const url = String(input)
    if (url.includes('/api/experiences/destinations')) {
      return jsonResponse({ total: 1, page: 1, pageSize: 20, items: [mockDestination] })
    }
    if (url.includes('/api/experiences/activities')) {
      return jsonResponse({ total: 1, page: 1, pageSize: 20, items: [mockActivity] })
    }
    if (url.includes('/api/experiences/offerings')) {
      return jsonResponse({ total: 1, page: 1, pageSize: 20, items: [mockOffering] })
    }
    if (url.includes('/api/experiences/favourites')) {
      return jsonResponse([])
    }
    return jsonResponse({})
  }

  renderInApp(createElement(ExperiencesPage), { path: '/experiences' })

  // Check hero heading
  assert.ok(await screen.findByRole('heading', { name: /Explore our coast/i }))
  assert.ok(screen.getByPlaceholderText(/e\.g\. Coral reef/i))

  // Check destination card rendered
  assert.ok((await screen.findAllByText('Mirissa Bay')).length >= 1)
  assert.ok(screen.getByText(/Southern coastal paradise/i))

  // Check offering card rendered
  assert.ok(await screen.findByText('Dawn Blue Whale Expedition'))
  assert.ok(screen.getByText(/USD 85/i))
})

// -------------------------------------------------------------
// WEB-EXP-002: Discovery Unified Catalogue Filter & Search
// -------------------------------------------------------------
test('WEB-EXP-002 discovery hub supports unified keyword search across destinations and offerings (ui-integration: experience-discovery)', async () => {
  let capturedQuery = ''
  globalThis.fetch = async (input) => {
    const url = String(input)
    if (url.includes('/api/experiences/destinations')) {
      const parsed = new URL(url, 'http://localhost')
      capturedQuery = parsed.searchParams.get('query') || ''
      return jsonResponse({ total: 1, page: 1, pageSize: 20, items: [mockDestination] })
    }
    if (url.includes('/api/experiences/activities')) {
      return jsonResponse({ total: 1, page: 1, pageSize: 20, items: [mockActivity] })
    }
    if (url.includes('/api/experiences/offerings')) {
      return jsonResponse({ total: 1, page: 1, pageSize: 20, items: [mockOffering] })
    }
    if (url.includes('/api/experiences/favourites')) {
      return jsonResponse([])
    }
    return jsonResponse({})
  }

  renderInApp(createElement(ExperiencesPage), { path: '/experiences' })
  await screen.findByRole('heading', { name: /Explore our coast/i })

  // Verify unified search bar is present in catalog tab
  const searchInput = screen.getByPlaceholderText(/e\.g\. Coral reef, Whales, Mirissa, Snorkeling\.\.\./i)
  assert.ok(searchInput)

  // Enter a search term
  await userEvent.setup().type(searchInput, 'Mirissa')
  const filterBtn = screen.getByRole('button', { name: /^Filter$/i })
  await userEvent.setup().click(filterBtn)

  // Verify query was sent to API
  assert.equal(capturedQuery, 'Mirissa')

  // Verify matching destination is visible
  assert.ok((await screen.findAllByText('Mirissa Bay')).length >= 1)
})

// -------------------------------------------------------------
// WEB-EXP-011: Discovery Keyword and Region Filtering
// -------------------------------------------------------------
test('WEB-EXP-011 discovery hub supports location keyword search for coastal discovery (ui-integration: experience-discovery)', async () => {
  let capturedUrl = ''
  globalThis.fetch = async (input) => {
    const url = String(input)
    if (url.includes('/api/experiences/destinations')) {
      capturedUrl = url
      return jsonResponse({ total: 1, page: 1, pageSize: 20, items: [mockDestination] })
    }
    if (url.includes('/api/experiences/activities')) {
      return jsonResponse({ total: 1, page: 1, pageSize: 20, items: [mockActivity] })
    }
    if (url.includes('/api/experiences/offerings')) {
      return jsonResponse({ total: 1, page: 1, pageSize: 20, items: [mockOffering] })
    }
    if (url.includes('/api/experiences/favourites')) {
      return jsonResponse([])
    }
    return jsonResponse({})
  }

  renderInApp(createElement(ExperiencesPage), { path: '/experiences' })
  await screen.findByRole('heading', { name: /Explore our coast/i })

  // Type a natural location keyword into the unified search bar
  const searchInput = screen.getByPlaceholderText(/e\.g\. Coral reef, Whales, Mirissa, Snorkeling\.\.\./i)
  await userEvent.setup().type(searchInput, 'Southern Province')

  // Submit search
  const filterBtn = screen.getByRole('button', { name: /^Filter$/i })
  await userEvent.setup().click(filterBtn)

  // Verify API was called with query
  assert.ok(capturedUrl.includes('query=Southern+Province') || capturedUrl.includes('query=Southern%20Province'))

  // Verify result rendered
  assert.ok((await screen.findAllByText('Mirissa Bay')).length >= 1)
})

// -------------------------------------------------------------
// WEB-EXP-003: Discovery Network Error Handling
// -------------------------------------------------------------
test('WEB-EXP-003 discovery hub displays error state when API fails with retry option (ui-integration: experience-discovery)', async () => {
  globalThis.fetch = async () =>
    jsonResponse({ detail: 'Coastal catalogue service temporarily unavailable.' }, 503)

  renderInApp(createElement(ExperiencesPage), { path: '/experiences' })

  assert.ok(await screen.findByRole('alert'))
  assert.ok(screen.getByText(/Coastal catalogue service temporarily unavailable/i))
})

// -------------------------------------------------------------
// WEB-EXP-004: Destination Detail Page
// -------------------------------------------------------------
test('WEB-EXP-004 destination detail displays geography, marine telemetry, and biodiversity ML predictions (ui-integration: experience-destination-detail)', async () => {
  globalThis.fetch = async (input) => {
    const url = String(input)
    if (url.includes('/marine-conditions')) {
      return jsonResponse(mockMarineConditions)
    }
    if (url.includes('/operational-advisories')) {
      return jsonResponse(mockAdvisories)
    }
    if (url.includes('/biodiversity')) {
      return jsonResponse(mockBiodiversity)
    }
    if (url.includes('/api/experiences/activities')) {
      return jsonResponse({ total: 1, page: 1, pageSize: 20, items: [mockActivity] })
    }
    if (url.includes('/api/experiences/offerings')) {
      return jsonResponse({ total: 1, page: 1, pageSize: 20, items: [mockOffering] })
    }
    if (url.includes('/api/experiences/destinations/')) {
      return jsonResponse(mockDestination)
    }
    if (url.includes('/api/experiences/favourites')) {
      return jsonResponse([])
    }
    return jsonResponse({})
  }

  renderInApp(
    createElement(
      Routes,
      null,
      createElement(Route, {
        path: '/experiences/destinations/:id',
        element: createElement(DestinationDetailPage),
      })
    ),
    {
      path: `/experiences/destinations/${mockDestination.id}`,
    }
  )

  // Title and geography
  assert.ok(await screen.findByRole('heading', { name: 'Mirissa Bay' }))
  assert.ok(screen.getByText('Southern Province'))

  // Marine safety conditions telemetry
  assert.ok(await screen.findByText(/MARINE SAFETY/i))
  assert.ok(screen.getByText('Calm swell'))
  assert.ok(screen.getByText('0.8 m'))
  assert.ok(screen.getByText('9.2 kts'))

  // Coastal Operations advisory
  assert.ok(await screen.findByText('Harbor Speed Limit Notice'))

  // Member 3 Biodiversity ML predictions & provenance
  assert.ok(await screen.findByText('Blue Whale'))
  assert.ok(screen.getByText(/Balaenoptera musculus/i))
  assert.ok(screen.getByText(/92% prob\./i))
  assert.ok(screen.getByText(/MODEL CONNECTED/i))
})

// -------------------------------------------------------------
// WEB-EXP-005: Destination Not Found Error
// -------------------------------------------------------------
test('WEB-EXP-005 destination detail handles not found response gracefully (ui-integration: experience-destination-detail)', async () => {
  globalThis.fetch = async () =>
    jsonResponse({ detail: 'The requested coastal destination was not found.' }, 404)

  renderInApp(
    createElement(
      Routes,
      null,
      createElement(Route, {
        path: '/experiences/destinations/:id',
        element: createElement(DestinationDetailPage),
      })
    ),
    {
      path: '/experiences/destinations/00000000-0000-0000-0000-000000000000',
    }
  )

  assert.ok(await screen.findByRole('alert'))
  assert.ok(screen.getByText(/The requested coastal destination was not found/i))
  assert.ok(screen.getByRole('link', { name: /Back to Experiences Directory/i }))
})

// -------------------------------------------------------------
// WEB-EXP-006: Offering Detail & Availability Evaluation
// -------------------------------------------------------------
test('WEB-EXP-006 offering detail evaluates live availability and displays schedule slots (ui-integration: experience-offering-detail)', async () => {
  let availabilityRequested = false

  globalThis.fetch = async (input, init) => {
    const url = String(input)
    if (url.includes('/schedules')) {
      return jsonResponse([mockSchedule])
    }
    if (url.includes('/availability/evaluations')) {
      availabilityRequested = true
      return jsonResponse(mockAvailabilityEvaluation)
    }
    if (url.includes('/api/experiences/offerings/')) {
      return jsonResponse(mockOffering)
    }
    if (url.includes('/api/experiences/favourites')) {
      return jsonResponse([])
    }
    return jsonResponse({})
  }

  renderInApp(
    createElement(
      Routes,
      null,
      createElement(Route, {
        path: '/experiences/offerings/:id',
        element: createElement(OfferingDetailPage),
      })
    ),
    {
      path: `/experiences/offerings/${mockOffering.id}`,
    }
  )

  // Title, price, duration
  assert.ok(await screen.findByRole('heading', { name: 'Dawn Blue Whale Expedition' }))
  assert.ok(screen.getByText(/USD 85/i))
  assert.ok(screen.getByText(/240 mins/i))

  // Departure slot
  assert.ok(await screen.findByText(/Scheduled Departure Slots/i))

  // Run availability evaluation
  const checkBtn = screen.getByRole('button', { name: /Evaluate Availability/i })
  await userEvent.setup().click(checkBtn)

  // Verify availability status rendered
  assert.ok(await screen.findByText(/^AVAILABLE$/))
  assert.equal(availabilityRequested, true)
})

// -------------------------------------------------------------
// WEB-EXP-007: Favourites Wishlist Management
// -------------------------------------------------------------
test('WEB-EXP-007 favourites page renders bookmarked items and allows removal (ui-integration: experience-favourites-management)', async () => {
  let removed = false

  globalThis.fetch = async (input, init) => {
    const url = String(input)
    if (init?.method === 'DELETE' && url.includes('/api/experiences/favourites')) {
      removed = true
      return jsonResponse(null, 204)
    }
    if (url.includes('/api/experiences/destinations/')) {
      return jsonResponse(mockDestination)
    }
    if (url.includes('/api/experiences/favourites')) {
      return jsonResponse([
        {
          id: 'fav-1',
          userId: 'u-1',
          targetType: 'destination',
          targetId: mockDestination.id,
          targetTitle: 'Mirissa Bay',
          targetStatus: 'PUBLISHED',
          createdAt: '2026-09-20T00:00:00Z',
          notes: 'Must visit during peak migration!',
        },
      ])
    }
    return jsonResponse({})
  }

  renderInApp(createElement(FavouritesPage), {
    path: '/experiences/favourites',
    auth: makeAuthSessionValue({ status: 'signed-in', user: makeAuthUser() }),
  })

  // Verify saved item is listed
  assert.ok(await screen.findByText('Mirissa Bay'))

  // Remove favourite
  const removeBtn = screen.getByRole('button', { name: /Remove/i })
  await userEvent.setup().click(removeBtn)

  assert.equal(removed, true)
})

// -------------------------------------------------------------
// WEB-EXP-008: Favourites Guest State
// -------------------------------------------------------------
test('WEB-EXP-008 favourites page displays sign-in banner when visitor is not signed in (ui-integration: experience-favourites-management)', async () => {
  globalThis.fetch = async () => jsonResponse([])

  renderInApp(createElement(FavouritesPage), {
    path: '/experiences/favourites',
    auth: makeAuthSessionValue({ status: 'signed-out', user: null }),
  })

  assert.ok(await screen.findByText(/Sign in to manage your saved wishlist/i))
  assert.ok(screen.getAllByRole('link', { name: 'Sign in' }).length > 0)
})

// -------------------------------------------------------------
// WEB-EXP-009: Catalogue Management & Pre-Flight Audits
// -------------------------------------------------------------
test('WEB-EXP-009 catalogue management renders curation workspace and microservice diagnostics (ui-integration: experience-catalogue-management)', async () => {
  globalThis.fetch = async (input) => {
    const url = String(input)
    if (url.includes('/publication-evaluations')) {
      return jsonResponse({
        isReadyForPublication: true,
        eligibleStatus: 'PUBLISHED',
        validationErrors: [],
        warnings: [],
      })
    }
    if (url.includes('/dependencies/status')) {
      return jsonResponse(mockDependenciesStatus)
    }
    if (url.includes('/agent/context')) {
      return jsonResponse({
        agentName: 'CoastalOperationsAgent',
        status: 'not_connected',
        detail: 'Pre-G07 integration seam. AI agents not connected.',
        plannedTools: ['evaluate_safety', 'schedule_patrol'],
        checkedAt: '2026-09-27T12:00:00Z',
      })
    }
    if (url.includes('/api/experiences/destinations')) {
      return jsonResponse({ total: 1, page: 1, pageSize: 20, items: [mockDestination] })
    }
    if (url.includes('/api/experiences/activities')) {
      return jsonResponse({ total: 1, page: 1, pageSize: 20, items: [mockActivity] })
    }
    if (url.includes('/api/experiences/offerings')) {
      return jsonResponse({ total: 1, page: 1, pageSize: 20, items: [mockOffering] })
    }
    return jsonResponse({})
  }

  // Test manager role view (canManage = true)
  renderInApp(createElement(CatalogueManagementPage), {
    path: '/experiences/manage',
    auth: makeAuthSessionValue({
      status: 'signed-in',
      user: makeAuthUser({
        roles: ['CatalogueManager'],
        permissions: ['experiences.catalogue.manage', 'experiences.catalogue.read'],
      }),
    }),
  })

  // Heading and action buttons
  assert.ok(await screen.findByRole('heading', { name: /Catalogue Management Workspace/i }))
  assert.ok(screen.getByRole('button', { name: /Save Destination/i }))

  // Destination listed in table
  assert.ok(await screen.findByText('Mirissa Bay'))

  // Switch to Dependencies & Agent Seam tab
  const diagTab = screen.getByRole('button', { name: /Dependencies & Agent Seam/i })
  await userEvent.setup().click(diagTab)

  // Verify microservice dependencies status
  assert.ok(await screen.findByText(/Peer Microservices Health/i))
  assert.ok(screen.getByText('marine-safety'))
  assert.ok(screen.getByText('biodiversity-ml'))

  // Verify Agent Seam reporting
  assert.ok(screen.getByText(/Agentic AI Pre-G07 Seam/i))
  assert.ok(screen.getByText(/CoastalOperationsAgent/i))
})

// -------------------------------------------------------------
// WEB-EXP-009B: Catalogue Management Workspace Read-Only Boundary
// -------------------------------------------------------------
test('WEB-EXP-009B renders read-only catalogue view without mutation forms when user lacks manage permission', async () => {
  globalThis.fetch = async (input) => {
    const url = String(input)
    if (url.includes('/api/experiences/destinations')) {
      return jsonResponse({ total: 1, page: 1, pageSize: 20, items: [mockDestination] })
    }
    if (url.includes('/api/experiences/activities') || url.includes('/api/experiences/offerings')) {
      return jsonResponse({ total: 0, page: 1, pageSize: 20, items: [] })
    }
    return jsonResponse({})
  }

  renderInApp(createElement(CatalogueManagementPage), {
    path: '/experiences/manage',
    auth: makeAuthSessionValue({
      status: 'signed-in',
      user: makeAuthUser({
        roles: ['Staff'],
        permissions: ['experiences.catalogue.read'],
      }),
    }),
  })

  assert.ok(await screen.findByRole('heading', { name: /Catalogue Management Workspace/i }))
  assert.ok(await screen.findByText(/Read-only view/i))
  assert.equal(screen.queryByRole('button', { name: /Save Destination/i }), null)
})

// -------------------------------------------------------------
// WEB-EXP-012: Role Names Do Not Grant Catalogue Permissions
// -------------------------------------------------------------
test('WEB-EXP-012 Admin role and auth.role.manage alone cannot enable catalogue mutations', async () => {
  globalThis.fetch = async (input) => {
    const url = String(input)
    if (url.includes('/api/experiences/destinations')) {
      return jsonResponse({ total: 1, page: 1, pageSize: 20, items: [mockDestination] })
    }
    if (url.includes('/api/experiences/activities') || url.includes('/api/experiences/offerings')) {
      return jsonResponse({ total: 0, page: 1, pageSize: 20, items: [] })
    }
    return jsonResponse({})
  }

  renderInApp(createElement(CatalogueManagementPage), {
    path: '/experiences/manage',
    auth: makeAuthSessionValue({
      status: 'signed-in',
      user: makeAuthUser({ roles: ['Admin'], permissions: ['auth.role.manage'] }),
    }),
  })

  assert.ok(await screen.findByRole('heading', { name: /Catalogue Management Workspace/i }))
  assert.ok(await screen.findByText(/Read-only view/i))
  assert.equal(screen.queryByRole('button', { name: /Save Destination/i }), null)
})

// -------------------------------------------------------------
// WEB-EXP-013: System Role Management Permission Grants Catalogue Management
// -------------------------------------------------------------
test('WEB-EXP-013 system role manager permission enables the catalogue management workflow', async () => {
  globalThis.fetch = async (input) => {
    const url = String(input)
    if (url.includes('/api/experiences/destinations')) {
      return jsonResponse({ total: 1, page: 1, pageSize: 20, items: [mockDestination] })
    }
    if (url.includes('/api/experiences/activities') || url.includes('/api/experiences/offerings')) {
      return jsonResponse({ total: 0, page: 1, pageSize: 20, items: [] })
    }
    return jsonResponse({})
  }

  renderInApp(createElement(CatalogueManagementPage), {
    path: '/experiences/manage',
    auth: makeAuthSessionValue({
      status: 'signed-in',
      user: makeAuthUser({ roles: ['SystemRoleManager'], permissions: ['auth.role.system.manage'] }),
    }),
  })

  assert.ok(await screen.findByRole('heading', { name: /Catalogue Management Workspace/i }))
  assert.ok(await screen.findByRole('button', { name: /Save Destination/i }))
})

// -------------------------------------------------------------
// WEB-EXP-014: Discovery Management Links Follow Catalogue Permissions
// -------------------------------------------------------------
test('WEB-EXP-014 Admin role and role-editor permission do not expose catalogue management links', async () => {
  globalThis.fetch = async (input) => {
    const url = String(input)
    if (url.includes('/api/experiences/destinations')) {
      return jsonResponse({ total: 1, page: 1, pageSize: 20, items: [mockDestination] })
    }
    if (url.includes('/api/experiences/activities')) {
      return jsonResponse({ total: 1, page: 1, pageSize: 20, items: [mockActivity] })
    }
    if (url.includes('/api/experiences/offerings')) {
      return jsonResponse({ total: 1, page: 1, pageSize: 20, items: [mockOffering] })
    }
    if (url.includes('/api/experiences/favourites')) return jsonResponse([])
    return jsonResponse({})
  }

  renderInApp(createElement(ExperiencesPage), {
    path: '/experiences',
    auth: makeAuthSessionValue({
      status: 'signed-in',
      user: makeAuthUser({ roles: ['Admin'], permissions: ['auth.role.manage'] }),
    }),
  })

  assert.ok(await screen.findByRole('heading', { name: /Explore our coast/i }))
  assert.ok((await screen.findAllByText('Mirissa Bay')).length >= 1)
  assert.equal(screen.queryByRole('link', { name: /Catalogue Management/i }), null)
  assert.equal(screen.queryByRole('link', { name: /Add Destination/i }), null)
  assert.equal(screen.queryByRole('link', { name: /Edit Mirissa Bay/i }), null)
})

// -------------------------------------------------------------
// WEB-EXP-010: Interactive Coastal Map & Place Search
// -------------------------------------------------------------
test('WEB-EXP-010 discovery hub renders interactive coastal map and searches places (ui-integration: experience-discovery)', async () => {
  globalThis.fetch = async (input) => {
    const url = String(input)
    if (url.includes('/api/experiences/destinations')) {
      return jsonResponse({ total: 1, page: 1, pageSize: 20, items: [mockDestination] })
    }
    if (url.includes('/api/experiences/activities') || url.includes('/api/experiences/offerings')) {
      return jsonResponse({ total: 0, page: 1, pageSize: 20, items: [] })
    }
    if (url.includes('/api/experiences/favourites')) {
      return jsonResponse([])
    }
    if (url.includes('/api/experiences/map/config')) {
      return jsonResponse({
        provider: 'OpenFreeMap',
        tileServiceType: 'VectorTiles',
        vectorTileUrl: 'https://tiles.openfreemap.org/styles/{style}',
        availableStyles: { liberty: 'https://tiles.openfreemap.org/styles/liberty' },
        defaultStyle: 'liberty',
        attribution: '© OpenStreetMap contributors, OpenFreeMap',
        documentationUrl: 'https://openfreemap.org/',
      })
    }
    if (url.includes('/api/experiences/map/search')) {
      return jsonResponse({
        query: 'mirissa',
        results: [
          {
            displayName: 'Mirissa Beach, Southern Province, Sri Lanka',
            latitude: 5.9482,
            longitude: 80.4578,
            type: 'beach',
            category: 'place',
            region: 'Southern Province',
            country: 'Sri Lanka',
          },
        ],
        source: 'Photon-OSM',
        fallback: false,
        retrievedAt: '2026-09-27T12:00:00Z',
      })
    }
    return jsonResponse({})
  }

  renderInApp(createElement(ExperiencesPage), { path: '/experiences' })
  await screen.findByRole('heading', { name: /Explore our coast/i })

  // Switch to "Interactive Coastal Map" tab
  const mapTab = screen.getByRole('button', { name: /Interactive Coastal Map/i })
  await userEvent.setup().click(mapTab)

  // Verify map section heading
  assert.ok(await screen.findByRole('heading', { name: /Interactive Coastal Map of Sri Lanka/i }))
  assert.ok(screen.getByText(/INDIAN OCEAN/i))
  assert.ok(screen.getByText(/BAY OF BENGAL/i))

  // Verify search input
  const searchInput = screen.getByPlaceholderText(/e\.g\. Mirissa, Trincomalee/i)
  await userEvent.setup().type(searchInput, 'mirissa')
  const searchBtn = screen.getByRole('button', { name: /^Search$/i })
  await userEvent.setup().click(searchBtn)

  // Verify place search result and selected location card
  assert.ok((await screen.findAllByText(/Mirissa Beach, Southern Province/i)).length >= 1)
  assert.ok(screen.getByRole('button', { name: /Explore Experiences Here/i }))
})

