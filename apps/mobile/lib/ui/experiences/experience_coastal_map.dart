import 'dart:async';
import 'dart:math' as math;

import 'package:flutter/foundation.dart';
import 'package:flutter/gestures.dart';
import 'package:flutter/material.dart';
import 'package:maplibre_gl/maplibre_gl.dart';

import '../../data/models/experience_models.dart';
import '../../data/services/experience_location_service.dart';

@immutable
class ExperienceMapViewport {
  const ExperienceMapViewport({
    required this.latitude,
    required this.longitude,
    required this.zoom,
  });

  final double latitude;
  final double longitude;
  final double zoom;
}

class ExperienceCoastalMap extends StatefulWidget {
  const ExperienceCoastalMap({
    super.key,
    required this.config,
    required this.destinations,
    required this.onDestinationSelected,
    required this.onCurrentLocationRequested,
    this.viewport,
    this.height = 360,
    this.focusLocation,
    this.focusIsCurrentLocation = false,
    this.isLocationLoading = false,
  });

  final MapConfigDto config;
  final List<DestinationDto> destinations;
  final ValueChanged<String> onDestinationSelected;
  final Future<void> Function() onCurrentLocationRequested;
  final ExperienceMapViewport? viewport;
  final double height;
  final ExperienceCoordinates? focusLocation;
  final bool focusIsCurrentLocation;
  final bool isLocationLoading;

  @override
  State<ExperienceCoastalMap> createState() => _ExperienceCoastalMapState();
}

class _ExperienceCoastalMapState extends State<ExperienceCoastalMap> {
  static const _loadTimeout = Duration(seconds: 15);

  MapLibreMapController? _controller;
  Timer? _loadTimer;
  UniqueKey _mapKey = UniqueKey();
  bool _styleReady = false;
  bool _mapHasRendered = false;
  bool _didAutoCenterForDestinations = false;
  String? _loadError;

  List<DestinationDto> get _locatedDestinations =>
      widget.destinations.where(_hasUsableCoordinates).toList(growable: false);

  String? get _styleUrl {
    final value = widget.config.availableStyles[widget.config.defaultStyle];
    final uri = value == null ? null : Uri.tryParse(value);
    if (uri == null ||
        uri.scheme != 'https' ||
        uri.host != 'tiles.openfreemap.org' ||
        uri.userInfo.isNotEmpty ||
        (uri.hasPort && uri.port != 443)) {
      return null;
    }
    return uri.toString();
  }

  String get _destinationSignature => _locatedDestinations
      .map(
        (destination) =>
            '${destination.id}:${destination.latitude}:${destination.longitude}',
      )
      .join('|');

  @override
  void initState() {
    super.initState();
    _didAutoCenterForDestinations = _locatedDestinations.isNotEmpty;
    if (_supportsMapPlatform && _styleUrl != null) _startLoadTimer();
  }

  @override
  void didUpdateWidget(covariant ExperienceCoastalMap oldWidget) {
    super.didUpdateWidget(oldWidget);
    final styleChanged =
        oldWidget.config.defaultStyle != widget.config.defaultStyle ||
        oldWidget.config.availableStyles[oldWidget.config.defaultStyle] !=
            widget.config.availableStyles[widget.config.defaultStyle];
    if (styleChanged) {
      _controller = null;
      _styleReady = false;
      _mapHasRendered = false;
      _loadError = null;
      _mapKey = UniqueKey();
      if (_supportsMapPlatform && _styleUrl != null) _startLoadTimer();
      return;
    }

    final oldSignature = oldWidget.destinations
        .where(_hasUsableCoordinates)
        .map(
          (destination) =>
              '${destination.id}:${destination.latitude}:${destination.longitude}',
        )
        .join('|');
    final focusChanged =
        oldWidget.focusLocation?.latitude != widget.focusLocation?.latitude ||
        oldWidget.focusLocation?.longitude != widget.focusLocation?.longitude ||
        oldWidget.focusIsCurrentLocation != widget.focusIsCurrentLocation;
    if ((oldSignature != _destinationSignature || focusChanged) &&
        _styleReady) {
      unawaited(_syncDestinationMarkers());
    }
    if (focusChanged && widget.focusLocation != null && _styleReady) {
      unawaited(_focusMapOnLocation(widget.focusLocation!));
    } else if (_viewportChanged(oldWidget.viewport, widget.viewport) &&
        widget.viewport != null &&
        widget.focusLocation == null &&
        _styleReady) {
      unawaited(_focusMapOnViewport(widget.viewport!));
    }
  }

  @override
  void dispose() {
    _loadTimer?.cancel();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    if (!_supportsMapPlatform) {
      return _statusSurface(
        height: widget.height,
        icon: Icons.map_outlined,
        message: 'The interactive coastal map is available on mobile and web.',
      );
    }

    final styleUrl = _styleUrl;
    if (styleUrl == null) {
      return _statusSurface(
        height: widget.height,
        icon: Icons.error_outline,
        message: 'The map provider configuration is unavailable.',
      );
    }

    return SizedBox(
      height: widget.height,
      child: Stack(
        fit: StackFit.expand,
        children: [
          MapLibreMap(
            key: _mapKey,
            initialCameraPosition: _initialCameraPosition(_locatedDestinations),
            styleString: styleUrl,
            gestureRecognizers: <Factory<OneSequenceGestureRecognizer>>{
              Factory<OneSequenceGestureRecognizer>(
                () => EagerGestureRecognizer(),
              ),
            },
            onMapCreated: _onMapCreated,
            onStyleLoadedCallback: _onStyleLoaded,
            onMapIdle: _onMapIdle,
            compassEnabled: true,
            logoEnabled: true,
            scaleControlEnabled: true,
            scaleControlUnit: ScaleControlUnit.metric,
          ),
          if (!_styleReady && _loadError == null)
            const ColoredBox(
              color: Color(0xFFF3F7F5),
              child: Center(
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    CircularProgressIndicator(),
                    SizedBox(height: 12),
                    Text('Loading coastal map…'),
                  ],
                ),
              ),
            ),
          if (_loadError case final message?)
            ColoredBox(
              color: const Color(0xFFF3F7F5),
              child: Center(
                child: Padding(
                  padding: const EdgeInsets.all(20),
                  child: Column(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      const Icon(Icons.cloud_off_outlined),
                      const SizedBox(height: 8),
                      Text(message, textAlign: TextAlign.center),
                      const SizedBox(height: 8),
                      TextButton.icon(
                        onPressed: _retryMap,
                        icon: const Icon(Icons.refresh),
                        label: const Text('Retry map'),
                      ),
                    ],
                  ),
                ),
              ),
            ),
          if (_styleReady && _locatedDestinations.isEmpty)
            const Positioned(
              left: 12,
              right: 12,
              top: 12,
              child: IgnorePointer(
                child: Card(
                  child: Padding(
                    padding: EdgeInsets.all(8),
                    child: Text(
                      'No destination coordinates are available yet.',
                      textAlign: TextAlign.center,
                    ),
                  ),
                ),
              ),
            ),
          Positioned(
            top: 12,
            right: 12,
            child: IconButton.filledTonal(
              key: const Key('btn-map-current-location'),
              tooltip: 'Use my current location',
              onPressed: widget.isLocationLoading
                  ? null
                  : () => unawaited(widget.onCurrentLocationRequested()),
              icon: widget.isLocationLoading
                  ? const SizedBox(
                      width: 18,
                      height: 18,
                      child: CircularProgressIndicator(strokeWidth: 2),
                    )
                  : const Icon(Icons.my_location),
            ),
          ),
        ],
      ),
    );
  }

  bool get _supportsMapPlatform =>
      kIsWeb ||
      defaultTargetPlatform == TargetPlatform.android ||
      defaultTargetPlatform == TargetPlatform.iOS;

  void _onMapCreated(MapLibreMapController controller) {
    _controller = controller;
    controller.onCircleTapped.add(_onCircleTapped);
  }

  void _onMapIdle() {
    _mapHasRendered = true;
    _loadTimer?.cancel();
  }

  void _onCircleTapped(Circle circle) {
    final destinationId = circle.data?['destinationId'];
    if (destinationId is String && destinationId.isNotEmpty) {
      widget.onDestinationSelected(destinationId);
    }
  }

  Future<void> _onStyleLoaded() async {
    try {
      await _syncDestinationMarkers();
      if (!mounted) return;
      setState(() {
        _styleReady = true;
        _loadError = null;
      });
    } catch (_) {
      _showLoadError(
        'Map tiles could not be loaded. Check your connection and retry.',
      );
    }
  }

  Future<void> _syncDestinationMarkers() async {
    final controller = _controller;
    if (controller == null) return;

    final destinations = _locatedDestinations;
    await controller.clearCircles();
    final circles = destinations
        .map(
          (destination) => CircleOptions(
            geometry: LatLng(destination.latitude, destination.longitude),
            circleRadius: 8,
            circleColor: '#246778',
            circleStrokeColor: '#FFFFFF',
            circleStrokeWidth: 2,
          ),
        )
        .toList(growable: true);
    final circleData = destinations
        .map<Map<String, dynamic>>(
          (destination) => {'destinationId': destination.id},
        )
        .toList(growable: true);
    final focus = widget.focusLocation;
    if (focus != null && _hasUsableFocus(focus)) {
      circles.add(
        CircleOptions(
          geometry: LatLng(focus.latitude, focus.longitude),
          circleRadius: widget.focusIsCurrentLocation ? 10 : 9,
          circleColor: widget.focusIsCurrentLocation ? '#347B9B' : '#C47B2B',
          circleStrokeColor: '#FFFFFF',
          circleStrokeWidth: 3,
        ),
      );
      circleData.add(const {'mapFocusMarker': true});
    }
    if (circles.isNotEmpty) await controller.addCircles(circles, circleData);

    if (!_didAutoCenterForDestinations && destinations.isNotEmpty) {
      await controller.animateCamera(
        CameraUpdate.newCameraPosition(_initialCameraPosition(destinations)),
      );
      _didAutoCenterForDestinations = true;
    }
  }

  CameraPosition _initialCameraPosition(List<DestinationDto> destinations) {
    final focus = widget.focusLocation;
    if (focus != null && _hasUsableFocus(focus)) {
      return CameraPosition(
        target: LatLng(focus.latitude, focus.longitude),
        zoom: widget.focusIsCurrentLocation ? 12 : 12.5,
      );
    }
    final viewport = widget.viewport;
    if (viewport != null) {
      return CameraPosition(
        target: LatLng(viewport.latitude, viewport.longitude),
        zoom: viewport.zoom,
      );
    }
    if (destinations.isEmpty) {
      return const CameraPosition(target: LatLng(7.8731, 80.7718), zoom: 6.5);
    }

    var minLatitude = destinations.first.latitude;
    var maxLatitude = minLatitude;
    var minLongitude = destinations.first.longitude;
    var maxLongitude = minLongitude;
    for (final destination in destinations.skip(1)) {
      minLatitude = math.min(minLatitude, destination.latitude);
      maxLatitude = math.max(maxLatitude, destination.latitude);
      minLongitude = math.min(minLongitude, destination.longitude);
      maxLongitude = math.max(maxLongitude, destination.longitude);
    }

    final span = math.max(
      maxLatitude - minLatitude,
      maxLongitude - minLongitude,
    );
    final zoom = span == 0
        ? 10.0
        : (6 + math.log(4.5 / span) / math.ln2).clamp(5.0, 11.0);
    return CameraPosition(
      target: LatLng(
        (minLatitude + maxLatitude) / 2,
        (minLongitude + maxLongitude) / 2,
      ),
      zoom: zoom.toDouble(),
    );
  }

  bool _hasUsableCoordinates(DestinationDto destination) =>
      destination.latitude.isFinite &&
      destination.longitude.isFinite &&
      destination.latitude >= -85.0511 &&
      destination.latitude <= 85.0511 &&
      destination.longitude >= -180 &&
      destination.longitude <= 180 &&
      !(destination.latitude == 0 && destination.longitude == 0);

  bool _hasUsableFocus(ExperienceCoordinates location) =>
      location.latitude.isFinite &&
      location.longitude.isFinite &&
      location.latitude >= -85.0511 &&
      location.latitude <= 85.0511 &&
      location.longitude >= -180 &&
      location.longitude <= 180 &&
      !(location.latitude == 0 && location.longitude == 0);

  Future<void> _focusMapOnLocation(ExperienceCoordinates location) async {
    final controller = _controller;
    if (controller == null || !_hasUsableFocus(location)) return;
    await controller.animateCamera(
      CameraUpdate.newCameraPosition(
        CameraPosition(
          target: LatLng(location.latitude, location.longitude),
          zoom: widget.focusIsCurrentLocation ? 12 : 12.5,
        ),
      ),
    );
  }

  Future<void> _focusMapOnViewport(ExperienceMapViewport viewport) async {
    final controller = _controller;
    if (controller == null) return;
    await controller.animateCamera(
      CameraUpdate.newCameraPosition(
        CameraPosition(
          target: LatLng(viewport.latitude, viewport.longitude),
          zoom: viewport.zoom,
        ),
      ),
    );
  }

  bool _viewportChanged(
    ExperienceMapViewport? previous,
    ExperienceMapViewport? next,
  ) =>
      previous?.latitude != next?.latitude ||
      previous?.longitude != next?.longitude ||
      previous?.zoom != next?.zoom;

  void _startLoadTimer() {
    _loadTimer?.cancel();
    _mapHasRendered = false;
    _loadTimer = Timer(_loadTimeout, () {
      if (mounted && !_mapHasRendered) {
        _showLoadError(
          'The map tiles are taking too long to load. Check your connection and retry.',
        );
      }
    });
  }

  void _showLoadError(String message) {
    if (!mounted) return;
    _loadTimer?.cancel();
    setState(() => _loadError = message);
  }

  void _retryMap() {
    setState(() {
      _controller = null;
      _styleReady = false;
      _mapHasRendered = false;
      _loadError = null;
      _mapKey = UniqueKey();
    });
    _startLoadTimer();
  }

  Widget _statusSurface({
    required double height,
    required IconData icon,
    required String message,
  }) => SizedBox(
    height: height,
    child: ColoredBox(
      color: const Color(0xFFF3F7F5),
      child: Center(
        child: Padding(
          padding: const EdgeInsets.all(20),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Icon(icon),
              const SizedBox(height: 8),
              Text(message, textAlign: TextAlign.center),
            ],
          ),
        ),
      ),
    ),
  );
}
