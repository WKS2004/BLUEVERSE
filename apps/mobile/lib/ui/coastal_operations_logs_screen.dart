import 'package:flutter/material.dart';

import '../data/services/coastal_operations_api_service.dart';
import '../features/coastal_operations/coastal_operations_permissions.dart';
import 'account_screens.dart';
import 'auth_view_model.dart';
import 'coastal_operations_activity.dart';
import 'coastal_operations_search.dart';
import 'coastal_operations_record_card.dart';
import '../data/models/coastal_operations_models.dart';

class CoastalOperationsLogsScreen extends StatefulWidget {
  const CoastalOperationsLogsScreen({
    required this.viewModel,
    required this.apiService,
    super.key,
  });
  final AuthViewModel viewModel;
  final CoastalOperationsApiService apiService;
  @override
  State<CoastalOperationsLogsScreen> createState() => _LogsState();
}

class _LogsState extends State<CoastalOperationsLogsScreen> {
  final _scroll = ScrollController();
  bool _assessments = true;
  bool _loading = true;
  String? _error, _scope, _next;
  int _size = 25, _page = 0, _generation = 0;
  List<String?> _cursors = [null];
  Map<String, String> _query = const {};
  List<Object> _records = const [];
  Set<String> get _grants => (widget.viewModel.user?.permissions ?? <String>[])
      .map((p) => p.toLowerCase())
      .toSet();
  bool get _assessmentAccess =>
      _grants.contains(CoastalOperationsPermissions.auditRead) &&
      (_grants.contains(CoastalOperationsPermissions.assessmentRead) ||
          _grants.contains(CoastalOperationsPermissions.assessmentQueueRead));
  bool get _alertAccess =>
      _grants.contains(CoastalOperationsPermissions.auditRead) &&
      (_grants.contains(CoastalOperationsPermissions.alertRead) ||
          CoastalOperationsPermissions.canManageAlerts(_grants));
  @override
  void initState() {
    super.initState();
    widget.viewModel.addListener(_accountChanged);
    _accountChanged();
  }

  @override
  void dispose() {
    _generation++;
    _scroll.dispose();
    widget.viewModel.removeListener(_accountChanged);
    super.dispose();
  }

  void _accountChanged() {
    final sorted = _grants.toList()..sort();
    final scope = '${widget.viewModel.user?.id}:${sorted.join(',')}';
    if (_scope == scope) return;
    _scope = scope;
    _assessments = _assessmentAccess;
    _records = const [];
    _query = const {};
    _resetPage();
    _load();
  }

  void _resetPage() {
    _page = 0;
    _cursors = [null];
    _next = null;
  }

  Future<void> _load() async {
    final generation = ++_generation;
    if (!(_assessments ? _assessmentAccess : _alertAccess)) {
      if (mounted) {
        setState(() {
          _loading = false;
          _records = const [];
        });
      }
      return;
    }
    setState(() {
      _loading = true;
      _error = null;
    });
    final filters = {..._query, 'pageSize': '$_size'};
    final cursor = _cursors[_page];
    try {
      List<Object> records;
      String? next;
      if (_assessments) {
        final result = await widget.apiService.listAssessmentLogRecords(
          filters: filters,
          cursor: cursor,
        );
        records = result.items;
        next = result.nextCursor;
      } else {
        final result = await widget.apiService.listAlertLogRecords(
          filters: filters,
          cursor: cursor,
        );
        records = result.items;
        next = result.nextCursor;
      }
      if (!mounted || generation != _generation) return;
      setState(() {
        _records = records;
        _next = next;
      });
    } on Object catch (error) {
      if (mounted && generation == _generation) {
        setState(
          () => _error = error is CoastalOperationsApiException
              ? error.message
              : 'Logs could not be loaded. Please retry.',
        );
      }
    } finally {
      if (mounted && generation == _generation) {
        setState(() => _loading = false);
      }
    }
  }

  void _switch(bool assessments) {
    if (_scroll.hasClients) _scroll.jumpTo(0);
    setState(() {
      _assessments = assessments;
      _query = const {};
      _records = const [];
      _resetPage();
    });
    _load();
  }

  void _activity(String id) {
    showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      builder: (context) => SizedBox(
        height: MediaQuery.sizeOf(context).height * .85,
        child: CoastalOperationsActivity(
          apiService: widget.apiService,
          id: id,
          assessment: _assessments,
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) => ListenableBuilder(
    listenable: widget.viewModel,
    builder: (context, _) => Scaffold(
      appBar: AppBar(
        title: const Text('Coastal Operations logs'),
        actions: [
          AuthAccountSwitcher(viewModel: widget.viewModel),
          AuthAdminNavigationMenu(viewModel: widget.viewModel),
          IconButton(
            tooltip: 'Open your profile',
            onPressed: () => Navigator.pushNamed(context, '/profile'),
            icon: const Icon(Icons.person_outline),
          ),
        ],
      ),
      body: SafeArea(
        child: Column(
          children: [
            Wrap(
              spacing: 12,
              children: [
                if (CoastalOperationsPermissions.hasAssessmentAccess(_grants))
                  TextButton(
                    onPressed: () => Navigator.pushReplacementNamed(
                      context,
                      '/operations/assessments',
                    ),
                    child: const Text('Assessments'),
                  ),
                if (CoastalOperationsPermissions.hasAlertAccess(_grants))
                  TextButton(
                    onPressed: () => Navigator.pushReplacementNamed(
                      context,
                      '/operations/alerts',
                    ),
                    child: const Text('Alerts'),
                  ),
                const TextButton(onPressed: null, child: Text('Logs')),
              ],
            ),
            if (_assessmentAccess || _alertAccess)
              Padding(
                padding: const EdgeInsets.symmetric(
                  horizontal: 20,
                  vertical: 8,
                ),
                child: Wrap(
                  spacing: 12,
                  runSpacing: 8,
                  children: [
                    if (_assessmentAccess)
                      ChoiceChip(
                        label: const Text('Assessment logs'),
                        selected: _assessments,
                        onSelected: (_) => _switch(true),
                      ),
                    if (_alertAccess)
                      ChoiceChip(
                        label: const Text('Alert logs'),
                        selected: !_assessments,
                        onSelected: (_) => _switch(false),
                      ),
                  ],
                ),
              ),
            Expanded(
              child: !(_assessmentAccess || _alertAccess)
                  ? const Center(
                      child: Text(
                        'Your permissions do not allow access to Coastal Operations logs.',
                      ),
                    )
                  : ListView(
                      controller: _scroll,
                      padding: const EdgeInsets.all(20),
                      children: [
                        const SizedBox(height: 16),
                        const Text(
                          'Follow retained drafts, published records and inactive records through their recorded activity.',
                        ),
                        const SizedBox(height: 20),
                        CoastalOperationsSearch(
                          key: ValueKey('$_scope:$_assessments'),
                          assessments: _assessments,
                          canManage: true,
                          loading: _loading,
                          onApply: (query) {
                            setState(() {
                              _query = query;
                              _resetPage();
                            });
                            _load();
                          },
                        ),
                        if (_error != null) ...[
                          Text(
                            _error!,
                            style: TextStyle(
                              color: Theme.of(context).colorScheme.error,
                            ),
                          ),
                          TextButton(
                            onPressed: _loading ? null : _load,
                            child: const Text('Retry logs'),
                          ),
                        ],
                        if (!_loading && _error == null && _records.isEmpty)
                          Padding(
                            padding: const EdgeInsets.all(24),
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Text(
                                  _assessments
                                      ? 'No assessments to show yet'
                                      : 'No advisories to show',
                                  style: Theme.of(context).textTheme.titleLarge,
                                ),
                                Text(
                                  _assessments
                                      ? 'Saved drafts and submitted reviews will appear here.'
                                      : 'Active public updates and your proposed drafts will appear here.',
                                ),
                              ],
                            ),
                          ),
                        for (final record in _records)
                          CoastalOperationsRecordCard(
                            record: record,
                            onOpen: () {
                              final id = record is CoastalAssessment
                                  ? record.assessmentId
                                  : (record as CoastalAlert).alertId;
                              Navigator.pushNamed(
                                context,
                                Uri(
                                  path: _assessments
                                      ? '/operations/assessments'
                                      : '/operations/alerts',
                                  queryParameters: {
                                    'view': 'detail',
                                    'id': id,
                                    'origin': 'logs',
                                  },
                                ).toString(),
                              ).then((_) {
                                if (mounted) _load();
                              });
                            },
                            onActivity: () => _activity(
                              record is CoastalAssessment
                                  ? record.assessmentId
                                  : (record as CoastalAlert).alertId,
                            ),
                          ),
                        const Divider(height: 32),
                        CoastalOperationsPagination(
                          assessments: _assessments,
                          size: _size,
                          count: _records.length,
                          page: _page,
                          onSize: (value) {
                            setState(() {
                              _size = value;
                              _resetPage();
                            });
                            _load();
                          },
                          previous: _loading || _page == 0
                              ? null
                              : () {
                                  setState(() => _page--);
                                  _load();
                                },
                          next: _loading || _next == null || _error != null
                              ? null
                              : () {
                                  setState(() {
                                    _cursors = [
                                      ..._cursors.take(_page + 1),
                                      _next,
                                    ];
                                    _page++;
                                  });
                                  _load();
                                },
                        ),
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
    ),
  );
}
