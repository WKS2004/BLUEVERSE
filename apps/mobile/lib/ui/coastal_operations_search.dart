import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';

class CoastalOperationsSearch extends StatefulWidget {
  const CoastalOperationsSearch({
    required this.assessments,
    required this.canManage,
    required this.onApply,
    this.loading = false,
    this.active = true,
    super.key,
  });
  final bool assessments;
  final bool canManage;
  final bool loading;
  final bool active;
  final ValueChanged<Map<String, String>> onApply;
  @override
  State<CoastalOperationsSearch> createState() =>
      _CoastalOperationsSearchState();
}

class _CoastalOperationsSearchState extends State<CoastalOperationsSearch> {
  final _form = GlobalKey<FormState>();
  final _search = TextEditingController();
  final _recordId = TextEditingController();
  final _targetId = TextEditingController();
  Timer? _timer;
  Map<String, String> _sent = const {};
  bool _open = false;
  bool _advanced = false;
  String? _error;
  String _tab = 'All', _targetType = '', _severity = '', _visibility = '';
  @override
  void didUpdateWidget(CoastalOperationsSearch oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (!widget.active) {
      _timer?.cancel();
    } else if (!oldWidget.active) {
      _schedule('');
    }
  }

  @override
  void dispose() {
    _timer?.cancel();
    _search.dispose();
    _recordId.dispose();
    _targetId.dispose();
    super.dispose();
  }

  void _schedule(String _) {
    _timer?.cancel();
    _timer = Timer(const Duration(milliseconds: 500), _apply);
  }

  void _apply() {
    _timer?.cancel();
    final invalidId =
        _advanced &&
        [_recordId.text, _targetId.text].any(
          (id) =>
              id.trim().isNotEmpty &&
              !RegExp(
                r'^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$',
              ).hasMatch(id.trim()),
        );
    if (invalidId) {
      setState(() => _error = 'Enter a valid record ID in Advanced search.');
      return;
    }
    if (_error != null) setState(() => _error = null);
    if (!(_form.currentState?.validate() ?? false)) return;
    final query = <String, String>{
      if (_search.text.trim().isNotEmpty) 'search': _search.text.trim(),
      if (_advanced && _recordId.text.trim().isNotEmpty)
        'recordId': _recordId.text.trim(),
      if (_advanced && _targetId.text.trim().isNotEmpty)
        'targetId': _targetId.text.trim(),
      if (_targetType.isNotEmpty) 'targetType': _targetType,
      if (widget.assessments) ...{
        if (_tab == 'Drafts') 'workflowStatus': 'DRAFT',
        if (_tab == 'Drafts') 'onlyMine': 'true',
        if (_tab == 'Published') 'publishedOnly': 'true',
        if (_tab == 'History') 'includeCancelled': 'true',
      } else ...{
        if (_tab == 'Drafts') 'lifecycle': 'PROPOSED',
        if (_tab == 'Active') 'lifecycle': 'ACTIVE',
        if (_tab == 'History') 'history': 'true',
        if (_severity.isNotEmpty) 'severity': _severity,
        if (_visibility.isNotEmpty && widget.canManage)
          'visibility': _visibility,
      },
    };
    if (mapEquals(query, _sent)) return;
    _sent = query;
    widget.onApply(query);
  }

  void _reset() {
    _timer?.cancel();
    setState(() {
      _tab = 'All';
      _targetType = '';
      _severity = '';
      _visibility = '';
      _advanced = false;
      _error = null;
      _open = false;
      _search.clear();
      _recordId.clear();
      _targetId.clear();
    });
    _apply();
  }

  Widget _filter(
    String label,
    String selected,
    List<String> values,
    ValueChanged<String> onChange,
  ) => DropdownButtonFormField<String>(
    key: ValueKey('$label:$selected'),
    initialValue: selected,
    isExpanded: true,
    decoration: InputDecoration(labelText: label),
    items: [
      const DropdownMenuItem(value: '', child: Text('All')),
      ...values.map(
        (value) => DropdownMenuItem(value: value, child: Text(value)),
      ),
    ],
    onChanged: (value) {
      setState(() => onChange(value ?? ''));
      _apply();
    },
  );
  Widget _id(TextEditingController controller, String label) => TextFormField(
    controller: controller,
    enabled: _advanced,
    maxLength: 36,
    onChanged: _schedule,
    decoration: InputDecoration(labelText: label),
    validator: (value) =>
        !_advanced ||
            (value ?? '').trim().isEmpty ||
            RegExp(
              r'^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$',
            ).hasMatch(value!.trim())
        ? null
        : 'Enter a valid record ID.',
  );
  @override
  Widget build(BuildContext context) {
    final tabs = widget.assessments
        ? ['All', 'Drafts', 'Published', if (widget.canManage) 'History']
        : [
            'All',
            if (widget.canManage) 'Drafts',
            'Active',
            if (widget.canManage) 'History',
          ];
    return Form(
      key: _form,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text(
            widget.assessments
                ? 'Find your coastal reviews'
                : 'Find your coastal updates',
            style: Theme.of(context).textTheme.titleLarge,
          ),
          const SizedBox(height: 12),
          Wrap(
            spacing: 8,
            runSpacing: 8,
            children: tabs
                .map(
                  (value) => ChoiceChip(
                    label: Text(value),
                    selected: _tab == value,
                    onSelected: (_) {
                      setState(() => _tab = value);
                      _apply();
                    },
                  ),
                )
                .toList(),
          ),
          const SizedBox(height: 16),
          TextFormField(
            controller: _search,
            maxLength: 160,
            textInputAction: TextInputAction.search,
            onChanged: _schedule,
            onFieldSubmitted: (_) => _apply(),
            decoration: InputDecoration(
              labelText: 'Search records',
              hintText: widget.assessments
                  ? 'Search the Assessments'
                  : 'Search the Alerts',
              counterText: '',
              suffixIcon: Row(
                mainAxisSize: MainAxisSize.min,
                children: [
                  IconButton(
                    tooltip: 'Filters',
                    isSelected: _open,
                    onPressed: () => setState(() => _open = !_open),
                    icon: const Icon(Icons.tune),
                  ),
                  IconButton(
                    tooltip: 'Reset filters',
                    onPressed: _reset,
                    icon: const Icon(Icons.restart_alt),
                  ),
                  IconButton(
                    tooltip: 'Search',
                    onPressed: _apply,
                    icon: widget.loading
                        ? Semantics(
                            label: 'Searching records',
                            liveRegion: true,
                            child: SizedBox(
                              width: 20,
                              height: 20,
                              child: CircularProgressIndicator(strokeWidth: 2),
                            ),
                          )
                        : const Icon(Icons.search),
                  ),
                ],
              ),
            ),
          ),
          if (_error != null)
            Semantics(
              liveRegion: true,
              child: Text(
                _error!,
                style: TextStyle(color: Theme.of(context).colorScheme.error),
              ),
            ),
          if (_open) ...[
            const SizedBox(height: 16),
            _filter('Coastal record type', _targetType, const [
              'DESTINATION',
              'ACTIVITY',
              'OFFERING',
              'SESSION',
            ], (value) => _targetType = value),
            if (!widget.assessments) ...[
              const SizedBox(height: 12),
              _filter('Severity', _severity, const [
                'LOW',
                'MODERATE',
                'HIGH',
                'CRITICAL',
              ], (value) => _severity = value),
              if (widget.canManage) ...[
                const SizedBox(height: 12),
                _filter('Audience', _visibility, const [
                  'PUBLIC',
                  'OPERATIONS',
                ], (value) => _visibility = value),
              ],
            ],
            ExpansionTile(
              initiallyExpanded: _advanced,
              title: const Text('Advanced search'),
              onExpansionChanged: (value) {
                setState(() => _advanced = value);
                if (!value) _apply();
              },
              children: [
                _id(_recordId, 'Assessment or alert ID'),
                _id(_targetId, 'Coastal record ID'),
                const Text(
                  'IDs appear beside titles and creation dates in search results.',
                ),
              ],
            ),
          ],
          const SizedBox(height: 20),
        ],
      ),
    );
  }
}
