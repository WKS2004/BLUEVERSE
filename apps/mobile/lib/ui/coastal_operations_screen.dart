import 'dart:async';

import 'package:flutter/material.dart';
import 'package:image_picker/image_picker.dart';

import '../data/models/coastal_operations_models.dart';
import '../data/services/coastal_operations_api_service.dart';
import '../features/coastal_operations/coastal_operations_permissions.dart';
import 'account_screens.dart';
import 'coastal_operations_search.dart';
import 'coastal_operations_draft_forms.dart';
import 'coastal_operations_activity.dart';
import 'coastal_operations_record_card.dart';
import 'auth_view_model.dart';
import 'blueverse_theme.dart';
import 'feedback/loading_screen_controller.dart';

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

String _humanize(String value) => value.toUpperCase() == 'PROPOSED'
    ? 'Draft'
    : value.toUpperCase() == 'SUBMITTED'
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

enum CoastalOperationsSection { all, assessments, alerts }

class CoastalOperationsScreen extends StatefulWidget {
  const CoastalOperationsScreen({
    required this.viewModel,
    required this.apiService,
    this.section = CoastalOperationsSection.all,
    this.initialView,
    this.initialRecordId,
    this.fromLogs = false,
    super.key,
  });

  final AuthViewModel viewModel;
  final CoastalOperationsApiService apiService;
  final CoastalOperationsSection section;
  final String? initialView, initialRecordId;
  final bool fromLogs;

  @override
  State<CoastalOperationsScreen> createState() =>
      _CoastalOperationsScreenState();
}

class _CoastalOperationsScreenState extends State<CoastalOperationsScreen> {
  int _pageSize = 25, _assessmentPage = 0, _alertPage = 0;
  List<String?> _assessmentPages = [null], _alertPages = [null];
  bool _restoreBusy = false, _restoreAttempted = false;
  String? _restoreError;
  List<CoastalAssessment> _assessments = const [];
  List<CoastalAlert> _alerts = const [];
  Map<String, String> _assessmentFilters = const {};
  Map<String, String> _alertFilters = const {};
  String? _assessmentCursor;
  String? _alertCursor;
  int _loadGeneration = 0;
  bool get _showAssessments =>
      widget.section != CoastalOperationsSection.alerts;
  bool get _showAlerts =>
      widget.section != CoastalOperationsSection.assessments;
  String? _assessmentError;
  String? _alertError;
  String? _notice;
  bool _noticeIsError = false;
  bool _loading = true;
  bool _busy = false;
  String? _loadedUserId;
  String _loadedGrants = '';
  CoastalAssessment? _selectedAssessment;
  CoastalAlert? _selectedAlert;
  String? _selectedAlertId;
  String? _alertDetailError;
  bool _alertDetailLoading = false;
  bool? _formAssessments;
  CoastalAssessment? _editingAssessment;
  CoastalAlert? _editingAlert;
  bool get _focused =>
      _restoreBusy ||
      _restoreError != null ||
      _formAssessments != null ||
      _selectedAssessment != null ||
      _selectedAlertId != null;
  void _backToList() {
    final name = ModalRoute.of(context)?.settings.name;
    if (widget.fromLogs && name != null && name.contains('?view=')) {
      final navigator = Navigator.of(context);
      var found = false;
      navigator.popUntil((route) {
        if (Uri.tryParse(route.settings.name ?? '')?.path ==
            '/operations/logs') {
          found = true;
          return true;
        }
        return route.isFirst;
      });
      if (!found) {
        navigator.pushNamed(
          '/operations/logs?kind=${_showAlerts && !_showAssessments ? 'alerts' : 'assessments'}',
        );
      }
      return;
    }
    if (name != null && name.contains('?view=')) {
      if (Navigator.canPop(context)) {
        Navigator.pop(context, true);
      } else {
        Navigator.pushReplacementNamed(
          context,
          widget.fromLogs
              ? '/operations/logs?kind=${_showAlerts && !_showAssessments ? 'alerts' : 'assessments'}'
              : _showAlerts && !_showAssessments
              ? '/operations/alerts'
              : '/operations/assessments',
        );
      }
      return;
    }
    setState(() {
      _restoreBusy = false;
      _restoreError = null;
      _formAssessments = null;
      _editingAssessment = null;
      _editingAlert = null;
      _selectedAssessment = null;
      _selectedAlert = null;
      _selectedAlertId = null;
      _alertDetailError = null;
      _alertDetailLoading = false;
    });
  }

  final Map<String, CoastalAssessmentDetail> _details = {};
  final Map<String, CoastalOperationalStatus> _statusByAssessment = {};
  final Map<String, List<CoastalHistoryItem>> _historyByAssessment = {};
  final Map<String, String> _detailErrors = {};

  List<String> get _permissions =>
      widget.viewModel.user?.permissions ?? const [];

  bool get _canReadAssessments =>
      _has(_permissions, CoastalOperationsPermissions.assessmentRead) ||
      _has(_permissions, CoastalOperationsPermissions.assessmentQueueRead);
  bool get _canReadQueue =>
      _has(_permissions, CoastalOperationsPermissions.assessmentQueueRead);
  bool get _canCreateAssessment =>
      _has(_permissions, CoastalOperationsPermissions.assessmentCreate);
  bool get _canUpdateAssessment =>
      _has(_permissions, CoastalOperationsPermissions.assessmentUpdate);
  bool get _canDeleteAssessment =>
      _has(_permissions, CoastalOperationsPermissions.assessmentDelete);
  bool get _canSubmitAssessment =>
      _has(_permissions, CoastalOperationsPermissions.assessmentSubmit);
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
  bool get _canManageAlerts =>
      CoastalOperationsPermissions.canManageAlerts(_permissions);
  bool get _canReadAlerts =>
      _has(_permissions, CoastalOperationsPermissions.alertRead) ||
      _canManageAlerts;
  bool get _canCreateAlerts =>
      _has(_permissions, CoastalOperationsPermissions.alertCreate) ||
      _has(_permissions, CoastalOperationsPermissions.alertManage);
  bool get _canUpdateAlerts =>
      _has(_permissions, CoastalOperationsPermissions.alertUpdate) ||
      _has(_permissions, CoastalOperationsPermissions.alertManage);
  bool get _canDeleteAlerts =>
      _has(_permissions, CoastalOperationsPermissions.alertDelete) ||
      _has(_permissions, CoastalOperationsPermissions.alertManage);
  bool get _canPublishAlerts =>
      _has(_permissions, CoastalOperationsPermissions.alertPublish) ||
      _has(_permissions, CoastalOperationsPermissions.alertDecide);
  bool get _canResolveAlerts =>
      _has(_permissions, CoastalOperationsPermissions.alertResolve) ||
      _has(_permissions, CoastalOperationsPermissions.alertDecide);
  bool get _canReadAudit =>
      _has(_permissions, CoastalOperationsPermissions.auditRead);
  bool get _hasAccess => CoastalOperationsPermissions.hasAccess(_permissions);

  @override
  void initState() {
    super.initState();
    widget.viewModel.addListener(_syncAccount);
    _loadedUserId = widget.viewModel.user?.id;
    _loadedGrants = _permissions.join('|');
    if (widget.viewModel.user == null && !widget.viewModel.isLoading) {
      WidgetsBinding.instance.addPostFrameCallback((_) {
        if (mounted && widget.viewModel.user == null) {
          unawaited(widget.viewModel.restore());
        }
      });
    } else if (widget.viewModel.user != null) {
      unawaited(_loadData());
      if (widget.initialView != null) {
        _restoreAttempted = true;
        _restoreBusy = true;
        WidgetsBinding.instance.addPostFrameCallback((_) {
          if (mounted) unawaited(_restoreView());
        });
      }
    }
  }

  @override
  void dispose() {
    widget.viewModel.removeListener(_syncAccount);
    _loadGeneration++;
    super.dispose();
  }

  void _syncAccount() {
    final id = widget.viewModel.user?.id;
    final grants = _permissions.join('|');
    if (id == _loadedUserId && grants == _loadedGrants) return;
    _loadedGrants = grants;
    _formAssessments = null;
    _selectedAssessment = null;
    _selectedAlert = null;
    _selectedAlertId = null;
    _alertDetailError = null;
    _alertDetailLoading = false;
    _editingAssessment = null;
    _editingAlert = null;
    _assessmentFilters = const {};
    _alertFilters = const {};
    _loadedUserId = id;
    _loadGeneration++;
    _details.clear();
    _statusByAssessment.clear();
    _historyByAssessment.clear();
    _detailErrors.clear();
    _assessments = const [];
    _alerts = const [];
    if (id != null) {
      unawaited(_loadData());
      if (!_restoreAttempted && widget.initialView != null) {
        _restoreAttempted = true;
        WidgetsBinding.instance.addPostFrameCallback((_) {
          if (mounted) unawaited(_restoreView());
        });
      }
    }
    if (mounted) setState(() {});
  }

  Future<void> _loadData() async {
    final generation = ++_loadGeneration;
    if (!_hasAccess) {
      if (mounted) setState(() => _loading = false);
      return;
    }
    setState(() {
      _loading = true;
      _assessmentError = null;
      _alertError = null;
      _assessmentCursor = null;
      _alertCursor = null;
    });
    {
      final results = await Future.wait<Object?>([
        _canReadAssessments && _showAssessments
            ? _settle(
                widget.apiService.listAssessments(
                  filters: {..._assessmentFilters, 'pageSize': '$_pageSize'},
                  cursor: _assessmentPages[_assessmentPage],
                ),
              )
            : Future<Object?>.value(null),
        _canReadAlerts && _showAlerts
            ? _settle(
                widget.apiService.listAlerts(
                  filters: {..._alertFilters, 'pageSize': '$_pageSize'},
                  cursor: _alertPages[_alertPage],
                ),
              )
            : Future<Object?>.value(null),
      ]);
      if (!mounted || generation != _loadGeneration) return;
      final assessmentResult = results[0];
      final alertResult = results[1];
      setState(() {
        if (assessmentResult is CoastalPage<CoastalAssessment>) {
          _assessments = assessmentResult.items;
          _assessmentCursor = assessmentResult.nextCursor;
        } else if (_canReadAssessments && _showAssessments) {
          _assessmentError =
              'We could not load assessments. Check your connection and retry.';
        }
        if (alertResult is CoastalPage<CoastalAlert>) {
          _alerts = alertResult.items;
          _alertCursor = alertResult.nextCursor;
        } else if (_canReadAlerts && _showAlerts) {
          _alertError =
              'We could not load advisories. Check your connection and retry.';
        }
        _loading = false;
      });
      if (_selectedAlert != null) await _openAlertDetails(_selectedAlert!);
    }
  }

  void _resetPages() {
    _assessmentPage = 0;
    _alertPage = 0;
    _assessmentPages = [null];
    _alertPages = [null];
  }

  Widget _pagination(bool assessments) {
    final page = assessments ? _assessmentPage : _alertPage;
    final next = assessments ? _assessmentCursor : _alertCursor;
    return CoastalOperationsPagination(
      assessments: assessments,
      size: _pageSize,
      count: assessments ? _assessments.length : _alerts.length,
      page: page,
      onSize: (value) {
        setState(() {
          _pageSize = value;
          _resetPages();
        });
        unawaited(_loadData());
      },
      previous: _loading || page == 0
          ? null
          : () {
              setState(() {
                if (assessments) {
                  _assessmentPage--;
                } else {
                  _alertPage--;
                }
              });
              unawaited(_loadData());
            },
      next: _loading || next == null
          ? null
          : () {
              setState(() {
                if (assessments) {
                  _assessmentPages = [..._assessmentPages.take(page + 1), next];
                  _assessmentPage++;
                } else {
                  _alertPages = [..._alertPages.take(page + 1), next];
                  _alertPage++;
                }
              });
              unawaited(_loadData());
            },
    );
  }

  bool _navigateView(bool assessments, String view, [String? id]) {
    final name = ModalRoute.of(context)?.settings.name;
    if (name == null || !name.startsWith('/operations/')) return false;
    final uri = Uri(
      path: assessments ? '/operations/assessments' : '/operations/alerts',
      queryParameters: {
        'view': view,
        'id': ?id,
        if (widget.fromLogs) 'origin': 'logs',
      },
    );
    Navigator.pushNamed(context, uri.toString()).then((result) {
      if (!mounted || result == null) return;
      if (result is String) {
        setState(() {
          _notice = result;
          _noticeIsError = false;
        });
      }
      unawaited(_loadData());
    });
    return true;
  }

  Future<void> _restoreView() async {
    final scope = '$_loadedUserId:$_loadedGrants';
    setState(() {
      _restoreBusy = true;
      _restoreError = null;
    });
    final assessments = widget.section != CoastalOperationsSection.alerts;
    try {
      if (widget.initialView == 'create') {
        if (assessments ? !_canCreateAssessment : !_canCreateAlerts) {
          throw StateError(
            'Your current access does not allow creating this draft.',
          );
        }
        setState(() => _formAssessments = assessments);
        return;
      }
      final id = widget.initialRecordId;
      if (id == null ||
          !RegExp(r'^[0-9a-fA-F]{8}-[0-9a-fA-F-]{27}$').hasMatch(id)) {
        throw StateError('This record link is invalid. Return to the records.');
      }
      if (assessments) {
        if (!_canReadAssessments ||
            (widget.initialView == 'edit' && !_canUpdateAssessment)) {
          throw StateError('This draft is outside your current access.');
        }
        final detail = await widget.apiService.getAssessmentDetail(id);
        if (!mounted || scope != '$_loadedUserId:$_loadedGrants') return;
        if (widget.initialView == 'edit') {
          if (detail.assessment.workflowStatus != 'DRAFT') {
            throw StateError('This assessment is no longer an editable draft.');
          }
          setState(() {
            _editingAssessment = detail.assessment;
            _formAssessments = true;
          });
        } else {
          setState(() => _selectedAssessment = detail.assessment);
          await _loadDetail(detail.assessment);
        }
      } else {
        if (!_canReadAlerts ||
            (widget.initialView == 'edit' && !_canUpdateAlerts)) {
          throw StateError('This advisory is outside your current access.');
        }
        final result = await (widget.fromLogs
            ? widget.apiService.listAlertLogRecords(filters: {'recordId': id})
            : widget.apiService.listAlerts(filters: {'recordId': id}));
        if (!mounted || scope != '$_loadedUserId:$_loadedGrants') return;
        final record = result.items.where((r) => r.alertId == id).firstOrNull;
        if (record == null) {
          throw StateError(
            'This advisory is unavailable or outside your current access.',
          );
        }
        if (widget.initialView == 'edit') {
          if (record.lifecycle != 'PROPOSED') {
            throw StateError('This advisory is no longer an editable draft.');
          }
          setState(() {
            _editingAlert = record;
            _formAssessments = false;
          });
        } else {
          setState(() {
            _selectedAlert = record;
            _selectedAlertId = id;
          });
        }
      }
    } on Object catch (error) {
      if (mounted && scope == '$_loadedUserId:$_loadedGrants') {
        setState(
          () => _restoreError = error is CoastalOperationsApiException
              ? error.message
              : error is StateError
              ? error.message.toString()
              : 'The draft could not be restored. Return and retry.',
        );
      }
    } finally {
      if (mounted && scope == '$_loadedUserId:$_loadedGrants') {
        setState(() => _restoreBusy = false);
      }
    }
  }

  void _openActivity(String id, bool assessment) {
    showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      useSafeArea: true,
      builder: (_) => SizedBox(
        height: MediaQuery.sizeOf(context).height * .85,
        child: CoastalOperationsActivity(
          apiService: widget.apiService,
          id: id,
          assessment: assessment,
          showReference: widget.fromLogs,
        ),
      ),
    );
  }

  Widget _navigation() => Wrap(
    spacing: 12,
    runSpacing: 8,
    children: [
      if (CoastalOperationsPermissions.hasAssessmentAccess(_permissions))
        TextButton(
          onPressed: _showAssessments
              ? null
              : () => Navigator.pushReplacementNamed(
                  context,
                  '/operations/assessments',
                ),
          child: const Text('Assessments'),
        ),
      if (CoastalOperationsPermissions.hasAlertAccess(_permissions))
        TextButton(
          onPressed: _showAlerts
              ? null
              : () => Navigator.pushReplacementNamed(
                  context,
                  '/operations/alerts',
                ),
          child: const Text('Alerts'),
        ),
      if (CoastalOperationsPermissions.canReadLogs(_permissions))
        TextButton(
          onPressed: () =>
              Navigator.pushReplacementNamed(context, '/operations/logs'),
          child: const Text('Logs'),
        ),
    ],
  );
  Widget _search(bool assessments) => CoastalOperationsSearch(
    key: ValueKey('search:$assessments:$_loadedUserId:$_loadedGrants'),
    assessments: assessments,
    canManage: assessments ? _canReadQueue : _canManageAlerts,
    loading: _loading,
    active: !_focused,
    onApply: (query) {
      _resetPages();
      if (assessments) {
        _assessmentFilters = query;
      } else {
        _alertFilters = query;
      }
      unawaited(_loadData());
    },
  );

  Future<void> _loadDetail(CoastalAssessment assessment) async {
    final scope = '$_loadedUserId:$_loadedGrants';
    if (_details.containsKey(assessment.assessmentId)) return;
    setState(() => _detailErrors.remove(assessment.assessmentId));
    try {
      final detail = await widget.apiService.getAssessmentDetail(
        assessment.assessmentId,
      );
      CoastalOperationalStatus? status;
      List<CoastalHistoryItem> history = const [];
      final results = await Future.wait<Object?>([
        _canReadStatus &&
                detail.assessment.targetId !=
                    '00000000-0000-0000-0000-000000000000'
            ? _settle(
                widget.apiService.getTargetStatus(
                  targetType: detail.assessment.targetType,
                  targetId: detail.assessment.targetId,
                ),
              )
            : Future<Object?>.value(null),
        _canReadHistory &&
                detail.assessment.targetId !=
                    '00000000-0000-0000-0000-000000000000'
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
      if (!mounted || scope != '$_loadedUserId:$_loadedGrants') {
        return;
      }
      setState(() {
        _details[assessment.assessmentId] = detail;
        if (_selectedAssessment?.assessmentId == assessment.assessmentId) {
          _selectedAssessment = detail.assessment;
        }
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
      if (mounted && scope == '$_loadedUserId:$_loadedGrants') {
        setState(() => _detailErrors[assessment.assessmentId] = error.message);
      }
    } on Object {
      if (mounted && scope == '$_loadedUserId:$_loadedGrants') {
        setState(
          () => _detailErrors[assessment.assessmentId] =
              'We could not open this assessment. Refresh and try again.',
        );
      }
    }
  }

  Future<void> _refreshDetail(String assessmentId) async {
    _details.remove(assessmentId);
    _statusByAssessment.remove(assessmentId);
    _historyByAssessment.remove(assessmentId);
    _detailErrors.remove(assessmentId);
    await _loadDetail(
      _assessments.firstWhere(
        (assessment) => assessment.assessmentId == assessmentId,
        orElse: () => _selectedAssessment!,
      ),
    );
  }

  Future<void> _openAssessmentForm({CoastalAssessment? existing}) async {
    if (_navigateView(
      true,
      existing == null ? 'create' : 'edit',
      existing?.assessmentId,
    )) {
      return;
    }
    setState(() {
      _formAssessments = true;
      _editingAssessment = existing;
      _editingAlert = null;
    });
  }

  void _draftSaved(Object result) {
    if (widget.fromLogs &&
        ModalRoute.of(context)?.settings.name?.contains('?view=') == true) {
      _backToList();
      return;
    }
    if (ModalRoute.of(context)?.settings.name?.contains('?view=') == true) {
      if (Navigator.canPop(context)) {
        Navigator.pop(
          context,
          result is CoastalAssessment
              ? 'Assessment draft saved. Submit it when you are ready to check coastal context.'
              : 'Advisory draft saved. It has not been published.',
        );
      } else {
        Navigator.pushReplacementNamed(
          context,
          result is CoastalAssessment
              ? '/operations/assessments'
              : '/operations/alerts',
        );
      }
      return;
    }
    _backToList();
    if (result is CoastalAssessment) {
      _details.remove(result.assessmentId);
      setState(() {
        _selectedAssessment = _canReadAssessments ? result : null;
        _notice = 'Assessment draft saved. Submit it when you are ready to check coastal context.';
        _noticeIsError = false;
      });
      if (_canReadAssessments) unawaited(_loadDetail(result));
    } else if (result is CoastalAlert) {
      setState(() {
        _selectedAlert = result;
        _selectedAlertId = result.alertId;
        _notice = 'Advisory draft saved. It has not been published.';
        _noticeIsError = false;
      });
    }
    unawaited(_loadData());
  }

  Future<void> _handleAssessmentDraftAction(
    CoastalAssessment assessment, {
    required bool submit,
  }) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: Text(
          submit
              ? (widget.section == CoastalOperationsSection.all
                    ? 'Submit this assessment?'
                    : 'Publish this assessment?')
              : 'Cancel this draft?',
        ),
        content: Text(
          submit
              ? 'Publishing closes draft editing and checks coastal context. The review is queued for the assessment agent when connected. Publication does not approve a recommendation or change coastal access.'
              : 'This draft will be cancelled and retained in the audit history for authorized reviewers.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: const Text('Keep draft'),
          ),
          FilledButton(
            style: submit
                ? null
                : FilledButton.styleFrom(
                    backgroundColor: Theme.of(context).colorScheme.error,
                    foregroundColor: Theme.of(context).colorScheme.onError,
                  ),
            onPressed: () => Navigator.pop(context, true),
            child: Text(
              submit
                  ? (widget.section == CoastalOperationsSection.all
                        ? 'Submit assessment'
                        : 'Confirm publication')
                  : 'Confirm cancellation',
            ),
          ),
        ],
      ),
    );
    if (confirmed != true) return;
    setState(() => _busy = true);
    try {
      await blueverseLoadingScreenController.track(() {
        if (submit) {
          return widget.apiService.submitAssessmentDraft(
            assessmentId: assessment.assessmentId,
            expectedVersion: assessment.version,
          );
        }
        return widget.apiService.cancelAssessmentDraft(
          assessmentId: assessment.assessmentId,
          expectedVersion: assessment.version,
        );
      });
      if (!mounted) return;
      setState(() {
        _notice = submit
            ? 'The assessment was submitted. No automated proposal was created.'
            : 'The assessment draft was cancelled and retained in the audit history for authorized reviewers.';
        _noticeIsError = false;
      });
      await _loadData();
      if (submit) {
        await _refreshDetail(assessment.assessmentId);
      } else {
        _details.remove(assessment.assessmentId);
        _statusByAssessment.remove(assessment.assessmentId);
        _historyByAssessment.remove(assessment.assessmentId);
        _detailErrors.remove(assessment.assessmentId);
        _backToList();
      }
    } on CoastalOperationsApiException catch (error) {
      _setNotice(error.message, error: true);
    } on Object {
      _setNotice(
        'We could not update this assessment. Refresh and retry.',
        error: true,
      );
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _withdrawAlertDraft(CoastalAlert alert) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Withdraw this draft?'),
        content: Text(
          '“${alert.title}” will be marked withdrawn and kept in the advisory history for managers.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: const Text('Keep draft'),
          ),
          FilledButton(
            style: FilledButton.styleFrom(
              backgroundColor: Theme.of(context).colorScheme.error,
              foregroundColor: Theme.of(context).colorScheme.onError,
            ),
            onPressed: () => Navigator.pop(context, true),
            child: const Text('Withdraw draft'),
          ),
        ],
      ),
    );
    if (confirmed != true) return;
    setState(() => _busy = true);
    try {
      await blueverseLoadingScreenController.track(
        () => widget.apiService.withdrawAlertDraft(
          alertId: alert.alertId,
          expectedVersion: alert.version,
        ),
      );
      if (!mounted) return;
      _setNotice(
        '“${alert.title}” was withdrawn. Its history remains available to advisory managers.',
        error: false,
      );
      await _loadData();
    } on CoastalOperationsApiException catch (error) {
      _setNotice(error.message, error: true);
    } on Object {
      _setNotice(
        'We could not withdraw this draft. Refresh and retry.',
        error: true,
      );
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _openAlertForm({CoastalAlert? existing}) async {
    if (_navigateView(
      false,
      existing == null ? 'create' : 'edit',
      existing?.alertId,
    )) {
      return;
    }
    setState(() {
      _formAssessments = false;
      _editingAlert = existing;
      _editingAssessment = null;
    });
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
            style: TextButton.styleFrom(
              foregroundColor: Theme.of(context).colorScheme.error,
            ),
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
      final uploaded = await blueverseLoadingScreenController.track(
        () => widget.apiService.uploadAssessmentEvidence(
          assessmentId: assessment.assessmentId,
          fileName: picked.name,
          bytes: bytes,
        ),
      );
      if (!mounted) return;
      setState(() {
        _assessments = _assessments
            .map(
              (item) => item.assessmentId == assessment.assessmentId
                  ? item.copyWith(version: uploaded.assessmentVersion)
                  : item,
            )
            .toList(growable: false);
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

  Future<void> _removeEvidence(
    CoastalAssessment assessment,
    CoastalEvidence evidence,
  ) async {
    final scope = '$_loadedUserId:$_loadedGrants';
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: Text(
          'Remove this draft image?',
          style: TextStyle(color: Theme.of(context).colorScheme.error),
        ),
        content: const Text(
          'This image will be removed from the draft. Its recorded activity will be retained.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: const Text('Keep image'),
          ),
          FilledButton(
            style: FilledButton.styleFrom(
              backgroundColor: Theme.of(context).colorScheme.error,
              foregroundColor: Theme.of(context).colorScheme.onError,
            ),
            onPressed: () => Navigator.pop(context, true),
            child: const Text('Confirm removal'),
          ),
        ],
      ),
    );
    if (confirmed != true ||
        !mounted ||
        scope != '$_loadedUserId:$_loadedGrants') {
      return;
    }
    setState(() => _busy = true);
    try {
      await blueverseLoadingScreenController.track(
        () => widget.apiService.removeAssessmentEvidence(
          assessmentId: assessment.assessmentId,
          evidenceId: evidence.evidenceId,
          expectedVersion: assessment.version,
        ),
      );
      if (!mounted || scope != '$_loadedUserId:$_loadedGrants') return;
      _setNotice('The image was removed from the draft.', error: false);
      await _refreshDetail(assessment.assessmentId);
    } on Object catch (error) {
      if (mounted && scope == '$_loadedUserId:$_loadedGrants') {
        _setNotice(
          error is CoastalOperationsApiException
              ? error.message
              : 'The image could not be removed. Please retry.',
          error: true,
        );
      }
    } finally {
      if (mounted && scope == '$_loadedUserId:$_loadedGrants') {
        setState(() => _busy = false);
      }
    }
  }

  Future<void> _viewEvidence(
    String assessmentId,
    CoastalEvidence evidence,
  ) async {
    try {
      final bytes = await widget.apiService.getEvidenceImage(
        assessmentId: assessmentId,
        evidenceId: evidence.evidenceId,
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

  void _setNotice(String message, {required bool error}) {
    if (!mounted) return;
    setState(() {
      _notice = message;
      _noticeIsError = error;
    });
  }

  Widget _assessmentCard(CoastalAssessment assessment) =>
      CoastalOperationsRecordCard(
        record: assessment,
        onOpen: () {
          if (_navigateView(true, 'detail', assessment.assessmentId)) return;
          setState(() => _selectedAssessment = assessment);
          unawaited(_loadDetail(assessment));
        },
        onActivity: _canReadAudit
            ? () => _openActivity(assessment.assessmentId, true)
            : null,
      );

  Widget _assessmentDetails(CoastalAssessment assessment) {
    final detail = _details[assessment.assessmentId];
    final status = _statusByAssessment[assessment.assessmentId];
    final history = _historyByAssessment[assessment.assessmentId] ?? const [];
    final detailError = _detailErrors[assessment.assessmentId];
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(20),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            if (detailError != null)
              _NoticeBox(text: detailError, error: true)
            else if (detail == null)
              const Padding(
                padding: EdgeInsets.all(12),
                child: LinearProgressIndicator(),
              )
            else ...[
              _InfoSurface(
                title: 'Review this saved assessment',
                child: Text(
                  widget.fromLogs
                      ? 'This is a read-only view of the retained assessment and its coastal context.'
                      : 'This is a read-only view. Choose Edit draft before changing an unpublished assessment.',
                ),
              ),
              const SizedBox(height: 12),
              _InfoSurface(
                title: 'Related coastal record',
                child: SelectableText(
                  '${_humanize(detail.assessment.targetType)} · ${detail.assessment.targetId}',
                ),
              ),
              const SizedBox(height: 12),
              _InfoSurface(
                title: 'Related coastal plan',
                child: SelectableText(
                  detail.assessment.sourceWorkflowId ?? 'No plan linked',
                ),
              ),
              const SizedBox(height: 12),
              Align(
                alignment: Alignment.centerLeft,
                child: Text(
                  'Period: ${_formatDate(detail.assessment.periodStartsAt)} – ${_formatDate(detail.assessment.periodEndsAt)}',
                  style: Theme.of(context).textTheme.bodySmall,
                ),
              ),
              const SizedBox(height: 14),
              if (detail.assessment.workflowStatus == 'DRAFT') ...[
                _InfoSurface(
                  title: 'Draft assessment',
                  child: Text(
                    'Coastal context is checked after you submit this draft.',
                  ),
                ),
                if (!widget.fromLogs &&
                    (_canUpdateAssessment ||
                        _canSubmitAssessment ||
                        _canDeleteAssessment)) ...[
                  const SizedBox(height: 12),
                  Wrap(
                    spacing: 8,
                    runSpacing: 6,
                    children: [
                      if (_canUpdateAssessment)
                        OutlinedButton(
                          onPressed: _busy
                              ? null
                              : () => _openAssessmentForm(
                                  existing: detail.assessment,
                                ),
                          child: const Text('Edit draft'),
                        ),
                      if (_canSubmitAssessment)
                        FilledButton.tonal(
                          onPressed: _busy
                              ? null
                              : () => _handleAssessmentDraftAction(
                                  detail.assessment,
                                  submit: true,
                                ),
                          child: Text(
                            widget.section == CoastalOperationsSection.all
                                ? 'Submit for review'
                                : 'Publish assessment',
                          ),
                        ),
                      if (_canDeleteAssessment)
                        TextButton(
                          style: TextButton.styleFrom(
                            foregroundColor: Theme.of(context)
                                .colorScheme
                                .error,
                          ),
                          onPressed: _busy
                              ? null
                              : () => _handleAssessmentDraftAction(
                                  detail.assessment,
                                  submit: false,
                                ),
                          child: const Text('Cancel draft'),
                        ),
                    ],
                  ),
                ],
              ],
              if (detail.assessment.workflowStatus != 'DRAFT') ...[
                _InfoSurface(
                  title: 'Coastal context',
                  child: Text(_humanize(detail.assessment.aiDependencyStatus)),
                ),
              ],
              if (detail.assessment.workflowStatus != 'DRAFT' &&
                  detail.assessment.aiDependencyStatus == 'NOT_CONNECTED') ...[
                const SizedBox(height: 12),
                const _NoticeBox(
                  text: 'Coastal context was recorded, but automated proposals are not available yet. No operational change has been suggested or applied.',
                ),
              ],
              if (detail.assessment.workflowStatus != 'DRAFT' &&
                  detail.assessment.aiDependencyStatus == 'UNAVAILABLE') ...[
                const SizedBox(height: 12),
                const _NoticeBox(
                  text: 'Coastal context could not be fully checked right now. The assessment remains submitted; refresh later to see the latest information.',
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
              if (_canDecideAssessment &&
                  detail.assessment.workflowStatus != 'DRAFT') ...[
                const SizedBox(height: 10),
                const _NoticeBox(
                  text: 'Reviewer decisions will be available when this assessment contains a validated proposal. There is no proposal to approve or apply yet.',
                ),
              ],
              if (_canReadAudit)
                TextButton.icon(
                  onPressed: () => _openActivity(assessment.assessmentId, true),
                  icon: const Icon(Icons.history),
                  label: const Text('View activity'),
                ),
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
                    if (!widget.fromLogs &&
                        _canUploadEvidence &&
                        detail.assessment.workflowStatus == 'DRAFT' &&
                        detail.evidence.length < 5)
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
                    child: Text(
                      'Evidence details require separate read access.',
                    ),
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
                      trailing: Row(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          IconButton(
                            tooltip: 'View evidence image',
                            onPressed: _busy
                                ? null
                                : () => _viewEvidence(
                                    assessment.assessmentId,
                                    evidence,
                                  ),
                            icon: const Icon(Icons.visibility_outlined),
                          ),
                          if (!widget.fromLogs &&
                              _canUploadEvidence &&
                              detail.assessment.workflowStatus == 'DRAFT')
                            IconButton(
                              tooltip: 'Remove evidence image',
                              color: Theme.of(context).colorScheme.error,
                              onPressed: _busy
                                  ? null
                                  : () => _removeEvidence(
                                      detail.assessment,
                                      evidence,
                                    ),
                              icon: const Icon(Icons.delete_outline),
                            ),
                        ],
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
                    subtitle: Text('Updated ${_formatDate(status.updatedAt)}'),
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
      ),
    );
  }

  Future<void> _openAlertDetails(CoastalAlert alert) async {
    final scope = '$_loadedUserId:$_loadedGrants';
    setState(() {
      _selectedAlertId = alert.alertId;
      _selectedAlert = null;
      _alertDetailLoading = true;
      _alertDetailError = null;
    });
    try {
      final page = await widget.apiService.listAlerts(
        filters: {'recordId': alert.alertId},
      );
      if (!mounted ||
          scope != '$_loadedUserId:$_loadedGrants' ||
          _selectedAlertId != alert.alertId) {
        return;
      }
      final matching = page.items.where(
        (item) => item.alertId == alert.alertId,
      );
      setState(() {
        _selectedAlert = matching.isEmpty ? null : matching.first;
        _alertDetailError = matching.isEmpty
            ? 'This advisory is unavailable or outside your current access.'
            : null;
      });
    } on Object catch (error) {
      if (mounted &&
          scope == '$_loadedUserId:$_loadedGrants' &&
          _selectedAlertId == alert.alertId) {
        setState(
          () => _alertDetailError = error is CoastalOperationsApiException
              ? error.message
              : 'The latest advisory could not be loaded. Return to the list and retry.',
        );
      }
    } finally {
      if (mounted &&
          scope == '$_loadedUserId:$_loadedGrants' &&
          _selectedAlertId == alert.alertId) {
        setState(() => _alertDetailLoading = false);
      }
    }
  }

  Widget _alertCard(CoastalAlert alert, {bool focused = false}) {
    if (!focused) {
      return CoastalOperationsRecordCard(
        record: alert,
        onOpen: () {
          if (_navigateView(false, 'detail', alert.alertId)) return;
          unawaited(_openAlertDetails(alert));
        },
        onActivity: _canReadAudit
            ? () => _openActivity(alert.alertId, false)
            : null,
        actions: !widget.fromLogs
            ? Wrap(
                spacing: 8,
                children: [
                  if (_canUpdateAlerts && alert.lifecycle == 'PROPOSED')
                    TextButton(
                      onPressed: () => _openAlertForm(existing: alert),
                      child: const Text('Edit draft'),
                    ),
                  if (_canDeleteAlerts && alert.lifecycle == 'PROPOSED')
                    TextButton(
                      style: TextButton.styleFrom(
                        foregroundColor: Theme.of(context).colorScheme.error,
                      ),
                      onPressed: _busy
                          ? null
                          : () => _withdrawAlertDraft(alert),
                      child: const Text('Withdraw draft'),
                    ),
                  if (_canPublishAlerts && alert.lifecycle == 'PROPOSED')
                    FilledButton.tonal(
                      onPressed: _busy
                          ? null
                          : () => _decideAlert(alert, 'PUBLISH'),
                      child: const Text('Publish advisory'),
                    ),
                  if (_canResolveAlerts && alert.lifecycle == 'ACTIVE')
                    OutlinedButton(
                      onPressed: () => _decideAlert(alert, 'RESOLVE'),
                      child: const Text('Resolve advisory'),
                    ),
                ],
              )
            : null,
      );
    }
    final canEdit =
        !widget.fromLogs && _canUpdateAlerts && alert.lifecycle == 'PROPOSED';
    final canWithdraw =
        !widget.fromLogs && _canDeleteAlerts && alert.lifecycle == 'PROPOSED';
    final canPublish =
        !widget.fromLogs && _canPublishAlerts && alert.lifecycle == 'PROPOSED';
    final canResolve =
        !widget.fromLogs && _canResolveAlerts && alert.lifecycle == 'ACTIVE';
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
                        _humanize(alert.targetType),
                        style: Theme.of(context).textTheme.labelSmall,
                      ),
                      const SizedBox(height: 6),
                      if (focused)
                        Text(
                          alert.title,
                          style: Theme.of(context).textTheme.titleMedium,
                        )
                      else
                        TextButton(
                          onPressed: () => _openAlertDetails(alert),
                          child: Text(
                            alert.title,
                            style: Theme.of(context).textTheme.titleMedium,
                          ),
                        ),
                    ],
                  ),
                ),
                _StatusPill(value: alert.lifecycle),
              ],
            ),
            const SizedBox(height: 10),
            Text(
              'ID: ${alert.alertId}',
              style: Theme.of(context).textTheme.bodySmall,
            ),
            Text(
              'Created ${_formatDate(alert.createdAt)}',
              style: Theme.of(context).textTheme.bodySmall,
            ),
            Text(
              'Updated ${_formatDate(alert.updatedAt)}',
              style: Theme.of(context).textTheme.bodySmall,
            ),
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
            if (canEdit || canWithdraw || canPublish || canResolve) ...[
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
                  if (canWithdraw)
                    OutlinedButton(
                      style: OutlinedButton.styleFrom(
                        foregroundColor: Theme.of(context).colorScheme.error,
                        side: BorderSide(
                          color: Theme.of(context).colorScheme.error,
                        ),
                      ),
                      onPressed: _busy
                          ? null
                          : () => _withdrawAlertDraft(alert),
                      child: const Text('Withdraw draft'),
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
            if (_canReadAudit)
              TextButton.icon(
                onPressed: () => _openActivity(alert.alertId, false),
                icon: const Icon(Icons.history),
                label: const Text('View activity'),
              ),
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
                : Column(
                    children: [
                      _navigation(),
                      Expanded(
                        child: Stack(
                          children: [
                            Offstage(
                              offstage: _focused,
                              child: _buildWorkspace(context),
                            ),
                            if (_focused) _focusedWorkspace(context),
                          ],
                        ),
                      ),
                      const Padding(
                        padding: EdgeInsets.all(8),
                        child: Text('BLUEVERSE · Care for the coast'),
                      ),
                    ],
                  ),
          ),
        );
      },
    );
  }

  Widget _focusedWorkspace(BuildContext context) {
    final assessment =
        _formAssessments ??
        (_selectedAssessment != null ||
            (widget.section == CoastalOperationsSection.assessments &&
                _selectedAlertId == null));
    final name = widget.fromLogs
        ? 'Logs'
        : assessment
        ? 'Assessments'
        : 'Alerts';
    final title = _formAssessments != null
        ? (_editingAssessment != null || _editingAlert != null
              ? 'Edit draft'
              : assessment
              ? 'New assessment'
              : 'New advisory')
        : _selectedAssessment?.title ??
              _selectedAlert?.title ??
              (_selectedAlertId != null
                  ? 'Advisory details'
                  : 'Assessment details');
    return ListView(
      padding: const EdgeInsets.all(24),
      children: [
        Align(
          alignment: Alignment.centerLeft,
          child: TextButton.icon(
            onPressed: _backToList,
            icon: const Icon(Icons.arrow_back),
            label: Text('Back to $name'),
          ),
        ),
        const SizedBox(height: 16),
        Focus(
          autofocus: true,
          child: Semantics(
            header: true,
            child: Text(
              title,
              style: Theme.of(context).textTheme.headlineMedium,
            ),
          ),
        ),
        const SizedBox(height: 20),
        if (_notice != null) _NoticeBox(text: _notice!, error: _noticeIsError),
        if (_restoreBusy) const LinearProgressIndicator(),
        if (_restoreError != null)
          _NoticeBox(text: _restoreError!, error: true),
        if (_formAssessments != null)
          CoastalDraftDialog(
            key: ValueKey(
              'form:$_formAssessments:${_editingAssessment?.assessmentId}:${_editingAlert?.alertId}',
            ),
            apiService: widget.apiService,
            assessments: _formAssessments!,
            assessment: _editingAssessment,
            alert: _editingAlert,
            embedded: true,
            onSaved: _draftSaved,
            onCancel: _backToList,
          )
        else if (_selectedAssessment != null) ...[
          Text(
            widget.fromLogs
                ? 'Review this retained assessment and its coastal context. Its fields are read-only in this view.'
                : 'Review the saved purpose and coastal context here. Choose Edit draft when you are ready to change an unpublished assessment.',
            style: Theme.of(context).textTheme.bodyMedium,
          ),
          const SizedBox(height: 12),
          Text(_selectedAssessment!.objective),
          _assessmentDetails(_selectedAssessment!),
        ] else if (_alertDetailError != null)
          _NoticeBox(text: _alertDetailError!, error: true)
        else if (_alertDetailLoading)
          const LinearProgressIndicator()
        else if (_selectedAlert != null) ...[
          Text(
            widget.fromLogs
                ? 'Review this retained advisory as saved; records opened from Logs are read-only.'
                : 'Review the audience, dates and coastal link here. Choose Edit draft before changing an unpublished advisory.',
            style: Theme.of(context).textTheme.bodyMedium,
          ),
          const SizedBox(height: 12),
          _alertCard(_selectedAlert!, focused: true),
        ],
      ],
    );
  }

  Widget _buildWorkspace(BuildContext context) {
    return RefreshIndicator(
      onRefresh: _loadData,
      child: ListView(
        padding: const EdgeInsets.fromLTRB(24, 24, 24, 40),
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
                  const SizedBox(height: 30),
                  const Text('OPERATIONS WORKSPACE'),
                  const SizedBox(height: 8),
                  Text(
                    widget.section == CoastalOperationsSection.all
                        ? 'Assessments and advisories'
                        : _showAssessments
                        ? 'Assessments'
                        : 'Alerts',
                    style: Theme.of(context).textTheme.headlineSmall,
                  ),
                  const SizedBox(height: 6),
                  const Text(
                    'Review coastal assessments, follow important context, and share updates with the right people.',
                    style: TextStyle(height: 1.45),
                  ),
                  const SizedBox(height: 14),
                  Wrap(
                    spacing: 10,
                    runSpacing: 8,
                    children: [
                      if (_showAssessments && _canCreateAssessment)
                        FilledButton.icon(
                          onPressed: _busy ? null : _openAssessmentForm,
                          icon: const Icon(Icons.add),
                          label: const Text('New assessment'),
                        ),
                      if (_showAlerts && _canCreateAlerts)
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
                  if (_showAssessments && _canReadQueue) ...[
                    const SizedBox(height: 10),
                    const Align(
                      alignment: Alignment.centerLeft,
                      child: _StatusPill(value: 'REVIEW QUEUE'),
                    ),
                  ],
                  const SizedBox(height: 32),
                  if (_showAssessments) ...[
                    _search(true),
                    if (_assessmentError != null)
                      _NoticeBox(text: _assessmentError!, error: true),
                    if (!_canReadAssessments)
                      const _InfoSurface(
                        title: 'Assessment reading is not included',
                        child: Text(
                          'Your current permissions allow other Coastal Operations actions, but do not include assessment reading.',
                        ),
                      )
                    else if (_assessments.isEmpty && !_loading)
                      const _InfoSurface(
                        title: 'No assessments to show yet',
                        child: Text(
                          'Saved drafts and submitted reviews will appear here.',
                        ),
                      )
                    else ...[
                      for (final assessment in _assessments)
                        _assessmentCard(assessment),
                    ],
                    const SizedBox(height: 32),
                    if (_canReadAssessments) _pagination(true),
                  ],
                  if (_showAlerts) ...[
                    _search(false),
                    if (_alertError != null)
                      _NoticeBox(text: _alertError!, error: true),
                    if (!_canReadAlerts)
                      const _InfoSurface(
                        title: 'Advisory reading is not included',
                        child: Text(
                          'Your current permissions do not include advisory reading. You may prepare a draft if you have advisory management access.',
                        ),
                      )
                    else if (_alerts.isEmpty && !_loading)
                      const _InfoSurface(
                        title: 'No advisories to show',
                        child: Text(
                          'Active public updates and authorized operations drafts will appear here.',
                        ),
                      )
                    else ...[
                      for (final alert in _alerts) _alertCard(alert),
                    ],
                    if (_canReadAlerts) _pagination(false),
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
              _showAssessments
                  ? 'assets/coastal/onboarding/assessment-hero.png'
                  : 'assets/coastal/onboarding/alerts-hero.png',
              semanticLabel: _showAssessments
                  ? 'Coastal field workers inspecting a beach access path'
                  : 'A coastal steward guiding visitors toward a safe path',
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
                      widget.section == CoastalOperationsSection.alerts
                          ? 'Clear updates. Safer coastal days.'
                          : 'Look after the places we share.',
                      style: Theme.of(context).textTheme.headlineSmall
                          ?.copyWith(
                            color: Colors.white,
                            fontWeight: FontWeight.w700,
                            height: 1.1,
                          ),
                    ),
                    const SizedBox(height: 8),
                    Text(
                      widget.section == CoastalOperationsSection.alerts
                          ? 'Prepare a clear notice, choose its audience, and follow it through publication and resolution.'
                          : 'Gather evidence in a draft, publish it for assessment, and follow the recommendation and human review when available.',
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
      'DRAFT',
      'NOT_REQUESTED',
      'NOT_STARTED',
      'CANCELLED',
      'WITHDRAWN',
      'EXPIRED',
      'RESOLVED',
      'LOW',
      'MODERATE',
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
