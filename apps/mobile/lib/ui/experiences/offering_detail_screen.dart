import 'package:flutter/material.dart';

import '../../data/models/experience_models.dart';
import '../../data/services/experience_time_zone.dart';
import '../auth_view_model.dart';
import '../blueverse_theme.dart';
import 'experience_view_model.dart';

class OfferingDetailScreen extends StatefulWidget {
  const OfferingDetailScreen({
    super.key,
    required this.offeringId,
    required this.viewModel,
    this.authViewModel,
  });

  final String offeringId;
  final ExperienceViewModel viewModel;
  final AuthViewModel? authViewModel;

  @override
  State<OfferingDetailScreen> createState() => _OfferingDetailScreenState();
}

class _OfferingDetailScreenState extends State<OfferingDetailScreen> {
  DateTime _selectedDate = ExperienceTimeZone.today(
    ExperienceTimeZone.defaultZoneId,
  ).add(const Duration(days: 1));
  TimeOfDay _startTime = const TimeOfDay(hour: 9, minute: 0);
  TimeOfDay _endTime = const TimeOfDay(hour: 12, minute: 0);

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      widget.viewModel.loadOfferingDetail(widget.offeringId);
      if (widget.authViewModel?.user != null) {
        widget.viewModel.loadFavourites();
      }
    });
  }

  @override
  Widget build(BuildContext context) {
    return ListenableBuilder(
      listenable: widget.viewModel,
      builder: (context, _) {
        final vm = widget.viewModel;
        final offering = vm.selectedOffering;
        if (vm.isLoadingOffering && offering == null) {
          return Scaffold(
            appBar: AppBar(title: const Text('Loading experience…')),
            body: const Center(child: CircularProgressIndicator()),
          );
        }
        if (offering == null) {
          return Scaffold(
            appBar: AppBar(title: const Text('Experience unavailable')),
            body: Center(
              child: Padding(
                padding: const EdgeInsets.all(24),
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Text(
                      vm.errorMessage ?? 'This experience could not be loaded.',
                      textAlign: TextAlign.center,
                    ),
                    const SizedBox(height: 12),
                    FilledButton(
                      onPressed: () => vm.loadOfferingDetail(widget.offeringId),
                      child: const Text('Try again'),
                    ),
                    TextButton(
                      onPressed: () => Navigator.pop(context),
                      child: const Text('Go back'),
                    ),
                  ],
                ),
              ),
            ),
          );
        }

        final isFavourite = vm.isFavourite('OFFERING', offering.id);
        final activeSchedules = vm.offeringSchedules
            .where((schedule) => schedule.isActive)
            .toList(growable: false);

        return Scaffold(
          appBar: AppBar(
            title: Text(offering.title),
            actions: [
              IconButton(
                tooltip: 'Refresh offering details',
                icon: const Icon(Icons.refresh),
                onPressed: () => vm.loadOfferingDetail(widget.offeringId),
              ),
              IconButton(
                key: const Key('btn-fav-offering-detail'),
                icon: Icon(
                  isFavourite ? Icons.bookmark : Icons.bookmark_border,
                  color: isFavourite ? BlueversePalette.coastDeep : null,
                ),
                tooltip: isFavourite
                    ? 'Remove from wishlist'
                    : 'Save to wishlist',
                onPressed: () => _toggleFavourite(context, offering.id),
              ),
            ],
          ),
          body: SingleChildScrollView(
            padding: const EdgeInsets.all(16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                if (vm.favouriteActionErrorMessage != null)
                  _message(vm.favouriteActionErrorMessage!, isError: true),
                if (vm.successMessage != null)
                  _message(vm.successMessage!, isError: false),
                _buildOfferingCard(context, offering),
                const SizedBox(height: 16),
                Text(
                  'Departure times (${vm.offeringSchedules.length})',
                  style: Theme.of(context).textTheme.titleMedium
                      ?.copyWith(fontWeight: FontWeight.bold),
                ),
                const SizedBox(height: 8),
                if (vm.offeringSchedulesError != null)
                  Card(
                    child: Padding(
                      padding: const EdgeInsets.all(16),
                      child: Column(
                        children: [
                          const Text('Departure times could not be loaded.'),
                          TextButton.icon(
                            onPressed: () => vm.loadOfferingDetail(offering.id),
                            icon: const Icon(Icons.refresh),
                            label: const Text('Try again'),
                          ),
                        ],
                      ),
                    ),
                  )
                else if (vm.offeringSchedules.isEmpty)
                  const Card(
                    child: Padding(
                      padding: EdgeInsets.all(16),
                      child: Text(
                        'No departure times are listed. You can ask the service to assess a time you choose.',
                      ),
                    ),
                  )
                else
                  ...vm.offeringSchedules.map(
                    (schedule) => _buildScheduleCard(vm, offering, schedule),
                  ),
                const SizedBox(height: 16),
                _buildAvailabilityCard(vm, offering, activeSchedules),
              ],
            ),
          ),
        );
      },
    );
  }

  Widget _buildOfferingCard(BuildContext context, OfferingDto offering) {
    return Card(
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(20)),
      child: Padding(
        padding: const EdgeInsets.all(20),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Wrap(
              spacing: 8,
              runSpacing: 8,
              crossAxisAlignment: WrapCrossAlignment.center,
              children: [
                Chip(
                  label: Text(_humanize(offering.status)),
                  backgroundColor: offering.status.toUpperCase() == 'PUBLISHED'
                      ? Colors.teal.shade50
                      : Colors.grey.shade200,
                ),
                Text(
                  offering.price != null
                      ? '${offering.currency ?? 'LKR'} ${offering.price!.toStringAsFixed(0)} per person'
                      : 'Price on request',
                  style: const TextStyle(
                    fontWeight: FontWeight.bold,
                    color: BlueversePalette.coastDeep,
                  ),
                ),
              ],
            ),
            const SizedBox(height: 8),
            Text(
              offering.title,
              style: Theme.of(context).textTheme.headlineSmall
                  ?.copyWith(fontWeight: FontWeight.bold),
            ),
            if (offering.activityName.isNotEmpty ||
                offering.destinationName.isNotEmpty) ...[
              const SizedBox(height: 4),
              Text(
                [
                  if (offering.activityName.isNotEmpty) offering.activityName,
                  if (offering.destinationName.isNotEmpty)
                    offering.destinationName,
                ].join(' • '),
                style: const TextStyle(
                  color: BlueversePalette.coastDeep,
                  fontWeight: FontWeight.w600,
                ),
              ),
            ],
            if (offering.description?.isNotEmpty == true) ...[
              const SizedBox(height: 12),
              Text(offering.description!, style: const TextStyle(height: 1.5)),
            ],
            const SizedBox(height: 14),
            Wrap(
              spacing: 16,
              runSpacing: 8,
              children: [
                if (offering.maxCapacity != null)
                  Text('Maximum group size: ${offering.maxCapacity}'),
                if (offering.durationMinutes != null)
                  Text('Duration: ${offering.durationMinutes} minutes'),
              ],
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildScheduleCard(
    ExperienceViewModel vm,
    OfferingDto offering,
    ScheduleDto schedule,
  ) {
    return Card(
      key: Key('card-schedule-${schedule.id}'),
      margin: const EdgeInsets.only(bottom: 8),
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              ExperienceTimeZone.format(schedule.startsAt, schedule.timeZoneId),
              style: const TextStyle(fontWeight: FontWeight.bold),
            ),
            const SizedBox(height: 4),
            Text(
              'Ends ${ExperienceTimeZone.format(schedule.endsAt, schedule.timeZoneId)} • ${schedule.isActive ? 'Active' : 'Paused'}',
              style: TextStyle(color: Colors.grey.shade700),
            ),
            if (schedule.isActive) ...[
              const SizedBox(height: 8),
              Align(
                alignment: Alignment.centerRight,
                child: OutlinedButton.icon(
                  onPressed: vm.isEvaluatingAvailability
                      ? null
                      : () => _evaluateSchedule(vm, offering.id, schedule),
                  icon: const Icon(Icons.event_available),
                  label: const Text('Check this time'),
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }

  Widget _buildAvailabilityCard(
    ExperienceViewModel vm,
    OfferingDto offering,
    List<ScheduleDto> activeSchedules,
  ) {
    return Card(
      color: BlueversePalette.coastDeep.withAlpha(15),
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(20)),
      child: Padding(
        padding: const EdgeInsets.all(20),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                const Icon(
                  Icons.event_available,
                  color: BlueversePalette.coastDeep,
                ),
                const SizedBox(width: 8),
                Expanded(
                  child: Text(
                    'Check availability',
                    style: Theme.of(context).textTheme.titleSmall?.copyWith(
                      fontWeight: FontWeight.bold,
                      color: BlueversePalette.coastDeep,
                    ),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 8),
            const Text(
              'The service checks the selected time against the schedule and current operational restrictions. This check does not create a reservation.',
              style: TextStyle(fontSize: 13, height: 1.4),
            ),
            const SizedBox(height: 12),
            if (activeSchedules.isEmpty) ...[
              const Text(
                'Choose a time in Sri Lanka Standard Time (Asia/Colombo).',
              ),
              const SizedBox(height: 8),
              Row(
                children: [
                  Expanded(
                    child: OutlinedButton.icon(
                      icon: const Icon(Icons.calendar_month),
                      label: Text(_formatDate(_selectedDate)),
                      onPressed: _pickDate,
                    ),
                  ),
                  const SizedBox(width: 8),
                  Expanded(
                    child: OutlinedButton.icon(
                      icon: const Icon(Icons.schedule),
                      label: Text(
                        '${MaterialLocalizations.of(context).formatTimeOfDay(_startTime)}–${MaterialLocalizations.of(context).formatTimeOfDay(_endTime)}',
                      ),
                      onPressed: _pickTimes,
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 8),
              FilledButton.icon(
                key: const Key('btn-evaluate-availability'),
                onPressed: vm.isEvaluatingAvailability
                    ? null
                    : () => vm.evaluateOfferingAvailability(
                        offeringId: offering.id,
                        startUtc: ExperienceTimeZone.toUtc(
                          date: _selectedDate,
                          hour: _startTime.hour,
                          minute: _startTime.minute,
                        ),
                        endUtc: ExperienceTimeZone.toUtc(
                          date: _selectedDate,
                          hour: _endTime.hour,
                          minute: _endTime.minute,
                        ),
                      ),
                icon: vm.isEvaluatingAvailability
                    ? const SizedBox(
                        width: 18,
                        height: 18,
                        child: CircularProgressIndicator(
                          color: Colors.white,
                          strokeWidth: 2,
                        ),
                      )
                    : const Icon(Icons.check_circle_outline),
                label: Text(
                  vm.isEvaluatingAvailability
                      ? 'Checking…'
                      : 'Check selected time',
                ),
              ),
            ] else
              const Text(
                'Select an active departure above to check its exact scheduled window.',
              ),
            if (vm.availabilityErrorMessage != null) ...[
              const SizedBox(height: 12),
              Text(
                vm.availabilityErrorMessage!,
                style: TextStyle(color: Colors.red.shade900),
              ),
            ],
            if (vm.availabilityResult != null) ...[
              const SizedBox(height: 12),
              _availabilityResult(vm.availabilityResult!),
            ],
          ],
        ),
      ),
    );
  }

  Widget _availabilityResult(AvailabilityEvaluationResponse result) {
    final status = result.status.toUpperCase();
    final isAvailable = status == 'AVAILABLE';
    final isUnknown = status == 'UNKNOWN';
    final MaterialColor color = isAvailable
        ? Colors.green
        : isUnknown
        ? Colors.blueGrey
        : Colors.orange;
    final title = isAvailable
        ? 'Availability confirmed'
        : isUnknown
        ? 'Availability could not be confirmed'
        : 'This time is unavailable';

    return Container(
      key: const Key('box-availability-result'),
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: color.shade50,
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: color.shade300),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Icon(
                isAvailable
                    ? Icons.check_circle
                    : isUnknown
                    ? Icons.help_outline
                    : Icons.info_outline,
                color: color.shade800,
              ),
              const SizedBox(width: 8),
              Expanded(
                child: Text(
                  title,
                  style: TextStyle(
                    color: color.shade900,
                    fontWeight: FontWeight.bold,
                  ),
                ),
              ),
            ],
          ),
          if (result.reasonCodes.isNotEmpty) ...[
            const SizedBox(height: 6),
            Text(
              result.reasonCodes.map(_humanize).join(' • '),
              style: const TextStyle(fontSize: 13, height: 1.4),
            ),
          ],
          if (result.operationalRestriction != null) ...[
            const SizedBox(height: 4),
            Text(
              result.operationalRestriction!.reason ??
                  'An operational restriction is in effect.',
              style: const TextStyle(fontSize: 12, fontWeight: FontWeight.bold),
            ),
          ],
        ],
      ),
    );
  }

  Future<void> _evaluateSchedule(
    ExperienceViewModel vm,
    String offeringId,
    ScheduleDto schedule,
  ) => vm.evaluateOfferingAvailability(
    offeringId: offeringId,
    startUtc: schedule.startsAt,
    endUtc: schedule.endsAt,
  );

  Future<void> _pickDate() async {
    final firstDate = ExperienceTimeZone.today(
      ExperienceTimeZone.defaultZoneId,
    );
    final picked = await showDatePicker(
      context: context,
      initialDate: _selectedDate.isBefore(firstDate)
          ? firstDate
          : _selectedDate,
      firstDate: firstDate,
      lastDate: firstDate.add(const Duration(days: 365)),
    );
    if (picked != null) setState(() => _selectedDate = picked);
  }

  Future<void> _pickTimes() async {
    final start = await showTimePicker(
      context: context,
      initialTime: _startTime,
    );
    if (start == null || !mounted) return;
    final end = await showTimePicker(context: context, initialTime: _endTime);
    if (end == null || !mounted) return;
    setState(() {
      _startTime = start;
      _endTime = end;
    });
  }

  Future<void> _toggleFavourite(BuildContext context, String offeringId) async {
    if (widget.authViewModel?.user == null) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: const Text('Sign in to save coastal experiences.'),
          action: SnackBarAction(
            label: 'Sign in',
            onPressed: () => Navigator.pushNamed(context, '/signin'),
          ),
        ),
      );
      return;
    }
    await widget.viewModel.toggleFavourite('OFFERING', offeringId);
  }

  Widget _message(String message, {required bool isError}) => Padding(
    padding: const EdgeInsets.only(bottom: 12),
    child: DecoratedBox(
      decoration: BoxDecoration(
        color: isError ? Colors.red.shade50 : Colors.teal.shade50,
        borderRadius: BorderRadius.circular(12),
      ),
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Text(
          message,
          style: TextStyle(
            color: isError ? Colors.red.shade900 : Colors.teal.shade900,
          ),
        ),
      ),
    ),
  );

  String _formatDate(DateTime date) =>
      '${date.year}-${date.month.toString().padLeft(2, '0')}-${date.day.toString().padLeft(2, '0')}';

  String _humanize(String value) {
    final words = value.replaceAll('_', ' ').toLowerCase();
    return words
        .split(' ')
        .map(
          (word) => word.isEmpty
              ? word
              : '${word[0].toUpperCase()}${word.substring(1)}',
        )
        .join(' ');
  }
}
