import 'package:flutter/material.dart';

import '../../data/models/experience_models.dart';
import '../auth_view_model.dart';
import 'experience_view_model.dart';
import 'experience_widgets.dart';
import 'offering_schedule_manager_dialog.dart';

class CatalogueManagementScreen extends StatefulWidget {
  const CatalogueManagementScreen({
    super.key,
    required this.viewModel,
    required this.authViewModel,
  });
  final ExperienceViewModel viewModel;
  final AuthViewModel authViewModel;
  @override
  State<CatalogueManagementScreen> createState() =>
      _CatalogueManagementScreenState();
}

class _CatalogueManagementScreenState extends State<CatalogueManagementScreen>
    with SingleTickerProviderStateMixin {
  late final TabController _tabs;
  ExperienceViewModel get vm => widget.viewModel;
  String? _managementAccount;
  bool get _canManage =>
      widget.authViewModel.user?.permissions.any(
        (p) =>
            p == 'experiences.catalogue.manage' ||
            p == 'auth.role.system.manage',
      ) ??
      false;
  @override
  void initState() {
    super.initState();
    _tabs = TabController(
      length: 3,
      vsync: this,
      initialIndex: vm.managementTab.clamp(0, 2),
    );
    _tabs.addListener(_rememberTab);
    _managementAccount = widget.authViewModel.user?.id;
    widget.authViewModel.addListener(_accountChanged);
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted && _canManage) vm.loadManagementCatalog();
    });
  }

  void _accountChanged() {
    final account = widget.authViewModel.user?.id;
    if (account == _managementAccount) return;
    _managementAccount = account;
    if (_canManage) vm.loadManagementCatalog();
  }

  void _rememberTab() => vm.managementTab = _tabs.index;
  @override
  void dispose() {
    widget.authViewModel.removeListener(_accountChanged);
    _tabs.removeListener(_rememberTab);
    _tabs.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => ListenableBuilder(
    listenable: Listenable.merge([vm, widget.authViewModel]),
    builder: (context, _) => Scaffold(
      appBar: AppBar(
        title: const Text('Manage Coastal Experiences'),
        bottom: TabBar(
          controller: _tabs,
          isScrollable: true,
          tabAlignment: TabAlignment.start,
          padding: EdgeInsets.zero,
          tabs: const [
            Tab(text: 'Destinations', icon: Icon(Icons.place_outlined)),
            Tab(text: 'Activities', icon: Icon(Icons.surfing)),
            Tab(
              text: 'Offerings & Schedules',
              icon: Icon(Icons.local_offer_outlined),
            ),
          ],
        ),
      ),
      body: !_canManage
          ? Center(
              child: Padding(
                padding: const EdgeInsets.all(24),
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    const Icon(Icons.lock_outline, size: 48),
                    const SizedBox(height: 12),
                    const Text('Experience Management Restricted'),
                    const SizedBox(height: 8),
                    const Text(
                      'Editing destinations, activities, and offerings requires experience-management permission.',
                      textAlign: TextAlign.center,
                    ),
                    TextButton(
                      onPressed: () => Navigator.maybePop(context),
                      child: const Text('Back to Experiences'),
                    ),
                  ],
                ),
              ),
            )
          : SafeArea(
              top: false,
              child: TabBarView(
                controller: _tabs,
                children: [
                  ExperienceTabBody(child: _section(0)),
                  ExperienceTabBody(child: _section(1)),
                  ExperienceTabBody(child: _section(2)),
                ],
              ),
            ),
    ),
  );

  Widget _section(int section) {
    const titles = [
      'Give every coastal place a clear story.',
      'Bring coastal activities to life.',
      'Plan experiences with clear departure times.',
    ];
    const details = [
      'Keep reefs, beaches and lagoons easy to recognize and ready for thoughtful visits.',
      'Describe what visitors can do and the activity each experience belongs to.',
      'Connect destinations and activities, then maintain schedules and review publishing.',
    ];
    const names = ['destinations', 'activities', 'offerings'];
    if (vm.isLoadingManagementCatalog &&
        vm.managementDestinations.isEmpty &&
        vm.managementActivities.isEmpty &&
        vm.managementOfferings.isEmpty) {
      return const ExperienceLoading();
    }
    return RefreshIndicator(
      onRefresh: vm.loadManagementCatalog,
      child: ListView(
        key: PageStorageKey('manage-${names[section]}'),
        physics: const AlwaysScrollableScrollPhysics(),
        padding: const EdgeInsets.all(16),
        children: [
          ExperienceHero(
            title: titles[section],
            detail: details[section],
            image: 'assets/coastal/experiences/${names[section]}.jpg',
          ),
          if (vm.managementCatalogErrorMessage != null)
            ExperienceNotice(
              vm.managementCatalogErrorMessage!,
              isError: true,
              onRetry: vm.isLoadingManagementCatalog
                  ? null
                  : vm.loadManagementCatalog,
            ),
          if (vm.isLoadingManagementCatalog)
            const ExperienceNotice('Refreshing experience data…'),
          if (vm.isPerformingCatalogueAction)
            const ExperienceNotice('Saving your changes…'),
          if (vm.errorMessage != null)
            ExperienceNotice(vm.errorMessage!, isError: true),
          if (vm.successMessage != null) ExperienceNotice(vm.successMessage!),
          Align(
            alignment: Alignment.centerLeft,
            child: FilledButton.icon(
              key: Key(
                [
                  'btn-create-destination-dialog',
                  'btn-create-activity-dialog',
                  'btn-create-offering-dialog',
                ][section],
              ),
              onPressed: vm.isPerformingCatalogueAction
                  ? null
                  : () {
                      switch (section) {
                        case 0:
                          _showCreateDestinationDialog(context, vm);
                        case 1:
                          _showCreateActivityDialog(context, vm);
                        case 2:
                          _showCreateOfferingDialog(context, vm);
                      }
                    },
              icon: const Icon(Icons.add),
              label: Text(
                ['Add Destination', 'Add Activity', 'Add Offering'][section],
              ),
            ),
          ),
          const SizedBox(height: 16),
          if (section == 0) ...[
            Text(
              'Coastal Destinations (${vm.managementDestinations.length})',
              style: Theme.of(context).textTheme.titleLarge,
            ),
            if (vm.managementDestinations.isEmpty)
              const ExperienceNotice(
                'No destinations yet. Add a coastal place to get started.',
              ),
            ...vm.managementDestinations.map(
              (d) => _record(
                key: Key('manage-card-destination-${d.id}'),
                name: d.name,
                detail: d.region ?? 'Coastal destination',
                status: d.status,
                onEdit: () => _showEditDestinationDialog(context, vm, d),
                onDelete: () => _delete(
                  'Delete ${d.name}?',
                  'Linked experiences may prevent deletion.',
                  () => vm.repository.deleteDestination(d.id),
                ),
                onReview: () => _review(
                  d.name,
                  d.status,
                  (s) => vm.repository.evaluateDestinationPublication(d.id, s),
                  (s) => vm.repository.updateDestinationPublication(d.id, s),
                ),
              ),
            ),
          ] else if (section == 1) ...[
            Text(
              'Coastal Activities (${vm.managementActivities.length})',
              style: Theme.of(context).textTheme.titleLarge,
            ),
            if (vm.managementActivities.isEmpty)
              const ExperienceNotice(
                'No activities yet. Add an activity for your experiences.',
              ),
            ...vm.managementActivities.map(
              (a) => _record(
                key: Key('manage-card-activity-${a.id}'),
                name: a.name,
                detail: a.description ?? a.category ?? 'Coastal activity',
                status: a.status,
                onEdit: () => _showEditActivityDialog(context, vm, a),
                onDelete: () => _delete(
                  'Delete ${a.name}?',
                  'This also removes linked offerings.',
                  () => vm.repository.deleteActivity(a.id),
                ),
                onReview: () => _review(
                  a.name,
                  a.status,
                  (s) => vm.repository.evaluateActivityPublication(a.id, s),
                  (s) => vm.repository.updateActivityPublication(a.id, s),
                ),
              ),
            ),
          ] else ...[
            Text(
              'Experience Offerings (${vm.managementOfferings.length})',
              style: Theme.of(context).textTheme.titleLarge,
            ),
            if (vm.managementOfferings.isEmpty)
              const ExperienceNotice(
                'No offerings yet. Link a destination and activity to create one.',
              ),
            ...vm.managementOfferings.map(
              (o) => _record(
                key: Key('manage-card-offering-${o.id}'),
                name: o.title,
                detail:
                    '${o.destinationName} · ${o.price == null ? 'Price on request' : '${o.currency ?? 'LKR'} ${o.price!.toStringAsFixed(0)}'}',
                status: o.status,
                onEdit: () => _showEditOfferingDialog(context, vm, o),
                onDelete: () => _deleteOffering(context, vm, o),
                onReview: () => _review(
                  o.title,
                  o.status,
                  (s) => vm.repository.evaluateOfferingPublication(o.id, s),
                  (s) => vm.repository.updateOfferingPublication(o.id, s),
                ),
                onSchedules: () => _showScheduleManager(context, vm, o),
              ),
            ),
          ],
        ],
      ),
    );
  }

  Widget _record({
    required Key key,
    required String name,
    required String detail,
    required String status,
    required VoidCallback onEdit,
    required VoidCallback onDelete,
    required VoidCallback onReview,
    VoidCallback? onSchedules,
  }) => Card(
    key: key,
    margin: const EdgeInsets.only(top: 12),
    child: Padding(
      padding: const EdgeInsets.all(20),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text(name, style: Theme.of(context).textTheme.titleMedium),
          const SizedBox(height: 8),
          Text(detail),
          const SizedBox(height: 8),
          Text('Status: $status'),
          const SizedBox(height: 12),
          Wrap(
            spacing: 8,
            runSpacing: 8,
            children: [
              OutlinedButton(
                onPressed: vm.isPerformingCatalogueAction ? null : onEdit,
                child: const Text('Edit'),
              ),
              OutlinedButton(
                onPressed: vm.isPerformingCatalogueAction ? null : onDelete,
                child: const Text('Delete'),
              ),
              OutlinedButton(
                onPressed: vm.isPerformingCatalogueAction ? null : onReview,
                child: const Text('Review publishing'),
              ),
              if (onSchedules != null)
                OutlinedButton(
                  onPressed: vm.isPerformingCatalogueAction
                      ? null
                      : onSchedules,
                  child: const Text('Departure times'),
                ),
            ],
          ),
        ],
      ),
    ),
  );
  Future<void> _delete(
    String title,
    String detail,
    Future<void> Function() operation,
  ) async {
    if (!await _confirmDestructiveAction(
          context,
          title: title,
          message: detail,
        ) ||
        !mounted ||
        !_canManage) {
      return;
    }
    await vm.performCatalogueAction<bool>(() async {
      await operation();
      return true;
    }, successText: 'Removed from experiences.');
  }

  Future<void> _review(
    String name,
    String currentStatus,
    Future<PublicationEvaluationResponse> Function(String) evaluate,
    Future<Object> Function(String) apply,
  ) async {
    var target = currentStatus == 'DRAFT' ? 'PUBLISHED' : 'DRAFT';
    PublicationEvaluationResponse? result;
    String? message;
    var busy = false;
    await showDialog<void>(
      context: context,
      barrierDismissible: false,
      builder: (dialogContext) => StatefulBuilder(
        builder: (context, update) => PopScope(
          canPop: !busy,
          child: AlertDialog(
            title: Text('Review publishing: $name'),
            content: SingleChildScrollView(
              child: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Text('Current status: $currentStatus'),
                  const SizedBox(height: 12),
                  const Text(
                    'The service checks content and schedule readiness. Availability and safety are assessed separately.',
                  ),
                  const SizedBox(height: 12),
                  DropdownButtonFormField<String>(
                    initialValue: target,
                    isExpanded: true,
                    items: const ['DRAFT', 'PUBLISHED', 'ARCHIVED']
                        .map((s) => DropdownMenuItem(value: s, child: Text(s)))
                        .toList(),
                    onChanged: busy
                        ? null
                        : (value) => update(() {
                            target = value!;
                            result = null;
                            message = null;
                          }),
                  ),
                  if (busy)
                    const Padding(
                      padding: EdgeInsets.all(12),
                      child: Text('Checking with the coastal service…'),
                    ),
                  if (message != null)
                    ExperienceNotice(message!, isError: true),
                  if (result != null) ...[
                    const SizedBox(height: 12),
                    Text(
                      result!.canTransition
                          ? 'Ready to apply'
                          : 'Changes are needed before publishing',
                    ),
                    ...result!.reasons.map((reason) => Text('• $reason')),
                  ],
                ],
              ),
            ),
            actions: [
              TextButton(
                onPressed: busy ? null : () => Navigator.pop(dialogContext),
                child: const Text('Close'),
              ),
              OutlinedButton(
                onPressed: busy || !_canManage
                    ? null
                    : () async {
                        update(() {
                          busy = true;
                          message = null;
                          result = null;
                        });
                        try {
                          final response = await evaluate(target);
                          if (dialogContext.mounted) {
                            update(() => result = response);
                          }
                        } catch (_) {
                          if (dialogContext.mounted) {
                            update(
                              () => message = 'Publishing could not be reviewed. Try again.',
                            );
                          }
                        } finally {
                          if (dialogContext.mounted) update(() => busy = false);
                        }
                      },
                child: const Text('Evaluate Transition'),
              ),
              FilledButton(
                onPressed:
                    busy ||
                        result?.canTransition != true ||
                        result?.requestedStatus != target ||
                        !_canManage
                    ? null
                    : () async {
                        update(() => busy = true);
                        final saved = await vm.performCatalogueAction(
                          () => apply(target),
                          successText: 'Publication updated.',
                        );
                        if (!dialogContext.mounted) return;
                        if (saved != null) {
                          Navigator.pop(dialogContext);
                        } else {
                          update(() {
                            busy = false;
                            result = null;
                            message =
                                vm.errorMessage ??
                                'Could not apply this change. Review it again.';
                          });
                        }
                      },
                child: const Text('Apply'),
              ),
            ],
          ),
        ),
      ),
    );
  }

  void _showCreateDestinationDialog(
    BuildContext context,
    ExperienceViewModel vm,
  ) {
    final nameCtrl = TextEditingController();
    final slugCtrl = TextEditingController();
    final regionCtrl = TextEditingController();
    final descCtrl = TextEditingController();
    final latCtrl = TextEditingController();
    final lonCtrl = TextEditingController();

    vm.clearMessages();
    showDialog<void>(
      context: context,
      barrierDismissible: false,
      builder: (dialogContext) => ExperienceFormLifecycle(
        viewModel: vm,
        controllers: [
          nameCtrl,
          slugCtrl,
          regionCtrl,
          descCtrl,
          latCtrl,
          lonCtrl,
        ],
        child: ListenableBuilder(
          listenable: vm,
          builder: (context, _) => AlertDialog(
            title: const Text('Add Coastal Destination'),
            content: SingleChildScrollView(
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  if (vm.errorMessage != null)
                    ExperienceNotice(vm.errorMessage!, isError: true),
                  TextField(
                    controller: nameCtrl,
                    decoration: const InputDecoration(labelText: 'Name *'),
                  ),
                  TextField(
                    controller: slugCtrl,
                    decoration: const InputDecoration(
                      labelText: 'URL slug (optional)',
                    ),
                  ),
                  TextField(
                    controller: regionCtrl,
                    decoration: const InputDecoration(labelText: 'Region'),
                  ),
                  TextField(
                    controller: descCtrl,
                    decoration: const InputDecoration(labelText: 'Description'),
                  ),
                  TextField(
                    controller: latCtrl,
                    decoration: const InputDecoration(labelText: 'Latitude *'),
                  ),
                  TextField(
                    controller: lonCtrl,
                    decoration: const InputDecoration(labelText: 'Longitude *'),
                  ),
                ],
              ),
            ),
            actions: [
              TextButton(
                onPressed: vm.isPerformingCatalogueAction
                    ? null
                    : () => Navigator.pop(dialogContext),
                child: const Text('Cancel'),
              ),
              FilledButton(
                onPressed: vm.isPerformingCatalogueAction
                    ? null
                    : () async {
                        if (!_canManage) return;
                        final latitude = double.tryParse(latCtrl.text.trim());
                        final longitude = double.tryParse(lonCtrl.text.trim());
                        if (nameCtrl.text.trim().isEmpty ||
                            latitude == null ||
                            longitude == null ||
                            !latitude.isFinite ||
                            !longitude.isFinite ||
                            latitude < -90 ||
                            latitude > 90 ||
                            longitude < -180 ||
                            longitude > 180) {
                          _showDialogMessage(
                            dialogContext,
                            'Enter a name and valid coordinates (latitude −90 to 90, longitude −180 to 180).',
                          );
                          return;
                        }
                        final req = CreateDestinationRequest(
                          name: nameCtrl.text.trim(),
                          slug: slugCtrl.text.trim().isEmpty
                              ? null
                              : slugCtrl.text.trim(),
                          region: regionCtrl.text.trim().isEmpty
                              ? null
                              : regionCtrl.text.trim(),
                          description: descCtrl.text.trim().isEmpty
                              ? null
                              : descCtrl.text.trim(),
                          latitude: latitude,
                          longitude: longitude,
                        );
                        final created = await vm.performCatalogueAction(
                          () => vm.repository.createDestination(req),
                          successText: 'Destination created in draft.',
                        );
                        if (created != null && dialogContext.mounted) {
                          Navigator.pop(dialogContext);
                        }
                      },
                child: const Text('Save'),
              ),
            ],
          ),
        ),
      ),
    );
  }

  void _showEditDestinationDialog(
    BuildContext context,
    ExperienceViewModel vm,
    DestinationDto dest,
  ) {
    final nameCtrl = TextEditingController(text: dest.name);
    final slugCtrl = TextEditingController(text: dest.slug);
    final regionCtrl = TextEditingController(text: dest.region);
    final descCtrl = TextEditingController(text: dest.description);
    final latCtrl = TextEditingController(text: dest.latitude.toString());
    final lonCtrl = TextEditingController(text: dest.longitude.toString());

    vm.clearMessages();
    showDialog<void>(
      context: context,
      barrierDismissible: false,
      builder: (dialogContext) => ExperienceFormLifecycle(
        viewModel: vm,
        controllers: [
          nameCtrl,
          slugCtrl,
          regionCtrl,
          descCtrl,
          latCtrl,
          lonCtrl,
        ],
        child: ListenableBuilder(
          listenable: vm,
          builder: (context, _) => AlertDialog(
            title: const Text('Edit Destination'),
            content: SingleChildScrollView(
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  if (vm.errorMessage != null)
                    ExperienceNotice(vm.errorMessage!, isError: true),
                  TextField(
                    controller: nameCtrl,
                    decoration: const InputDecoration(labelText: 'Name'),
                  ),
                  TextField(
                    controller: slugCtrl,
                    decoration: const InputDecoration(labelText: 'URL slug'),
                  ),
                  TextField(
                    controller: regionCtrl,
                    decoration: const InputDecoration(labelText: 'Region'),
                  ),
                  TextField(
                    controller: descCtrl,
                    decoration: const InputDecoration(labelText: 'Description'),
                  ),
                  TextField(
                    controller: latCtrl,
                    keyboardType: const TextInputType.numberWithOptions(
                      decimal: true,
                      signed: true,
                    ),
                    decoration: const InputDecoration(labelText: 'Latitude'),
                  ),
                  TextField(
                    controller: lonCtrl,
                    keyboardType: const TextInputType.numberWithOptions(
                      decimal: true,
                      signed: true,
                    ),
                    decoration: const InputDecoration(labelText: 'Longitude'),
                  ),
                ],
              ),
            ),
            actions: [
              TextButton(
                onPressed: vm.isPerformingCatalogueAction
                    ? null
                    : () => Navigator.pop(dialogContext),
                child: const Text('Cancel'),
              ),
              FilledButton(
                onPressed: vm.isPerformingCatalogueAction
                    ? null
                    : () async {
                        if (!_canManage) return;
                        final latitude = double.tryParse(latCtrl.text.trim());
                        final longitude = double.tryParse(lonCtrl.text.trim());
                        if (nameCtrl.text.trim().isEmpty ||
                            latitude == null ||
                            longitude == null ||
                            !latitude.isFinite ||
                            !longitude.isFinite ||
                            latitude < -90 ||
                            latitude > 90 ||
                            longitude < -180 ||
                            longitude > 180) {
                          _showDialogMessage(
                            dialogContext,
                            'Enter a name and valid coordinates.',
                          );
                          return;
                        }
                        final req = UpdateDestinationRequest(
                          name: nameCtrl.text.trim(),
                          slug: slugCtrl.text.trim().isEmpty
                              ? null
                              : slugCtrl.text.trim(),
                          region: regionCtrl.text.trim().isEmpty
                              ? null
                              : regionCtrl.text.trim(),
                          description: descCtrl.text.trim().isEmpty
                              ? null
                              : descCtrl.text.trim(),
                          latitude: latitude,
                          longitude: longitude,
                        );
                        final updated = await vm.performCatalogueAction(
                          () => vm.repository.updateDestination(dest.id, req),
                          successText: 'Destination updated.',
                        );
                        if (updated != null && dialogContext.mounted) {
                          Navigator.pop(dialogContext);
                        }
                      },
                child: const Text('Update'),
              ),
            ],
          ),
        ),
      ),
    );
  }

  void _showCreateActivityDialog(BuildContext context, ExperienceViewModel vm) {
    final codeCtrl = TextEditingController();
    final titleCtrl = TextEditingController();
    final catCtrl = TextEditingController();
    final descCtrl = TextEditingController();

    vm.clearMessages();
    showDialog<void>(
      context: context,
      barrierDismissible: false,
      builder: (dialogContext) => ExperienceFormLifecycle(
        viewModel: vm,
        controllers: [codeCtrl, titleCtrl, catCtrl, descCtrl],
        child: ListenableBuilder(
          listenable: vm,
          builder: (context, _) => AlertDialog(
            title: const Text('Add Coastal Activity'),
            content: SingleChildScrollView(
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  if (vm.errorMessage != null)
                    ExperienceNotice(vm.errorMessage!, isError: true),
                  TextField(
                    controller: codeCtrl,
                    decoration: const InputDecoration(
                      labelText: 'Stable code *',
                    ),
                  ),
                  TextField(
                    controller: titleCtrl,
                    decoration: const InputDecoration(labelText: 'Name *'),
                  ),
                  TextField(
                    controller: catCtrl,
                    decoration: const InputDecoration(labelText: 'Category'),
                  ),
                  TextField(
                    controller: descCtrl,
                    decoration: const InputDecoration(labelText: 'Description'),
                  ),
                ],
              ),
            ),
            actions: [
              TextButton(
                onPressed: vm.isPerformingCatalogueAction
                    ? null
                    : () => Navigator.pop(dialogContext),
                child: const Text('Cancel'),
              ),
              FilledButton(
                onPressed: () async {
                  if (codeCtrl.text.trim().isEmpty ||
                      titleCtrl.text.trim().isEmpty) {
                    _showDialogMessage(
                      dialogContext,
                      'Enter an activity code and name.',
                    );
                    return;
                  }
                  final req = CreateActivityRequest(
                    code: codeCtrl.text.trim().toUpperCase(),
                    name: titleCtrl.text.trim(),
                    category: catCtrl.text.trim().isEmpty
                        ? null
                        : catCtrl.text.trim(),
                    description: descCtrl.text.trim().isEmpty
                        ? null
                        : descCtrl.text.trim(),
                  );
                  final created = await vm.performCatalogueAction(
                    () => vm.repository.createActivity(req),
                    successText: 'Activity created in draft.',
                  );
                  if (created != null && dialogContext.mounted) {
                    Navigator.pop(dialogContext);
                  }
                },
                child: const Text('Save'),
              ),
            ],
          ),
        ),
      ),
    );
  }

  void _showEditActivityDialog(
    BuildContext context,
    ExperienceViewModel vm,
    ActivityDto activity,
  ) {
    final nameCtrl = TextEditingController(text: activity.name);
    final categoryCtrl = TextEditingController(text: activity.category);
    final descriptionCtrl = TextEditingController(text: activity.description);
    vm.clearMessages();
    showDialog<void>(
      context: context,
      barrierDismissible: false,
      builder: (dialogContext) => ExperienceFormLifecycle(
        viewModel: vm,
        controllers: [nameCtrl, categoryCtrl, descriptionCtrl],
        child: ListenableBuilder(
          listenable: vm,
          builder: (context, _) => AlertDialog(
            title: const Text('Edit coastal activity'),
            content: SingleChildScrollView(
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  if (vm.errorMessage != null)
                    ExperienceNotice(vm.errorMessage!, isError: true),
                  TextField(
                    controller: nameCtrl,
                    decoration: const InputDecoration(labelText: 'Name *'),
                  ),
                  TextField(
                    controller: categoryCtrl,
                    decoration: const InputDecoration(labelText: 'Category'),
                  ),
                  TextField(
                    controller: descriptionCtrl,
                    decoration: const InputDecoration(labelText: 'Description'),
                  ),
                ],
              ),
            ),
            actions: [
              TextButton(
                onPressed: vm.isPerformingCatalogueAction
                    ? null
                    : () => Navigator.pop(dialogContext),
                child: const Text('Cancel'),
              ),
              FilledButton(
                onPressed: () async {
                  if (nameCtrl.text.trim().isEmpty) {
                    _showDialogMessage(
                      dialogContext,
                      'Enter an activity name.',
                    );
                    return;
                  }
                  final request = UpdateActivityRequest(
                    name: nameCtrl.text.trim(),
                    category: categoryCtrl.text.trim().isEmpty
                        ? null
                        : categoryCtrl.text.trim(),
                    description: descriptionCtrl.text.trim().isEmpty
                        ? null
                        : descriptionCtrl.text.trim(),
                  );
                  final updated = await vm.performCatalogueAction(
                    () => vm.repository.updateActivity(activity.id, request),
                    successText: 'Activity updated.',
                  );
                  if (updated != null && dialogContext.mounted) {
                    Navigator.pop(dialogContext);
                  }
                },
                child: const Text('Save changes'),
              ),
            ],
          ),
        ),
      ),
    );
  }

  void _showCreateOfferingDialog(BuildContext context, ExperienceViewModel vm) {
    if (vm.managementDestinations.isEmpty || vm.managementActivities.isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Add at least one destination and activity first.'),
        ),
      );
      return;
    }
    final titleCtrl = TextEditingController();
    final descCtrl = TextEditingController();
    final priceCtrl = TextEditingController();
    final durationCtrl = TextEditingController();
    final capCtrl = TextEditingController();
    var destId = vm.managementDestinations.first.id;
    var actId = vm.managementActivities.first.id;

    vm.clearMessages();
    showDialog<void>(
      context: context,
      barrierDismissible: false,
      builder: (dialogContext) => ExperienceFormLifecycle(
        viewModel: vm,
        controllers: [titleCtrl, descCtrl, priceCtrl, durationCtrl, capCtrl],
        child: ListenableBuilder(
          listenable: vm,
          builder: (context, _) => AlertDialog(
            title: const Text('Add Bookable Offering'),
            content: SingleChildScrollView(
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  if (vm.errorMessage != null)
                    ExperienceNotice(vm.errorMessage!, isError: true),
                  DropdownButtonFormField<String>(
                    isExpanded: true,
                    initialValue: destId,
                    items: vm.managementDestinations
                        .map(
                          (d) => DropdownMenuItem(
                            value: d.id,
                            child: Text(d.name),
                          ),
                        )
                        .toList(),
                    onChanged: (val) => destId = val ?? destId,
                  ),
                  DropdownButtonFormField<String>(
                    isExpanded: true,
                    initialValue: actId,
                    items: vm.managementActivities
                        .map(
                          (a) => DropdownMenuItem(
                            value: a.id,
                            child: Text(a.name),
                          ),
                        )
                        .toList(),
                    onChanged: (val) => actId = val ?? actId,
                  ),
                  TextField(
                    controller: titleCtrl,
                    decoration: const InputDecoration(labelText: 'Title *'),
                  ),
                  TextField(
                    controller: descCtrl,
                    decoration: const InputDecoration(labelText: 'Description'),
                  ),
                  TextField(
                    controller: priceCtrl,
                    keyboardType: const TextInputType.numberWithOptions(
                      decimal: true,
                    ),
                    decoration: const InputDecoration(
                      labelText: 'Price in LKR (optional)',
                    ),
                  ),
                  TextField(
                    controller: durationCtrl,
                    keyboardType: TextInputType.number,
                    decoration: const InputDecoration(
                      labelText: 'Duration in minutes (optional)',
                    ),
                  ),
                  TextField(
                    controller: capCtrl,
                    keyboardType: TextInputType.number,
                    decoration: const InputDecoration(
                      labelText: 'Group capacity (optional)',
                    ),
                  ),
                ],
              ),
            ),
            actions: [
              TextButton(
                onPressed: vm.isPerformingCatalogueAction
                    ? null
                    : () => Navigator.pop(dialogContext),
                child: const Text('Cancel'),
              ),
              FilledButton(
                onPressed: vm.isPerformingCatalogueAction
                    ? null
                    : () async {
                        if (!_canManage) return;
                        final price = priceCtrl.text.trim().isEmpty
                            ? null
                            : double.tryParse(priceCtrl.text.trim());
                        final duration = durationCtrl.text.trim().isEmpty
                            ? null
                            : int.tryParse(durationCtrl.text.trim());
                        final capacity = capCtrl.text.trim().isEmpty
                            ? null
                            : int.tryParse(capCtrl.text.trim());
                        if (titleCtrl.text.trim().isEmpty ||
                            (priceCtrl.text.trim().isNotEmpty &&
                                (price == null ||
                                    !price.isFinite ||
                                    price < 0)) ||
                            (durationCtrl.text.trim().isNotEmpty &&
                                (duration == null || duration <= 0)) ||
                            (capCtrl.text.trim().isNotEmpty &&
                                (capacity == null || capacity <= 0))) {
                          _showDialogMessage(
                            dialogContext,
                            'Enter a title and valid optional price, duration, and capacity values.',
                          );
                          return;
                        }
                        final req = CreateOfferingRequest(
                          destinationId: destId,
                          activityId: actId,
                          title: titleCtrl.text.trim(),
                          description: descCtrl.text.trim().isEmpty
                              ? null
                              : descCtrl.text.trim(),
                          price: price,
                          currency: price == null ? null : 'LKR',
                          durationMinutes: duration,
                          maxCapacity: capacity,
                        );
                        final created = await vm.performCatalogueAction(
                          () => vm.repository.createOffering(req),
                          successText: 'Offering created in draft.',
                        );
                        if (created != null && dialogContext.mounted) {
                          Navigator.pop(dialogContext);
                        }
                      },
                child: const Text('Save'),
              ),
            ],
          ),
        ),
      ),
    );
  }

  void _showEditOfferingDialog(
    BuildContext context,
    ExperienceViewModel vm,
    OfferingDto offering,
  ) {
    final titleCtrl = TextEditingController(text: offering.title);
    final descriptionCtrl = TextEditingController(text: offering.description);
    final priceCtrl = TextEditingController(
      text: offering.price?.toString() ?? '',
    );
    final durationCtrl = TextEditingController(
      text: offering.durationMinutes?.toString() ?? '',
    );
    final capacityCtrl = TextEditingController(
      text: offering.maxCapacity?.toString() ?? '',
    );
    vm.clearMessages();
    showDialog<void>(
      context: context,
      barrierDismissible: false,
      builder: (dialogContext) => ExperienceFormLifecycle(
        viewModel: vm,
        controllers: [
          titleCtrl,
          descriptionCtrl,
          priceCtrl,
          durationCtrl,
          capacityCtrl,
        ],
        child: ListenableBuilder(
          listenable: vm,
          builder: (context, _) => AlertDialog(
            title: const Text('Edit bookable offering'),
            content: SingleChildScrollView(
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  if (vm.errorMessage != null)
                    ExperienceNotice(vm.errorMessage!, isError: true),
                  TextField(
                    controller: titleCtrl,
                    decoration: const InputDecoration(labelText: 'Title *'),
                  ),
                  TextField(
                    controller: descriptionCtrl,
                    decoration: const InputDecoration(labelText: 'Description'),
                  ),
                  TextField(
                    controller: priceCtrl,
                    keyboardType: const TextInputType.numberWithOptions(
                      decimal: true,
                    ),
                    decoration: InputDecoration(
                      labelText: 'Price in ${offering.currency ?? 'LKR'}',
                    ),
                  ),
                  TextField(
                    controller: durationCtrl,
                    keyboardType: TextInputType.number,
                    decoration: const InputDecoration(
                      labelText: 'Duration in minutes',
                    ),
                  ),
                  TextField(
                    controller: capacityCtrl,
                    keyboardType: TextInputType.number,
                    decoration: const InputDecoration(
                      labelText: 'Group capacity',
                    ),
                  ),
                ],
              ),
            ),
            actions: [
              TextButton(
                onPressed: vm.isPerformingCatalogueAction
                    ? null
                    : () => Navigator.pop(dialogContext),
                child: const Text('Cancel'),
              ),
              FilledButton(
                onPressed: vm.isPerformingCatalogueAction
                    ? null
                    : () async {
                        if (!_canManage) return;
                        final price = priceCtrl.text.trim().isEmpty
                            ? null
                            : double.tryParse(priceCtrl.text.trim());
                        final duration = durationCtrl.text.trim().isEmpty
                            ? null
                            : int.tryParse(durationCtrl.text.trim());
                        final capacity = capacityCtrl.text.trim().isEmpty
                            ? null
                            : int.tryParse(capacityCtrl.text.trim());
                        if (titleCtrl.text.trim().isEmpty ||
                            (priceCtrl.text.trim().isNotEmpty &&
                                (price == null ||
                                    !price.isFinite ||
                                    price < 0)) ||
                            (durationCtrl.text.trim().isNotEmpty &&
                                (duration == null || duration <= 0)) ||
                            (capacityCtrl.text.trim().isNotEmpty &&
                                (capacity == null || capacity <= 0))) {
                          _showDialogMessage(
                            dialogContext,
                            'Enter a title and valid optional price, duration, and capacity values.',
                          );
                          return;
                        }
                        final request = UpdateOfferingRequest(
                          title: titleCtrl.text.trim(),
                          description: descriptionCtrl.text.trim().isEmpty
                              ? null
                              : descriptionCtrl.text.trim(),
                          price: price,
                          currency: price == null
                              ? null
                              : offering.currency ?? 'LKR',
                          durationMinutes: duration,
                          maxCapacity: capacity,
                        );
                        final updated = await vm.performCatalogueAction(
                          () => vm.repository.updateOffering(
                            offering.id,
                            request,
                          ),
                          successText: 'Offering updated.',
                        );
                        if (updated != null && dialogContext.mounted) {
                          Navigator.pop(dialogContext);
                        }
                      },
                child: const Text('Save changes'),
              ),
            ],
          ),
        ),
      ),
    );
  }

  void _showScheduleManager(
    BuildContext context,
    ExperienceViewModel vm,
    OfferingDto offering,
  ) {
    showDialog<void>(
      context: context,
      barrierDismissible: false,
      builder: (_) =>
          OfferingScheduleManagerDialog(viewModel: vm, offering: offering),
    );
  }

  Future<void> _deleteOffering(
    BuildContext context,
    ExperienceViewModel vm,
    OfferingDto offering,
  ) async {
    final confirmed = await _confirmDestructiveAction(
      context,
      title: 'Delete ${offering.title}?',
      message: 'Its departure times and saved references will no longer be available.',
    );
    if (!confirmed) return;
    await vm.performCatalogueAction<bool>(() async {
      await vm.repository.deleteOffering(offering.id);
      return true;
    }, successText: 'Offering deleted.');
  }

  Future<bool> _confirmDestructiveAction(
    BuildContext context, {
    required String title,
    required String message,
  }) async {
    return await showDialog<bool>(
          context: context,
          builder: (dialogContext) => AlertDialog(
            title: Text(title),
            content: Text(message),
            actions: [
              TextButton(
                onPressed: () => Navigator.pop(dialogContext, false),
                child: const Text('Cancel'),
              ),
              FilledButton(
                onPressed: () => Navigator.pop(dialogContext, true),
                child: const Text('Delete'),
              ),
            ],
          ),
        ) ??
        false;
  }

  void _showDialogMessage(BuildContext context, String message) {
    ScaffoldMessenger.of(context)
        .showSnackBar(SnackBar(content: Text(message)));
  }
}
