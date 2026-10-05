import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../../data/models/marine_models.dart';
import '../../data/services/marine_api_client.dart';
import '../../data/services/marine_service.dart';

class MarineHistoryScreen extends StatefulWidget {
  const MarineHistoryScreen({super.key, required this.service});

  final MarineService service;

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
  bool _loading = true;
  String? _error;
  String? _formError;

  @override
  void dispose() {
    _latitudeController.dispose();
    _longitudeController.dispose();
    _fromController.dispose();
    _toController.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    if (!mounted) return;
    setState(() {
      _loading = true;
      _error = null;
      _formError = null;
      _snapshots = const [];
    });

    try {
      final from = _fromController.text.isNotEmpty
          ? DateTime.parse(_fromController.text)
          : null;
      final to = _toController.text.isNotEmpty
          ? DateTime.parse(_toController.text)
          : null;
      final latitude = _latitudeController.text.isNotEmpty
          ? double.tryParse(_latitudeController.text)
          : null;
      final longitude = _longitudeController.text.isNotEmpty
          ? double.tryParse(_longitudeController.text)
          : null;

      if (latitude != null && (latitude < -90 || latitude > 90)) {
        throw const FormatException('Latitude must be between -90 and 90.');
      }
      if (longitude != null && (longitude < -180 || longitude > 180)) {
        throw const FormatException('Longitude must be between -180 and 180.');
      }
      if (from != null && to != null && from.isAfter(to)) {
        throw const FormatException(
          'The from time must not be later than the to time.',
        );
      }

      final list = await widget.service.history(
        latitude: latitude,
        longitude: longitude,
        fromUtc: from,
        toUtc: to,
      );
      final assessments = await widget.service.assessments(
        activityId: _activityFilter,
        result: _resultFilter,
        fromUtc: from,
        toUtc: to,
      );
      if (mounted) {
        setState(() {
          _snapshots = list;
          _assessments = assessments;
          _loading = false;
        });
      }
    } on FormatException catch (error) {
      if (mounted)
        setState(() {
          _formError = error.message ?? 'Invalid filter.';
          _loading = false;
        });
    } on MarineApiException catch (error) {
      if (mounted)
        setState(() {
          _error = error.message;
          _loading = false;
        });
    } catch (error) {
      if (mounted)
        setState(() {
          _error = 'The marine service could not be reached. Check the gateway address and try again.';
          _loading = false;
        });
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Marine history')),
      body: _buildBody(),
    );
  }

  Widget _buildBody() {
    return RefreshIndicator(
      onRefresh: () => _load(),
      child: SingleChildScrollView(
        padding: const EdgeInsets.fromLTRB(16, 16, 16, 28),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            if (_error != null) _errorCard(),
            if (_formError != null) _formErrorCard(),
            _filterCard(),
            _listCard(),
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
              child: Text('Enter valid filters before applying.', maxLines: 3),
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
                const Icon(Icons.history, color: Colors.blueAccent),
                const SizedBox(width: 8),
                const Text(
                  'FILTER HISTORY',
                  style: TextStyle(
                    color: Colors.blueAccent,
                    fontWeight: FontWeight.w800,
                    letterSpacing: 1.2,
                  ),
                ),
              ],
            ),
            const SizedBox(height: 12),
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
              value: _activityFilter,
              decoration: const InputDecoration(
                labelText: 'Assessment activity',
              ),
              items: [
                const DropdownMenuItem(
                  value: '',
                  child: Text('All activities'),
                ),
                ...widget.service.referenceActivities.map(
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
              value: _resultFilter,
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
      return Card(
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: const Text(
            'No condition snapshots match these filters yet. Run a condition query to store the first one.',
            style: TextStyle(color: Colors.blueGrey),
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
                const Text(
                  'STORED SNAPSHOTS',
                  style: TextStyle(
                    color: Colors.blueAccent,
                    fontWeight: FontWeight.w800,
                    letterSpacing: 1.2,
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
      return const Card(
        child: Padding(
          padding: EdgeInsets.all(16),
          child: Text(
            'No suitability assessments match these filters yet.',
            style: TextStyle(color: Colors.blueGrey),
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
                color: Colors.blueAccent,
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
                style: TextStyle(color: Colors.blueGrey),
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
              Text(
                '${snapshot.latitude}, ${snapshot.longitude}',
                style: const TextStyle(fontWeight: FontWeight.w700),
              ),
              _freshnessChip(snapshot.freshnessStatus),
            ],
          ),
          const SizedBox(height: 6),
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

  Widget _detail(String label, dynamic value) {
    final text = value is DateTime
        ? value.toIso8601String()
        : (value?.toString() ?? '—');
    return Padding(
      padding: const EdgeInsets.only(bottom: 4),
      child: Text(
        '$label: $text',
        style: const TextStyle(color: Colors.blueGrey, fontSize: 13),
      ),
    );
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
          borderSide: BorderSide(color: Colors.blueAccent, width: 1.5),
        ),
      ),
      inputFormatters:
          keyboardType == TextInputType.numberWithOptions(decimal: true)
          ? <TextInputFormatter>[FilteringTextInputFormatter.digitsOnly]
          : null,
    );
  }
}
