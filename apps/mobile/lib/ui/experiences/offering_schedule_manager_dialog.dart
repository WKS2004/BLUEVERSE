import 'package:flutter/material.dart';

import '../../data/models/experience_models.dart';
import '../../data/services/experience_time_zone.dart';
import 'experience_view_model.dart';

class OfferingScheduleManagerDialog extends StatefulWidget {
  const OfferingScheduleManagerDialog({
    super.key,
    required this.viewModel,
    required this.offering,
  });

  final ExperienceViewModel viewModel;
  final OfferingDto offering;

  @override
  State<OfferingScheduleManagerDialog> createState() =>
      _OfferingScheduleManagerDialogState();
}

class _OfferingScheduleManagerDialogState
    extends State<OfferingScheduleManagerDialog> {
  List<ScheduleDto> _schedules = const [];
  bool _isLoading = true;
  bool _isSaving = false;
  String? _errorMessage;
  String? _successMessage;

  @override
  void initState() {
    super.initState();
    _loadSchedules();
  }

  Future<void> _loadSchedules() async {
    setState(() {
      _isLoading = true;
      _errorMessage = null;
    });
    try {
      final schedules = await widget.viewModel.repository.getOfferingSchedules(
        widget.offering.id,
      );
      if (!mounted) return;
      setState(() {
        _schedules = schedules;
        _isLoading = false;
      });
    } on Object {
      if (!mounted) return;
      setState(() {
        _isLoading = false;
        _errorMessage = 'Schedules could not be loaded. Try again.';
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    return AlertDialog(
      title: Text('Departure times for ${widget.offering.title}'),
      content: SizedBox(
        width: 520,
        child: ConstrainedBox(
          constraints: const BoxConstraints(maxHeight: 460),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              if (_errorMessage != null) _notice(_errorMessage!, isError: true),
              if (_successMessage != null)
                _notice(_successMessage!, isError: false),
              if (_isLoading)
                const Expanded(
                  child: Center(child: CircularProgressIndicator()),
                )
              else if (_schedules.isEmpty)
                const Expanded(
                  child: Center(
                    child: Text('No departure times have been added yet.'),
                  ),
                )
              else
                Expanded(
                  child: ListView.separated(
                    shrinkWrap: true,
                    itemCount: _schedules.length,
                    separatorBuilder: (_, _) => const Divider(height: 1),
                    itemBuilder: (context, index) {
                      final schedule = _schedules[index];
                      return ListTile(
                        contentPadding: EdgeInsets.zero,
                        title: Text(
                          ExperienceTimeZone.format(
                            schedule.startsAt,
                            schedule.timeZoneId,
                          ),
                        ),
                        subtitle: Text(
                          'Ends ${ExperienceTimeZone.format(schedule.endsAt, schedule.timeZoneId)} • ${schedule.isActive ? 'Active' : 'Paused'}',
                        ),
                        trailing: Wrap(
                          spacing: 0,
                          children: [
                            IconButton(
                              tooltip: 'Edit departure time',
                              icon: const Icon(Icons.edit_outlined),
                              onPressed: _isSaving
                                  ? null
                                  : () => _editSchedule(schedule),
                            ),
                            IconButton(
                              tooltip: 'Remove departure time',
                              icon: const Icon(Icons.delete_outline),
                              onPressed: _isSaving
                                  ? null
                                  : () => _deleteSchedule(schedule),
                            ),
                          ],
                        ),
                      );
                    },
                  ),
                ),
              const SizedBox(height: 12),
              OutlinedButton.icon(
                onPressed: _isSaving ? null : _addSchedule,
                icon: const Icon(Icons.add),
                label: const Text('Add departure time'),
              ),
            ],
          ),
        ),
      ),
      actions: [
        TextButton(
          onPressed: () => Navigator.pop(context),
          child: const Text('Done'),
        ),
      ],
    );
  }

  Future<void> _addSchedule() async {
    final window = await _pickWindow();
    if (window == null) return;
    setState(() {
      _isSaving = true;
      _errorMessage = null;
      _successMessage = null;
    });
    final created = await widget.viewModel.performCatalogueAction<ScheduleDto>(
      () => widget.viewModel.repository.addOfferingSchedule(
        widget.offering.id,
        CreateScheduleRequest(
          startsAt: window.startsAt,
          endsAt: window.endsAt,
          timeZoneId: window.timeZoneId,
          isActive: window.isActive,
        ),
      ),
      successText: 'Departure time added.',
      refreshCatalog: false,
      refreshManagementCatalog: false,
    );
    if (!mounted) return;
    setState(() {
      _isSaving = false;
      if (created != null) {
        _schedules = [..._schedules, created];
        _successMessage = 'Departure time added.';
      } else {
        _errorMessage =
            widget.viewModel.errorMessage ??
            'The departure time could not be added.';
      }
    });
  }

  Future<void> _editSchedule(ScheduleDto schedule) async {
    if (!ExperienceTimeZone.isSupported(schedule.timeZoneId)) {
      if (!mounted) return;
      setState(() {
        _errorMessage = 'This departure time uses an unsupported time zone. Correct the schedule time zone before editing it.';
      });
      return;
    }
    final window = await _pickWindow(schedule: schedule);
    if (window == null) return;
    setState(() {
      _isSaving = true;
      _errorMessage = null;
      _successMessage = null;
    });
    final updated = await widget.viewModel.performCatalogueAction<ScheduleDto>(
      () => widget.viewModel.repository.updateOfferingSchedule(
        widget.offering.id,
        schedule.id,
        UpdateScheduleRequest(
          startsAt: window.startsAt,
          endsAt: window.endsAt,
          timeZoneId: window.timeZoneId,
          isActive: window.isActive,
        ),
      ),
      successText: 'Departure time updated.',
      refreshCatalog: false,
      refreshManagementCatalog: false,
    );
    if (!mounted) return;
    setState(() {
      _isSaving = false;
      if (updated != null) {
        _schedules = _schedules
            .map((item) => item.id == updated.id ? updated : item)
            .toList(growable: false);
        _successMessage = 'Departure time updated.';
      } else {
        _errorMessage =
            widget.viewModel.errorMessage ??
            'The departure time could not be updated.';
      }
    });
  }

  Future<void> _deleteSchedule(ScheduleDto schedule) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Remove this departure time?'),
        content: const Text(
          'Visitors will no longer be able to check availability for this time.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: const Text('Keep it'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(context, true),
            child: const Text('Remove'),
          ),
        ],
      ),
    );
    if (confirmed != true) return;
    setState(() {
      _isSaving = true;
      _errorMessage = null;
      _successMessage = null;
    });
    final removed = await widget.viewModel.performCatalogueAction<bool>(
      () async {
        await widget.viewModel.repository.deleteOfferingSchedule(
          widget.offering.id,
          schedule.id,
        );
        return true;
      },
      successText: 'Departure time removed.',
      refreshCatalog: false,
      refreshManagementCatalog: false,
    );
    if (!mounted) return;
    setState(() {
      _isSaving = false;
      if (removed == true) {
        _schedules = _schedules
            .where((item) => item.id != schedule.id)
            .toList(growable: false);
        _successMessage = 'Departure time removed.';
      } else {
        _errorMessage =
            widget.viewModel.errorMessage ??
            'The departure time could not be removed.';
      }
    });
  }

  Future<_ScheduleWindow?> _pickWindow({ScheduleDto? schedule}) async {
    final zoneId = schedule?.timeZoneId ?? ExperienceTimeZone.defaultZoneId;
    final startLocal = schedule == null
        ? ExperienceTimeZone.inZone(
            DateTime.now().add(const Duration(days: 1)),
            zoneId,
          )
        : ExperienceTimeZone.inZone(schedule.startsAt, zoneId);
    final endLocal = schedule == null
        ? startLocal.add(
            Duration(minutes: widget.offering.durationMinutes ?? 60),
          )
        : ExperienceTimeZone.inZone(schedule.endsAt, zoneId);
    var startDate = DateTime(startLocal.year, startLocal.month, startLocal.day);
    var endDate = DateTime(endLocal.year, endLocal.month, endLocal.day);
    var startTime = TimeOfDay(hour: startLocal.hour, minute: startLocal.minute);
    var endTime = TimeOfDay(hour: endLocal.hour, minute: endLocal.minute);
    var isActive = schedule?.isActive ?? true;
    String? validationMessage;

    return showDialog<_ScheduleWindow>(
      context: context,
      builder: (dialogContext) => StatefulBuilder(
        builder: (context, setDialogState) => AlertDialog(
          title: Text(
            schedule == null ? 'Add departure time' : 'Edit departure time',
          ),
          content: SingleChildScrollView(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Text('Time zone: $zoneId'),
                const SizedBox(height: 8),
                _dateTimeSelector(
                  context,
                  label: 'Starts',
                  date: startDate,
                  time: startTime,
                  onDate: (date) => setDialogState(() => startDate = date),
                  onTime: (time) => setDialogState(() => startTime = time),
                ),
                const SizedBox(height: 8),
                _dateTimeSelector(
                  context,
                  label: 'Ends',
                  date: endDate,
                  time: endTime,
                  onDate: (date) => setDialogState(() => endDate = date),
                  onTime: (time) => setDialogState(() => endTime = time),
                ),
                SwitchListTile(
                  contentPadding: EdgeInsets.zero,
                  title: const Text('Active for availability checks'),
                  value: isActive,
                  onChanged: (value) => setDialogState(() => isActive = value),
                ),
                if (validationMessage != null)
                  Text(
                    validationMessage!,
                    style: TextStyle(
                      color: Theme.of(context).colorScheme.error,
                    ),
                  ),
              ],
            ),
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(dialogContext),
              child: const Text('Cancel'),
            ),
            FilledButton(
              onPressed: () {
                final startsAt = ExperienceTimeZone.toUtc(
                  date: startDate,
                  hour: startTime.hour,
                  minute: startTime.minute,
                  zoneId: zoneId,
                );
                final endsAt = ExperienceTimeZone.toUtc(
                  date: endDate,
                  hour: endTime.hour,
                  minute: endTime.minute,
                  zoneId: zoneId,
                );
                if (!endsAt.isAfter(startsAt)) {
                  setDialogState(() {
                    validationMessage =
                        'Choose an end time after the start time.';
                  });
                  return;
                }
                Navigator.pop(
                  dialogContext,
                  _ScheduleWindow(
                    startsAt: startsAt,
                    endsAt: endsAt,
                    timeZoneId: zoneId,
                    isActive: isActive,
                  ),
                );
              },
              child: const Text('Save'),
            ),
          ],
        ),
      ),
    );
  }

  Widget _dateTimeSelector(
    BuildContext context, {
    required String label,
    required DateTime date,
    required TimeOfDay time,
    required ValueChanged<DateTime> onDate,
    required ValueChanged<TimeOfDay> onTime,
  }) {
    final localizations = MaterialLocalizations.of(context);
    final today = ExperienceTimeZone.today(ExperienceTimeZone.defaultZoneId);
    final earliestDate = today.subtract(const Duration(days: 1));
    final latestDate = today.add(const Duration(days: 730));
    final firstDate = date.isBefore(earliestDate) ? date : earliestDate;
    final lastDate = date.isAfter(latestDate) ? date : latestDate;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(label, style: const TextStyle(fontWeight: FontWeight.bold)),
        Row(
          children: [
            Expanded(
              child: OutlinedButton.icon(
                icon: const Icon(Icons.calendar_month),
                label: Text(_formatDate(date)),
                onPressed: () async {
                  final picked = await showDatePicker(
                    context: context,
                    initialDate: date,
                    firstDate: firstDate,
                    lastDate: lastDate,
                  );
                  if (picked != null) onDate(picked);
                },
              ),
            ),
            const SizedBox(width: 8),
            OutlinedButton.icon(
              icon: const Icon(Icons.schedule),
              label: Text(localizations.formatTimeOfDay(time)),
              onPressed: () async {
                final picked = await showTimePicker(
                  context: context,
                  initialTime: time,
                );
                if (picked != null) onTime(picked);
              },
            ),
          ],
        ),
      ],
    );
  }

  Widget _notice(String message, {required bool isError}) => Padding(
    padding: const EdgeInsets.only(bottom: 8),
    child: Text(
      message,
      style: TextStyle(
        color: isError ? Colors.red.shade900 : Colors.green.shade900,
      ),
    ),
  );

  String _formatDate(DateTime date) =>
      '${date.year}-${date.month.toString().padLeft(2, '0')}-${date.day.toString().padLeft(2, '0')}';
}

class _ScheduleWindow {
  const _ScheduleWindow({
    required this.startsAt,
    required this.endsAt,
    required this.timeZoneId,
    required this.isActive,
  });

  final DateTime startsAt;
  final DateTime endsAt;
  final String timeZoneId;
  final bool isActive;
}
