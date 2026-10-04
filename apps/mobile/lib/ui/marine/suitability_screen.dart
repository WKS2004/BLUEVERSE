import 'dart:async';

import 'package:flutter/material.dart';

import 'package:flutter/services.dart';

import '../../data/models/marine_models.dart';
import '../../data/repositories/marine_repository.dart';
import '../../data/services/marine_api_client.dart';
import '../../data/services/marine_service.dart';
import 'marine_route_table.dart';

class SuitabilityScreen extends StatefulWidget {
  const SuitabilityScreen({super.key, required this.service});
  final MarineService service;

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
  String _activityId = MarineRepository.referenceActivities.first['id'] as String;
  List<Map<String, dynamic>> _activities = const [];
  ConditionSnapshotDto? _snapshot;
  SuitabilityResultDto? _result;
  bool _loading = false;
  String? _error;
  String? _formError;

  @override
  void initState() {
    super.initState();
    _activities = widget.service.referenceActivities;
  }

  @override
  void dispose() {
    _latitudeController.dispose();
    _longitudeController.dispose();
    _timeController.dispose();
    super.dispose();
  }

  Future<void> _loadConditionsAndEvaluate() async {
    if (!mounted) return;
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
    final requestedTime = _timeController.text.isNotEmpty
        ? _parseUtcTimestamp(_timeController.text)
        : null;
    if (requestedTime == null && _timeController.text.isNotEmpty) {
      setState(() {
        _formError =
            'Enter the requested time as a valid UTC moment, e.g. 2026-09-26T10:00.';
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
    try {
      final conditions = await widget.service.currentConditions(
        latitude: latitude,
        longitude: longitude,
        timeUtc: requestedTime,
      );
      final result = await widget.service.evaluateSuitability(
        activityId: _activityId,
        latitude: latitude,
        longitude: longitude,
        timeUtc: requestedTime,
      );
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
          _error =
              'The marine service could not be reached. Check the gateway address and try again.';
          _loading = false;
          _snapshot = null;
          _result = null;
        });
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Marine conditions')),
      body: _buildBody(),
    );
  }

  Widget _buildBody() {
    return RefreshIndicator(
      onRefresh: () => _loadConditionsAndEvaluate(),
      child: SingleChildScrollView(
        padding: const EdgeInsets.fromLTRB(16, 16, 16, 28),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
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
              const Icon(Icons.error_outline, color: Colors.amberAccent),
              const SizedBox(width: 12),
              const Flexible(
                child: Padding(
                  padding: EdgeInsets.symmetric(horizontal: 4),
                  child: Text(
                    'Enter valid values before checking the coast.',
                    maxLines: 3,
                  ),
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
                const Icon(Icons.waves_outlined, color: Colors.blueAccent),
                const SizedBox(width: 8),
                const Text(
                  'CONDITION QUERY',
                  style: TextStyle(
                    color: Colors.blueAccent,
                    fontWeight: FontWeight.w800,
                    letterSpacing: 1.2,
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
                  keyboardType: TextInputType.numberWithOptions(decimal: true),
                ),
                const SizedBox(height: 12),
                _field(
                  controller: _longitudeController,
                  label: 'Longitude (−180 to 180)',
                  keyboardType: TextInputType.numberWithOptions(decimal: true),
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
                  value: _activityId,
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
            FilledButton.icon(
              onPressed: _loading ? null : _loadConditionsAndEvaluate,
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
                Text(
                  '${snapshot.latitude}, ${snapshot.longitude}',
                  style: const TextStyle(fontWeight: FontWeight.w700),
                ),
                _freshnessChip(snapshot.freshnessStatus),
              ],
            ),
            const SizedBox(height: 8),
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
                Text(
                  result.activityName,
                  style: const TextStyle(fontWeight: FontWeight.w700),
                ),
              ],
            ),
            const SizedBox(height: 8),
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
            style: TextStyle(color: Colors.blueGrey),
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
              color: Colors.blueGrey,
            ),
          ),
          Expanded(
            child: Text(
              text,
              style: const TextStyle(color: Colors.blueGrey),
            ),
          ),
        ],
      ),
    );
  }

  Widget _freshnessChip(String freshness) {
    final tone = freshness == 'FRESH'
        ? 'bg-green-100 text-green-800'
        : freshness == 'STALE'
            ? 'bg-amber-100 text-amber-800'
            : 'bg-red-100 text-red-800';
    return Chip(
      label: Text(freshness),
      backgroundColor: Colors.transparent,
      labelStyle: TextStyle(
        backgroundColor: Colors.transparent,
        color: Colors.blueGrey,
      ),
    );
  }

  Widget _statusPill(String status) {
    final tone = status == 'SUITABLE'
        ? 'bg-green-100 text-green-800'
        : status == 'CAUTION'
            ? 'bg-amber-100 text-amber-800'
            : status == 'UNSUITABLE'
                ? 'bg-red-100 text-red-800'
                : 'bg-slate-100 text-slate-700';
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
      decoration: BoxDecoration(
        color: Colors.blueGrey.withValues(alpha: 0.12),
        borderRadius: BorderRadius.circular(16),
      ),
      child: Text(
        status,
        style: const TextStyle(
          fontWeight: FontWeight.w800,
          color: Colors.blueGrey,
        ),
      ),
    );
  }

  Widget _violationList(List<String> violations) =>
      _list('LIMITS EXCEEDED', violations, Colors.redAccent);

  Widget _cautionList(List<String> factors) =>
      _list('CAUTION FACTORS', factors, Colors.amberAccent);

  Widget _missingList(List<String> missing) =>
      _list('MISSING OR STALE EVIDENCE', missing, Colors.blueGrey);

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
                child: Text(
                  item,
                  style: TextStyle(color: color),
                ),
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
          borderSide: BorderSide(color: Colors.blueAccent, width: 1.5),
        ),
      ),
      inputFormatters: keyboardType == TextInputType.numberWithOptions(decimal: true)
          ? <TextInputFormatter>[
              FilteringTextInputFormatter.digitsOnly
            ]
          : null,
    );
  }

  String _formatTimestamp(DateTime value) {
    try {
      return value.toLocal().toString();
    } catch (_) {
      return value.toIso8601String();
    }
  }

  double? _parseCoordinate(String raw, double min, double max) {
    final trimmed = raw.trim();
    if (trimmed.isEmpty) return null;
    final parts = trimmed.split('.');
    if (parts.length > 2) return null;
    final integer = int.tryParse(parts[0]);
    if (integer == null) return null;
    final fractional =
        parts.length == 2 ? double.parse('0.' + parts[1]) : 0.0;
    final value = integer.toDouble() + fractional;
    if (value < min || value > max) return null;
    return value;
  }

  DateTime? _parseUtcTimestamp(String raw) {
    try {
      return DateTime.parse(raw);
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
                  color: Colors.blueAccent,
                ),
              ),
              const SizedBox(height: 6),
              const Text(
                'The outcome is calculated on the backend from configured safety profiles and retrieved conditions. Flutter never reclassifies the result, even when fields are missing or stale.',
                style: TextStyle(height: 1.4, color: Colors.blueGrey),
              ),
            ],
          ),
        ),
      );
}
