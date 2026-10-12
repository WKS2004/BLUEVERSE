import { useEffect, useMemo, useRef, useState } from 'react'
import {
  Map as MapLibreMap,
  Marker,
  NavigationControl,
  ScaleControl,
  setWorkerUrl,
} from 'maplibre-gl'
import mapLibreWorkerUrl from 'maplibre-gl/dist/maplibre-gl-worker.mjs?worker&url'
import 'maplibre-gl/dist/maplibre-gl.css'
import type { DestinationDto } from './experienceApi'

setWorkerUrl(mapLibreWorkerUrl)

export type ExperienceMapFocus = {
  name: string
  latitude: number
  longitude: number
  isCurrentLocation?: boolean
}

export type ExperienceMapView = {
  latitude: number
  longitude: number
  zoom: number
}

type ExperienceCoastalMapProps = {
  styleUrl: string | null
  destinations: DestinationDto[]
  focus: ExperienceMapFocus | null
  view: ExperienceMapView
  onDestinationSelected: (destination: DestinationDto) => void
}

function isAllowedStyleUrl(value: string | null): value is string {
  if (!value) return false
  try {
    const url = new URL(value)
    return url.protocol === 'https:'
      && url.hostname === 'tiles.openfreemap.org'
      && !url.username
      && !url.password
      && (!url.port || url.port === '443')
  } catch {
    return false
  }
}

function isValidCoordinate(latitude: number, longitude: number) {
  return Number.isFinite(latitude)
    && Number.isFinite(longitude)
    && latitude >= -85.0511
    && latitude <= 85.0511
    && longitude >= -180
    && longitude <= 180
    && !(latitude === 0 && longitude === 0)
}

function destinationMarker(destination: DestinationDto, onSelect: () => void) {
  const button = document.createElement('button')
  button.type = 'button'
  // Keep the MapLibre anchor fixed during hover. Transform effects on marker
  // controls can look like a location is drifting away from its coordinates.
  button.className = 'grid size-8 place-items-center rounded-full border-2 border-white bg-coast-deep text-white shadow-lg transition-[background-color,box-shadow] hover:bg-coast-blue hover:shadow-xl focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-coast-blue'
  button.setAttribute('aria-label', `Open ${destination.name}`)
  button.title = destination.name

  const dot = document.createElement('span')
  dot.className = 'size-2.5 rounded-full bg-white'
  button.append(dot)
  button.addEventListener('click', (event) => {
    event.stopPropagation()
    onSelect()
  })
  return button
}

function focusMarker(focus: ExperienceMapFocus) {
  const button = document.createElement('button')
  button.type = 'button'
  button.className = focus.isCurrentLocation
    ? 'relative grid size-9 place-items-center rounded-full border-2 border-white bg-coast-blue shadow-lg focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-coast-deep'
    : 'grid size-8 place-items-center rounded-full border-2 border-white bg-coast-sand shadow-lg focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-coast-deep'
  button.setAttribute(
    'aria-label',
    focus.isCurrentLocation ? 'Approximate current location' : `Selected place: ${focus.name}`,
  )
  button.title = focus.isCurrentLocation ? 'Approximate current location' : focus.name

  if (focus.isCurrentLocation) {
    const pulse = document.createElement('span')
    pulse.className = 'absolute size-7 animate-ping rounded-full bg-coast-blue/50'
    const dot = document.createElement('span')
    dot.className = 'relative size-3 rounded-full border-2 border-white bg-coast-blue'
    button.append(pulse, dot)
  } else {
    const dot = document.createElement('span')
    dot.className = 'size-3 rounded-full bg-coast-deep'
    button.append(dot)
  }
  return button
}

export default function ExperienceCoastalMap({
  styleUrl,
  destinations,
  focus,
  view,
  onDestinationSelected,
}: ExperienceCoastalMapProps) {
  const containerRef = useRef<HTMLDivElement>(null)
  const mapRef = useRef<MapLibreMap | null>(null)
  const markersRef = useRef<Marker[]>([])
  const selectDestinationRef = useRef(onDestinationSelected)
  const [mapReady, setMapReady] = useState(false)
  const [mapError, setMapError] = useState<string | null>(null)
  const [retryCount, setRetryCount] = useState(0)
  const { latitude: viewLatitude, longitude: viewLongitude, zoom: viewZoom } = view
  const viewRef = useRef(view)

  const safeStyleUrl = useMemo(
    () => isAllowedStyleUrl(styleUrl) ? styleUrl : null,
    [styleUrl],
  )

  useEffect(() => {
    selectDestinationRef.current = onDestinationSelected
  }, [onDestinationSelected])

  useEffect(() => {
    viewRef.current = {
      latitude: viewLatitude,
      longitude: viewLongitude,
      zoom: viewZoom,
    }
  }, [viewLatitude, viewLongitude, viewZoom])

  useEffect(() => {
    const container = containerRef.current
    if (!container || !safeStyleUrl) return

    let loaded = false
    const timer = window.setTimeout(() => {
      if (!loaded) setMapError('The coastal map is taking too long to load. Check your connection and retry.')
    }, 15000)

    setMapError(null)
    setMapReady(false)
    let map: MapLibreMap
    try {
      const initialView = viewRef.current
      map = new MapLibreMap({
        container,
        style: safeStyleUrl,
        center: [initialView.longitude, initialView.latitude],
        zoom: initialView.zoom,
        // Keep the provider credit in MapLibre's compact attribution pill on
        // every viewport size, matching the intended pill-and-info treatment.
        attributionControl: { compact: true },
        dragRotate: false,
        pitchWithRotate: false,
        maxPitch: 0,
      })
      map.addControl(new NavigationControl({ showCompass: false }), 'top-right')
      map.addControl(new ScaleControl({ unit: 'metric' }), 'bottom-left')
      map.on('load', () => {
        loaded = true
        window.clearTimeout(timer)
        const attributionControl = container.querySelector<HTMLElement>(
          '.maplibregl-ctrl-attrib',
        )
        attributionControl?.classList.remove('maplibregl-compact-show')
        attributionControl?.removeAttribute('open')
        setMapReady(true)
        setMapError(null)
      })
      mapRef.current = map
    } catch {
      window.clearTimeout(timer)
      const startupFailureTimer = window.setTimeout(() => {
        setMapError('The interactive map could not start on this browser. Search or browse the destination list instead.')
      }, 0)
      return () => {
        window.clearTimeout(timer)
        window.clearTimeout(startupFailureTimer)
      }
    }

    return () => {
      window.clearTimeout(timer)
      markersRef.current.forEach((marker) => marker.remove())
      markersRef.current = []
      map.remove()
      mapRef.current = null
      setMapReady(false)
    }
  }, [safeStyleUrl, retryCount])

  useEffect(() => {
    const map = mapRef.current
    if (!map || !mapReady) return
    map.flyTo({
      center: [viewLongitude, viewLatitude],
      zoom: viewZoom,
      essential: true,
      duration: 700,
    })
  }, [mapReady, viewLatitude, viewLongitude, viewZoom])

  useEffect(() => {
    const map = mapRef.current
    if (!map || !mapReady) return

    markersRef.current.forEach((marker) => marker.remove())
    markersRef.current = []

    const markers: Marker[] = []
    for (const destination of destinations) {
      if (!isValidCoordinate(destination.latitude, destination.longitude)) continue
      const element = destinationMarker(destination, () => selectDestinationRef.current(destination))
      markers.push(
        new Marker({ element, anchor: 'center' })
          .setLngLat([destination.longitude, destination.latitude])
          .addTo(map),
      )
    }

    const focusMatchesDestination = focus && destinations.some((destination) =>
      isValidCoordinate(destination.latitude, destination.longitude)
      && Math.abs(destination.latitude - focus.latitude) < 0.00001
      && Math.abs(destination.longitude - focus.longitude) < 0.00001,
    )
    if (focus && isValidCoordinate(focus.latitude, focus.longitude) && !focusMatchesDestination) {
      markers.push(
        new Marker({ element: focusMarker(focus), anchor: 'center' })
          .setLngLat([focus.longitude, focus.latitude])
          .addTo(map),
      )
    }

    markersRef.current = markers
    return () => {
      markers.forEach((marker) => marker.remove())
      markersRef.current = []
    }
  }, [destinations, focus, mapReady])

  if (!safeStyleUrl) {
    return (
      <div className="grid h-full min-h-72 place-items-center p-6 text-center text-sm text-coast-muted" role="status">
        The map provider configuration is unavailable. Search or browse the destination list instead.
      </div>
    )
  }

  return (
    <div className="relative h-full min-h-72 w-full" data-testid="experience-coastal-map">
      <div className="absolute inset-0">
        <div ref={containerRef} className="h-full w-full" aria-label="Interactive coastal map" />
      </div>
      {(!mapReady || mapError) && (
        <div className="absolute inset-0 z-20 grid place-items-center bg-coast-sage/95 p-6 text-center" role={mapError ? 'alert' : 'status'}>
          <div className="max-w-sm">
            <p className="text-sm font-semibold text-coast-deep">
              {mapError || 'Loading coastal map…'}
            </p>
            {mapError && (
              <button
                className="mt-3 rounded-full border border-coast-line bg-white px-4 py-2 text-xs font-extrabold text-coast-deep transition hover:bg-coast-pearl"
                onClick={() => setRetryCount((count) => count + 1)}
                type="button"
              >
                Retry map
              </button>
            )}
          </div>
        </div>
      )}
    </div>
  )
}
