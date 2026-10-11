import 'package:flutter/material.dart';

import '../data/models/coastal_operations_models.dart';
import 'blueverse_theme.dart';

String coastalRecordTime(String value) {
  final time = DateTime.tryParse(value)?.toLocal();
  if (time == null) return 'Time not available';
  const months = [
    'Jan',
    'Feb',
    'Mar',
    'Apr',
    'May',
    'Jun',
    'Jul',
    'Aug',
    'Sep',
    'Oct',
    'Nov',
    'Dec',
  ];
  return '${months[time.month - 1]} ${time.day}, ${time.year}, ${time.hour % 12 == 0 ? 12 : time.hour % 12}:${time.minute.toString().padLeft(2, '0')} ${time.hour < 12 ? 'AM' : 'PM'}';
}

String _label(String value) => value == 'PROPOSED'
    ? 'Draft'
    : value == 'SUBMITTED'
    ? 'Published for assessment'
    : value
          .toLowerCase()
          .split('_')
          .map(
            (part) => part.isEmpty
                ? part
                : '${part[0].toUpperCase()}${part.substring(1)}',
          )
          .join(' ');

class CoastalOperationsRecordCard extends StatelessWidget {
  const CoastalOperationsRecordCard({
    required this.record,
    this.onOpen,
    this.onActivity,
    this.actions,
    super.key,
  });
  final Object record;
  final VoidCallback? onOpen, onActivity;
  final Widget? actions;
  @override
  Widget build(BuildContext context) {
    final assessment = record is CoastalAssessment
        ? record as CoastalAssessment
        : null;
    final alert = record is CoastalAlert ? record as CoastalAlert : null;
    final id = assessment?.assessmentId ?? alert!.alertId;
    final title = assessment == null
        ? alert!.title
        : assessment.title.isEmpty
        ? assessment.objective
        : assessment.title;
    final content = Padding(
      padding: const EdgeInsets.all(20),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Wrap(
            spacing: 10,
            runSpacing: 8,
            crossAxisAlignment: WrapCrossAlignment.center,
            children: [
              Text(
                _label(assessment?.targetType ?? alert!.targetType),
                style: Theme.of(context).textTheme.labelSmall?.copyWith(
                  color: BlueversePalette.coastMuted,
                  fontWeight: FontWeight.w700,
                ),
              ),
              Container(
                padding: const EdgeInsets.symmetric(
                  horizontal: 10,
                  vertical: 6,
                ),
                decoration: BoxDecoration(
                  color: BlueversePalette.coastSand,
                  borderRadius: BorderRadius.circular(16),
                ),
                child: Text(
                  _label(assessment?.workflowStatus ?? alert!.lifecycle),
                  style: const TextStyle(
                    color: BlueversePalette.coastDeep,
                    fontSize: 12,
                    fontWeight: FontWeight.w600,
                  ),
                ),
              ),
            ],
          ),
          const SizedBox(height: 12),
          Text(
            title,
            style: Theme.of(context).textTheme.titleLarge?.copyWith(
              color: BlueversePalette.coastInk,
              fontWeight: FontWeight.w700,
            ),
          ),
          const SizedBox(height: 8),
          if (assessment != null && assessment.objective != title)
            Text(
              assessment.objective,
              style: const TextStyle(
                color: BlueversePalette.coastMuted,
                height: 1.5,
              ),
            ),
          if (alert != null) ...[
            Text(alert.description, style: const TextStyle(height: 1.5)),
            const SizedBox(height: 8),
            Text(
              '${_label(alert.severity)} · ${alert.visibility == 'PUBLIC' ? 'For coastal visitors' : 'Operations team'}',
            ),
          ],
          const Divider(height: 28, color: BlueversePalette.coastLine),
          Text(
            'ID: $id\n${assessment != null ? 'Period:' : 'Valid'} ${coastalRecordTime(assessment?.periodStartsAt ?? alert!.validFrom)} – ${coastalRecordTime(assessment?.periodEndsAt ?? alert!.validUntil)}\nCreated ${coastalRecordTime(assessment?.createdAt ?? alert!.createdAt)}\nUpdated ${coastalRecordTime(assessment?.updatedAt ?? alert!.updatedAt)}',
            style: Theme.of(context).textTheme.bodySmall,
          ),
          if (onOpen != null)
            const Padding(
              padding: EdgeInsets.only(top: 14),
              child: Text(
                'Open details →',
                style: TextStyle(
                  color: BlueversePalette.coastDeep,
                  fontWeight: FontWeight.w700,
                ),
              ),
            ),
        ],
      ),
    );
    return Card(
      color: BlueversePalette.coastPearl,
      margin: const EdgeInsets.only(bottom: 14),
      clipBehavior: Clip.antiAlias,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(22),
        side: const BorderSide(color: BlueversePalette.coastLine),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          onOpen == null
              ? content
              : InkWell(
                  borderRadius: BorderRadius.circular(22),
                  onTap: onOpen,
                  child: content,
                ),
          if (actions != null)
            Padding(
              padding: const EdgeInsets.fromLTRB(20, 0, 20, 8),
              child: actions!,
            ),
          if (onActivity != null)
            Align(
              alignment: Alignment.centerLeft,
              child: Padding(
                padding: const EdgeInsets.fromLTRB(12, 0, 12, 8),
                child: TextButton.icon(
                  onPressed: onActivity,
                  icon: const Icon(Icons.history),
                  label: const Text('View activity'),
                ),
              ),
            ),
        ],
      ),
    );
  }
}

class CoastalOperationsPagination extends StatelessWidget {
  const CoastalOperationsPagination({
    required this.assessments,
    required this.size,
    required this.count,
    required this.page,
    required this.onSize,
    this.previous,
    this.next,
    super.key,
  });
  final bool assessments;
  final int size, count, page;
  final ValueChanged<int> onSize;
  final VoidCallback? previous, next;
  @override
  Widget build(BuildContext context) => Wrap(
    spacing: 16,
    runSpacing: 12,
    crossAxisAlignment: WrapCrossAlignment.center,
    children: [
      Wrap(
        spacing: 12,
        crossAxisAlignment: WrapCrossAlignment.center,
        children: [
          Text('${assessments ? 'Assessments' : 'Alerts'} per page'),
          DropdownButton<int>(
            value: size,
            items: [5, 10, 25, 50, 100]
                .map(
                  (value) =>
                      DropdownMenuItem(value: value, child: Text('$value')),
                )
                .toList(),
            onChanged: (value) {
              if (value != null) onSize(value);
            },
          ),
        ],
      ),
      Text(
        '$count ${assessments ? 'assessments' : 'alerts'} · Page ${page + 1}',
      ),
      OutlinedButton(onPressed: previous, child: const Text('Previous')),
      OutlinedButton(onPressed: next, child: const Text('Next')),
    ],
  );
}
