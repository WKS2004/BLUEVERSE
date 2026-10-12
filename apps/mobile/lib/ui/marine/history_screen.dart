import 'package:flutter/material.dart';

import '../blueverse_theme.dart';

import '../../data/models/marine_models.dart';
import '../../data/services/marine_api_client.dart';
import '../../data/services/marine_service.dart';
import 'marine_presentation.dart';
import '../feedback/loading_screen_controller.dart';

class MarineHistoryScreen extends StatefulWidget {
  const MarineHistoryScreen({
    super.key,
    required this.service,
    this.embedded = false,
  });

  final MarineService service;
  final bool embedded;

  @override
  State<MarineHistoryScreen> createState() => _MarineHistoryScreenState();
}

class _MarineHistoryScreenState extends State<MarineHistoryScreen> {
  final _latitudeController = TextEditingController();
  final _longitudeController = TextEditingController();
  final _fromController = TextEditingController();
  final _toController = TextEditingController();
  List<ConditionSnapshotDto> _snapshots = const [];
  List<MarineAssessmentHistoryDto> _assessments = const [];
  String _activityFilter = '';
  String _resultFilter = '';
  List<Map<String, dynamic>> _activities = const [];
  bool _loading = true;
  String? _error;
  String? _formError;
  String? _snapshotError;
  String? _assessmentError;

  @override
  void initState() {
    super.initState();
    _loadActivities();
    _load(initial: true);
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
          _activityFilter = '';
          _catalogueLoading = false;
        });
      }
    } catch (error) {
      if (mounted) {
        setState(() {
          _activities = const [];
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
    _fromController.dispose();
    _toController.dispose();
    super.dispose();
  }

  Future<void> _load({bool initial = false}) async {
    if (!mounted) return;
    if (_loading && !initial) return;
    setState(() {
      _loading = true;
      _error = null;
      _formError = null;
    });

    try {
      final from = marineUtcTime(_fromController.text);
      final to = marineUtcTime(_toController.text);
      final latitude = _optionalCoordinate(
        _latitudeController.text,
        -90,
        90,
        'Latitude',
      );
      final longitude = _optionalCoordinate(
        _longitudeController.text,
        -180,
        180,
        'Longitude',
      );

      if (from != null && to != null && from.isAfter(to)) {
        throw const FormatException(
          'The from time must not be later than the to time.',
        );
      }

      Future<(List<T>?, String?)> read<T>(
        Future<List<T>> Function() action,
      ) async {
        try {
          return (await action(), null);
        } catch (error) {
          return (
            null,
            error is MarineApiException
                ? error.message
                : 'Saved data could not be read. Please retry.',
          );
        }
      }

      final finish = initial ? null : blueverseLoadingScreenController.begin();
      try {
        final snapshots = read(
          () => widget.service.history(
            latitude: latitude,
            longitude: longitude,
            fromUtc: from,
            toUtc: to,
          ),
        );
        final assessments = read(
          () => widget.service.assessments(
            activityId: _activityFilter,
            result: _resultFilter,
            fromUtc: from,
            toUtc: to,
          ),
        );
        final (snapshotData, snapshotError) = await snapshots;
        final (assessmentData, assessmentError) = await assessments;
        if (mounted) {
          setState(() {
            if (snapshotData != null) _snapshots = snapshotData;
            if (assessmentData != null) _assessments = assessmentData;
            _snapshotError = snapshotError;
            _assessmentError = assessmentError;
            _loading = false;
          });
        }
      } finally {
        finish?.call();
      }
    } on FormatException catch (error) {
      if (mounted) {
        setState(() {
          _formError = error.message;
          _loading = false;
        });
      }
    } on MarineApiException catch (error) {
      if (mounted) {
        setState(() {
          _error = error.message;
          _loading = false;
        });
      }
    } catch (error) {
      if (mounted) {
        setState(() {
          _error = 'The marine service could not be reached. Check the gateway address and try again.';
          _loading = false;
        });
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    if (widget.embedded) return _buildBody();
    return Scaffold(
      appBar: AppBar(title: const Text('Marine history')),
      body: _buildBody(),
    );
  }

  Widget _buildBody() {
    return RefreshIndicator(
      onRefresh: () => _load(),
      child: SingleChildScrollView(
        physics: const AlwaysScrollableScrollPhysics(),
        padding: const EdgeInsets.fromLTRB(16, 16, 16, 28),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            if (widget.embedded) const MarineHero(index: 1),
            if (_loading)
              Semantics(
                liveRegion: true,
                child: Text('Loading saved coastal evidence…'),
              ),
            _catalogueStatus(),
            if (_error != null) _errorCard(),
            if (_formError != null) _formErrorCard(),
            _filterCard(),
            if (_snapshotError != null)
              _dataError(
                'Saved readings',
                _snapshotError!,
                _snapshots.isNotEmpty,
              ),
            _listCard(),
            if (_assessmentError != null)
              _dataError(
                'Saved assessments',
                _assessmentError!,
                _assessments.isNotEmpty,
              ),
            _assessmentListCard(),
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

  Widget _dataError(String label, String error, bool retained) => Card(
    child: Padding(
      padding: const EdgeInsets.all(16),
      child: Semantics(
        liveRegion: true,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              '$label unavailable',
              style: const TextStyle(fontWeight: FontWeight.w800),
            ),
            Text(error),
            if (retained)
              const Text(
                'Showing last-loaded data; it may not match the current filters.',
              ),
            TextButton(
              onPressed: _loading ? null : () => _load(),
              child: Text('Retry $label'),
            ),
          ],
        ),
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
              padding: const EdgeInsets.symmetric(horizontal: 4),
              child: Text(_formError!, maxLines: 3),
            ),
          ),
        ],
      ),
    ),
  );

  Widget _filterCard() {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                const Icon(Icons.history, color: BlueversePalette.coastBlue),
                const SizedBox(width: 8),
                const Expanded(
                  child: Text(
                    'FILTER HISTORY',
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
              controller: _fromController,
              label: 'From (UTC)',
              keyboardType: TextInputType.datetime,
            ),
            const SizedBox(height: 12),
            _field(
              controller: _toController,
              label: 'To (UTC)',
              keyboardType: TextInputType.datetime,
            ),
            const SizedBox(height: 12),
            DropdownButtonFormField<String>(
              isExpanded: true,
              initialValue: _activityFilter,
              decoration: const InputDecoration(
                labelText: 'Assessment activity',
              ),
              items: [
                const DropdownMenuItem(
                  value: '',
                  child: Text('All activities'),
                ),
                ..._activities.map(
                  (activity) => DropdownMenuItem<String>(
                    value: activity['id'] as String,
                    child: Text(activity['name'] as String),
                  ),
                ),
              ],
              onChanged: _loading
                  ? null
                  : (value) => setState(() => _activityFilter = value ?? ''),
            ),
            const SizedBox(height: 12),
            DropdownButtonFormField<String>(
              initialValue: _resultFilter,
              isExpanded: true,
              decoration: const InputDecoration(labelText: 'Assessment result'),
              items: const [
                DropdownMenuItem(value: '', child: Text('All results')),
                DropdownMenuItem(value: 'SUITABLE', child: Text('SUITABLE')),
                DropdownMenuItem(value: 'CAUTION', child: Text('CAUTION')),
                DropdownMenuItem(
                  value: 'UNSUITABLE',
                  child: Text('UNSUITABLE'),
                ),
                DropdownMenuItem(value: 'UNKNOWN', child: Text('UNKNOWN')),
              ],
              onChanged: _loading
                  ? null
                  : (value) => setState(() => _resultFilter = value ?? ''),
            ),
            const SizedBox(height: 16),
            FilledButton.icon(
              onPressed: _loading ? null : _load,
              icon: const Icon(Icons.search),
              label: _loading
                  ? const Text('Fetching…')
                  : const Text('Apply filters'),
            ),
          ],
        ),
      ),
    );
  }

  Widget _listCard() {
    if (_loading) return const SizedBox.shrink();
    if (_snapshots.isEmpty) {
      if (_snapshotError != null) return const SizedBox.shrink();
      return Card(
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: const Text(
            'No condition snapshots match these filters. Run a condition query to store the first one.',
            style: TextStyle(color: BlueversePalette.coastMuted),
          ),
        ),
      );
    }

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          children: [
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                const Expanded(
                  child: Text(
                    'STORED SNAPSHOTS',
                    style: TextStyle(
                      color: BlueversePalette.coastBlue,
                      fontWeight: FontWeight.w800,
                      letterSpacing: 1.2,
                    ),
                  ),
                ),
                Text(
                  '${_snapshots.length} snapshots',
                  style: const TextStyle(fontWeight: FontWeight.w700),
                ),
              ],
            ),
            const SizedBox(height: 8),
            ..._snapshots.map((snapshot) => _snapshotTile(snapshot)),
          ],
        ),
      ),
    );
  }

  Widget _assessmentListCard() {
    if (_loading) return const SizedBox.shrink();
    if (_assessments.isEmpty) {
      if (_assessmentError != null) return const SizedBox.shrink();
      return const Card(
        child: Padding(
          padding: EdgeInsets.all(16),
          child: Text(
            'No suitability assessments match these filters.',
            style: TextStyle(color: BlueversePalette.coastMuted),
          ),
        ),
      );
    }
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            const Text(
              'SUITABILITY ASSESSMENTS',
              style: TextStyle(
                color: BlueversePalette.coastBlue,
                fontWeight: FontWeight.w800,
                letterSpacing: 1.2,
              ),
            ),
            Text(
              '${_assessments.length} assessments',
              style: const TextStyle(fontWeight: FontWeight.w700),
            ),
            const SizedBox(height: 8),
            ..._assessments.map(_assessmentTile),
          ],
        ),
      ),
    );
  }

  Widget _assessmentTile(MarineAssessmentHistoryDto assessment) => Card(
    margin: const EdgeInsets.only(bottom: 10),
    child: Padding(
      padding: const EdgeInsets.all(12),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            '${assessment.activityName ?? 'Activity name not recorded'} · ${assessment.result}',
            style: const TextStyle(fontWeight: FontWeight.w800),
          ),
          const SizedBox(height: 4),
          MarineOutcomeExplanation(status: assessment.result),
          _detail('Assessment', assessment.id),
          _detail('Profile version', assessment.profileVersion),
          _detail('Condition snapshot', assessment.conditionSnapshotId),
          _detail(
            'Requested / forecast',
            '${assessment.requestedTime.toIso8601String()} / ${assessment.forecastTime?.toIso8601String() ?? 'not recorded'}',
          ),
          _detail(
            'Location',
            '${assessment.latitude}, ${assessment.longitude}',
          ),
          _detail(
            'Conditions',
            'Wind ${assessment.conditions.windSpeed ?? '—'} km/h · Wave ${assessment.conditions.waveHeight ?? '—'} m · Swell ${assessment.conditions.swellHeight ?? '—'} m · Rain ${assessment.conditions.rain ?? '—'} mm',
          ),
          _detail(
            'Source / freshness',
            '${assessment.source} / ${assessment.freshnessStatus}',
          ),
          if (assessment.evidenceCompleteness != 'COMPLETE')
            const Padding(
              padding: EdgeInsets.symmetric(vertical: 4),
              child: Text(
                'Some evidence predates evidence snapshots and cannot be reconstructed reliably.',
                style: TextStyle(color: BlueversePalette.coastMuted),
              ),
            ),
          if (assessment.violations.isNotEmpty)
            _detail('Exceeded limits', assessment.violations.join('; ')),
          if (assessment.cautionFactors.isNotEmpty)
            _detail('Caution factors', assessment.cautionFactors.join(', ')),
          if (assessment.missingFields.isNotEmpty)
            _detail('Missing fields', assessment.missingFields.join(', ')),
          ...assessment.criteria.map(
            (criterion) => _detail(
              '${criterion.factor} criteria',
              '${criterion.maximum?.toString() ?? 'not recorded'} · ${criterion.source ?? 'source not recorded'} · ${criterion.rationale ?? 'rationale not recorded'}',
            ),
          ),
        ],
      ),
    ),
  );

  Widget _snapshotTile(ConditionSnapshotDto snapshot) => Card(
    margin: const EdgeInsets.only(bottom: 10),
    child: Padding(
      padding: const EdgeInsets.all(12),
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
          const SizedBox(height: 6),
          MarineReadings(snapshot: snapshot),
          const SizedBox(height: 8),
          _detail('Forecast time', snapshot.forecastTime),
          _detail('Retrieved at', snapshot.retrievedAt),
          _detail('Source', snapshot.source),
          if (snapshot.missingFields.isNotEmpty) ...[
            const SizedBox(height: 6),
            _detail('Missing fields', snapshot.missingFields.join(', ')),
          ],
        ],
      ),
    ),
  );

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

  Widget _detail(String label, dynamic value) {
    final text = value is DateTime
        ? value.toIso8601String()
        : (value?.toString() ?? '—');
    return Padding(
      padding: const EdgeInsets.only(bottom: 4),
      child: Text(
        '$label: $text',
        style: const TextStyle(
          color: BlueversePalette.coastMuted,
          fontSize: 13,
        ),
      ),
    );
  }

  double? _optionalCoordinate(
    String raw,
    double min,
    double max,
    String label,
  ) {
    if (raw.trim().isEmpty) return null;
    final value = double.tryParse(raw.trim());
    if (value == null || !value.isFinite || value < min || value > max) {
      throw FormatException(
        '$label must be a decimal value between $min and $max.',
      );
    }
    return value;
  }

  Widget _field({
    required TextEditingController controller,
    required String label,
    TextInputType keyboardType = TextInputType.text,
  }) {
    return TextField(
      controller: controller,
      keyboardType: keyboardType,
      decoration: InputDecoration(
        labelText: label,
        border: const OutlineInputBorder(),
        focusedBorder: const OutlineInputBorder(
          borderSide: BorderSide(color: BlueversePalette.coastBlue, width: 1.5),
        ),
      ),
    );
  }
}
