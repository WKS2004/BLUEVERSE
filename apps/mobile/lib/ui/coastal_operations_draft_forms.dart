import 'package:flutter/material.dart';

import '../data/models/coastal_operations_models.dart';
import '../data/services/coastal_operations_api_service.dart';
import 'feedback/loading_screen_controller.dart';

String _offsetLabel(int? minutes) {
  if (minutes == null) return '';
  final value = minutes.abs();
  return ' · UTC${minutes >= 0 ? '+' : '-'}${(value ~/ 60).toString().padLeft(2, '0')}:${(value % 60).toString().padLeft(2, '0')} now';
}

class CoastalAssessmentDraftDialog extends StatelessWidget {
  const CoastalAssessmentDraftDialog({
    required this.apiService,
    this.existing,
    super.key,
  });
  final CoastalOperationsApiService apiService;
  final CoastalAssessment? existing;
  @override
  Widget build(BuildContext context) => CoastalDraftDialog(
    apiService: apiService,
    assessment: existing,
    assessments: true,
  );
}

class CoastalAlertDraftDialog extends StatelessWidget {
  const CoastalAlertDraftDialog({
    required this.apiService,
    this.existing,
    super.key,
  });
  final CoastalOperationsApiService apiService;
  final CoastalAlert? existing;
  @override
  Widget build(BuildContext context) => CoastalDraftDialog(
    apiService: apiService,
    alert: existing,
    assessments: false,
  );
}

class CoastalDraftDialog extends StatefulWidget {
  const CoastalDraftDialog({
    required this.apiService,
    required this.assessments,
    this.assessment,
    this.alert,
    this.embedded = false,
    this.onSaved,
    this.onCancel,
    super.key,
  });
  final CoastalOperationsApiService apiService;
  final bool assessments;
  final CoastalAssessment? assessment;
  final CoastalAlert? alert;
  final bool embedded;
  final ValueChanged<Object>? onSaved;
  final VoidCallback? onCancel;
  @override
  State<CoastalDraftDialog> createState() => _CoastalDraftDialogState();
}

class _CoastalDraftDialogState extends State<CoastalDraftDialog> {
  final _form = GlobalKey<FormState>();
  final _title = TextEditingController();
  final _content = TextEditingController();
  final _start = TextEditingController();
  final _end = TextEditingController();
  String _targetType = 'DESTINATION';
  String _targetId = '';
  String _planId = '';
  String _assessmentId = '';
  String _zoneId = 'Etc/UTC';
  String _severity = 'MODERATE';
  String _visibility = 'OPERATIONS';
  CoastalFormOptions? _options;
  String? _error;
  bool _saving = false;
  static const _emptyId = '00000000-0000-0000-0000-000000000000';
  @override
  void initState() {
    super.initState();
    final a = widget.assessment;
    final n = widget.alert;
    _title.text = a == null
        ? n?.title ?? ''
        : a.title.isNotEmpty
        ? a.title
        : a.objective.substring(0, a.objective.length.clamp(0, 160));
    _content.text = a?.objective ?? n?.description ?? '';
    _targetType = a?.targetType ?? n?.targetType ?? 'DESTINATION';
    _targetId = a?.targetId ?? n?.targetId ?? '';
    if (_targetId == _emptyId) _targetId = '';
    _zoneId = a?.timeZoneId ?? n?.timeZoneId ?? 'Etc/UTC';
    _planId = a?.sourceWorkflowId ?? '';
    _assessmentId = n?.assessmentId ?? '';
    _severity = n?.severity ?? 'MODERATE';
    _visibility = n?.visibility ?? 'OPERATIONS';
    _start.text =
        a?.periodStartsLocal ??
        n?.validFromLocal ??
        _utcLocal(a?.periodStartsAt ?? n?.validFrom);
    _end.text =
        a?.periodEndsLocal ??
        n?.validUntilLocal ??
        _utcLocal(a?.periodEndsAt ?? n?.validUntil);
    _load();
  }

  String _utcLocal(String? value) => value == null
      ? ''
      : DateTime.parse(value).toUtc().toIso8601String().substring(0, 16);
  Future<void> _load() async {
    try {
      final data = await widget.apiService.getFormOptions();
      if (mounted) {
        setState(() {
          _options = data;
          _error = null;
        });
      }
    } on Object {
      if (mounted) {
        setState(
          () => _error =
              'The selection lists could not be loaded. Retry before saving.',
        );
      }
    }
  }

  @override
  void dispose() {
    _title.dispose();
    _content.dispose();
    _start.dispose();
    _end.dispose();
    super.dispose();
  }

  Future<void> _chooseDate(TextEditingController controller) async {
    final current = DateTime.tryParse(controller.text) ?? DateTime.now();
    final date = await showDatePicker(
      context: context,
      initialDate: current,
      firstDate: DateTime(1900),
      lastDate: DateTime(2200),
    );
    if (date == null || !mounted) return;
    final time = await showTimePicker(
      context: context,
      initialTime: TimeOfDay.fromDateTime(current),
    );
    if (time == null || !mounted) return;
    setState(
      () => controller.text = DateTime(
        date.year,
        date.month,
        date.day,
        time.hour,
        time.minute,
      ).toIso8601String().substring(0, 16),
    );
  }

  Future<void> _save() async {
    if (_options == null || !_form.currentState!.validate()) return;
    setState(() {
      _saving = true;
      _error = null;
    });
    try {
      final result = await blueverseLoadingScreenController.track(() async {
        if (widget.assessments) {
          final existing = widget.assessment;
          if (existing != null) {
            return widget.apiService.updateAssessmentDraft(
              assessmentId: existing.assessmentId,
              expectedVersion: existing.version,
              title: _title.text.trim(),
              objective: _content.text.trim(),
              targetType: _targetType,
              targetId: _targetId.isEmpty ? null : _targetId,
              sourceWorkflowId: _planId.isEmpty ? null : _planId,
              timeZoneId: _zoneId,
              periodStartsAt: _start.text,
              periodEndsAt: _end.text,
            );
          }
          return widget.apiService.createAssessment(
            title: _title.text.trim(),
            objective: _content.text.trim(),
            targetType: _targetType,
            targetId: _targetId.isEmpty ? null : _targetId,
            sourceWorkflowId: _planId.isEmpty ? null : _planId,
            timeZoneId: _zoneId,
            periodStartsAt: _start.text,
            periodEndsAt: _end.text,
          );
        }
        final existing = widget.alert;
        if (existing != null) {
          return widget.apiService.updateAlertDraft(
            alertId: existing.alertId,
            expectedVersion: existing.version,
            targetType: _targetType,
            targetId: _targetId.isEmpty ? null : _targetId,
            title: _title.text.trim(),
            description: _content.text.trim(),
            severity: _severity,
            visibility: _visibility,
            timeZoneId: _zoneId,
            validFrom: _start.text,
            validUntil: _end.text,
          );
        }
        return widget.apiService.createAlertDraft(
          targetType: _targetType,
          targetId: _targetId.isEmpty ? null : _targetId,
          assessmentId: _assessmentId.isEmpty ? null : _assessmentId,
          title: _title.text.trim(),
          description: _content.text.trim(),
          severity: _severity,
          visibility: _visibility,
          timeZoneId: _zoneId,
          validFrom: _start.text,
          validUntil: _end.text,
        );
      });
      if (mounted) {
        if (widget.onSaved != null) {
          widget.onSaved!(result);
        } else {
          Navigator.pop(context, result);
        }
      }
    } on CoastalOperationsApiException catch (error) {
      if (mounted) setState(() => _error = error.message);
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  List<CoastalNamedReference> _retain(
    List<CoastalNamedReference> values,
    String id,
    String label,
  ) => id.isNotEmpty && !values.any((item) => item.id == id)
      ? [CoastalNamedReference(id: id, title: label), ...values]
      : values;
  Widget _select(
    String label,
    String value,
    List<DropdownMenuItem<String>> items,
    ValueChanged<String>? onChange,
  ) => Padding(
    padding: const EdgeInsets.only(top: 14),
    child: DropdownButtonFormField<String>(
      key: ValueKey('$label:$value'),
      initialValue: value,
      isExpanded: true,
      decoration: InputDecoration(labelText: label),
      items: items,
      onChanged: onChange == null
          ? null
          : (next) => setState(() => onChange(next ?? '')),
    ),
  );
  List<DropdownMenuItem<String>> _named(
    List<CoastalNamedReference> values,
    String none,
  ) => [
    DropdownMenuItem(value: '', child: Text(none)),
    ...values.map(
      (item) => DropdownMenuItem(
        value: item.id,
        child: Text(item.title, overflow: TextOverflow.ellipsis),
      ),
    ),
  ];
  Widget _date(String label, TextEditingController controller) => Padding(
    padding: const EdgeInsets.only(top: 14),
    child: TextFormField(
      controller: controller,
      readOnly: true,
      decoration: InputDecoration(
        labelText: label,
        suffixIcon: const Icon(Icons.calendar_month),
      ),
      onTap: () => _chooseDate(controller),
      validator: (value) =>
          value == null || value.isEmpty ? 'Choose a date and time.' : null,
    ),
  );
  Widget _draftContent(Widget child) =>
      widget.embedded ? child : SingleChildScrollView(child: child);

  @override
  Widget build(BuildContext context) {
    final options = _options;
    final targets = _retain(
      (options?.targets.items ?? [])
          .where((item) => item.targetType == _targetType)
          .toList(),
      _targetId,
      'Previously linked ${_targetType.toLowerCase()}',
    );
    final dialog = AlertDialog(
      title: Text(widget.assessments ? 'Assessment draft' : 'Advisory draft'),
      content: SizedBox(
        width: 520,
        child: Form(
          key: _form,
          child: _draftContent(
            Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                const Text(
                  'Give your draft a clear title. Link a coastal record before publication.',
                ),
                if (options == null && _error == null)
                  const Padding(
                    padding: EdgeInsets.only(top: 12),
                    child: Text('Loading selection lists…'),
                  ),
                if (_error != null) ...[
                  Text(
                    _error!,
                    style: TextStyle(
                      color: Theme.of(context).colorScheme.error,
                    ),
                  ),
                  if (options == null)
                    TextButton(
                      onPressed: _load,
                      child: const Text('Retry selection lists'),
                    ),
                ],
                const SizedBox(height: 14),
                TextFormField(
                  controller: _title,
                  maxLength: 160,
                  decoration: InputDecoration(
                    labelText: widget.assessments
                        ? 'Assessment title'
                        : 'Advisory title',
                  ),
                  validator: (value) => value == null || value.trim().isEmpty
                      ? 'Add a clear title.'
                      : null,
                ),
                _select(
                  'Coastal record type',
                  _targetType,
                  ['DESTINATION', 'ACTIVITY', 'OFFERING', 'SESSION']
                      .map(
                        (value) =>
                            DropdownMenuItem(value: value, child: Text(value)),
                      )
                      .toList(),
                  _assessmentId.isNotEmpty
                      ? null
                      : (value) {
                          _targetType = value;
                          _targetId = '';
                        },
                ),
                _select(
                  'Coastal record',
                  _targetId,
                  _named(targets, 'Link later'),
                  options == null || _assessmentId.isNotEmpty
                      ? null
                      : (value) => _targetId = value,
                ),
                if (options?.targets.status != 'AVAILABLE')
                  const Padding(
                    padding: EdgeInsets.only(top: 8),
                    child: Text(
                      'The coastal catalogue is not connected yet. Save your draft and link its record before publication.',
                    ),
                  ),
                if (widget.assessments) ...[
                  _select(
                    'Related coastal plan',
                    _planId,
                    _named(
                      _retain(
                        options?.plans.items ?? [],
                        _planId,
                        'Previously linked coastal plan',
                      ),
                      'No related plan',
                    ),
                    options == null ? null : (value) => _planId = value,
                  ),
                  if (options?.plans.status != 'AVAILABLE')
                    const Text(
                      'Coastal plans will appear when the planner is connected.',
                    ),
                ] else ...[
                  _select(
                    'Related assessment',
                    _assessmentId,
                    _named(
                      _retain(
                        options?.assessments ?? [],
                        _assessmentId,
                        'Previously linked assessment',
                      ),
                      'No related assessment',
                    ),
                    options == null || widget.alert != null
                        ? null
                        : (value) {
                            _assessmentId = value;
                            final matches = options.assessments.where(
                              (item) => item.id == value,
                            );
                            if (matches.isNotEmpty &&
                                matches.first.targetId != null &&
                                matches.first.targetType != null) {
                              _targetId = matches.first.targetId!;
                              _targetType = matches.first.targetType!;
                            }
                          },
                  ),
                  _select(
                    'Severity',
                    _severity,
                    ['LOW', 'MODERATE', 'HIGH', 'CRITICAL']
                        .map(
                          (value) => DropdownMenuItem(
                            value: value,
                            child: Text(value),
                          ),
                        )
                        .toList(),
                    (value) => _severity = value,
                  ),
                  _select(
                    'Audience',
                    _visibility,
                    ['OPERATIONS', 'PUBLIC']
                        .map(
                          (value) => DropdownMenuItem(
                            value: value,
                            child: Text(
                              value == 'PUBLIC'
                                  ? 'Coastal visitors'
                                  : 'Operations team',
                            ),
                          ),
                        )
                        .toList(),
                    (value) => _visibility = value,
                  ),
                ],
                if (options != null)
                  _select(
                    'Time zone',
                    _zoneId,
                    options.timeZones
                        .map(
                          (zone) => DropdownMenuItem(
                            value: zone.id,
                            enabled: zone.rulesAvailable,
                            child: Text(
                              '${zone.country} — ${zone.location}${_offsetLabel(zone.currentOffsetMinutes)}',
                              overflow: TextOverflow.ellipsis,
                            ),
                          ),
                        )
                        .toList(),
                    (value) => _zoneId = value,
                  ),
                const Padding(
                  padding: EdgeInsets.only(top: 8),
                  child: Text(
                    'One time zone applies to both dates. Daylight-saving changes are resolved automatically.',
                  ),
                ),
                _date(
                  widget.assessments ? 'Starts at' : 'Visible from',
                  _start,
                ),
                _date(widget.assessments ? 'Ends at' : 'Valid until', _end),
                const SizedBox(height: 14),
                TextFormField(
                  controller: _content,
                  maxLines: 4,
                  maxLength: widget.assessments ? 2000 : 4000,
                  decoration: InputDecoration(
                    labelText: widget.assessments
                        ? 'What should be reviewed?'
                        : 'Advisory message',
                  ),
                  validator: (value) => value == null || value.trim().isEmpty
                      ? 'Describe the coastal question or notice.'
                      : null,
                ),
              ],
            ),
          ),
        ),
      ),
      actions: [
        TextButton(
          style: TextButton.styleFrom(
            foregroundColor: Theme.of(context).colorScheme.error,
          ),
          onPressed: _saving
              ? null
              : () {
                  if (widget.onCancel != null) {
                    widget.onCancel!();
                  } else {
                    Navigator.pop(context);
                  }
                },
          child: const Text('Cancel'),
        ),
        FilledButton(
          onPressed:
              _saving ||
                  options == null ||
                  !options.timeZones.any(
                    (zone) => zone.id == _zoneId && zone.rulesAvailable,
                  )
              ? null
              : _save,
          child: Text(
            _saving
                ? 'Saving…'
                : widget.assessment != null || widget.alert != null
                ? 'Update draft'
                : 'Save draft',
          ),
        ),
      ],
    );
    if (!widget.embedded) return dialog;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        dialog.content!,
        const SizedBox(height: 16),
        Wrap(spacing: 12, runSpacing: 8, children: dialog.actions!),
      ],
    );
  }
}
