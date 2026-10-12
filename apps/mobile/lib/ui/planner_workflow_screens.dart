import 'package:flutter/material.dart';

import '../data/models/auth_models.dart';
import '../data/models/planner_models.dart';
import '../data/services/planner_api_service.dart';
import 'planner_draft_picker.dart';

/// Native wall-clock input. The owning service resolves the displayed clock
/// in the supplied IANA zone and rejects invalid or ambiguous local times.
class PlannerClockInput extends StatelessWidget {
  const PlannerClockInput({
    super.key,
    required this.label,
    required this.value,
    required this.onChanged,
  });
  final String label;
  final String value;
  final ValueChanged<String> onChanged;
  @override
  Widget build(BuildContext context) => OutlinedButton(
    onPressed: () async {
      final initial = DateTime.parse(value.replaceFirst(RegExp(r'Z$'), ''));
      final date = await showDatePicker(
        context: context,
        initialDate: initial,
        firstDate: DateTime(initial.year - 1),
        lastDate: DateTime(initial.year + 5),
      );
      if (date == null || !context.mounted) return;
      final time = await showTimePicker(
        context: context,
        initialTime: TimeOfDay.fromDateTime(initial),
      );
      if (time == null) return;
      onChanged(
        DateTime(
          date.year,
          date.month,
          date.day,
          time.hour,
          time.minute,
        ).toIso8601String(),
      );
    },
    child: Text('$label: ${value.replaceFirst('T', ' ')}'),
  );
}

class PlannerDraftEditor extends StatefulWidget {
  const PlannerDraftEditor({super.key, required this.trip, required this.api});
  final PlannerItinerary trip;
  final PlannerApiService api;
  @override
  State<PlannerDraftEditor> createState() => _PlannerDraftEditorState();
}

class _PlannerDraftEditorState extends State<PlannerDraftEditor> {
  late final TextEditingController _title, _notes;
  late String _start, _end;
  late final List<Map<String, dynamic>> _items;
  bool _busy = false;
  String? _error;
  @override
  void initState() {
    super.initState();
    _title = TextEditingController(text: widget.trip.title);
    _notes = TextEditingController(text: widget.trip.description);
    _start = widget.trip.startsAtLocal;
    _end = widget.trip.endsAtLocal;
    _items = [
      for (var i = 0; i < widget.trip.items.length; i++)
        {
          ...PlannerApiService.itemRequest(widget.trip.items[i], i),
          'scheduledStart': widget.trip.items[i].startsAtLocal,
          'scheduledEnd': widget.trip.items[i].endsAtLocal,
        },
    ];
  }

  @override
  void dispose() {
    _title.dispose();
    _notes.dispose();
    super.dispose();
  }

  Future<void> _save(bool confirm) async {
    if (_busy) return;
    if (_title.text.trim().isEmpty || _items.isEmpty) {
      setState(
        () => _error = 'Name your trip and keep at least one experience.',
      );
      return;
    }
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      final trip = await widget.api.updateItinerary(
        widget.trip,
        title: _title.text.trim(),
        description: _notes.text.trim(),
        startsAt: _start,
        endsAt: _end,
        confirmPlan: confirm,
        items: [
          for (var i = 0; i < _items.length; i++)
            {..._items[i], 'orderIndex': i},
        ],
      );
      if (mounted) Navigator.pop(context, trip);
    } on Object catch (error) {
      if (mounted) {
        setState(() {
          _busy = false;
          _error = error.toString();
        });
      }
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('Shape your coastal day')),
    body: ListView(
      padding: const EdgeInsets.all(20),
      children: [
        TextField(
          controller: _title,
          enabled: !_busy,
          decoration: const InputDecoration(labelText: 'Trip name'),
        ),
        TextField(
          controller: _notes,
          enabled: !_busy,
          maxLines: 3,
          decoration: const InputDecoration(labelText: 'Notes'),
        ),
        Text('Trip times · ${widget.trip.timeZone}'),
        PlannerClockInput(
          label: 'Start',
          value: _start,
          onChanged: (v) {
            if (!_busy) setState(() => _start = v);
          },
        ),
        PlannerClockInput(
          label: 'End',
          value: _end,
          onChanged: (v) {
            if (!_busy) setState(() => _end = v);
          },
        ),
        const Text('Experiences · use the arrows to change their order'),
        for (var i = 0; i < _items.length; i++)
          Card(
            key: ValueKey('planner-editor-item-$i'),
            child: Padding(
              padding: const EdgeInsets.all(12),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(_items[i]['title'] as String),
                  Text('Times · ${_items[i]['timeZone']}'),
                  PlannerClockInput(
                    label: 'Experience start',
                    value: _items[i]['scheduledStart'] as String,
                    onChanged: (v) {
                      if (!_busy) {
                        setState(() => _items[i]['scheduledStart'] = v);
                      }
                    },
                  ),
                  PlannerClockInput(
                    label: 'Experience end',
                    value: _items[i]['scheduledEnd'] as String,
                    onChanged: (v) {
                      if (!_busy) setState(() => _items[i]['scheduledEnd'] = v);
                    },
                  ),
                  Wrap(
                    children: [
                      IconButton(
                        tooltip: 'Move earlier',
                        onPressed: _busy || i == 0
                            ? null
                            : () => setState(() {
                                final item = _items.removeAt(i);
                                _items.insert(i - 1, item);
                              }),
                        icon: const Icon(Icons.arrow_upward),
                      ),
                      IconButton(
                        tooltip: 'Move later',
                        onPressed: _busy || i == _items.length - 1
                            ? null
                            : () => setState(() {
                                final item = _items.removeAt(i);
                                _items.insert(i + 1, item);
                              }),
                        icon: const Icon(Icons.arrow_downward),
                      ),
                      TextButton(
                        onPressed: _busy
                            ? null
                            : () => setState(() => _items.removeAt(i)),
                        child: const Text('Remove experience'),
                      ),
                    ],
                  ),
                ],
              ),
            ),
          ),
        if (_error != null) Text(_error!),
        FilledButton(
          onPressed: _busy ? null : () => _save(false),
          child: const Text('Save draft changes'),
        ),
        OutlinedButton(
          onPressed: _busy
              ? null
              : () async {
                  final approved = await showDialog<bool>(
                    context: context,
                    builder: (context) => AlertDialog(
                      title: const Text('Save and confirm?'),
                      content: const Text(
                        'Confirmed trips cannot be edited or cancelled.',
                      ),
                      actions: [
                        TextButton(
                          onPressed: () => Navigator.pop(context, false),
                          child: const Text('Keep editing'),
                        ),
                        FilledButton(
                          onPressed: () => Navigator.pop(context, true),
                          child: const Text('Confirm trip'),
                        ),
                      ],
                    ),
                  );
                  if (approved == true) await _save(true);
                },
          child: const Text('Save and confirm trip'),
        ),
        if (_busy) const LinearProgressIndicator(),
      ],
    ),
  );
}

class PlannerRecommendationScreen extends StatefulWidget {
  const PlannerRecommendationScreen({
    super.key,
    required this.api,
    required this.user,
    required this.recommendationId,
  });
  final PlannerApiService api;
  final AuthUser? user;
  final String recommendationId;
  @override
  State<PlannerRecommendationScreen> createState() =>
      _PlannerRecommendationScreenState();
}

class _PlannerRecommendationScreenState
    extends State<PlannerRecommendationScreen> {
  Future<PlannerRecommendationResult>? _result;
  Future<PlannerWorkflowStatus>? _workflow;
  bool _busy = false;
  String? _error;
  bool _has(String permission) =>
      widget.user?.permissions.contains(permission) == true;
  @override
  void initState() {
    super.initState();
    _load();
  }

  void _load() {
    if (_has('planner.recommendations.read')) {
      _result = widget.api.recommendation(widget.recommendationId);
      _workflow = null;
    }
  }

  Future<void> _save(
    PlannerRecommendationResult result,
    PlannerCandidate candidate,
    bool append,
  ) async {
    if (_busy) return;
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      PlannerItinerary trip;
      if (append) {
        final selected = await Navigator.push<String>(
          context,
          MaterialPageRoute(
            builder: (_) => PlannerDraftPicker(api: widget.api),
          ),
        );
        if (selected == null) return;
        // Reload to use the latest concurrency version; the API remains the
        // authority for duplicate, eligibility and schedule validation.
        trip = await widget.api.addCandidate(
          await widget.api.itinerary(selected),
          candidate,
          result.recommendationId,
        );
      } else {
        trip = await widget.api.saveDraft(
          title: candidate.title,
          candidate: candidate,
          recommendationId: result.recommendationId,
        );
      }
      if (mounted) {
        await Navigator.pushNamed(
          context,
          '/planner/itinerary',
          arguments: trip.id,
        );
      }
    } on Object catch (error) {
      if (mounted) setState(() => _error = error.toString());
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('Your coastal ideas')),
    body: !_has('planner.recommendations.read')
        ? const Center(
            child: Text('Your account cannot view these trip ideas.'),
          )
        : FutureBuilder<PlannerRecommendationResult>(
            future: _result,
            builder: (context, snapshot) {
              if (snapshot.hasError) {
                return Center(
                  child: Column(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      const Text('These trip ideas could not be loaded.'),
                      TextButton(
                        onPressed: () => setState(_load),
                        child: const Text('Try again'),
                      ),
                    ],
                  ),
                );
              }
              if (!snapshot.hasData) {
                return const Center(child: CircularProgressIndicator());
              }
              final result = snapshot.data!;
              if (_has('planner.workflows.read')) {
                _workflow ??= widget.api.workflow(result.workflowId);
              }
              return ListView(
                padding: const EdgeInsets.all(20),
                children: [
                  Text('Prepared ${result.generatedAt}'),
                  if (_workflow != null)
                    FutureBuilder<PlannerWorkflowStatus>(
                      future: _workflow,
                      builder: (context, s) => Text(
                        s.hasError
                            ? 'Planning progress is unavailable.'
                            : s.hasData
                            ? 'Planning ${s.data!.status}${s.data!.failureReason == null ? '' : ': ${s.data!.failureReason}'}'
                            : 'Checking planning progress…',
                      ),
                    ),
                  if (result.candidates.isEmpty)
                    Text(
                      result.outcome == 'DEPENDENCIES_UNAVAILABLE'
                          ? 'Current experience information is unavailable. Please try again later.'
                          : 'No eligible experiences matched this request. Adjust your dates or activities.',
                    ),
                  for (final note in result.uncertaintyNotes) Text(note),
                  for (final candidate in result.candidates)
                    Card(
                      child: Padding(
                        padding: const EdgeInsets.all(16),
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(
                              candidate.title,
                              style: Theme.of(context).textTheme.titleLarge,
                            ),
                            Text(
                              candidate.scheduledStartLocal != null &&
                                      candidate.scheduledEndLocal != null
                                  ? '${candidate.scheduledStartLocal} – ${candidate.scheduledEndLocal} (${candidate.timeZone})'
                                  : '${candidate.scheduledStart} – ${candidate.scheduledEnd} (UTC)',
                            ),
                            Text(
                              'Availability ${candidate.availabilityStatus} · Marine ${candidate.suitability.status} · Operations ${candidate.operationalStatus}',
                            ),
                            for (final reason in candidate.reasons)
                              Text(reason),
                            if (candidate.suitability.marineConditionTime !=
                                null)
                              Text(
                                'Conditions checked ${candidate.suitability.marineConditionTime}',
                              ),
                            if (candidate.biodiversityContext != null)
                              Text(
                                '${candidate.biodiversityContext!.speciesName} · ${candidate.biodiversityContext!.uncertainty}\n'
                                'Prediction issued ${candidate.biodiversityContext!.predictionTimestamp}. '
                                'Wildlife context does not guarantee a sighting or establish marine safety.',
                              ),
                            if (_has('planner.biodiversity.read'))
                              PlannerWildlifeContextCard(
                                key: ValueKey(
                                  '${candidate.destinationId}:${candidate.activityId}:${candidate.offeringId}',
                                ),
                                api: widget.api,
                                candidate: candidate,
                              ),
                            if (_has('planner.itineraries.manage'))
                              Wrap(
                                spacing: 8,
                                children: [
                                  FilledButton(
                                    onPressed: _busy
                                        ? null
                                        : () => _save(result, candidate, false),
                                    child: const Text('Save draft'),
                                  ),
                                  OutlinedButton(
                                    onPressed: _busy
                                        ? null
                                        : () => _save(result, candidate, true),
                                    child: const Text('Add to a draft'),
                                  ),
                                ],
                              ),
                          ],
                        ),
                      ),
                    ),
                  if (_error != null) Text(_error!),
                  if (_busy) const LinearProgressIndicator(),
                ],
              );
            },
          ),
  );
}

class PlannerWildlifeContextCard extends StatefulWidget {
  const PlannerWildlifeContextCard({
    super.key,
    required this.api,
    required this.candidate,
  });
  final PlannerApiService api;
  final PlannerCandidate candidate;
  @override
  State<PlannerWildlifeContextCard> createState() =>
      _PlannerWildlifeContextCardState();
}

class _PlannerWildlifeContextCardState
    extends State<PlannerWildlifeContextCard> {
  Future<PlannerBiodiversityPrediction>? _future;
  @override
  Widget build(BuildContext context) => Column(
    crossAxisAlignment: CrossAxisAlignment.start,
    children: [
      OutlinedButton(
        onPressed: () => setState(() {
          _future = widget.api.biodiversityPredictions(
            destinationId: widget.candidate.destinationId,
            activityId: widget.candidate.activityId,
          );
        }),
        child: const Text('Check current wildlife context'),
      ),
      if (_future != null)
        FutureBuilder<PlannerBiodiversityPrediction>(
          future: _future,
          builder: (context, snapshot) {
            if (snapshot.hasError) {
              return const Text(
                'Wildlife context is unavailable. You can try again.',
              );
            }
            if (!snapshot.hasData) {
              return const LinearProgressIndicator();
            }
            final prediction = snapshot.data!;
            return Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(prediction.message),
                if (prediction.status == 'available') ...[
                  for (final species in prediction.predictedSpecies)
                    Text('${species.commonName} (${species.scientificName})'),
                  if (prediction.modelMetadata != null)
                    Text(
                      'Prediction issued ${prediction.modelMetadata!.inferenceTimestamp} · Model ${prediction.modelMetadata!.modelVersion}',
                    ),
                  if (prediction.uncertaintyText != null)
                    Text(prediction.uncertaintyText!),
                  Text(prediction.limitations),
                ],
              ],
            );
          },
        ),
    ],
  );
}

class PlannerReviewHistory extends StatefulWidget {
  const PlannerReviewHistory({
    super.key,
    required this.trip,
    required this.api,
  });
  final PlannerItinerary trip;
  final PlannerApiService api;
  @override
  State<PlannerReviewHistory> createState() => _PlannerReviewHistoryState();
}

class _PlannerReviewHistoryState extends State<PlannerReviewHistory> {
  late Future<List<Map<String, dynamic>>> _history;
  @override
  void initState() {
    super.initState();
    _history = widget.api.evaluationHistory(widget.trip.id);
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('Condition review history')),
    body: FutureBuilder<List<Map<String, dynamic>>>(
      future: _history,
      builder: (context, snapshot) {
        if (snapshot.hasError) {
          return Center(
            child: TextButton(
              onPressed: () => setState(() {
                _history = widget.api.evaluationHistory(widget.trip.id);
              }),
              child: const Text('History unavailable · Try again'),
            ),
          );
        }
        if (!snapshot.hasData) {
          return const Center(child: CircularProgressIndicator());
        }
        return ListView(
          padding: const EdgeInsets.all(20),
          children: [
            if (snapshot.data!.isEmpty) const Text('No condition reviews yet.'),
            for (final review in snapshot.data!)
              PlannerReviewCard(trip: widget.trip, review: review),
          ],
        );
      },
    ),
  );
}

class PlannerReviewCard extends StatelessWidget {
  const PlannerReviewCard({
    super.key,
    required this.trip,
    required this.review,
  });
  final PlannerItinerary trip;
  final Map<String, dynamic> review;
  @override
  Widget build(BuildContext context) => Card(
    child: Padding(
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text('${review['evaluatedAt'] ?? ''}'),
          Text('${review['summary'] ?? 'Condition review'}'),
          if (review['requiresReview'] == true)
            const Text(
              'Please review the changed conditions before travelling.',
            ),
          for (final item
              in (review['items'] as List? ?? const []).whereType<Map>()) ...[
            const Divider(),
            Text(
              trip.items
                      .where((stop) => stop.id == item['itemId'])
                      .firstOrNull
                      ?.title ??
                  'Previous experience',
            ),
            Text(
              'Availability: ${item['previousAvailability']} → ${item['currentAvailability']}',
            ),
            Text(
              'Marine suitability: ${item['previousSuitability']} → ${item['currentSuitability']}',
            ),
            Text(
              'Operations: ${item['previousOperationalStatus']} → ${item['currentOperationalStatus']}',
            ),
            Text('Suggested action: ${item['suggestedAction']}'),
            if (item['advisoryMessage'] != null)
              Text('${item['advisoryMessage']}'),
            if (item['marineConditionTime'] != null)
              Text('Conditions checked ${item['marineConditionTime']}'),
          ],
          const Text('Your selected experiences remain unchanged.'),
        ],
      ),
    ),
  );
}
