import 'dart:async';

import 'package:flutter/material.dart';

import '../blueverse_theme.dart';

import '../../data/models/marine_models.dart';
import '../../data/services/marine_api_client.dart';
import '../../data/services/marine_service.dart';
import 'marine_presentation.dart';
import '../feedback/loading_screen_controller.dart';

class SuitabilityScreen extends StatefulWidget {
  const SuitabilityScreen({
    super.key,
    required this.service,
    this.embedded = false,
  });
  final MarineService service;
  final bool embedded;

  @override
  State<SuitabilityScreen> createState() => _SuitabilityScreenState();
}

class _SuitabilityScreenState extends State<SuitabilityScreen> {
  static const _initialLatitude = 6.025;
  static const _initialLongitude = 80.216;

  final _latitudeController = TextEditingController(
    text: _initialLatitude.toStringAsFixed(5),
  );
  final _longitudeController = TextEditingController(
    text: _initialLongitude.toStringAsFixed(5),
  );
  final _timeController = TextEditingController();
  String _activityId = '';
  List<Map<String, dynamic>> _activities = const [];
  ConditionSnapshotDto? _snapshot;
  SuitabilityResultDto? _result;
  bool _loading = false;
  String? _error;
  String? _formError;

  @override
  void initState() {
    super.initState();
    _loadActivities();
  }

  String? _catalogueError;
  bool _catalogueLoading = true;

  Future<void> _loadActivities() async {
    if (!mounted) return;
    setState(() {
      _catalogueLoading = true;
      _catalogueError = null;
    });
    try {
      final rows = await widget.service.activities();
      if (mounted) {
        setState(() {
          _activities = rows;
          _activityId = rows.isEmpty ? '' : rows.first['id'] as String;
          _catalogueLoading = false;
        });
      }
    } catch (error) {
      if (mounted) {
        setState(() {
          _activities = const [];
          _activityId = '';
          _catalogueError = error is MarineApiException
              ? error.message
              : 'Activities could not be loaded. Please retry.';
          _catalogueLoading = false;
        });
      }
    }
  }

  Widget _catalogueStatus() {
    if (_catalogueLoading) return const Text('Loading available activities…');
    if (_catalogueError != null) {
      return Column(
        children: [
          const Text('Activity data unavailable'),
          Text(_catalogueError!),
          const Text('Condition lookup and saved records remain accessible.'),
          TextButton(
            onPressed: _loadActivities,
            child: const Text('Retry activities'),
          ),
        ],
      );
    }
    if (_activities.isEmpty) {
      return const Text('No published activities are available.');
    }
    return const SizedBox.shrink();
  }

  @override
  void dispose() {
    _latitudeController.dispose();
    _longitudeController.dispose();
    _timeController.dispose();
    super.dispose();
  }

  Future<void> _loadConditionsAndEvaluate({bool conditionsOnly = false}) async {
    if (!mounted || _loading) return;
    final latitude = _parseCoordinate(_latitudeController.text, -90, 90);
    final longitude = _parseCoordinate(_longitudeController.text, -180, 180);
    if (latitude == null || longitude == null) {
      setState(() {
        _formError = 'Enter a valid latitude and longitude in decimal degrees.';
        _error = null;
        _snapshot = null;
        _result = null;
      });
      return;
    }
    final requestedTime = _parseUtcTimestamp(_timeController.text);
    if (requestedTime == null && _timeController.text.trim().isNotEmpty) {
      setState(() {
        _formError = 'Enter the requested time as a valid UTC moment, e.g. 2026-09-26T10:00.';
        _error = null;
        _snapshot = null;
        _result = null;
      });
      return;
    }
    setState(() {
      _formError = null;
      _error = null;
      _loading = true;
      _snapshot = null;
      _result = null;
    });
    final finishLoading = blueverseLoadingScreenController.begin();
    try {
      if (conditionsOnly) {
        final conditions = await widget.service.currentConditions(
          latitude: latitude,
          longitude: longitude,
          timeUtc: requestedTime,
        );
        if (mounted) {
          setState(() {
            _snapshot = conditions;
            _result = null;
            _loading = false;
          });
        }
        return;
      }
      final result = await widget.service.evaluateSuitability(
        activityId: _activityId,
        latitude: latitude,
        longitude: longitude,
        timeUtc: requestedTime,
      );
      final conditions =
          result.conditions != null &&
              result.source != null &&
              result.retrievedAt != null &&
              result.freshness != null
          ? result.toConditionSnapshot()
          : null;
      if (mounted) {
        setState(() {
          _snapshot = conditions;
          _result = result;
          _loading = false;
        });
      }
    } on MarineApiException catch (error) {
      if (mounted) {
        setState(() {
          _error = error.message;
          _loading = false;
          _snapshot = null;
          _result = null;
        });
      }
    } catch (error) {
      if (mounted) {
        setState(() {
          _error = 'The marine service could not be reached. Check the gateway address and try again.';
          _loading = false;
          _snapshot = null;
          _result = null;
        });
      }
    } finally {
      finishLoading();
    }
  }

  @override
  Widget build(BuildContext context) {
    if (widget.embedded) return _buildBody();
    return Scaffold(
      appBar: AppBar(title: const Text('Marine conditions')),
      body: _buildBody(),
    );
  }

  Widget _buildBody() {
    return RefreshIndicator(
      onRefresh: () => _loadConditionsAndEvaluate(conditionsOnly: true),
      child: SingleChildScrollView(
        physics: const AlwaysScrollableScrollPhysics(),
        padding: const EdgeInsets.fromLTRB(16, 16, 16, 28),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            if (widget.embedded) const MarineHero(index: 0),
            _catalogueStatus(),
            if (_error != null) _errorCard(),
            if (_formError != null) _formErrorCard(),
            _queryCard(),
            if (_snapshot != null) _evidenceCard(),
            if (_result != null) _resultCard(),
            if (_snapshot == null &&
                _result == null &&
                !_loading &&
                _error == null)
              _emptyPrompt(),
            const _NoteCard(),
          ],
        ),
      ),
    );
  }

  Widget _errorCard() => Card(
    child: Padding(
      padding: const EdgeInsets.all(14),
      child: Row(
        children: [
          const Icon(Icons.cloud_off_outlined, color: Colors.redAccent),
          const SizedBox(width: 12),
          Expanded(
            child: Text(
              _error!,
              style: const TextStyle(color: Colors.redAccent),
            ),
          ),
        ],
      ),
    ),
  );

  Widget _formErrorCard() => Card(
    child: Padding(
      padding: const EdgeInsets.all(14),
      child: Row(
        children: [
          const Icon(Icons.error_outline, color: Color(0xFF92621B)),
          const SizedBox(width: 12),
          Flexible(
            child: Padding(
              padding: EdgeInsets.symmetric(horizontal: 4),
              child: Text(_formError!),
            ),
          ),
        ],
      ),
    ),
  );

  Widget _queryCard() {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                const Icon(
                  Icons.waves_outlined,
                  color: BlueversePalette.coastBlue,
                ),
                const SizedBox(width: 8),
                const Expanded(
                  child: Text(
                    'CONDITION QUERY',
                    style: TextStyle(
                      color: BlueversePalette.coastBlue,
                      fontWeight: FontWeight.w800,
                      letterSpacing: 1.2,
                    ),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 12),
            Column(
              children: [
                _field(
                  controller: _latitudeController,
                  label: 'Latitude (−90 to 90)',
                  keyboardType: TextInputType.numberWithOptions(
                    decimal: true,
                    signed: true,
                  ),
                ),
                const SizedBox(height: 12),
                _field(
                  controller: _longitudeController,
                  label: 'Longitude (−180 to 180)',
                  keyboardType: TextInputType.numberWithOptions(
                    decimal: true,
                    signed: true,
                  ),
                ),
                const SizedBox(height: 12),
                _field(
                  controller: _timeController,
                  label: 'Requested time (UTC, optional)',
                  keyboardType: TextInputType.datetime,
                  suffixText: _timeController.text.isNotEmpty ? 'UTC' : null,
                  helperText: 'Leave empty for now.',
                ),
                const SizedBox(height: 12),
                DropdownButtonFormField<String>(
                  isExpanded: true,
                  initialValue: _activityId.isEmpty ? null : _activityId,
                  decoration: const InputDecoration(labelText: 'Activity'),
                  items: _activities
                      .map(
                        (activity) => DropdownMenuItem<String>(
                          value: activity['id'] as String,
                          child: Text(activity['name'] as String),
                        ),
                      )
                      .toList(),
                  onChanged: (value) {
                    if (value != null) setState(() => _activityId = value);
                  },
                ),
              ],
            ),
            const SizedBox(height: 16),
            OutlinedButton(
              onPressed: _loading
                  ? null
                  : () => _loadConditionsAndEvaluate(conditionsOnly: true),
              child: const Text('View conditions only'),
            ),
            const SizedBox(height: 8),
            FilledButton.icon(
              onPressed:
                  _loading ||
                      _catalogueLoading ||
                      _activities.isEmpty ||
                      _activityId.isEmpty
                  ? null
                  : _loadConditionsAndEvaluate,
              icon: const Icon(Icons.check_circle_outline),
              label: _loading
                  ? const Text('Checking the coast…')
                  : const Text('Check conditions & suitability'),
            ),
          ],
        ),
      ),
    );
  }

  Widget _evidenceCard() {
    final snapshot = _snapshot!;
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(14),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Expanded(
                  child: Text(
                    '${snapshot.latitude}, ${snapshot.longitude}',
                    style: const TextStyle(fontWeight: FontWeight.w700),
                  ),
                ),
                _freshnessChip(snapshot.freshnessStatus),
              ],
            ),
            const SizedBox(height: 8),
            MarineReadings(snapshot: snapshot),
            const SizedBox(height: 12),
            _detailRow('Forecast time', snapshot.forecastTime),
            _detailRow('Retrieved at', snapshot.retrievedAt),
            _detailRow('Source', snapshot.source),
            _detailRow('Freshness', snapshot.freshnessStatus),
            if (snapshot.missingFields.isNotEmpty) ...[
              const SizedBox(height: 8),
              _detailRow('Missing fields', snapshot.missingFields.join(', ')),
            ],
          ],
        ),
      ),
    );
  }

  Widget _resultCard() {
    final result = _result!;
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(14),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                _statusPill(result.status),
                const SizedBox(width: 8),
                Expanded(
                  child: Text(
                    result.activityName,
                    style: const TextStyle(fontWeight: FontWeight.w700),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 8),
            MarineOutcomeExplanation(status: result.status),
            Text(
              'Evaluated for ${result.location.latitude}, ${result.location.longitude} at ${_formatTimestamp(result.requestedTime)}.',
              style: const TextStyle(height: 1.4),
            ),
            if (result.violations.isNotEmpty) _violationList(result.violations),
            if (result.cautionFactors.isNotEmpty)
              _cautionList(result.cautionFactors),
            if (result.missingFields.isNotEmpty)
              _missingList(result.missingFields),
          ],
        ),
      ),
    );
  }

  Widget _emptyPrompt() => Card(
    child: Padding(
      padding: const EdgeInsets.all(16),
      child: const Text(
        'Enter a coastal location and check the sea before you plan. Missing or stale evidence is always labelled rather than silently treated as safe.',
        style: TextStyle(color: BlueversePalette.coastMuted),
      ),
    ),
  );

  Widget _detailRow(String label, dynamic value) {
    final text = value is DateTime
        ? _formatTimestamp(value)
        : (value?.toString() ?? '—');
    return Padding(
      padding: const EdgeInsets.only(bottom: 6),
      child: Row(
        children: [
          const SizedBox(width: 8),
          Text(
            '$label: ',
            style: const TextStyle(
              fontWeight: FontWeight.w600,
              color: BlueversePalette.coastMuted,
            ),
          ),
          Expanded(
            child: Text(
              text,
              style: const TextStyle(color: BlueversePalette.coastMuted),
            ),
          ),
        ],
      ),
    );
  }

  Widget _freshnessChip(String freshness) {
    return Chip(
      label: Text(freshness),
      backgroundColor: Colors.transparent,
      labelStyle: TextStyle(
        backgroundColor: Colors.transparent,
        color: BlueversePalette.coastMuted,
      ),
    );
  }

  Widget _statusPill(String status) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
      decoration: BoxDecoration(
        color: BlueversePalette.coastMuted.withValues(alpha: 0.12),
        borderRadius: BorderRadius.circular(16),
      ),
      child: Text(
        status,
        style: const TextStyle(
          fontWeight: FontWeight.w800,
          color: BlueversePalette.coastMuted,
        ),
      ),
    );
  }

  Widget _violationList(List<String> violations) =>
      _list('LIMITS EXCEEDED', violations, Colors.redAccent);

  Widget _cautionList(List<String> factors) =>
      _list('CAUTION FACTORS', factors, const Color(0xFF92621B));

  Widget _missingList(List<String> missing) =>
      _list('MISSING OR STALE EVIDENCE', missing, BlueversePalette.coastMuted);

  Widget _list(String title, List<String> items, Color color) => Padding(
    padding: const EdgeInsets.only(top: 6),
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          title,
          style: TextStyle(fontWeight: FontWeight.w700, color: color),
        ),
        const SizedBox(height: 4),
        ...items.map(
          (item) => Padding(
            padding: const EdgeInsets.only(bottom: 4),
            child: Text(item, style: TextStyle(color: color)),
          ),
        ),
      ],
    ),
  );

  Widget _field({
    required TextEditingController controller,
    required String label,
    TextInputType keyboardType = TextInputType.text,
    String? suffixText,
    String? helperText,
  }) {
    return TextField(
      controller: controller,
      keyboardType: keyboardType,
      decoration: InputDecoration(
        labelText: label,
        suffixText: suffixText,
        helperText: helperText,
        border: const OutlineInputBorder(),
        focusedBorder: const OutlineInputBorder(
          borderSide: BorderSide(color: BlueversePalette.coastBlue, width: 1.5),
        ),
      ),
    );
  }

  String _formatTimestamp(DateTime value) {
    try {
      return '${value.toUtc().toIso8601String()} (UTC)';
    } catch (_) {
      return value.toIso8601String();
    }
  }

  double? _parseCoordinate(String raw, double min, double max) {
    final value = double.tryParse(raw.trim());
    if (value == null || !value.isFinite || value < min || value > max) {
      return null;
    }
    return value;
  }

  DateTime? _parseUtcTimestamp(String raw) {
    try {
      return marineUtcTime(raw);
    } catch (_) {
      return null;
    }
  }
}

class _NoteCard extends StatelessWidget {
  const _NoteCard();

  @override
  Widget build(BuildContext context) => Card(
    child: Padding(
      padding: const EdgeInsets.all(14),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text(
            'Server assessment',
            style: TextStyle(
              fontWeight: FontWeight.w800,
              color: BlueversePalette.coastBlue,
            ),
          ),
          const SizedBox(height: 6),
          const Text(
            'The outcome is calculated on the backend from configured safety profiles and retrieved conditions. Flutter never reclassifies the result, even when fields are missing or stale.',
            style: TextStyle(height: 1.4, color: BlueversePalette.coastMuted),
          ),
        ],
      ),
    ),
  );
}
