import 'package:flutter/material.dart';

import 'coastal_operations_record_card.dart';
import '../data/models/coastal_operations_models.dart';
import '../data/services/coastal_operations_api_service.dart';

class CoastalOperationsActivity extends StatefulWidget {
  const CoastalOperationsActivity({
    required this.apiService,
    required this.id,
    required this.assessment,
    super.key,
  });
  final CoastalOperationsApiService apiService;
  final String id;
  final bool assessment;
  @override
  State<CoastalOperationsActivity> createState() =>
      _CoastalOperationsActivityState();
}

class _CoastalOperationsActivityState extends State<CoastalOperationsActivity> {
  List<CoastalAuditItem> _items = const [];
  String? _cursor;
  String? _error;
  bool _busy = false;
  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load({bool more = false}) async {
    if (_busy) return;
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      final page = await (widget.assessment
          ? widget.apiService.getAssessmentAudit(
              widget.id,
              cursor: more ? _cursor : null,
            )
          : widget.apiService.getAlertAudit(
              widget.id,
              cursor: more ? _cursor : null,
            ));
      if (!mounted) return;
      setState(() {
        _items = more ? [..._items, ...page.items] : page.items;
        _cursor = page.nextCursor;
      });
    } on Object catch (error) {
      if (mounted) {
        setState(
          () => _error = error is CoastalOperationsApiException
              ? error.message
              : 'Activity could not be loaded. Please retry.',
        );
      }
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) => SafeArea(
    child: Padding(
      padding: const EdgeInsets.all(20),
      child: Column(
        children: [
          Row(
            children: [
              Expanded(
                child: Text(
                  'Record activity',
                  style: Theme.of(context).textTheme.titleLarge,
                ),
              ),
              IconButton(
                tooltip: 'Close activity',
                onPressed: () => Navigator.pop(context),
                icon: const Icon(Icons.close),
              ),
            ],
          ),
          const Text(
            'Draft changes, publication and decisions, newest first. Times use your local time zone.',
          ),
          if (_error != null) Text(_error!, semanticsLabel: _error),
          if (_error != null)
            TextButton(
              onPressed: _busy ? null : () => _load(more: _cursor != null),
              child: const Text('Retry activity'),
            ),
          if (_busy) const LinearProgressIndicator(),
          Expanded(
            child: ListView(
              children: [
                if (!_busy && _error == null && _items.isEmpty)
                  const ListTile(title: Text('No activity recorded yet.')),
                for (final item in _items)
                  ExpansionTile(
                    title: Text(
                      item.summary ??
                          _actions[item.action] ??
                          item.action.toLowerCase().replaceAll('_', ' '),
                    ),
                    subtitle: Text(
                      '${item.actorName ?? (item.actorId == '00000000-0000-0000-0000-000000000000' ? 'System' : 'Team member (name not recorded)')} · ${item.actorRoles.isEmpty ? 'Role not recorded' : item.actorRoles.join(', ')}\n${DateTime.tryParse(item.createdAt)?.toLocal().toString() ?? item.createdAt}',
                    ),
                    children: [
                      if (item.recordTitle != null) Text(item.recordTitle!),
                      if (item.changes.isEmpty)
                        const Padding(
                          padding: EdgeInsets.all(12),
                          child: Text(
                            'Field details were not recorded for this event.',
                          ),
                        ),
                      for (final change in item.changes)
                        ListTile(
                          title: Text(_field(change.field)),
                          subtitle: SelectableText(
                            'Before: ${_value(change.before, change.field)}\nAfter: ${_value(change.after, change.field)}',
                          ),
                        ),
                      Padding(
                        padding: const EdgeInsets.all(12),
                        child: SelectableText(
                          'Actor: ${item.actorId}\nReference: ${item.correlationId}',
                        ),
                      ),
                    ],
                  ),
                if (_cursor != null)
                  TextButton(
                    onPressed: _busy ? null : () => _load(more: true),
                    child: const Text('Load earlier activity'),
                  ),
              ],
            ),
          ),
        ],
      ),
    ),
  );
}

String _field(String field) {
  const names = {
    'AiDependencyStatus': 'Context availability',
    'AiDispatchOutcome': 'Review delivery status',
    'AiDispatchRetryable': 'Retry available',
    'TargetId': 'Related coastal record',
    'SourceWorkflowId': 'Related coastal plan',
    'PeriodStartsAt': 'Period starts',
    'PeriodEndsAt': 'Period ends',
    'ValidFrom': 'Valid from',
    'ValidUntil': 'Valid until',
    'TimeZoneId': 'Time zone',
    'WorkflowStatus': 'Assessment status',
    'InspectionStatus': 'Image status',
    'ByteLength': 'Image size (bytes)',
    'AppliedOperationalState': 'Applied coastal state',
    'WorkflowStatusAfterDecision': 'Status after decision',
  };
  return names[field] ??
      field.replaceAllMapped(
        RegExp(r'([a-z])([A-Z])'),
        (m) => '${m[1]} ${m[2]}',
      );
}

const _actions = {
  'CREATED': 'Created a draft',
  'DRAFT_UPDATED': 'Updated the draft',
  'UPDATED': 'Updated the record',
  'SUBMITTED': 'Published for assessment',
  'CANCELLED': 'Cancelled the draft',
  'DRAFT_WITHDRAWN': 'Withdrew the draft',
  'WITHDRAWN': 'Withdrew the record',
  'PUBLISH': 'Published the advisory',
  'RESOLVE': 'Resolved the advisory',
  'UPLOADED': 'Attached an evidence image',
  'REMOVED': 'Removed the draft evidence image',
  'EXPIRED': 'Expired the retained content',
  'DECISION_APPROVE': 'Approved the recommendation',
  'DECISION_REJECT': 'Rejected the recommendation',
  'DECISION_REQUEST_REVISION': 'Requested a revision',
};
String _value(String? value, String field) {
  if (value == null || value.isEmpty) return 'Not set';
  if (RegExp(r'At$|^ValidFrom$|^ValidUntil$').hasMatch(field) &&
      DateTime.tryParse(value) != null) {
    return coastalRecordTime(value);
  }
  if ([
    'TargetType',
    'WorkflowStatus',
    'Lifecycle',
    'Severity',
    'Visibility',
    'InspectionStatus',
    'Decision',
    'AppliedOperationalState',
    'WorkflowStatusAfterDecision',
  ].contains(field)) {
    return value
        .toLowerCase()
        .split('_')
        .map(
          (part) => part.isEmpty
              ? part
              : '${part[0].toUpperCase()}${part.substring(1)}',
        )
        .join(' ');
  }
  return value;
}
