import 'package:flutter/material.dart';

import '../data/models/auth_models.dart';
import '../data/models/planner_models.dart';
import '../data/services/planner_api_service.dart';
import 'blueverse_theme.dart';
import 'planner_workflow_screens.dart';
import 'planner_components.dart';
import 'planner_history_view_model.dart';
import 'planner_draft_picker.dart';
import 'planner_time.dart';

class PlannerHomeScreen extends StatefulWidget {
  const PlannerHomeScreen({super.key, required this.api, this.user});
  final PlannerApiService api;
  final AuthUser? user;
  @override
  State<PlannerHomeScreen> createState() => _PlannerHomeScreenState();
}

class _PlannerHomeScreenState extends State<PlannerHomeScreen> {
  late Future<List<PlannerHighlight>> _future;
  @override
  void initState() {
    super.initState();
    _future = widget.api.highlights();
  }

  Future<void> _refresh() async {
    setState(() => _future = widget.api.highlights());
    try {
      await _future;
    } on Object {
      /* Display the error in the ideas section. */
    }
  }

  void _plan([Object? arguments]) => Navigator.pushNamed(
    context,
    widget.user == null ? '/signin' : '/planner/plan',
    arguments: widget.user == null
        ? {'returnTo': '/planner/plan', 'returnArguments': arguments}
        : arguments,
  );
  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('The planner')),
    body: RefreshIndicator(
      onRefresh: _refresh,
      child: ListView(
        padding: _plannerPagePadding(context),
        physics: const AlwaysScrollableScrollPhysics(),
        children: [
          ClipRRect(
            borderRadius: BorderRadius.circular(28),
            child: Image.asset(
              'assets/coastal/planner/coastal-discovery.webp',
              height: 300,
              fit: BoxFit.cover,
              semanticLabel: 'A quiet tropical coast',
            ),
          ),
          const SizedBox(height: 24),
          Text(
            'A coastal day, made for you.',
            style: Theme.of(context).textTheme.headlineLarge,
          ),
          const SizedBox(height: 12),
          const Text(
            'From a quiet stretch of shore to a little adventure on the water. Find experiences that fit your time, interests and conditions along the coast.',
          ),
          const SizedBox(height: 20),
          FilledButton.icon(
            onPressed: _plan,
            icon: const Icon(Icons.arrow_forward),
            label: const Text('Let’s plan your trip'),
          ),
          const SizedBox(height: 24),
          Text(
            'A little planning. A lot of coast.',
            style: Theme.of(context).textTheme.headlineSmall,
          ),
          for (final step in [
            (
              '01',
              'Choose your coast',
              'Tell us where, when and what you enjoy.',
            ),
            (
              '02',
              'Explore what fits',
              'Consider current experience, availability and conditions information.',
            ),
            (
              '03',
              'Make the day yours',
              'Save a private draft, arrange its details and confirm when ready.',
            ),
          ])
            Card(
              child: ListTile(
                leading: Text(
                  step.$1,
                  style: const TextStyle(color: BlueversePalette.coastTeal),
                ),
                title: Text(step.$2),
                subtitle: Text(step.$3),
                isThreeLine: true,
              ),
            ),
          const SizedBox(height: 20),
          ClipRRect(
            borderRadius: BorderRadius.circular(24),
            child: Image.asset(
              'assets/coastal/planner/guided-experiences.webp',
              height: 180,
              width: double.infinity,
              fit: BoxFit.cover,
              semanticLabel: 'Illustrative guided coastal experiences',
            ),
          ),
          const SizedBox(height: 24),
          Text(
            'Coastal days to inspire you.',
            style: Theme.of(context).textTheme.headlineSmall,
          ),
          const SizedBox(height: 12),
          FutureBuilder<List<PlannerHighlight>>(
            future: _future,
            builder: (context, snapshot) {
              if (snapshot.connectionState != ConnectionState.done) {
                return const Padding(
                  padding: EdgeInsets.all(20),
                  child: Text('Finding current coastal ideas…'),
                );
              }
              if (snapshot.hasError) {
                return _Message(
                  message: 'Coastal ideas are unavailable right now. You can still explore the planner.',
                  retry: _refresh,
                );
              }
              final highlights = snapshot.data ?? const <PlannerHighlight>[];
              return Column(
                children: [
                  if (highlights.isEmpty)
                    const Card(
                      child: Padding(
                        padding: EdgeInsets.all(20),
                        child: Text(
                          'No current coastal ideas. Start with a destination or try again later.',
                        ),
                      ),
                    ),
                  ...highlights.map(
                    (item) => _HighlightCard(
                      item: item,
                      onPlan: () => _plan({
                        'destinationId': item.destinationId,
                        'activityId': item.activityId,
                      }),
                    ),
                  ),
                ],
              );
            },
          ),
        ],
      ),
    ),
  );
}

class PlannerPlanScreen extends StatefulWidget {
  const PlannerPlanScreen({
    super.key,
    required this.user,
    required this.api,
    this.routeArguments,
  });
  final AuthUser? user;
  final PlannerApiService api;
  final Object? routeArguments;
  @override
  State<PlannerPlanScreen> createState() => _PlannerPlanScreenState();
}

class _PlannerPlanScreenState extends State<PlannerPlanScreen> {
  PlannerCatalogue? _catalogue;
  PlannerDestination? _destination;
  PlannerRecommendationResult? _result;
  final Set<String> _activityIds = {};
  final _title = TextEditingController();
  final _notes = TextEditingController();
  PlannerCandidate? _selected;
  final Map<String, PlannerBiodiversityPrediction> _wildlifePredictions = {};
  final Set<String> _wildlifeLoading = {};
  DateTime? _starts, _ends;
  int _durationHours = 4;
  String? _experience;
  bool _includeBiodiversity = true;
  bool _loading = true, _busy = false, _prefilled = false;
  bool _catalogueRequestActive = false;
  String? _error;

  bool get _canPlan =>
      widget.user?.permissions.contains('planner.recommendations.create') ==
      true;
  bool get _canManage =>
      widget.user?.permissions.contains('planner.itineraries.manage') == true;
  bool get _canReadWildlife =>
      widget.user?.permissions.contains('planner.biodiversity.read') == true;
  @override
  void initState() {
    super.initState();
    if (_canPlan) _loadCatalogue();
  }

  @override
  void dispose() {
    _title.dispose();
    _notes.dispose();
    super.dispose();
  }

  Future<void> _loadCatalogue() async {
    if (!mounted || _catalogueRequestActive) return;
    _catalogueRequestActive = true;
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final value = await widget.api.catalogue(showLoadingScreen: false);
      if (!mounted) return;
      setState(() {
        _catalogue = value;
        _loading = false;
        _error = null;
      });
      _applyPrefill();
    } on Object catch (error) {
      if (mounted) {
        setState(() {
          _loading = false;
          _error = error.toString();
        });
      }
    } finally {
      _catalogueRequestActive = false;
    }
  }

  void _applyPrefill() {
    if (_prefilled || widget.routeArguments is! Map || _catalogue == null) {
      return;
    }
    final args = Map<String, dynamic>.from(widget.routeArguments as Map);
    final destination = _firstWhereOrNull(
      _catalogue!.destinations,
      (d) => d.id == args['destinationId'],
    );
    if (destination == null) return;
    setState(() {
      _destination = destination;
      final activity = _firstWhereOrNull(
        destination.activities,
        (a) => a.id == args['activityId'],
      );
      if (activity != null) _activityIds.add(activity.id);
      _prefilled = true;
    });
  }

  void _invalidate() {
    _result = null;
    _selected = null;
    _wildlifePredictions.clear();
    _error = null;
  }

  Future<void> _recommend() async {
    if (_busy) return;
    final destination = _destination;
    if (destination == null || _starts == null || _ends == null) {
      setState(() => _error = 'Choose a destination and start and end time.');
      return;
    }
    if (!_ends!.isAfter(_starts!)) {
      setState(() => _error = 'Trip end must be after trip start.');
      return;
    }
    late final DateTime startInstant;
    late final DateTime endInstant;
    try {
      startInstant = destinationWallTimeToUtc(
        _wallClock(_starts!),
        destination.timeZone,
      );
      endInstant = destinationWallTimeToUtc(
        _wallClock(_ends!),
        destination.timeZone,
      );
    } on PlannerTimeException catch (error) {
      setState(() => _error = error.message);
      return;
    }
    final now = DateTime.now().toUtc();
    if (startInstant.isBefore(now) ||
        endInstant.isAfter(now.add(const Duration(days: 30)))) {
      setState(() => _error = 'Choose future dates within the next 30 days.');
      return;
    }
    if (endInstant.isBefore(startInstant) ||
        endInstant.difference(startInstant).inMinutes < _durationHours * 60) {
      setState(
        () => _error =
            'Choose a whole number of hours that fits within your trip.',
      );
      return;
    }
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      final value = await widget.api.createRecommendations(
        targetDestinationId: destination.id,
        startsAt: _starts!,
        endsAt: _ends!,
        durationHours: _durationHours,
        preferredActivityIds: _activityIds.toList(),
        timeZone: destination.timeZone,
        experienceLevel: _experience,
        includeBiodiversityContext: _includeBiodiversity,
      );
      if (mounted) {
        setState(() {
          _result = value;
          _selected = value.candidates.isEmpty ? null : value.candidates.first;
          _title.text = _selected?.title ?? '';
          _busy = false;
        });
      }
    } on Object catch (error) {
      if (mounted) {
        setState(() {
          _error = error is PlannerApiException ? error.message : 'We couldn’t find coastal experiences just now. Please try again.';
          _busy = false;
        });
      }
    }
  }

  Future<void> _save(PlannerCandidate candidate, {bool append = false}) async {
    if (_busy || !_canManage || _result == null) return;
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      final chosenDraft = append
          ? await Navigator.push<String>(
              context,
              MaterialPageRoute(
                builder: (_) => PlannerDraftPicker(api: widget.api),
              ),
            )
          : null;
      if (!mounted || append && chosenDraft == null) return;
      final args = widget.routeArguments;
      final targetId =
          chosenDraft ?? (args is Map ? args['itineraryId'] as String? : null);
      final trip = targetId == null
          ? await widget.api.saveDraft(
              title: _title.text.trim().isEmpty
                  ? candidate.title
                  : _title.text.trim(),
              candidate: candidate,
              description: _notes.text,
              recommendationId: _result?.recommendationId,
            )
          : await widget.api.addCandidate(
              await widget.api.itinerary(targetId),
              candidate,
              _result!.recommendationId,
            );
      if (mounted) {
        setState(() => _busy = false);
        await Navigator.pushNamed(
          context,
          '/planner/itinerary',
          arguments: trip.id,
        );
      }
    } on Object catch (error) {
      if (mounted) {
        setState(() {
          _error = error is PlannerApiException
              ? error.message
              : 'We couldn’t save your plan just now. Please try again.';
        });
      }
    } finally {
      if (mounted && _busy) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    if (widget.user == null || !_canPlan) {
      return PlannerAccess(
        user: widget.user,
        route: '/planner/plan',
        arguments: widget.routeArguments,
      );
    }
    return Scaffold(
      appBar: AppBar(
        title: const Text('Create a plan'),
        actions: [PlannerNavigationMenu(user: widget.user)],
      ),
      body: ListView(
        padding: _plannerPagePadding(context),
        children: [
          PlannerSectionHero(history: false, user: widget.user),
          if (_loading)
            const Padding(
              padding: EdgeInsets.all(24),
              child: Text('Preparing your coastal planner…'),
            ),
          if (_error != null) _Message(message: _error!, retry: _loadCatalogue),
          if (!_loading && _error == null && _catalogue?.status != 'AVAILABLE')
            _Message(
              message:
                  'Destinations are unavailable right now. Try again later.',
              retry: _loadCatalogue,
            ),
          if (_catalogue?.status == 'AVAILABLE') ...[
            DropdownButtonFormField<PlannerDestination>(
              decoration: const InputDecoration(labelText: 'Destination'),
              initialValue: _destination,
              items: _catalogue!.destinations
                  .map(
                    (d) => DropdownMenuItem(
                      value: d,
                      child: Text('${d.name} · ${d.region}'),
                    ),
                  )
                  .toList(),
              onChanged: _busy
                  ? null
                  : (d) => setState(() {
                      _destination = d;
                      _activityIds.clear();
                      _invalidate();
                    }),
            ),
            if (_destination != null) ...[
              const SizedBox(height: 16),
              Text(
                'Activities',
                style: Theme.of(context).textTheme.titleMedium,
              ),
              ..._destination!.activities.map(
                (activity) => CheckboxListTile(
                  contentPadding: EdgeInsets.zero,
                  title: Text(activity.name),
                  value: _activityIds.contains(activity.id),
                  onChanged: _busy
                      ? null
                      : (checked) => setState(() {
                          checked == true
                              ? _activityIds.add(activity.id)
                              : _activityIds.remove(activity.id);
                          _invalidate();
                        }),
                ),
              ),
            ],
            const SizedBox(height: 10),
            DropdownButtonFormField<int>(
              decoration: const InputDecoration(
                labelText: 'Time for activities',
              ),
              initialValue: _durationHours,
              items: [1, 2, 3, 4, 5, 6, 7, 8]
                  .map(
                    (hours) => DropdownMenuItem(
                      value: hours,
                      child: Text('$hours ${hours == 1 ? 'hour' : 'hours'}'),
                    ),
                  )
                  .toList(),
              onChanged: _busy
                  ? null
                  : (value) => setState(() {
                      _durationHours = value ?? 4;
                      _invalidate();
                    }),
            ),
            const SizedBox(height: 14),
            DropdownButtonFormField<String?>(
              decoration: const InputDecoration(
                labelText: 'Experience level (optional)',
              ),
              initialValue: _experience,
              items: const [
                DropdownMenuItem(value: null, child: Text('Any experience')),
                DropdownMenuItem(value: 'BEGINNER', child: Text('Beginner')),
                DropdownMenuItem(
                  value: 'INTERMEDIATE',
                  child: Text('Intermediate'),
                ),
                DropdownMenuItem(value: 'ADVANCED', child: Text('Advanced')),
              ],
              onChanged: _busy
                  ? null
                  : (value) => setState(() {
                      _experience = value;
                      _invalidate();
                    }),
            ),
            CheckboxListTile(
              contentPadding: EdgeInsets.zero,
              title: const Text('Include optional wildlife context'),
              value: _includeBiodiversity,
              onChanged: _busy
                  ? null
                  : (value) => setState(() {
                      _includeBiodiversity = value ?? false;
                      _invalidate();
                    }),
            ),
            const SizedBox(height: 14),
            OutlinedButton.icon(
              onPressed: _busy ? null : () => _pickDateTime(true),
              icon: const Icon(Icons.calendar_today_outlined),
              label: Text(
                _starts == null
                    ? 'Choose start date and time'
                    : 'Starts ${_displayLocal(_starts!)}',
              ),
            ),
            OutlinedButton.icon(
              onPressed: _busy ? null : () => _pickDateTime(false),
              icon: const Icon(Icons.event_outlined),
              label: Text(
                _ends == null
                    ? 'Choose end date and time'
                    : 'Ends ${_displayLocal(_ends!)}',
              ),
            ),
            const SizedBox(height: 14),
            FilledButton.icon(
              onPressed: _busy ? null : _recommend,
              icon: const Icon(Icons.auto_awesome),
              label: Text(
                _busy ? 'Finding ideas…' : 'Find coastal experiences',
              ),
            ),
          ],
          if (_result != null) ...[
            const SizedBox(height: 24),
            if (widget.user?.permissions.contains(
                  'planner.recommendations.read',
                ) ==
                true)
              OutlinedButton(
                onPressed: () => Navigator.pushNamed(
                  context,
                  '/planner/recommendations/${_result!.recommendationId}',
                ),
                child: const Text(
                  'Open saved trip ideas and planning progress',
                ),
              ),
            Text(
              'Your coastal possibilities',
              style: Theme.of(context).textTheme.headlineSmall,
            ),
            if (_result!.candidates.isEmpty)
              Padding(
                padding: EdgeInsets.symmetric(vertical: 18),
                child: Text(
                  _result!.outcome == 'DEPENDENCIES_UNAVAILABLE'
                      ? 'Current experience information is unavailable. Please try again later.'
                      : 'No experiences matched those dates and choices. Try a different time or activity.',
                ),
              ),
            ..._result!.candidates.map(
              (candidate) => Card(
                child: Padding(
                  padding: const EdgeInsets.all(18),
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
                      const SizedBox(height: 8),
                      Text(candidate.reasons.join(' ')),
                      if (candidate.biodiversityContext != null)
                        Padding(
                          padding: const EdgeInsets.only(top: 8),
                          child: Text(
                            'Wildlife note: ${candidate.biodiversityContext!.speciesName}',
                          ),
                        ),
                      if (_canReadWildlife) ...[
                        const SizedBox(height: 8),
                        OutlinedButton(
                          onPressed:
                              _wildlifeLoading.contains(
                                '${candidate.destinationId}:${candidate.activityId}',
                              )
                              ? null
                              : () => _checkWildlife(candidate),
                          child: Text(
                            _wildlifeLoading.contains(
                                  '${candidate.destinationId}:${candidate.activityId}',
                                )
                                ? 'Checking wildlife context…'
                                : 'Check wildlife context',
                          ),
                        ),
                        if (_wildlifePredictions.containsKey(
                          '${candidate.destinationId}:${candidate.activityId}',
                        ))
                          _PlannerWildlifeResult(
                            prediction:
                                _wildlifePredictions['${candidate.destinationId}:${candidate.activityId}']!,
                          ),
                      ],
                      const SizedBox(height: 12),
                      OutlinedButton.icon(
                        onPressed: _busy
                            ? null
                            : () => setState(() {
                                _selected = candidate;
                                _title.text = candidate.title;
                              }),
                        icon: Icon(
                          _selected == candidate
                              ? Icons.check_circle
                              : Icons.radio_button_unchecked,
                        ),
                        label: Text(
                          _selected == candidate
                              ? 'Selected experience'
                              : 'Choose this experience',
                        ),
                      ),
                    ],
                  ),
                ),
              ),
            ),
            if (_selected != null && _canManage)
              Card(
                child: Padding(
                  padding: const EdgeInsets.all(20),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      Text(
                        'Keep this coastal day',
                        style: Theme.of(context).textTheme.titleLarge,
                      ),
                      const SizedBox(height: 12),
                      TextField(
                        controller: _title,
                        enabled: !_busy,
                        decoration: const InputDecoration(
                          labelText: 'Draft trip name',
                        ),
                      ),
                      const SizedBox(height: 12),
                      TextField(
                        controller: _notes,
                        enabled: !_busy,
                        maxLines: 3,
                        decoration: const InputDecoration(
                          labelText: 'Trip notes (optional)',
                        ),
                      ),
                      const SizedBox(height: 16),
                      FilledButton(
                        onPressed: _busy ? null : () => _save(_selected!),
                        child: const Text('Save as draft'),
                      ),
                      const SizedBox(height: 8),
                      OutlinedButton(
                        onPressed: _busy
                            ? null
                            : () => _save(_selected!, append: true),
                        child: const Text('Add to an existing draft'),
                      ),
                      const Text(
                        'Saving creates a private draft. Confirm later from plan details.',
                      ),
                    ],
                  ),
                ),
              ),
          ],
        ],
      ),
    );
  }

  Future<void> _checkWildlife(PlannerCandidate candidate) async {
    final key = '${candidate.destinationId}:${candidate.activityId}';
    setState(() => _wildlifeLoading.add(key));
    try {
      final prediction = await widget.api.biodiversityPredictions(
        destinationId: candidate.destinationId,
        activityId: candidate.activityId,
      );
      if (mounted) setState(() => _wildlifePredictions[key] = prediction);
    } on Object {
      if (mounted) {
        setState(
          () => _wildlifePredictions[key] = PlannerBiodiversityPrediction(
            contractVersion: 1,
            destinationId: candidate.destinationId,
            activityId: candidate.activityId,
            status: 'unavailable',
            message: 'Wildlife predictions are temporarily unavailable. Please try again later.',
            uncertaintyText: null,
            provenance: null,
            predictedSpecies: const [],
            modelMetadata: null,
            limitations: 'Wildlife predictions are temporarily unavailable. Please try again later.',
          ),
        );
      }
    } finally {
      if (mounted) setState(() => _wildlifeLoading.remove(key));
    }
  }

  Future<void> _pickDateTime(bool start) async {
    final now = DateTime.now();
    final tomorrow = DateTime.utc(
      now.year,
      now.month,
      now.day,
    ).add(const Duration(days: 1));
    final initialWall = start
        ? (_starts ?? tomorrow)
        : (_ends ?? _starts ?? tomorrow);
    final initialDate = DateTime(
      initialWall.year,
      initialWall.month,
      initialWall.day,
    );
    final firstDate = DateTime(now.year, now.month, now.day);
    final date = await showDatePicker(
      context: context,
      firstDate: firstDate,
      lastDate: firstDate.add(const Duration(days: 30)),
      initialDate: initialDate,
    );
    if (date == null || !mounted) return;
    final time = await showTimePicker(
      context: context,
      initialTime: TimeOfDay.fromDateTime(initialWall),
    );
    if (time == null || !mounted) return;
    final value = DateTime.utc(
      date.year,
      date.month,
      date.day,
      time.hour,
      time.minute,
    );
    setState(() {
      _invalidate();
      if (start) {
        _starts = value;
      } else {
        _ends = value;
      }
    });
  }
}

class _PlannerWildlifeResult extends StatelessWidget {
  const _PlannerWildlifeResult({required this.prediction});
  final PlannerBiodiversityPrediction prediction;

  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.only(top: 8),
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(prediction.message),
        if (prediction.status == 'available') ...[
          for (final species in prediction.predictedSpecies)
            Text('${species.commonName} (${species.scientificName})'),
          if (prediction.uncertaintyText case final text?) Text(text),
          if (prediction.modelMetadata?.inferenceTimestamp
              case final timestamp?)
            Text(
              'Prediction issued $timestamp · Model ${prediction.modelMetadata?.modelVersion ?? 'unknown'}',
            ),
        ],
        if (prediction.status == 'available') Text(prediction.limitations),
      ],
    ),
  );
}

class PlannerSavedTripsScreen extends StatefulWidget {
  const PlannerSavedTripsScreen({
    super.key,
    required this.api,
    required this.user,
  });
  final PlannerApiService api;
  final AuthUser? user;
  @override
  State<PlannerSavedTripsScreen> createState() =>
      _PlannerSavedTripsScreenState();
}

class _PlannerSavedTripsScreenState extends State<PlannerSavedTripsScreen> {
  late final PlannerHistoryViewModel _model;
  final _search = TextEditingController();
  bool _showFilters = false;
  bool get _allowed =>
      plannerGranted(widget.user, 'planner.itineraries.manage');
  @override
  void initState() {
    super.initState();
    _model = PlannerHistoryViewModel(widget.api);
    if (_allowed) _model.load();
  }

  @override
  void dispose() {
    _model.dispose();
    _search.dispose();
    super.dispose();
  }

  void _move(int page) {
    _search.clear();
    _model.load(nextPage: page);
  }

  @override
  Widget build(BuildContext context) {
    if (!_allowed) {
      return PlannerAccess(user: widget.user, route: '/planner/saved');
    }
    return Scaffold(
      appBar: AppBar(
        title: const Text('Plan history'),
        actions: [PlannerNavigationMenu(user: widget.user)],
      ),
      body: ListenableBuilder(
        listenable: _model,
        builder: (context, _) => RefreshIndicator(
          onRefresh: _model.load,
          child: ListView(
            padding: _plannerPagePadding(context),
            physics: const AlwaysScrollableScrollPhysics(),
            children: [
              PlannerSectionHero(history: true, user: widget.user),
              Card(
                margin: EdgeInsets.zero,
                child: Padding(
                  padding: const EdgeInsets.all(20),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      Text(
                        'Find your coastal plans',
                        style: Theme.of(context).textTheme.headlineSmall,
                      ),
                      const SizedBox(height: 16),
                      Wrap(
                        spacing: 8,
                        runSpacing: 8,
                        children: [
                          for (final filter in [
                            'All plans',
                            'Drafts',
                            'Confirmed',
                            'Past',
                          ])
                            ChoiceChip(
                              label: Text(filter),
                              selected: _model.filter == filter,
                              onSelected: _model.loading
                                  ? null
                                  : (_) => _model.refine(status: filter),
                            ),
                        ],
                      ),
                      const SizedBox(height: 16),
                      TextField(
                        controller: _search,
                        enabled: !_model.loading,
                        onChanged: (query) => _model.refine(query: query),
                        decoration: InputDecoration(
                          labelText: 'Search plans by name or notes',
                          prefixIcon: const Icon(Icons.search),
                          suffixIcon: IconButton(
                            tooltip: 'Reset plan filters',
                            onPressed: () {
                              _search.clear();
                              _model.reset();
                            },
                            icon: const Icon(Icons.restart_alt),
                          ),
                        ),
                      ),
                      TextButton.icon(
                        onPressed: () =>
                            setState(() => _showFilters = !_showFilters),
                        icon: const Icon(Icons.tune),
                        label: const Text('Filter options'),
                      ),
                      if (_showFilters)
                        CheckboxListTile(
                          contentPadding: EdgeInsets.zero,
                          title: const Text(
                            'Only plans needing a conditions check',
                          ),
                          value: _model.reviewOnly,
                          onChanged: _model.loading
                              ? null
                              : (value) =>
                                    _model.refine(needsReview: value ?? false),
                        ),
                      const Text(
                        'Filters apply to this page. Times are local to each trip.',
                        style: TextStyle(
                          fontSize: 12,
                          color: BlueversePalette.coastMuted,
                        ),
                      ),
                      const SizedBox(height: 24),
                      if (_model.loading)
                        Semantics(
                          liveRegion: true,
                          child: Text('Opening your saved trips…'),
                        )
                      else if (_model.error != null)
                        _Message(message: _model.error!, retry: _model.load)
                      else if (_model.trips.isEmpty) ...[
                        const Text(
                          'No trips on this page. Your next coastal day awaits.',
                        ),
                        const SizedBox(height: 10),
                        if (plannerGranted(
                          widget.user,
                          'planner.recommendations.create',
                        ))
                          FilledButton(
                            onPressed: () => Navigator.pushReplacementNamed(
                              context,
                              '/planner/plan',
                            ),
                            child: const Text('Create your first plan'),
                          ),
                      ] else if (_model.visible.isEmpty)
                        const Text(
                          'No plans match these filters. Try another name or reset the filters.',
                        )
                      else
                        for (final trip in _model.visible)
                          Card(
                            child: ListTile(
                              contentPadding: const EdgeInsets.all(16),
                              leading: const Icon(Icons.beach_access_outlined),
                              title: Text(trip.title),
                              subtitle: Text(
                                [
                                  '${trip.status} · ${_formatLocalString(trip.startsAtLocal)} · ${trip.items.length} experiences',
                                  if (trip.description?.trim().isNotEmpty ==
                                      true)
                                    trip.description!.trim(),
                                ].join('\n'),
                              ),
                              trailing: const Icon(Icons.chevron_right),
                              onTap: () async {
                                await Navigator.pushNamed(
                                  context,
                                  '/planner/itinerary',
                                  arguments: trip.id,
                                );
                                if (mounted) await _model.load();
                              },
                            ),
                          ),
                      const Divider(height: 36),
                      DropdownButtonFormField<int>(
                        initialValue: _model.pageSize,
                        decoration: const InputDecoration(
                          labelText: 'Plans per page',
                        ),
                        items: [10, 20, 25, 50]
                            .map(
                              (size) => DropdownMenuItem(
                                value: size,
                                child: Text('$size'),
                              ),
                            )
                            .toList(),
                        onChanged: _model.loading
                            ? null
                            : (value) {
                                _search.clear();
                                _model.load(size: value);
                              },
                      ),
                      const SizedBox(height: 12),
                      Text(
                        '${_model.visible.length} of ${_model.trips.length} plans on this page',
                      ),
                      Row(
                        mainAxisAlignment: MainAxisAlignment.spaceBetween,
                        children: [
                          TextButton(
                            onPressed: _model.loading || _model.page == 1
                                ? null
                                : () => _move(_model.page - 1),
                            child: const Text('Previous'),
                          ),
                          Text('Page ${_model.page}'),
                          TextButton(
                            onPressed:
                                _model.loading ||
                                    _model.error != null ||
                                    !_model.hasNext
                                ? null
                                : () => _move(_model.page + 1),
                            child: const Text('Next'),
                          ),
                        ],
                      ),
                    ],
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class PlannerItineraryScreen extends StatefulWidget {
  const PlannerItineraryScreen({
    super.key,
    required this.api,
    required this.itineraryId,
    required this.user,
  });
  final PlannerApiService api;
  final String itineraryId;
  final AuthUser? user;
  @override
  State<PlannerItineraryScreen> createState() => _PlannerItineraryScreenState();
}

class _PlannerItineraryScreenState extends State<PlannerItineraryScreen> {
  Future<PlannerItinerary>? _future;
  bool get _allowed =>
      widget.user?.permissions.contains('planner.itineraries.manage') == true;
  bool _busy = false;
  @override
  void initState() {
    super.initState();
    if (_allowed) _future = widget.api.itinerary(widget.itineraryId);
  }

  void _reload() => setState(() {
    _future = widget.api.itinerary(widget.itineraryId);
  });
  Future<void> _edit(PlannerItinerary trip) async {
    await Navigator.push<PlannerItinerary>(
      context,
      MaterialPageRoute(
        builder: (_) => PlannerDraftEditor(trip: trip, api: widget.api),
      ),
    );
    if (mounted) _reload();
  }

  Future<void> _confirm(PlannerItinerary trip) async {
    final approved = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Confirm this trip?'),
        content: const Text(
          'After confirmation, you cannot edit or cancel this trip.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: const Text('Keep as draft'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(context, true),
            child: const Text('Confirm trip'),
          ),
        ],
      ),
    );
    if (approved != true) return;
    setState(() => _busy = true);
    try {
      await widget.api.confirmItinerary(trip.id);
      if (mounted) {
        setState(() => _busy = false);
        _reload();
      }
    } on Object catch (error) {
      if (mounted) {
        setState(() => _busy = false);
        _showError(error.toString());
      }
    }
  }

  Future<void> _cancel(PlannerItinerary trip) async {
    final approved = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Delete this draft?'),
        content: const Text('This action cannot be undone.'),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: const Text('Keep draft'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(context, true),
            child: const Text('Delete draft'),
          ),
        ],
      ),
    );
    if (approved != true) return;
    setState(() => _busy = true);
    try {
      await widget.api.deleteItinerary(trip.id);
      if (mounted) {
        Navigator.pop(context);
      }
    } on Object catch (error) {
      if (mounted) {
        setState(() => _busy = false);
        _showError(error.toString());
      }
    }
  }

  Future<void> _review(PlannerItinerary trip) async {
    setState(() => _busy = true);
    try {
      final result = await widget.api.reEvaluateItinerary(trip.id);
      if (!mounted) return;
      setState(() => _busy = false);
      await showDialog<void>(
        context: context,
        builder: (context) => AlertDialog(
          title: const Text('Condition review'),
          content: SingleChildScrollView(
            child: PlannerReviewCard(trip: trip, review: result),
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(context),
              child: const Text('Close'),
            ),
          ],
        ),
      );
      if (mounted) _reload();
    } on Object catch (error) {
      if (mounted) {
        setState(() => _busy = false);
        _showError(error.toString());
      }
    }
  }

  void _showError(String message) =>
      ScaffoldMessenger.of(context)
          .showSnackBar(SnackBar(content: Text(message)));
  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('Trip details')),
    body: !_allowed
        ? const Center(child: Text('Your account cannot manage this trip.'))
        : FutureBuilder<PlannerItinerary>(
            future: _future,
            builder: (context, snapshot) {
              if (snapshot.connectionState != ConnectionState.done) {
                return const Center(child: CircularProgressIndicator());
              }
              if (snapshot.hasError) {
                return Center(
                  child: TextButton(
                    onPressed: _reload,
                    child: const Text('Trip details unavailable · Try again'),
                  ),
                );
              }
              final trip = snapshot.data!;
              final draft = trip.status.toUpperCase() == 'DRAFT';
              return ListView(
                padding: _plannerPagePadding(context),
                children: [
                  Text(
                    trip.title,
                    style: Theme.of(context).textTheme.headlineMedium,
                  ),
                  const SizedBox(height: 8),
                  Chip(label: Text(trip.status)),
                  Text(
                    '${_formatLocalString(trip.startsAtLocal)} – ${_formatLocalString(trip.endsAtLocal)} (${trip.timeZone})',
                  ),
                  if (trip.description?.isNotEmpty == true)
                    Padding(
                      padding: const EdgeInsets.only(top: 12),
                      child: Text(trip.description!),
                    ),
                  const SizedBox(height: 20),
                  Text(
                    'Experiences',
                    style: Theme.of(context).textTheme.titleLarge,
                  ),
                  ...trip.items.map(
                    (item) => Card(
                      child: ListTile(
                        title: Text(item.title),
                        subtitle: Text(
                          '${_formatLocalString(item.startsAtLocal)} – ${_formatLocalString(item.endsAtLocal)} (${item.timeZone})\nAvailability ${item.availability} · Marine ${item.suitability} · Operations ${item.operations}',
                        ),
                      ),
                    ),
                  ),
                  OutlinedButton(
                    onPressed: _busy
                        ? null
                        : () => Navigator.push(
                            context,
                            MaterialPageRoute<void>(
                              builder: (_) => PlannerReviewHistory(
                                trip: trip,
                                api: widget.api,
                              ),
                            ),
                          ),
                    child: const Text('Condition review history'),
                  ),
                  if (draft) ...[
                    const SizedBox(height: 14),
                    OutlinedButton(
                      onPressed: _busy ? null : () => _edit(trip),
                      child: const Text('Edit draft'),
                    ),
                    if (widget.user?.permissions.contains(
                          'planner.recommendations.create',
                        ) ==
                        true)
                      OutlinedButton(
                        onPressed: _busy
                            ? null
                            : () async {
                                await Navigator.pushNamed(
                                  context,
                                  '/planner/plan',
                                  arguments: {'itineraryId': trip.id},
                                );
                                if (mounted) _reload();
                              },
                        child: const Text('Add experiences'),
                      ),
                    OutlinedButton(
                      onPressed: _busy ? null : () => _cancel(trip),
                      child: const Text('Delete draft'),
                    ),
                    FilledButton(
                      onPressed: _busy ? null : () => _confirm(trip),
                      child: const Text('Confirm trip'),
                    ),
                    const Padding(
                      padding: EdgeInsets.only(top: 8),
                      child: Text(
                        'Confirmed trips cannot be edited or cancelled.',
                      ),
                    ),
                  ],
                  if (trip.status == 'DRAFT' || trip.status == 'CONFIRMED') ...[
                    const SizedBox(height: 14),
                    OutlinedButton(
                      onPressed: _busy ? null : () => _review(trip),
                      child: const Text('Review current conditions'),
                    ),
                  ],
                ],
              );
            },
          ),
  );
}

class _HighlightCard extends StatelessWidget {
  const _HighlightCard({required this.item, required this.onPlan});
  final PlannerHighlight item;
  final VoidCallback onPlan;
  @override
  Widget build(BuildContext context) => Card(
    child: Padding(
      padding: const EdgeInsets.all(18),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            item.destinationName.toUpperCase(),
            style: const TextStyle(
              color: BlueversePalette.coastTeal,
              fontSize: 11,
              fontWeight: FontWeight.w800,
              letterSpacing: 1.1,
            ),
          ),
          const SizedBox(height: 8),
          Text(item.title, style: Theme.of(context).textTheme.titleLarge),
          const SizedBox(height: 6),
          Text(
            '${item.activityName} · ${_formatLocalString(item.startsAtLocal)} (${item.timeZone})',
          ),
          const SizedBox(height: 10),
          Text(
            'Availability ${item.availabilityStatus} · Marine ${item.suitabilityStatus} · Operations ${item.operationalStatus}',
            style: const TextStyle(color: BlueversePalette.coastMuted),
          ),
          if (item.evidenceCheckedAt != null)
            Text(
              'Availability checked ${_formatInstant(item.evidenceCheckedAt!)}',
              style: const TextStyle(fontSize: 12),
            ),
          ...item.uncertaintyNotes.map(
            (note) =>
                Text(note, style: const TextStyle(color: Colors.deepOrange)),
          ),
          TextButton(
            onPressed: onPlan,
            child: const Text('Plan around this idea'),
          ),
        ],
      ),
    ),
  );
}

EdgeInsets _plannerPagePadding(BuildContext context) =>
    EdgeInsets.fromLTRB(20, 20, 20, 20 + MediaQuery.paddingOf(context).bottom);

class _Message extends StatelessWidget {
  const _Message({required this.message, required this.retry});
  final String message;
  final VoidCallback retry;
  @override
  Widget build(BuildContext context) => Card(
    color: BlueversePalette.coastSage,
    child: Padding(
      padding: const EdgeInsets.all(18),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(message),
          const SizedBox(height: 10),
          OutlinedButton(onPressed: retry, child: const Text('Try again')),
        ],
      ),
    ),
  );
}

String _formatInstant(String value) {
  final date = DateTime.parse(value).toUtc();
  final month = date.month.toString().padLeft(2, '0'),
      day = date.day.toString().padLeft(2, '0'),
      hour = date.hour.toString().padLeft(2, '0'),
      minute = date.minute.toString().padLeft(2, '0');
  return '${date.year}-$month-$day $hour:$minute UTC';
}

String _formatLocalString(String value) =>
    value.replaceFirst('T', ' ').replaceFirst(RegExp(r'Z$'), '');
String _displayLocal(DateTime date) =>
    '${date.year}-${date.month.toString().padLeft(2, '0')}-${date.day.toString().padLeft(2, '0')} ${date.hour.toString().padLeft(2, '0')}:${date.minute.toString().padLeft(2, '0')}';
String _wallClock(DateTime date) =>
    '${date.year.toString().padLeft(4, '0')}-${date.month.toString().padLeft(2, '0')}-${date.day.toString().padLeft(2, '0')}T${date.hour.toString().padLeft(2, '0')}:${date.minute.toString().padLeft(2, '0')}';
T? _firstWhereOrNull<T>(Iterable<T> values, bool Function(T) predicate) {
  for (final value in values) {
    if (predicate(value)) return value;
  }
  return null;
}
