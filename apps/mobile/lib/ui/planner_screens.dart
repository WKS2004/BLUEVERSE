import 'package:flutter/material.dart';

import '../data/models/auth_models.dart';
import '../data/models/planner_models.dart';
import '../data/services/planner_api_service.dart';
import 'blueverse_theme.dart';

class PlannerHomeScreen extends StatefulWidget {
  const PlannerHomeScreen({super.key, required this.api});
  final PlannerApiService api;
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

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('Coastal planner')),
    body: FutureBuilder<List<PlannerHighlight>>(
      future: _future,
      builder: (context, snapshot) {
        if (snapshot.connectionState != ConnectionState.done) {
          return const Center(child: CircularProgressIndicator());
        }
        if (snapshot.hasError) {
          return _Message(
            message: 'Coastal ideas are unavailable right now.',
            retry: () => setState(() => _future = widget.api.highlights()),
          );
        }
        final highlights = snapshot.data ?? const <PlannerHighlight>[];
        return RefreshIndicator(
          onRefresh: () async =>
              setState(() => _future = widget.api.highlights()),
          child: ListView(
            padding: const EdgeInsets.all(20),
            children: [
              _PlannerHero(
                onPlan: () => Navigator.pushNamed(context, '/planner/plan'),
              ),
              const SizedBox(height: 24),
              Text(
                'Coastal days to inspire you.',
                style: Theme.of(context).textTheme.headlineSmall,
              ),
              const SizedBox(height: 8),
              const Text(
                'Current ideas, considered conditions, and plans you can shape around your own day.',
              ),
              const SizedBox(height: 14),
              if (highlights.isEmpty)
                const Card(
                  child: Padding(
                    padding: EdgeInsets.all(20),
                    child: Text(
                      'Verified coastal ideas are unavailable right now. Try again later or start planning when destinations return.',
                    ),
                  ),
                ),
              ...highlights.map(
                (item) => _HighlightCard(
                  item: item,
                  onPlan: () => Navigator.pushNamed(
                    context,
                    '/planner/plan',
                    arguments: {
                      'destinationId': item.destinationId,
                      'activityId': item.activityId,
                    },
                  ),
                ),
              ),
            ],
          ),
        );
      },
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
  DateTime? _starts, _ends;
  int _durationHours = 4;
  String? _experience;
  bool _includeBiodiversity = true;
  bool _loading = true, _busy = false, _prefilled = false;
  String? _error;

  bool get _canPlan =>
      widget.user?.permissions.contains('planner.recommendations.create') ==
          true &&
      widget.user?.permissions.contains('planner.itineraries.manage') == true;
  @override
  void initState() {
    super.initState();
    _loadCatalogue();
  }

  @override
  void dispose() {
    _title.dispose();
    super.dispose();
  }

  Future<void> _loadCatalogue() async {
    try {
      final value = await widget.api.catalogue();
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

  Future<void> _recommend() async {
    final destination = _destination;
    if (destination == null ||
        _starts == null ||
        _ends == null ||
        _activityIds.isEmpty) {
      setState(
        () =>
            _error = 'Choose a destination, activity, and start and end time.',
      );
      return;
    }
    if (!_ends!.isAfter(_starts!)) {
      setState(() => _error = 'Trip end must be after trip start.');
      return;
    }
    if (_ends!.difference(_starts!).inHours < _durationHours) {
      setState(
        () => _error = 'Duration must fit within the selected trip times.',
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
          _busy = false;
        });
      }
    } on Object catch (error) {
      if (mounted) {
        setState(() {
          _error = error.toString();
          _busy = false;
        });
      }
    }
  }

  Future<void> _save(PlannerCandidate candidate) async {
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      final trip = await widget.api.saveDraft(
        title: _title.text.trim().isEmpty
            ? candidate.title
            : _title.text.trim(),
        candidate: candidate,
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
          _error = error.toString();
          _busy = false;
        });
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    if (widget.user == null) {
      return const Scaffold(
        body: Center(child: Text('Sign in to plan a trip.')),
      );
    }
    if (!_canPlan) {
      return const Scaffold(
        body: Center(
          child: Text('Your account cannot create and manage coastal trips.'),
        ),
      );
    }
    return Scaffold(
      appBar: AppBar(
        title: const Text('Plan a trip'),
        actions: [
          TextButton(
            onPressed: () => Navigator.pushNamed(context, '/planner/saved'),
            child: const Text('Saved trips'),
          ),
        ],
      ),
      body: _loading
          ? const Center(child: CircularProgressIndicator())
          : ListView(
              padding: const EdgeInsets.all(20),
              children: [
                Text(
                  'Build your coastal day.',
                  style: Theme.of(context).textTheme.headlineMedium,
                ),
                const SizedBox(height: 8),
                const Text(
                  'Choose a place, time, and activities. Review suggestions, then save a draft before you confirm.',
                ),
                const SizedBox(height: 18),
                if (_error != null)
                  _Message(message: _error!, retry: _loadCatalogue),
                if (_catalogue?.status != 'AVAILABLE')
                  const Card(
                    child: Padding(
                      padding: EdgeInsets.all(18),
                      child: Text(
                        'Destinations are unavailable right now. Try again later.',
                      ),
                    ),
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
                    onChanged: (d) => setState(() {
                      _destination = d;
                      _activityIds.clear();
                      _result = null;
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
                        onChanged: (checked) => setState(() {
                          checked == true
                              ? _activityIds.add(activity.id)
                              : _activityIds.remove(activity.id);
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
                            child: Text(
                              '$hours ${hours == 1 ? 'hour' : 'hours'}',
                            ),
                          ),
                        )
                        .toList(),
                    onChanged: (value) =>
                        setState(() => _durationHours = value ?? 4),
                  ),
                  const SizedBox(height: 14),
                  DropdownButtonFormField<String?>(
                    decoration: const InputDecoration(
                      labelText: 'Experience level (optional)',
                    ),
                    initialValue: _experience,
                    items: const [
                      DropdownMenuItem(
                        value: null,
                        child: Text('Any experience'),
                      ),
                      DropdownMenuItem(
                        value: 'BEGINNER',
                        child: Text('Beginner'),
                      ),
                      DropdownMenuItem(
                        value: 'INTERMEDIATE',
                        child: Text('Intermediate'),
                      ),
                      DropdownMenuItem(
                        value: 'ADVANCED',
                        child: Text('Advanced'),
                      ),
                    ],
                    onChanged: (value) => setState(() => _experience = value),
                  ),
                  CheckboxListTile(
                    contentPadding: EdgeInsets.zero,
                    title: const Text('Include optional wildlife context'),
                    value: _includeBiodiversity,
                    onChanged: (value) =>
                        setState(() => _includeBiodiversity = value ?? false),
                  ),
                  const SizedBox(height: 14),
                  OutlinedButton.icon(
                    onPressed: () => _pickDateTime(true),
                    icon: const Icon(Icons.calendar_today_outlined),
                    label: Text(
                      _starts == null
                          ? 'Choose start date and time'
                          : 'Starts ${_displayLocal(_starts!)}',
                    ),
                  ),
                  OutlinedButton.icon(
                    onPressed: () => _pickDateTime(false),
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
                  Text(
                    'Your coastal possibilities',
                    style: Theme.of(context).textTheme.headlineSmall,
                  ),
                  if (_result!.candidates.isEmpty)
                    const Padding(
                      padding: EdgeInsets.symmetric(vertical: 18),
                      child: Text(
                        'No experiences matched those dates and choices. Try a different time or activity.',
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
                            const SizedBox(height: 8),
                            Text(candidate.reasons.join(' ')),
                            if (candidate.biodiversityContext != null)
                              Padding(
                                padding: const EdgeInsets.only(top: 8),
                                child: Text(
                                  'Wildlife note: ${candidate.biodiversityContext!.speciesName}',
                                ),
                              ),
                            const SizedBox(height: 12),
                            TextField(
                              controller: _title,
                              decoration: const InputDecoration(
                                labelText: 'Draft trip name',
                              ),
                            ),
                            const SizedBox(height: 12),
                            FilledButton(
                              onPressed: _busy ? null : () => _save(candidate),
                              child: const Text('Save as draft'),
                            ),
                          ],
                        ),
                      ),
                    ),
                  ),
                ],
              ],
            ),
    );
  }

  Future<void> _pickDateTime(bool start) async {
    final now = DateTime.now();
    final initial = start
        ? (_starts ?? now.add(const Duration(days: 1)))
        : (_ends ?? _starts ?? now.add(const Duration(days: 1)));
    final date = await showDatePicker(
      context: context,
      firstDate: DateTime(now.year, now.month, now.day),
      lastDate: now.add(const Duration(days: 30)),
      initialDate: initial,
    );
    if (date == null || !mounted) return;
    final time = await showTimePicker(
      context: context,
      initialTime: TimeOfDay.fromDateTime(initial),
    );
    if (time == null || !mounted) return;
    final value = DateTime(
      date.year,
      date.month,
      date.day,
      time.hour,
      time.minute,
    );
    setState(() {
      if (start) {
        _starts = value;
      } else {
        _ends = value;
      }
    });
  }
}

class PlannerSavedTripsScreen extends StatelessWidget {
  const PlannerSavedTripsScreen({super.key, required this.api});
  final PlannerApiService api;
  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('Saved trips')),
    body: FutureBuilder<List<PlannerItinerary>>(
      future: api.itineraries(),
      builder: (context, snapshot) {
        if (snapshot.connectionState != ConnectionState.done) {
          return const Center(child: CircularProgressIndicator());
        }
        if (snapshot.hasError) {
          return Center(
            child: Text(
              'Saved trips are unavailable right now. ${snapshot.error}',
            ),
          );
        }
        final trips = snapshot.data ?? const <PlannerItinerary>[];
        if (trips.isEmpty) {
          return Center(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                const Text('Your first coastal day awaits.'),
                const SizedBox(height: 12),
                FilledButton(
                  onPressed: () =>
                      Navigator.pushNamed(context, '/planner/plan'),
                  child: const Text('Plan a trip'),
                ),
              ],
            ),
          );
        }
        return ListView(
          padding: const EdgeInsets.all(20),
          children: trips
              .map(
                (trip) => Card(
                  child: ListTile(
                    title: Text(trip.title),
                    subtitle: Text(
                      '${trip.status} · ${_formatLocalString(trip.startsAtLocal)} · ${trip.items.length} experiences',
                    ),
                    trailing: const Icon(Icons.chevron_right),
                    onTap: () => Navigator.pushNamed(
                      context,
                      '/planner/itinerary',
                      arguments: trip.id,
                    ),
                  ),
                ),
              )
              .toList(),
        );
      },
    ),
  );
}

class PlannerItineraryScreen extends StatefulWidget {
  const PlannerItineraryScreen({
    super.key,
    required this.api,
    required this.itineraryId,
  });
  final PlannerApiService api;
  final String itineraryId;
  @override
  State<PlannerItineraryScreen> createState() => _PlannerItineraryScreenState();
}

class _PlannerItineraryScreenState extends State<PlannerItineraryScreen> {
  late Future<PlannerItinerary> _future;
  bool _busy = false;
  @override
  void initState() {
    super.initState();
    _future = widget.api.itinerary(widget.itineraryId);
  }

  void _reload() =>
      setState(() => _future = widget.api.itinerary(widget.itineraryId));
  Future<void> _edit(PlannerItinerary trip) async {
    final title = TextEditingController(text: trip.title),
        description = TextEditingController(text: trip.description ?? '');
    final form = await showDialog<(String, String)>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Edit draft'),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            TextField(
              controller: title,
              decoration: const InputDecoration(labelText: 'Trip name'),
            ),
            TextField(
              controller: description,
              decoration: const InputDecoration(labelText: 'Notes'),
              maxLines: 3,
            ),
          ],
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context),
            child: const Text('Keep draft'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(context, (
              title.text.trim(),
              description.text.trim(),
            )),
            child: const Text('Save changes'),
          ),
        ],
      ),
    );
    title.dispose();
    description.dispose();
    if (form == null || form.$1.isEmpty) return;
    setState(() => _busy = true);
    try {
      await widget.api.updateItinerary(
        trip,
        title: form.$1,
        description: form.$2,
      );
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
          content: Text(
            '${result['summary'] ?? 'Review complete.'}\n\n${(result['items'] as List? ?? const []).map((item) => '${item['title'] ?? 'Experience'}: ${item['currentSuitability'] ?? 'UNKNOWN'} · ${item['currentAvailability'] ?? 'UNKNOWN'} · ${item['currentOperationalStatus'] ?? 'UNKNOWN'}').join('\n')}',
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
    body: FutureBuilder<PlannerItinerary>(
      future: _future,
      builder: (context, snapshot) {
        if (snapshot.connectionState != ConnectionState.done) {
          return const Center(child: CircularProgressIndicator());
        }
        if (snapshot.hasError) {
          return Center(
            child: Text('Trip details are unavailable. ${snapshot.error}'),
          );
        }
        final trip = snapshot.data!;
        final draft = trip.status.toUpperCase() == 'DRAFT';
        return ListView(
          padding: const EdgeInsets.all(20),
          children: [
            Text(trip.title, style: Theme.of(context).textTheme.headlineMedium),
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
            Text('Experiences', style: Theme.of(context).textTheme.titleLarge),
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
            if (draft) ...[
              const SizedBox(height: 14),
              OutlinedButton(
                onPressed: _busy ? null : () => _edit(trip),
                child: const Text('Edit draft'),
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
                child: Text('Confirmed trips cannot be edited or cancelled.'),
              ),
            ] else ...[
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

class _PlannerHero extends StatelessWidget {
  const _PlannerHero({required this.onPlan});
  final VoidCallback onPlan;
  @override
  Widget build(BuildContext context) => Container(
    decoration: BoxDecoration(
      color: BlueversePalette.coastDeep,
      borderRadius: BorderRadius.circular(24),
    ),
    padding: const EdgeInsets.all(22),
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const Text(
          'A LITTLE PLANNING. A LOT OF COAST.',
          style: TextStyle(
            color: BlueversePalette.coastGlass,
            fontSize: 11,
            fontWeight: FontWeight.w800,
            letterSpacing: 1.2,
          ),
        ),
        const SizedBox(height: 10),
        Text(
          'Find your kind of coastal day.',
          style: Theme.of(context).textTheme.headlineMedium
              ?.copyWith(color: Colors.white),
        ),
        const SizedBox(height: 10),
        const Text(
          'Current ideas and considered conditions, ready to make your own.',
          style: TextStyle(color: Colors.white70),
        ),
        const SizedBox(height: 18),
        FilledButton.tonal(onPressed: onPlan, child: const Text('Plan a trip')),
      ],
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
T? _firstWhereOrNull<T>(Iterable<T> values, bool Function(T) predicate) {
  for (final value in values) {
    if (predicate(value)) return value;
  }
  return null;
}
