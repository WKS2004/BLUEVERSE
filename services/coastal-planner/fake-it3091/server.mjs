import { createServer } from 'node:http'

const port = Number(process.env.PORT ?? 8080)
const expectedToken = process.env.IT3091_FAKE_API_KEY ?? ''
let behavior = process.env.IT3091_FAKE_BEHAVIOR ?? 'valid'
let requestCount = 0
let lastInput = null
let lastCorrelationId = null
let lastHadAuthorization = false
let lastAuthorizationMatchesCredential = false
const behaviors = new Set(['valid', 'timeout', '5xx', 'malformed', 'wrong-identity', 'out-of-range', 'stale', 'unsupported-location'])

function json(response, status, value) {
  response.writeHead(status, { 'content-type': 'application/json; charset=utf-8' })
  response.end(JSON.stringify(value))
}

const server = createServer(async (request, response) => {
  const url = new URL(request.url ?? '/', 'http://localhost')
  if (url.pathname === '/health') return json(response, 200, { status: 'ok' })
  if (url.pathname === '/__fake/state' && request.method === 'GET')
    return json(response, 200, { behavior, requestCount, lastInput, lastCorrelationId, lastHadAuthorization, lastAuthorizationMatchesCredential })

  if (url.pathname === '/__fake/behavior' && request.method === 'POST') {
    let body = ''
    for await (const chunk of request) body += chunk
    let input
    try { input = JSON.parse(body) } catch { return json(response, 400, { code: 'INVALID_BEHAVIOR' }) }
    if (!behaviors.has(input.behavior)) return json(response, 400, { code: 'UNKNOWN_BEHAVIOR' })
    behavior = input.behavior
    requestCount = 0
    lastInput = null
    lastCorrelationId = null
    lastHadAuthorization = false
    lastAuthorizationMatchesCredential = false
    return json(response, 200, { behavior })
  }

  if (url.pathname !== '/v1/predictions' || request.method !== 'POST') return json(response, 404, { code: 'NOT_FOUND' })
  if (expectedToken && request.headers.authorization !== `Bearer ${expectedToken}`) return json(response, 401, { code: 'UNAUTHORIZED' })
  requestCount++
  lastCorrelationId = request.headers['x-correlation-id'] ?? null
  lastHadAuthorization = Boolean(request.headers.authorization)
  lastAuthorizationMatchesCredential = request.headers.authorization === `Bearer ${expectedToken}`

  let body = ''
  for await (const chunk of request) body += chunk
  let input
  try { input = JSON.parse(body) } catch { return json(response, 400, { code: 'INVALID_JSON' }) }
  lastInput = input
  if (!input.destinationId || !/^[0-9a-f-]{36}$/i.test(input.destinationId)) return json(response, 400, { code: 'INVALID_DESTINATION' })

  switch (behavior) {
    case 'timeout': await new Promise(resolve => setTimeout(resolve, 7000)); break
    case '5xx': return json(response, 503, { code: 'SYNTHETIC_PROVIDER_FAILURE' })
    case 'malformed': response.writeHead(200, { 'content-type': 'application/json' }); return response.end('{not-json')
    case 'unsupported-location': return json(response, 422, { code: 'UNSUPPORTED_LOCATION', message: 'The requested location is not supported.' })
  }

  const now = new Date()
  const responseId = behavior === 'wrong-identity' ? '00000000-0000-4000-8000-000000000099' : input.destinationId
  const payload = {
    contractVersion: 1,
    provider: 'synthetic-it3091-fake',
    destinationId: responseId,
    activityId: input.activityId ?? null,
    status: 'available',
    modelVersion: 'fake-v1',
    inferenceTimestampUtc: behavior === 'stale' ? new Date(now.getTime() - 7 * 60 * 60_000).toISOString() : now.toISOString(),
    species: [{
      speciesId: '00000000-0000-4000-8000-000000000001',
      scientificName: 'Chelonia mydas (synthetic fixture)',
      commonName: 'Synthetic green turtle fixture',
      habitatSuitability: behavior === 'out-of-range' ? 1.5 : 0.81,
      confidenceLevel: 'synthetic-test-only',
      uncertaintyText: 'Synthetic test response; not a real prediction.'
    }],
    limitations: 'Synthetic fake-service fixture. Never represents an IT3091 model result.'
  }
  if (behavior === 'wrong-identity') payload.activityId = '00000000-0000-4000-8000-000000000099'
  if (behavior === 'malformed') payload.species = 'not-an-array'
  return json(response, 200, payload)
})

server.listen(port, '0.0.0.0', () => process.stdout.write(`fake-it3091 listening on ${port}\n`))
