import 'dart:async';

import 'package:flutter/material.dart';
import 'package:image_picker/image_picker.dart';

import '../data/models/coastal_operations_models.dart';
import '../data/services/coastal_operations_api_service.dart';
import '../features/coastal_operations/coastal_operations_permissions.dart';
import 'account_screens.dart';
import 'auth_view_model.dart';
import 'blueverse_theme.dart';
import 'feedback/loading_screen_controller.dart';

const _coastalTargetTypes = ['DESTINATION', 'ACTIVITY', 'OFFERING', 'SESSION'];
const _uuidPattern =
    r'^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-8][0-9a-fA-F]{3}-[89aAbB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}$';
const _explicitInstantPattern =
    r'T\d{2}:\d{2}(?::\d{2}(?:\.\d+)?)?(?:Z|[+-](?:0\d|1[0-3]):[0-5]\d|[+-]14:00)$';
const _maxEvidenceBytes = 5 * 1024 * 1024;

Future<Object?> _settle<T>(Future<T> future) async {
  try {
    return await future;
  } on Object {
    return null;
  }
}

bool _has(List<String> grants, String permission) =>
    grants.any((grant) => grant.toLowerCase() == permission);

String _humanize(String value) => value
    .toLowerCase()
    .split('_')
    .map(
      (part) =>
          part.isEmpty ? part : '${part[0].toUpperCase()}${part.substring(1)}',
    )
    .join(' ');

String _shortId(String value) => value.length > 14
    ? '${value.substring(0, 8)}…${value.substring(value.length - 4)}'
    : value;

String _formatDate(String value) {
  final date = DateTime.tryParse(value)?.toLocal();
  if (date == null) return 'Time not available';
  final hour = date.hour % 12 == 0 ? 12 : date.hour % 12;
  final minute = date.minute.toString().padLeft(2, '0');
  final period = date.hour < 12 ? 'am' : 'pm';
  return '${date.day} ${_month(date.month)} ${date.year}, $hour:$minute $period';
}

String _month(int month) => const [
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
][month - 1];

bool _validUuid(String value) => RegExp(_uuidPattern).hasMatch(value.trim());

bool _validInstant(String value) {
  return RegExp(_explicitInstantPattern).hasMatch(value.trim()) &&
      DateTime.tryParse(value.trim()) != null;
}

bool _validPeriod(String startsAt, String endsAt) {
  final start = DateTime.tryParse(startsAt.trim());
  final end = DateTime.tryParse(endsAt.trim());
  return _validInstant(startsAt) &&
      _validInstant(endsAt) &&
      start != null &&
      end != null &&
      end.isAfter(start);
}

class CoastalOperationsScreen extends StatefulWidget {
  const CoastalOperationsScreen({
    required this.viewModel,
    required this.apiService,
    super.key,
  });

  final AuthViewModel viewModel;
  final CoastalOperationsApiService apiService;

  @override
  State<CoastalOperationsScreen> createState() =>
      _CoastalOperationsScreenState();
}

class _CoastalOperationsScreenState extends State<CoastalOperationsScreen> {
  List<CoastalAssessment> _assessments = const [];
  List<CoastalAlert> _alerts = const [];
  String? _assessmentError;
  String? _alertError;
  String? _notice;
  bool _noticeIsError = false;
  bool _loading = true;
  bool _busy = false;
  String? _loadedUserId;
  final Map<String, CoastalAssessmentDetail> _details = {};
  final Map<String, CoastalOperationalStatus> _statusByAssessment = {};
  final Map<String, List<CoastalHistoryItem>> _historyByAssessment = {};
  final Map<String, String> _detailErrors = {};
  final _lookupFormKey = GlobalKey<FormState>();
  final _lookupIdController = TextEditingController();
  String _lookupType = 'DESTINATION';
  CoastalOperationalStatus? _lookupStatus;
  List<CoastalHistoryItem> _lookupHistory = const [];
  String? _lookupError;

  List<String> get _permissions =>
      widget.viewModel.user?.permissions ?? const [];

  bool get _canReadAssessments =>
      _has(_permissions, CoastalOperationsPermissions.assessmentRead);
  bool get _canReadQueue =>
      _canReadAssessments &&
      _has(_permissions, CoastalOperationsPermissions.assessmentQueueRead);
  bool get _canCreateAssessment =>
      _has(_permissions, CoastalOperationsPermissions.assessmentCreate);
  bool get _canDecideAssessment =>
      _has(_permissions, CoastalOperationsPermissions.assessmentDecide);
  bool get _canUploadEvidence =>
      _has(_permissions, CoastalOperationsPermissions.evidenceUpload);
  bool get _canReadEvidence =>
      _has(_permissions, CoastalOperationsPermissions.evidenceRead);
  bool get _canReadStatus =>
      _has(_permissions, CoastalOperationsPermissions.targetStatusRead);
  bool get _canReadHistory =>
      _has(_permissions, CoastalOperationsPermissions.targetHistoryRead);
  bool get _canReadAlerts =>
      _has(_permissions, CoastalOperationsPermissions.alertRead);
  bool get _canManageAlerts =>
      _has(_permissions, CoastalOperationsPermissions.alertManage);
  bool get _canDecideAlerts =>
      _has(_permissions, CoastalOperationsPermissions.alertDecide);
  bool get _hasAccess => CoastalOperationsPermissions.hasAccess(_permissions);

  @override
  void initState() {
    super.initState();
    widget.viewModel.addListener(_syncAccount);
    _loadedUserId = widget.viewModel.user?.id;
    if (widget.viewModel.user == null && !widget.viewModel.isLoading) {
      WidgetsBinding.instance.addPostFrameCallback((_) {
        if (mounted && widget.viewModel.user == null) {
          unawaited(widget.viewModel.restore());
        }
      });
    } else if (widget.viewModel.user != null) {
      unawaited(_loadData());
    }
  }

  @override
  void dispose() {
    widget.viewModel.removeListener(_syncAccount);
    _lookupIdController.dispose();
    super.dispose();
  }

  void _syncAccount() {
    final id = widget.viewModel.user?.id;
    if (id == _loadedUserId) return;
    _loadedUserId = id;
    if (id != null) unawaited(_loadData());
    if (mounted) setState(() {});
  }

  Future<void> _loadData() async {
    if (!_hasAccess) {
      if (mounted) setState(() => _loading = false);
      return;
    }
    setState(() {
      _loading = true;
      _assessmentError = null;
      _alertError = null;
    });
    await blueverseLoadingScreenController.track(() async {
      final results = await Future.wait<Object?>([
        _canReadAssessments
            ? _settle(widget.apiService.listAssessments())
            : Future<Object?>.value(null),
        _canReadAlerts
            ? _settle(widget.apiService.listAlerts())
            : Future<Object?>.value(null),
      ]);
      if (!mounted) return;
      final assessmentResult = results[0];
      final alertResult = results[1];
      setState(() {
        if (assessmentResult is CoastalPage<CoastalAssessment>) {
          _assessments = assessmentResult.items;
        } else if (_canReadAssessments) {
          _assessmentError =
              'We could not load assessments. Check your connection and retry.';
        }
        if (alertResult is CoastalPage<CoastalAlert>) {
          _alerts = alertResult.items;
        } else if (_canReadAlerts) {
          _alertError =
              'We could not load advisories. Check your connection and retry.';
        }
        _loading = false;
      });
    });
  }

  Future<void> _loadDetail(CoastalAssessment assessment) async {
    if (_details.containsKey(assessment.assessmentId)) return;
    setState(() => _detailErrors.remove(assessment.assessmentId));
    try {
      final detail = await blueverseLoadingScreenController.track(
        () => widget.apiService.getAssessmentDetail(assessment.assessmentId),
      );
      CoastalOperationalStatus? status;
      List<CoastalHistoryItem> history = const [];
      final results = await Future.wait<Object?>([
        _canReadStatus
            ? _settle(
                widget.apiService.getTargetStatus(
                  targetType: detail.assessment.targetType,
                  targetId: detail.assessment.targetId,
                ),
              )
            : Future<Object?>.value(null),
        _canReadHistory
            ? _settle(
                widget.apiService.getTargetHistory(
                  targetType: detail.assessment.targetType,
                  targetId: detail.assessment.targetId,
                ),
              )
            : Future<Object?>.value(null),
      ]);
      if (results[0] case final CoastalOperationalStatus operationalStatus) {
        status = operationalStatus;
      }
      if (results[1] case final CoastalPage<CoastalHistoryItem> historyPage) {
        history = historyPage.items;
      }
      if (!mounted) {
        return;
      }
      setState(() {
        _details[assessment.assessmentId] = detail;
        if (status != null) {
          _statusByAssessment[assessment.assessmentId] = status;
        }
        _historyByAssessment[assessment.assessmentId] = history;
        if ((_canReadStatus && status == null) ||
            (_canReadHistory && results[1] == null)) {
          _detailErrors[assessment.assessmentId] =
              'Some operational history is not available for this record yet.';
        }
      });
    } on CoastalOperationsApiException catch (error) {
      if (mounted) {
        setState(() => _detailErrors[assessment.assessmentId] = error.message);
      }
    } on Object {
      if (mounted) {
        setState(
          () => _detailErrors[assessment.assessmentId] =
              'We could not open this assessment. Refresh and try again.',
        );
      }
    }
  }

  Future<void> _refreshDetail(String assessmentId) async {
    _details.remove(assessmentId);
    await _loadDetail(
      _assessments.firstWhere(
        (assessment) => assessment.assessmentId == assessmentId,
      ),
    );
  }

  Future<void> _openAssessmentForm() async {
    final created = await showDialog<CoastalAssessment>(
      context: context,
      builder: (_) => _AssessmentFormDialog(apiService: widget.apiService),
    );
    if (created == null || !mounted) return;
    setState(() {
      _notice = 'The assessment was recorded. Its linked coastal context is ready to review.';
      _noticeIsError = false;
    });
    await _loadData();
  }

  Future<void> _openAlertForm({CoastalAlert? existing}) async {
    final saved = await showDialog<CoastalAlert>(
      context: context,
      builder: (_) =>
          _AlertFormDialog(apiService: widget.apiService, existing: existing),
    );
    if (saved == null || !mounted) return;
    setState(() {
      _notice =
          '“${saved.title}” is saved as a proposed draft. It has not been published.';
      _noticeIsError = false;
    });
    await _loadData();
  }

  Future<void> _decideAlert(CoastalAlert alert, String decision) async {
    final action = decision == 'PUBLISH' ? 'Publish' : 'Resolve';
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: Text('$action this advisory?'),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              '“${alert.title}” will be ${decision == 'PUBLISH' ? 'made active' : 'marked resolved'}.',
            ),
            if (decision == 'PUBLISH' &&
                (alert.severity == 'HIGH' || alert.severity == 'CRITICAL')) ...[
              const SizedBox(height: 12),
              const Text(
                'A different authorized reviewer from the draft creator and linked assessment initiator must publish this advisory.',
              ),
            ],
          ],
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: const Text('Cancel'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(context, true),
            child: Text('Confirm $action'.toLowerCase()),
          ),
        ],
      ),
    );
    if (confirmed != true) return;
    setState(() => _busy = true);
    try {
      await blueverseLoadingScreenController.track(
        () => widget.apiService.decideAlert(
          alertId: alert.alertId,
          decision: decision,
          expectedVersion: alert.version,
        ),
      );
      if (!mounted) return;
      setState(() {
        _notice = decision == 'PUBLISH'
            ? 'The advisory is now active.'
            : 'The advisory has been resolved.';
        _noticeIsError = false;
      });
      await _loadData();
    } on CoastalOperationsApiException catch (error) {
      if (mounted) {
        setState(() {
          _notice = error.message;
          _noticeIsError = true;
        });
      }
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _uploadEvidence(CoastalAssessment assessment) async {
    if ((_details[assessment.assessmentId]?.evidence.length ?? 0) >= 5) {
      _setNotice(
        'This assessment already has five evidence images.',
        error: true,
      );
      return;
    }
    try {
      final picked = await ImagePicker().pickImage(
        source: ImageSource.gallery,
        requestFullMetadata: false,
      );
      if (picked == null) return;
      if (!picked.name.toLowerCase().endsWith('.png')) {
        _setNotice(
          'Choose a PNG image. Other image formats are not accepted.',
          error: true,
        );
        return;
      }
      final bytes = await picked.readAsBytes();
      if (bytes.length > _maxEvidenceBytes) {
        _setNotice('That image is larger than the 5 MiB limit.', error: true);
        return;
      }
      setState(() => _busy = true);
      await blueverseLoadingScreenController.track(
        () => widget.apiService.uploadAssessmentEvidence(
          assessmentId: assessment.assessmentId,
          fileName: picked.name,
          bytes: bytes,
        ),
      );
      if (!mounted) return;
      setState(() {
        _notice = 'Evidence was added to the assessment.';
        _noticeIsError = false;
      });
      await _refreshDetail(assessment.assessmentId);
    } on CoastalOperationsApiException catch (error) {
      _setNotice(error.message, error: true);
    } on Object {
      _setNotice(
        'We could not select or upload that image. Please retry.',
        error: true,
      );
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _viewEvidence(
    String assessmentId,
    CoastalEvidence evidence,
  ) async {
    try {
      final bytes = await blueverseLoadingScreenController.track(
        () => widget.apiService.getEvidenceImage(
          assessmentId: assessmentId,
          evidenceId: evidence.evidenceId,
        ),
      );
      if (!mounted) return;
      await showDialog<void>(
        context: context,
        builder: (context) => Dialog(
          child: Padding(
            padding: const EdgeInsets.all(12),
            child: Image.memory(
              bytes,
              fit: BoxFit.contain,
              errorBuilder: (_, _, _) => const Padding(
                padding: EdgeInsets.all(24),
                child: Text('This evidence image could not be displayed.'),
              ),
            ),
          ),
        ),
      );
    } on CoastalOperationsApiException catch (error) {
      _setNotice(error.message, error: true);
    }
  }

  Future<void> _lookupTarget() async {
    if (!(_lookupFormKey.currentState?.validate() ?? false)) return;
    setState(() {
      _lookupError = null;
      _lookupStatus = null;
      _lookupHistory = const [];
    });
    final results = await Future.wait<Object?>([
      _canReadStatus
          ? _settle(
              widget.apiService.getTargetStatus(
                targetType: _lookupType,
                targetId: _lookupIdController.text.trim(),
              ),
            )
          : Future<Object?>.value(null),
      _canReadHistory
          ? _settle(
              widget.apiService.getTargetHistory(
                targetType: _lookupType,
                targetId: _lookupIdController.text.trim(),
              ),
            )
          : Future<Object?>.value(null),
    ]);
    if (!mounted) return;
    final status = results[0];
    final history = results[1];
    setState(() {
      if (status is CoastalOperationalStatus) _lookupStatus = status;
      if (history is CoastalPage<CoastalHistoryItem>) {
        _lookupHistory = history.items;
      }
      if (status == null && history == null) {
        _lookupError = 'That coastal record is not available to your account. Check its ID and permissions, then retry.';
      } else if ((_canReadStatus && status == null) ||
          (_canReadHistory && history == null)) {
        _lookupError = 'Some coastal status or history is not available yet.';
      }
    });
  }

  void _setNotice(String message, {required bool error}) {
    if (!mounted) return;
    setState(() {
      _notice = message;
      _noticeIsError = error;
    });
  }

  Widget _assessmentCard(CoastalAssessment assessment) {
    final detail = _details[assessment.assessmentId];
    final status = _statusByAssessment[assessment.assessmentId];
    final history = _historyByAssessment[assessment.assessmentId] ?? const [];
    final detailError = _detailErrors[assessment.assessmentId];
    return Card(
      child: ExpansionTile(
        key: PageStorageKey<String>('assessment-${assessment.assessmentId}'),
        onExpansionChanged: (expanded) {
          if (expanded) unawaited(_loadDetail(assessment));
        },
        title: Text(
          assessment.objective,
          style: const TextStyle(fontWeight: FontWeight.w700),
        ),
        subtitle: Padding(
          padding: const EdgeInsets.only(top: 6),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                '${_humanize(assessment.targetType)} · ${_shortId(assessment.targetId)}',
              ),
              const SizedBox(height: 6),
              _StatusPill(value: assessment.workflowStatus),
            ],
          ),
        ),
        childrenPadding: const EdgeInsets.fromLTRB(16, 0, 16, 18),
        children: [
          if (detailError != null)
            _NoticeBox(text: detailError, error: true)
          else if (detail == null)
            const Padding(
              padding: EdgeInsets.all(12),
              child: LinearProgressIndicator(),
            )
          else ...[
            Align(
              alignment: Alignment.centerLeft,
              child: Text(
                'Period: ${_formatDate(detail.assessment.periodStartsAt)} – ${_formatDate(detail.assessment.periodEndsAt)}',
                style: Theme.of(context).textTheme.bodySmall,
              ),
            ),
            const SizedBox(height: 14),
            _InfoSurface(
              title: 'Automated operations support',
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(_humanize(detail.assessment.aiDependencyStatus)),
                  const SizedBox(height: 4),
                  Text(_humanize(detail.assessment.aiDispatchOutcome)),
                ],
              ),
            ),
            if (detail.assessment.aiDependencyStatus == 'NOT_CONNECTED') ...[
              const SizedBox(height: 12),
              const _NoticeBox(
                text: 'Automated operations support is not connected yet. This assessment is saved without an automated proposal, so no operational change has been suggested or applied.',
              ),
            ],
            if (detail.assessment.aiDependencyStatus == 'UNAVAILABLE') ...[
              const SizedBox(height: 12),
              const _NoticeBox(
                text: 'Automated operations support is temporarily unavailable. The assessment remains recorded; retry when the service is available.',
              ),
            ],
            if (detail.assessment.componentDependencies.isNotEmpty) ...[
              const SizedBox(height: 16),
              Align(
                alignment: Alignment.centerLeft,
                child: Text(
                  'Coastal context checks',
                  style: Theme.of(context).textTheme.titleSmall,
                ),
              ),
              const SizedBox(height: 8),
              ...detail.assessment.componentDependencies.map(
                (dependency) => ListTile(
                  dense: true,
                  contentPadding: EdgeInsets.zero,
                  title: Text(_dependencyLabel(dependency.service)),
                  subtitle: Text(_humanize(dependency.status)),
                  trailing: dependency.checkedAt == null
                      ? null
                      : Text(
                          _formatDate(dependency.checkedAt!),
                          style: Theme.of(context).textTheme.labelSmall,
                        ),
                ),
              ),
            ],
            if (_canDecideAssessment) ...[
              const SizedBox(height: 10),
              const _NoticeBox(
                text: 'Reviewer decisions will be available when this assessment contains a validated proposal. There is no proposal to approve or apply yet.',
              ),
            ],
            if (detail.decisions.isNotEmpty) ...[
              const SizedBox(height: 16),
              Align(
                alignment: Alignment.centerLeft,
                child: Text(
                  'Recorded decisions',
                  style: Theme.of(context).textTheme.titleSmall,
                ),
              ),
              ...detail.decisions.map(
                (decision) => ListTile(
                  dense: true,
                  contentPadding: EdgeInsets.zero,
                  title: Text(_humanize(decision.decision)),
                  subtitle: Text(_formatDate(decision.decidedAt)),
                ),
              ),
            ],
            if (_canReadEvidence || _canUploadEvidence) ...[
              const SizedBox(height: 16),
              Row(
                children: [
                  Expanded(
                    child: Text(
                      'Evidence',
                      style: Theme.of(context).textTheme.titleSmall,
                    ),
                  ),
                  if (_canUploadEvidence && detail.evidence.length < 5)
                    TextButton.icon(
                      onPressed: _busy
                          ? null
                          : () => _uploadEvidence(assessment),
                      icon: const Icon(Icons.add_photo_alternate_outlined),
                      label: const Text('Add PNG'),
                    ),
                ],
              ),
              if (_canReadEvidence && detail.evidence.isEmpty)
                const Align(
                  alignment: Alignment.centerLeft,
                  child: Text('No evidence images have been added.'),
                ),
              if (!_canReadEvidence && _canUploadEvidence)
                const Align(
                  alignment: Alignment.centerLeft,
                  child: Text('Evidence details require separate read access.'),
                ),
              if (_canReadEvidence)
                ...detail.evidence.map(
                  (evidence) => ListTile(
                    dense: true,
                    contentPadding: EdgeInsets.zero,
                    title: Text(
                      '${(evidence.byteLength / 1024).round()} KiB PNG',
                    ),
                    subtitle: Text(_humanize(evidence.inspectionStatus)),
                    trailing: IconButton(
                      tooltip: 'View evidence image',
                      onPressed: () =>
                          _viewEvidence(assessment.assessmentId, evidence),
                      icon: const Icon(Icons.visibility_outlined),
                    ),
                  ),
                ),
            ],
            if (_canReadStatus || _canReadHistory) ...[
              const SizedBox(height: 16),
              Align(
                alignment: Alignment.centerLeft,
                child: Text(
                  'Operational status and history',
                  style: Theme.of(context).textTheme.titleSmall,
                ),
              ),
              if (status != null)
                ListTile(
                  dense: true,
                  contentPadding: EdgeInsets.zero,
                  title: Text(_humanize(status.operationalState)),
                  subtitle: Text(
                    'Updated ${_formatDate(status.updatedAt)} · version ${status.stateVersion}',
                  ),
                  leading: const Icon(Icons.waves_outlined),
                ),
              if (_canReadHistory && history.isEmpty)
                const Align(
                  alignment: Alignment.centerLeft,
                  child: Text(
                    'No operational state changes have been recorded.',
                  ),
                ),
              ...history.map(
                (item) => ListTile(
                  dense: true,
                  contentPadding: EdgeInsets.zero,
                  title: Text(
                    '${_humanize(item.previousState)} → ${_humanize(item.newState)}',
                  ),
                  subtitle: Text(_formatDate(item.createdAt)),
                ),
              ),
            ],
          ],
        ],
      ),
    );
  }

  Widget _alertCard(CoastalAlert alert) {
    final canEdit = _canManageAlerts && alert.lifecycle == 'PROPOSED';
    final canPublish = _canDecideAlerts && alert.lifecycle == 'PROPOSED';
    final canResolve = _canDecideAlerts && alert.lifecycle == 'ACTIVE';
    return Card(
      child: Padding(
        padding: const EdgeInsets.fromLTRB(16, 16, 16, 12),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        '${_humanize(alert.targetType)} · ${_shortId(alert.targetId)}',
                        style: Theme.of(context).textTheme.labelSmall,
                      ),
                      const SizedBox(height: 6),
                      Text(
                        alert.title,
                        style: Theme.of(context).textTheme.titleMedium,
                      ),
                    ],
                  ),
                ),
                _StatusPill(value: alert.lifecycle),
              ],
            ),
            const SizedBox(height: 10),
            Text(alert.description, style: const TextStyle(height: 1.45)),
            const SizedBox(height: 10),
            Wrap(
              spacing: 8,
              runSpacing: 6,
              children: [
                _StatusPill(value: alert.severity),
                Chip(
                  label: Text(
                    alert.visibility == 'PUBLIC'
                        ? 'For coastal visitors'
                        : 'Operations team',
                  ),
                  backgroundColor: BlueversePalette.coastSand,
                  visualDensity: VisualDensity.compact,
                ),
              ],
            ),
            const SizedBox(height: 6),
            Text(
              'Valid ${_formatDate(alert.validFrom)} – ${_formatDate(alert.validUntil)}',
              style: Theme.of(context).textTheme.bodySmall,
            ),
            if (canEdit || canPublish || canResolve) ...[
              const SizedBox(height: 8),
              Wrap(
                spacing: 8,
                runSpacing: 6,
                children: [
                  if (canEdit)
                    TextButton(
                      onPressed: () => _openAlertForm(existing: alert),
                      child: const Text('Edit draft'),
                    ),
                  if (canPublish)
                    FilledButton.tonal(
                      onPressed: _busy
                          ? null
                          : () => _decideAlert(alert, 'PUBLISH'),
                      child: const Text('Publish advisory'),
                    ),
                  if (canResolve)
                    OutlinedButton(
                      onPressed: _busy
                          ? null
                          : () => _decideAlert(alert, 'RESOLVE'),
                      child: const Text('Resolve advisory'),
                    ),
                ],
              ),
            ],
          ],
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return ListenableBuilder(
      listenable: widget.viewModel,
      builder: (context, _) {
        final user = widget.viewModel.user;
        return Scaffold(
          appBar: AppBar(
            title: const Text('Coastal operations'),
            actions: [
              AuthAccountSwitcher(viewModel: widget.viewModel),
              AuthAdminNavigationMenu(viewModel: widget.viewModel),
              if (user != null)
                IconButton(
                  tooltip: 'Open your profile',
                  onPressed: () => Navigator.pushNamed(context, '/profile'),
                  icon: const Icon(Icons.person_outline),
                ),
            ],
          ),
          body: SafeArea(
            child: _loading && user == null
                ? const Center(child: CircularProgressIndicator())
                : user == null
                ? const _AccessState()
                : !_hasAccess
                ? const _AccessDeniedState()
                : _buildWorkspace(context),
          ),
        );
      },
    );
  }

  Widget _buildWorkspace(BuildContext context) {
    return RefreshIndicator(
      onRefresh: _loadData,
      child: ListView(
        padding: const EdgeInsets.fromLTRB(18, 18, 18, 32),
        children: [
          Center(
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 900),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  _hero(context),
                  if (_notice != null) ...[
                    const SizedBox(height: 14),
                    _NoticeBox(text: _notice!, error: _noticeIsError),
                  ],
                  const SizedBox(height: 22),
                  Text(
                    'Today’s coastal reviews',
                    style: Theme.of(context).textTheme.headlineSmall,
                  ),
                  const SizedBox(height: 6),
                  const Text(
                    'Your permissions determine which assessments, evidence and advisories appear here.',
                    style: TextStyle(height: 1.45),
                  ),
                  const SizedBox(height: 14),
                  Wrap(
                    spacing: 10,
                    runSpacing: 8,
                    children: [
                      if (_canCreateAssessment)
                        FilledButton.icon(
                          onPressed: _busy ? null : _openAssessmentForm,
                          icon: const Icon(Icons.add),
                          label: const Text('New assessment'),
                        ),
                      if (_canManageAlerts)
                        OutlinedButton.icon(
                          onPressed: _busy ? null : _openAlertForm,
                          icon: const Icon(Icons.campaign_outlined),
                          label: const Text('Prepare an advisory'),
                        ),
                      IconButton.filledTonal(
                        tooltip: 'Refresh coastal operations',
                        onPressed: _busy ? null : _loadData,
                        icon: const Icon(Icons.refresh),
                      ),
                    ],
                  ),
                  if (_canReadQueue) ...[
                    const SizedBox(height: 10),
                    const Align(
                      alignment: Alignment.centerLeft,
                      child: _StatusPill(value: 'REVIEW QUEUE'),
                    ),
                  ],
                  const SizedBox(height: 24),
                  _sectionHeading(
                    context,
                    'Assessments',
                    'A clear record of each review',
                  ),
                  const Padding(
                    padding: EdgeInsets.only(bottom: 10),
                    child: Text(
                      'Showing up to 100 assessments in your permission scope.',
                    ),
                  ),
                  if (!_canReadAssessments)
                    const _InfoSurface(
                      title: 'Assessment reading is not included',
                      child: Text(
                        'Your current permissions allow other Coastal Operations actions, but do not include assessment reading.',
                      ),
                    )
                  else if (_assessmentError != null)
                    _NoticeBox(text: _assessmentError!, error: true)
                  else if (_loading)
                    const Center(child: CircularProgressIndicator())
                  else if (_assessments.isEmpty)
                    const _InfoSurface(
                      title: 'No assessments to show yet',
                      child: Text(
                        'When an assessment is recorded for your account or review queue, it will appear here.',
                      ),
                    )
                  else ...[
                    for (final assessment in _assessments)
                      _assessmentCard(assessment),
                  ],
                  const SizedBox(height: 24),
                  _sectionHeading(
                    context,
                    'Advisories',
                    'Useful updates for the coast',
                  ),
                  const Padding(
                    padding: EdgeInsets.only(bottom: 10),
                    child: Text(
                      'Showing up to 100 advisories available to your account.',
                    ),
                  ),
                  if (!_canReadAlerts)
                    const _InfoSurface(
                      title: 'Advisory reading is not included',
                      child: Text(
                        'Your current permissions do not include advisory reading. You may prepare a draft if you have advisory management access.',
                      ),
                    )
                  else if (_alertError != null)
                    _NoticeBox(text: _alertError!, error: true)
                  else if (_loading)
                    const Center(child: CircularProgressIndicator())
                  else if (_alerts.isEmpty)
                    const _InfoSurface(
                      title: 'No advisories to show',
                      child: Text(
                        'Active public updates and authorized operations drafts will appear here.',
                      ),
                    )
                  else ...[
                    for (final alert in _alerts) _alertCard(alert),
                  ],
                  if (_canReadStatus || _canReadHistory) ...[
                    const SizedBox(height: 24),
                    _targetLookup(context),
                  ],
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _hero(BuildContext context) {
    return ClipRRect(
      borderRadius: BorderRadius.circular(26),
      child: SizedBox(
        height: 238,
        child: Stack(
          fit: StackFit.expand,
          children: [
            Image.asset(
              'assets/coastal/onboarding/coastal-walk.jpg',
              fit: BoxFit.cover,
              alignment: Alignment.center,
              errorBuilder: (_, _, _) => const ColoredBox(
                color: BlueversePalette.coastDeep,
                child: Icon(Icons.waves, color: Colors.white54, size: 88),
              ),
            ),
            const DecoratedBox(
              decoration: BoxDecoration(
                gradient: LinearGradient(
                  begin: Alignment.centerLeft,
                  end: Alignment.centerRight,
                  colors: [
                    Color(0xE6205C79),
                    Color(0xB0205C79),
                    Color(0x55205C79),
                  ],
                ),
              ),
            ),
            Padding(
              padding: const EdgeInsets.all(22),
              child: Align(
                alignment: Alignment.bottomLeft,
                child: Column(
                  mainAxisAlignment: MainAxisAlignment.end,
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      'COASTAL CARE',
                      style: Theme.of(context).textTheme.labelSmall?.copyWith(
                        color: BlueversePalette.coastGlass,
                        fontWeight: FontWeight.w800,
                        letterSpacing: 1.4,
                      ),
                    ),
                    const SizedBox(height: 8),
                    Text(
                      'Look after the places we share.',
                      style: Theme.of(context).textTheme.headlineSmall
                          ?.copyWith(
                            color: Colors.white,
                            fontWeight: FontWeight.w700,
                            height: 1.1,
                          ),
                    ),
                    const SizedBox(height: 8),
                    const Text(
                      'Record a review and prepare clear advisories for the right people.',
                      style: TextStyle(color: Colors.white, height: 1.35),
                    ),
                  ],
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _sectionHeading(BuildContext context, String eyebrow, String title) =>
      Padding(
        padding: const EdgeInsets.only(bottom: 10),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              eyebrow.toUpperCase(),
              style: Theme.of(context).textTheme.labelSmall?.copyWith(
                color: BlueversePalette.coastBlue,
                fontWeight: FontWeight.w800,
                letterSpacing: 1.2,
              ),
            ),
            const SizedBox(height: 4),
            Text(title, style: Theme.of(context).textTheme.titleLarge),
          ],
        ),
      );

  Widget _targetLookup(BuildContext context) => _InfoSurface(
    title: 'Check a coastal record',
    child: Form(
      key: _lookupFormKey,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          const Text(
            'Enter a canonical experience ID to see current operational status and recorded history.',
          ),
          const SizedBox(height: 14),
          DropdownButtonFormField<String>(
            initialValue: _lookupType,
            decoration: const InputDecoration(labelText: 'Record type'),
            items: _coastalTargetTypes
                .map(
                  (type) => DropdownMenuItem(
                    value: type,
                    child: Text(_humanize(type)),
                  ),
                )
                .toList(growable: false),
            onChanged: (value) {
              if (value != null) setState(() => _lookupType = value);
            },
          ),
          const SizedBox(height: 12),
          TextFormField(
            controller: _lookupIdController,
            decoration: const InputDecoration(labelText: 'Canonical record ID'),
            validator: (value) =>
                _validUuid(value ?? '') ? null : 'Enter a valid UUID.',
          ),
          const SizedBox(height: 12),
          FilledButton.tonal(
            onPressed: _lookupTarget,
            child: const Text('Check record'),
          ),
          if (_lookupError != null) ...[
            const SizedBox(height: 12),
            _NoticeBox(text: _lookupError!, error: true),
          ],
          if (_lookupStatus != null) ...[
            const SizedBox(height: 12),
            ListTile(
              contentPadding: EdgeInsets.zero,
              leading: const Icon(Icons.waves_outlined),
              title: Text(_humanize(_lookupStatus!.operationalState)),
              subtitle: Text(
                'Updated ${_formatDate(_lookupStatus!.updatedAt)} · version ${_lookupStatus!.stateVersion}',
              ),
            ),
          ],
          if (_canReadHistory &&
              _lookupStatus != null &&
              _lookupHistory.isEmpty)
            const Text('No state changes have been recorded for this record.'),
          if (_canReadHistory)
            ..._lookupHistory.map(
              (item) => ListTile(
                dense: true,
                contentPadding: EdgeInsets.zero,
                title: Text(
                  '${_humanize(item.previousState)} → ${_humanize(item.newState)}',
                ),
                subtitle: Text(_formatDate(item.createdAt)),
              ),
            ),
        ],
      ),
    ),
  );
}

class _AssessmentFormDialog extends StatefulWidget {
  const _AssessmentFormDialog({required this.apiService});

  final CoastalOperationsApiService apiService;

  @override
  State<_AssessmentFormDialog> createState() => _AssessmentFormDialogState();
}

class _AssessmentFormDialogState extends State<_AssessmentFormDialog> {
  final _formKey = GlobalKey<FormState>();
  final _targetId = TextEditingController();
  final _sourceWorkflowId = TextEditingController();
  final _startsAt = TextEditingController();
  final _endsAt = TextEditingController();
  final _objective = TextEditingController();
  String _targetType = 'DESTINATION';
  String? _error;
  bool _saving = false;

  @override
  void dispose() {
    _targetId.dispose();
    _sourceWorkflowId.dispose();
    _startsAt.dispose();
    _endsAt.dispose();
    _objective.dispose();
    super.dispose();
  }

  Future<void> _save() async {
    if (!(_formKey.currentState?.validate() ?? false)) return;
    if (!_validPeriod(_startsAt.text, _endsAt.text)) {
      setState(
        () => _error = 'Enter valid RFC 3339 times with offsets and an end later than the start.',
      );
      return;
    }
    setState(() {
      _saving = true;
      _error = null;
    });
    try {
      final assessment = await blueverseLoadingScreenController.track(
        () => widget.apiService.createAssessment(
          targetType: _targetType,
          targetId: _targetId.text.trim(),
          sourceWorkflowId: _sourceWorkflowId.text.trim().isEmpty
              ? null
              : _sourceWorkflowId.text.trim(),
          periodStartsAt: _startsAt.text.trim(),
          periodEndsAt: _endsAt.text.trim(),
          objective: _objective.text.trim(),
        ),
      );
      if (mounted) Navigator.pop(context, assessment);
    } on CoastalOperationsApiException catch (error) {
      if (mounted) setState(() => _error = error.message);
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  @override
  Widget build(BuildContext context) => AlertDialog(
    title: const Text('Record an operations review'),
    content: SizedBox(
      width: 480,
      child: Form(
        key: _formKey,
        child: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              const Text(
                'Use the canonical experience ID so the review stays connected to the right coastal record.',
              ),
              const SizedBox(height: 14),
              DropdownButtonFormField<String>(
                initialValue: _targetType,
                decoration: const InputDecoration(
                  labelText: 'Coastal record type',
                ),
                items: _coastalTargetTypes
                    .map(
                      (type) => DropdownMenuItem(
                        value: type,
                        child: Text(_humanize(type)),
                      ),
                    )
                    .toList(growable: false),
                onChanged: (value) {
                  if (value != null) setState(() => _targetType = value);
                },
              ),
              const SizedBox(height: 12),
              TextFormField(
                controller: _targetId,
                decoration: const InputDecoration(
                  labelText: 'Canonical record ID',
                ),
                validator: (value) =>
                    _validUuid(value ?? '') ? null : 'Enter a valid UUID.',
              ),
              const SizedBox(height: 12),
              TextFormField(
                controller: _sourceWorkflowId,
                decoration: const InputDecoration(
                  labelText: 'Planning workflow ID (optional)',
                ),
                validator: (value) =>
                    value == null || value.trim().isEmpty || _validUuid(value)
                    ? null
                    : 'Enter a valid UUID.',
              ),
              const SizedBox(height: 12),
              TextFormField(
                controller: _startsAt,
                decoration: const InputDecoration(
                  labelText: 'Period starts at',
                  hintText: '2026-09-28T09:00:00+05:30',
                  helperText: 'Include Z or the correct UTC offset.',
                ),
                validator: (value) => _validInstant(value ?? '')
                    ? null
                    : 'Include an explicit UTC offset.',
              ),
              const SizedBox(height: 12),
              TextFormField(
                controller: _endsAt,
                decoration: const InputDecoration(
                  labelText: 'Period ends at',
                  hintText: '2026-09-28T11:00:00+05:30',
                  helperText: 'Choose the offset that applies on this date.',
                ),
                validator: (value) => _validInstant(value ?? '')
                    ? null
                    : 'Include an explicit UTC offset.',
              ),
              const SizedBox(height: 12),
              TextFormField(
                controller: _objective,
                maxLength: 2000,
                maxLines: 3,
                decoration: const InputDecoration(
                  labelText: 'What should the team assess?',
                ),
                validator: (value) => value == null || value.trim().isEmpty
                    ? 'Add a short assessment objective.'
                    : null,
              ),
              if (_error != null) ...[
                const SizedBox(height: 8),
                _NoticeBox(text: _error!, error: true),
              ],
            ],
          ),
        ),
      ),
    ),
    actions: [
      TextButton(
        onPressed: _saving ? null : () => Navigator.pop(context),
        child: const Text('Cancel'),
      ),
      FilledButton(
        onPressed: _saving ? null : _save,
        child: Text(_saving ? 'Recording…' : 'Record assessment'),
      ),
    ],
  );
}

class _AlertFormDialog extends StatefulWidget {
  const _AlertFormDialog({required this.apiService, this.existing});

  final CoastalOperationsApiService apiService;
  final CoastalAlert? existing;

  @override
  State<_AlertFormDialog> createState() => _AlertFormDialogState();
}

class _AlertFormDialogState extends State<_AlertFormDialog> {
  final _formKey = GlobalKey<FormState>();
  late final TextEditingController _targetId;
  late final TextEditingController _assessmentId;
  late final TextEditingController _title;
  late final TextEditingController _description;
  late final TextEditingController _validFrom;
  late final TextEditingController _validUntil;
  late String _targetType;
  late String _severity;
  late String _visibility;
  String? _error;
  bool _saving = false;

  @override
  void initState() {
    super.initState();
    final existing = widget.existing;
    _targetId = TextEditingController(text: existing?.targetId ?? '');
    _assessmentId = TextEditingController(text: existing?.assessmentId ?? '');
    _title = TextEditingController(text: existing?.title ?? '');
    _description = TextEditingController(text: existing?.description ?? '');
    _validFrom = TextEditingController(text: existing?.validFrom ?? '');
    _validUntil = TextEditingController(text: existing?.validUntil ?? '');
    _targetType = existing?.targetType ?? 'DESTINATION';
    _severity = existing?.severity ?? 'MODERATE';
    _visibility = existing?.visibility ?? 'OPERATIONS';
  }

  @override
  void dispose() {
    _targetId.dispose();
    _assessmentId.dispose();
    _title.dispose();
    _description.dispose();
    _validFrom.dispose();
    _validUntil.dispose();
    super.dispose();
  }

  Future<void> _save() async {
    if (!(_formKey.currentState?.validate() ?? false)) return;
    if (!_validPeriod(_validFrom.text, _validUntil.text)) {
      setState(
        () => _error = 'Enter valid RFC 3339 times with offsets and an end later than the start.',
      );
      return;
    }
    setState(() {
      _saving = true;
      _error = null;
    });
    try {
      final existing = widget.existing;
      final alert = await blueverseLoadingScreenController.track(() {
        if (existing != null) {
          return widget.apiService.updateAlertDraft(
            alertId: existing.alertId,
            expectedVersion: existing.version,
            title: _title.text.trim(),
            description: _description.text.trim(),
            severity: _severity,
            visibility: _visibility,
            validFrom: _validFrom.text.trim(),
            validUntil: _validUntil.text.trim(),
          );
        }
        return widget.apiService.createAlertDraft(
          targetType: _targetType,
          targetId: _targetId.text.trim(),
          assessmentId: _assessmentId.text.trim().isEmpty
              ? null
              : _assessmentId.text.trim(),
          title: _title.text.trim(),
          description: _description.text.trim(),
          severity: _severity,
          visibility: _visibility,
          validFrom: _validFrom.text.trim(),
          validUntil: _validUntil.text.trim(),
        );
      });
      if (mounted) Navigator.pop(context, alert);
    } on CoastalOperationsApiException catch (error) {
      if (mounted) setState(() => _error = error.message);
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  @override
  Widget build(BuildContext context) => AlertDialog(
    title: Text(
      widget.existing == null
          ? 'Prepare a coastal advisory'
          : 'Edit proposed advisory',
    ),
    content: SizedBox(
      width: 480,
      child: Form(
        key: _formKey,
        child: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              const Text(
                'This creates an unpublished draft. Publishing is a separate permission-checked action.',
              ),
              const SizedBox(height: 14),
              if (widget.existing == null) ...[
                DropdownButtonFormField<String>(
                  initialValue: _targetType,
                  decoration: const InputDecoration(
                    labelText: 'Coastal record type',
                  ),
                  items: _coastalTargetTypes
                      .map(
                        (type) => DropdownMenuItem(
                          value: type,
                          child: Text(_humanize(type)),
                        ),
                      )
                      .toList(growable: false),
                  onChanged: (value) {
                    if (value != null) setState(() => _targetType = value);
                  },
                ),
                const SizedBox(height: 12),
                TextFormField(
                  controller: _targetId,
                  decoration: const InputDecoration(
                    labelText: 'Canonical record ID',
                  ),
                  validator: (value) =>
                      _validUuid(value ?? '') ? null : 'Enter a valid UUID.',
                ),
                const SizedBox(height: 12),
                TextFormField(
                  controller: _assessmentId,
                  decoration: const InputDecoration(
                    labelText: 'Related assessment ID (optional)',
                  ),
                  validator: (value) =>
                      value == null || value.trim().isEmpty || _validUuid(value)
                      ? null
                      : 'Enter a valid UUID.',
                ),
                const SizedBox(height: 12),
              ],
              DropdownButtonFormField<String>(
                initialValue: _severity,
                decoration: const InputDecoration(labelText: 'Severity'),
                items: const ['LOW', 'MODERATE', 'HIGH', 'CRITICAL']
                    .map(
                      (value) => DropdownMenuItem(
                        value: value,
                        child: Text(_humanize(value)),
                      ),
                    )
                    .toList(growable: false),
                onChanged: (value) {
                  if (value != null) setState(() => _severity = value);
                },
              ),
              const SizedBox(height: 12),
              DropdownButtonFormField<String>(
                initialValue: _visibility,
                decoration: const InputDecoration(
                  labelText: 'Who can see this?',
                ),
                items: const [
                  DropdownMenuItem(
                    value: 'OPERATIONS',
                    child: Text('Operations team'),
                  ),
                  DropdownMenuItem(
                    value: 'PUBLIC',
                    child: Text('Public coastal visitors'),
                  ),
                ],
                onChanged: (value) {
                  if (value != null) setState(() => _visibility = value);
                },
              ),
              const SizedBox(height: 12),
              TextFormField(
                controller: _title,
                maxLength: 160,
                decoration: const InputDecoration(labelText: 'Title'),
                validator: (value) => value == null || value.trim().isEmpty
                    ? 'Add a clear title.'
                    : null,
              ),
              const SizedBox(height: 12),
              TextFormField(
                controller: _description,
                maxLength: 4000,
                minLines: 3,
                maxLines: 5,
                decoration: const InputDecoration(
                  labelText: 'What should people know?',
                ),
                validator: (value) => value == null || value.trim().isEmpty
                    ? 'Add the advisory details.'
                    : null,
              ),
              const SizedBox(height: 12),
              TextFormField(
                controller: _validFrom,
                decoration: const InputDecoration(
                  labelText: 'Visible from',
                  hintText: '2026-09-28T09:00:00+05:30',
                ),
                validator: (value) => _validInstant(value ?? '')
                    ? null
                    : 'Include an explicit UTC offset.',
              ),
              const SizedBox(height: 12),
              TextFormField(
                controller: _validUntil,
                decoration: const InputDecoration(
                  labelText: 'Valid until',
                  hintText: '2026-09-28T11:00:00+05:30',
                ),
                validator: (value) => _validInstant(value ?? '')
                    ? null
                    : 'Include an explicit UTC offset.',
              ),
              if (_severity == 'HIGH' || _severity == 'CRITICAL') ...[
                const SizedBox(height: 12),
                const _NoticeBox(
                  text: 'A different authorized reviewer from the draft creator and linked assessment initiator must publish this advisory.',
                ),
              ],
              if (_error != null) ...[
                const SizedBox(height: 12),
                _NoticeBox(text: _error!, error: true),
              ],
            ],
          ),
        ),
      ),
    ),
    actions: [
      TextButton(
        onPressed: _saving ? null : () => Navigator.pop(context),
        child: const Text('Cancel'),
      ),
      FilledButton(
        onPressed: _saving ? null : _save,
        child: Text(
          _saving
              ? 'Saving…'
              : widget.existing == null
              ? 'Create draft'
              : 'Save draft',
        ),
      ),
    ],
  );
}

class _StatusPill extends StatelessWidget {
  const _StatusPill({required this.value});

  final String value;

  @override
  Widget build(BuildContext context) {
    final normalized = value.toUpperCase();
    final positive = [
      'AVAILABLE',
      'OPEN',
      'ACTIVE',
      'PUBLISHED',
      'SUCCEEDED',
      'APPROVED',
    ].contains(normalized);
    final quiet = [
      'NOT_CONNECTED',
      'SUBMITTED',
      'PROPOSED',
      'UNKNOWN',
      'PENDING_APPROVAL',
      'REVIEW QUEUE',
    ].contains(normalized);
    final background = positive
        ? BlueversePalette.coastSage
        : quiet
        ? BlueversePalette.coastSand
        : const Color(0xFFFCECEC);
    final foreground = positive || quiet
        ? BlueversePalette.coastDeep
        : Colors.red.shade900;
    return Container(
      decoration: BoxDecoration(
        color: background,
        borderRadius: BorderRadius.circular(24),
      ),
      padding: const EdgeInsets.symmetric(horizontal: 11, vertical: 5),
      child: Text(
        _humanize(value),
        style: TextStyle(
          color: foreground,
          fontSize: 11,
          fontWeight: FontWeight.w700,
        ),
      ),
    );
  }
}

class _NoticeBox extends StatelessWidget {
  const _NoticeBox({required this.text, this.error = false});

  final String text;
  final bool error;

  @override
  Widget build(BuildContext context) => Semantics(
    liveRegion: true,
    child: Container(
      decoration: BoxDecoration(
        color: error ? const Color(0xFFFFF1F1) : BlueversePalette.coastSand,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(
          color: error ? const Color(0xFFE7B4B4) : BlueversePalette.coastLine,
        ),
      ),
      padding: const EdgeInsets.all(12),
      child: Text(
        text,
        style: TextStyle(
          color: error ? Colors.red.shade900 : BlueversePalette.coastInk,
          height: 1.4,
        ),
      ),
    ),
  );
}

class _InfoSurface extends StatelessWidget {
  const _InfoSurface({required this.title, required this.child});

  final String title;
  final Widget child;

  @override
  Widget build(BuildContext context) => Card(
    child: Padding(
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(title, style: Theme.of(context).textTheme.titleSmall),
          const SizedBox(height: 6),
          child,
        ],
      ),
    ),
  );
}

class _AccessState extends StatelessWidget {
  const _AccessState();

  @override
  Widget build(BuildContext context) => Center(
    child: Padding(
      padding: const EdgeInsets.all(24),
      child: _InfoSurface(
        title: 'Sign in to see Coastal Operations',
        child: const Text(
          'Your BLUEVERSE account is needed to open this workspace.',
        ),
      ),
    ),
  );
}

class _AccessDeniedState extends StatelessWidget {
  const _AccessDeniedState();

  @override
  Widget build(BuildContext context) => Center(
    child: Padding(
      padding: const EdgeInsets.all(24),
      child: _InfoSurface(
        title: 'This workspace is not available to your account',
        child: const Text(
          'Coastal Operations access is granted through your account permissions. Contact your BLUEVERSE administrator if you need access.',
        ),
      ),
    ),
  );
}

String _dependencyLabel(String service) => switch (service) {
  'experience-biodiversity' => 'Coastal experience',
  'marine-safety' => 'Marine safety',
  'coastal-planner' => 'Coastal planning',
  _ => 'Coastal context',
};
