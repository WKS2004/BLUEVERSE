import 'package:flutter/material.dart';

import '../../data/models/experience_models.dart';
import '../auth_view_model.dart';
import '../blueverse_theme.dart';
import 'experience_view_model.dart';
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
  late TabController _tabController;

  // Selected for evaluation or edit
  String? _selectedDestId;
  String? _selectedDestStatus = 'DRAFT';
  PublicationEvaluationResponse? _destEvalResult;

  String? _selectedActId;
  String? _selectedActStatus = 'DRAFT';
  PublicationEvaluationResponse? _actEvalResult;

  String? _selectedOffId;
  String? _selectedOffStatus = 'DRAFT';
  PublicationEvaluationResponse? _offEvalResult;

  @override
  void initState() {
    super.initState();
    _tabController = TabController(length: 3, vsync: this);
    WidgetsBinding.instance.addPostFrameCallback((_) {
      final user = widget.authViewModel.user;
      final canManage =
          user != null &&
          (user.permissions.contains('experiences.catalogue.manage') ||
              user.permissions.contains('auth.role.system.manage'));
      if (!canManage) return;
      widget.viewModel.loadManagementCatalog();
    });
  }

  @override
  void dispose() {
    _tabController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return ListenableBuilder(
      listenable: Listenable.merge([widget.viewModel, widget.authViewModel]),
      builder: (context, _) {
        final vm = widget.viewModel;
        final auth = widget.authViewModel;

        final canManage =
            auth.user != null &&
            (auth.user!.permissions.contains('experiences.catalogue.manage') ||
                auth.user!.permissions.contains('auth.role.system.manage'));

        return Scaffold(
          appBar: AppBar(
            title: const Text('Manage Coastal Experiences'),
            bottom: TabBar(
              controller: _tabController,
              isScrollable: true,
              indicatorColor: BlueversePalette.coastDeep,
              tabs: const [
                Tab(icon: Icon(Icons.place_outlined), text: 'Destinations'),
                Tab(icon: Icon(Icons.snowshoeing_outlined), text: 'Activities'),
                Tab(icon: Icon(Icons.local_offer_outlined), text: 'Offerings'),
              ],
            ),
          ),
          body: !canManage
              ? _buildAccessDenied()
              : Column(
                  children: [
                    if (vm.errorMessage != null)
                      _messageCard(vm.errorMessage!, isError: true),
                    if (vm.managementCatalogErrorMessage != null) ...[
                      _messageCard(
                        vm.managementCatalogErrorMessage!,
                        isError: true,
                      ),
                      Align(
                        alignment: Alignment.centerRight,
                        child: TextButton.icon(
                          onPressed: vm.isLoadingManagementCatalog
                              ? null
                              : vm.loadManagementCatalog,
                          icon: const Icon(Icons.refresh),
                          label: const Text('Retry loading experience data'),
                        ),
                      ),
                    ],
                    if (vm.isLoadingManagementCatalog &&
                        vm.managementDestinations.isEmpty &&
                        vm.managementActivities.isEmpty &&
                        vm.managementOfferings.isEmpty)
                      const LinearProgressIndicator(),
                    if (vm.successMessage != null)
                      _messageCard(vm.successMessage!, isError: false),
                    Expanded(
                      child: TabBarView(
                        controller: _tabController,
                        children: [
                          _buildDestinationsManager(context, vm),
                          _buildActivitiesManager(context, vm),
                          _buildOfferingsManager(context, vm),
                        ],
                      ),
                    ),
                  ],
                ),
        );
      },
    );
  }

  Widget _buildAccessDenied() {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Icon(Icons.lock_outline, size: 54, color: Colors.orange),
            const SizedBox(height: 16),
            const Text(
              'Experience Management Restricted',
              style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold),
            ),
            const SizedBox(height: 8),
            const Text(
              'Editing destinations, activities, and offerings requires experience-management permission.',
              textAlign: TextAlign.center,
              style: TextStyle(color: Colors.grey),
            ),
            const SizedBox(height: 16),
            ElevatedButton(
              onPressed: () => Navigator.pop(context),
              child: const Text('Back to Experiences'),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildDestinationsManager(
    BuildContext context,
    ExperienceViewModel vm,
  ) {
    return SingleChildScrollView(
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Text(
                'Coastal Destinations (${vm.managementDestinations.length})',
                style: const TextStyle(
                  fontSize: 16,
                  fontWeight: FontWeight.bold,
                ),
              ),
              FilledButton.icon(
                key: const Key('btn-create-destination-dialog'),
                icon: const Icon(Icons.add),
                label: const Text('Add Destination'),
                onPressed: () => _showCreateDestinationDialog(context, vm),
              ),
            ],
          ),
          const SizedBox(height: 16),

          // Publication Evaluation Card
          Card(
            color: BlueversePalette.coastDeep.withAlpha(15),
            shape: RoundedRectangleBorder(
              borderRadius: BorderRadius.circular(16),
            ),
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Text(
                    'Destination Publication Evaluation',
                    style: TextStyle(fontWeight: FontWeight.bold),
                  ),
                  const SizedBox(height: 8),
                  const Text(
                    'Evaluate compliance, environmental safety, and readiness before transitioning lifecycle status.',
                    style: TextStyle(fontSize: 12),
                  ),
                  const SizedBox(height: 12),
                  DropdownButtonFormField<String>(
                    initialValue: _selectedDestId,
                    hint: const Text('Select Destination'),
                    items: vm.managementDestinations
                        .map(
                          (d) => DropdownMenuItem(
                            value: d.id,
                            child: Text(d.name),
                          ),
                        )
                        .toList(),
                    onChanged: (val) => setState(() {
                      _selectedDestId = val;
                      _destEvalResult = null;
                    }),
                  ),
                  const SizedBox(height: 8),
                  DropdownButtonFormField<String>(
                    initialValue: _selectedDestStatus,
                    items: const [
                      DropdownMenuItem(
                        value: 'DRAFT',
                        child: Text('Target: DRAFT'),
                      ),
                      DropdownMenuItem(
                        value: 'PUBLISHED',
                        child: Text('Target: PUBLISHED'),
                      ),
                      DropdownMenuItem(
                        value: 'ARCHIVED',
                        child: Text('Target: ARCHIVED'),
                      ),
                    ],
                    onChanged: (val) => setState(() {
                      _selectedDestStatus = val;
                      _destEvalResult = null;
                    }),
                  ),
                  const SizedBox(height: 12),
                  Row(
                    children: [
                      ElevatedButton.icon(
                        key: const Key('btn-evaluate-destination-pub'),
                        icon: const Icon(Icons.rule),
                        label: const Text('Evaluate Transition'),
                        onPressed: _selectedDestId == null
                            ? null
                            : () async {
                                final result = await vm.performCatalogueAction(
                                  () => vm.repository
                                      .evaluateDestinationPublication(
                                        _selectedDestId!,
                                        _selectedDestStatus ?? 'PUBLISHED',
                                      ),
                                  successText: 'Destination review complete.',
                                  refreshCatalog: false,
                                );
                                if (mounted && result != null) {
                                  setState(() => _destEvalResult = result);
                                }
                              },
                      ),
                      const SizedBox(width: 8),
                      OutlinedButton.icon(
                        icon: const Icon(Icons.publish),
                        label: const Text('Apply Publication'),
                        onPressed:
                            _selectedDestId == null ||
                                _destEvalResult?.canTransition != true ||
                                _destEvalResult?.requestedStatus !=
                                    _selectedDestStatus
                            ? null
                            : () async {
                                await vm.performCatalogueAction(
                                  () => vm.repository
                                      .updateDestinationPublication(
                                        _selectedDestId!,
                                        _selectedDestStatus ?? 'PUBLISHED',
                                      ),
                                  successText:
                                      'Destination publication updated.',
                                );
                                if (mounted) {
                                  setState(() => _destEvalResult = null);
                                }
                              },
                      ),
                    ],
                  ),
                  if (_destEvalResult != null) ...[
                    const SizedBox(height: 12),
                    Container(
                      padding: const EdgeInsets.all(10),
                      decoration: BoxDecoration(
                        color: _destEvalResult!.canTransition
                            ? Colors.green.shade50
                            : Colors.red.shade50,
                        borderRadius: BorderRadius.circular(8),
                      ),
                      child: Text(
                        'Evaluation: ${_destEvalResult!.canTransition ? "ELIGIBLE" : "NOT ELIGIBLE"} • ${_destEvalResult!.reasons.join(", ")}',
                        style: TextStyle(
                          color: _destEvalResult!.canTransition
                              ? Colors.green.shade900
                              : Colors.red.shade900,
                          fontWeight: FontWeight.bold,
                          fontSize: 12,
                        ),
                      ),
                    ),
                  ],
                ],
              ),
            ),
          ),
          const SizedBox(height: 16),

          // Destinations List
          ...vm.managementDestinations.map(
            (d) => Card(
              key: Key('manage-card-destination-${d.id}'),
              margin: const EdgeInsets.only(bottom: 8),
              child: ListTile(
                title: Text(
                  d.name,
                  style: const TextStyle(fontWeight: FontWeight.bold),
                ),
                subtitle: Text('${d.region} • Status: ${d.status}'),
                trailing: Row(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    IconButton(
                      icon: const Icon(Icons.edit, size: 20),
                      onPressed: () =>
                          _showEditDestinationDialog(context, vm, d),
                    ),
                    IconButton(
                      icon: const Icon(
                        Icons.delete_outline,
                        size: 20,
                        color: Colors.red,
                      ),
                      onPressed: () async {
                        final confirmed = await _confirmDestructiveAction(
                          context,
                          title: 'Delete ${d.name}?',
                          message: 'This destination may have linked activities and offerings. The service will reject deletion when its rules prevent it.',
                        );
                        if (!confirmed) return;
                        await vm.performCatalogueAction<bool>(() async {
                          await vm.repository.deleteDestination(d.id);
                          return true;
                        }, successText: 'Destination deleted.');
                      },
                    ),
                  ],
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildActivitiesManager(BuildContext context, ExperienceViewModel vm) {
    return SingleChildScrollView(
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Text(
                'Coastal Activities (${vm.managementActivities.length})',
                style: const TextStyle(
                  fontSize: 16,
                  fontWeight: FontWeight.bold,
                ),
              ),
              FilledButton.icon(
                icon: const Icon(Icons.add),
                label: const Text('Add Activity'),
                onPressed: () => _showCreateActivityDialog(context, vm),
              ),
            ],
          ),
          const SizedBox(height: 16),

          // Activity publication evaluation
          Card(
            color: BlueversePalette.coastDeep.withAlpha(15),
            shape: RoundedRectangleBorder(
              borderRadius: BorderRadius.circular(16),
            ),
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Text(
                    'Activity Publication Evaluation',
                    style: TextStyle(fontWeight: FontWeight.bold),
                  ),
                  const SizedBox(height: 12),
                  DropdownButtonFormField<String>(
                    initialValue: _selectedActId,
                    hint: const Text('Select Activity'),
                    items: vm.managementActivities
                        .map(
                          (a) => DropdownMenuItem(
                            value: a.id,
                            child: Text(a.name),
                          ),
                        )
                        .toList(),
                    onChanged: (val) => setState(() {
                      _selectedActId = val;
                      _actEvalResult = null;
                    }),
                  ),
                  const SizedBox(height: 12),
                  DropdownButtonFormField<String>(
                    initialValue: _selectedActStatus,
                    decoration: const InputDecoration(
                      labelText: 'Target status',
                    ),
                    items: const [
                      DropdownMenuItem(value: 'DRAFT', child: Text('Draft')),
                      DropdownMenuItem(
                        value: 'PUBLISHED',
                        child: Text('Published'),
                      ),
                      DropdownMenuItem(
                        value: 'ARCHIVED',
                        child: Text('Archived'),
                      ),
                    ],
                    onChanged: (value) => setState(() {
                      _selectedActStatus = value;
                      _actEvalResult = null;
                    }),
                  ),
                  const SizedBox(height: 12),
                  Row(
                    children: [
                      ElevatedButton(
                        onPressed: _selectedActId == null
                            ? null
                            : () async {
                                final result = await vm.performCatalogueAction(
                                  () =>
                                      vm.repository.evaluateActivityPublication(
                                        _selectedActId!,
                                        _selectedActStatus ?? 'PUBLISHED',
                                      ),
                                  successText: 'Activity review complete.',
                                  refreshCatalog: false,
                                );
                                if (mounted && result != null) {
                                  setState(() => _actEvalResult = result);
                                }
                              },
                        child: const Text('Evaluate Transition'),
                      ),
                      const SizedBox(width: 8),
                      OutlinedButton(
                        onPressed:
                            _selectedActId == null ||
                                _actEvalResult?.canTransition != true ||
                                _actEvalResult?.requestedStatus !=
                                    _selectedActStatus
                            ? null
                            : () async {
                                await vm.performCatalogueAction(
                                  () => vm.repository.updateActivityPublication(
                                    _selectedActId!,
                                    _selectedActStatus ?? 'PUBLISHED',
                                  ),
                                  successText: 'Activity publication updated.',
                                );
                                if (mounted) {
                                  setState(() => _actEvalResult = null);
                                }
                              },
                        child: const Text('Apply'),
                      ),
                    ],
                  ),
                  if (_actEvalResult != null) ...[
                    const SizedBox(height: 12),
                    Text('Result: ${_actEvalResult!.reasons.join(", ")}'),
                  ],
                ],
              ),
            ),
          ),
          const SizedBox(height: 16),

          ...vm.managementActivities.map(
            (a) => Card(
              key: Key('manage-card-activity-${a.id}'),
              margin: const EdgeInsets.only(bottom: 8),
              child: ListTile(
                title: Text(
                  a.name,
                  style: const TextStyle(fontWeight: FontWeight.bold),
                ),
                subtitle: Text(
                  '${a.category ?? "General"} • Status: ${a.status}',
                ),
                trailing: Wrap(
                  spacing: 0,
                  children: [
                    IconButton(
                      tooltip: 'Edit activity',
                      icon: const Icon(Icons.edit_outlined),
                      onPressed: () => _showEditActivityDialog(context, vm, a),
                    ),
                    IconButton(
                      tooltip: 'Delete activity',
                      icon: const Icon(Icons.delete_outline, color: Colors.red),
                      onPressed: () async {
                        final confirmed = await _confirmDestructiveAction(
                          context,
                          title: 'Delete ${a.name}?',
                          message: 'This also removes offerings linked to this activity.',
                        );
                        if (!confirmed) return;
                        await vm.performCatalogueAction<bool>(
                          () async {
                            await vm.repository.deleteActivity(a.id);
                            return true;
                          },
                          successText: 'Activity and linked offerings deleted.',
                        );
                      },
                    ),
                  ],
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildOfferingsManager(BuildContext context, ExperienceViewModel vm) {
    return SingleChildScrollView(
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Text(
                'Bookable Offerings (${vm.managementOfferings.length})',
                style: const TextStyle(
                  fontSize: 16,
                  fontWeight: FontWeight.bold,
                ),
              ),
              FilledButton.icon(
                icon: const Icon(Icons.add),
                label: const Text('Add Offering'),
                onPressed: () => _showCreateOfferingDialog(context, vm),
              ),
            ],
          ),
          const SizedBox(height: 16),

          // Offering publication evaluation
          Card(
            color: BlueversePalette.coastDeep.withAlpha(15),
            shape: RoundedRectangleBorder(
              borderRadius: BorderRadius.circular(16),
            ),
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Text(
                    'Offering Publication Evaluation',
                    style: TextStyle(fontWeight: FontWeight.bold),
                  ),
                  const SizedBox(height: 12),
                  DropdownButtonFormField<String>(
                    initialValue: _selectedOffId,
                    hint: const Text('Select Offering'),
                    items: vm.managementOfferings
                        .map(
                          (o) => DropdownMenuItem(
                            value: o.id,
                            child: Text(o.title),
                          ),
                        )
                        .toList(),
                    onChanged: (val) => setState(() {
                      _selectedOffId = val;
                      _offEvalResult = null;
                    }),
                  ),
                  const SizedBox(height: 12),
                  DropdownButtonFormField<String>(
                    initialValue: _selectedOffStatus,
                    decoration: const InputDecoration(
                      labelText: 'Target status',
                    ),
                    items: const [
                      DropdownMenuItem(value: 'DRAFT', child: Text('Draft')),
                      DropdownMenuItem(
                        value: 'PUBLISHED',
                        child: Text('Published'),
                      ),
                      DropdownMenuItem(
                        value: 'ARCHIVED',
                        child: Text('Archived'),
                      ),
                    ],
                    onChanged: (value) => setState(() {
                      _selectedOffStatus = value;
                      _offEvalResult = null;
                    }),
                  ),
                  const SizedBox(height: 12),
                  Row(
                    children: [
                      ElevatedButton(
                        onPressed: _selectedOffId == null
                            ? null
                            : () async {
                                final result = await vm.performCatalogueAction(
                                  () =>
                                      vm.repository.evaluateOfferingPublication(
                                        _selectedOffId!,
                                        _selectedOffStatus ?? 'PUBLISHED',
                                      ),
                                  successText: 'Offering review complete.',
                                  refreshCatalog: false,
                                );
                                if (mounted && result != null) {
                                  setState(() => _offEvalResult = result);
                                }
                              },
                        child: const Text('Evaluate Transition'),
                      ),
                      const SizedBox(width: 8),
                      OutlinedButton(
                        onPressed:
                            _selectedOffId == null ||
                                _offEvalResult?.canTransition != true ||
                                _offEvalResult?.requestedStatus !=
                                    _selectedOffStatus
                            ? null
                            : () async {
                                await vm.performCatalogueAction(
                                  () => vm.repository.updateOfferingPublication(
                                    _selectedOffId!,
                                    _selectedOffStatus ?? 'PUBLISHED',
                                  ),
                                  successText: 'Offering publication updated.',
                                );
                                if (mounted) {
                                  setState(() => _offEvalResult = null);
                                }
                              },
                        child: const Text('Apply'),
                      ),
                    ],
                  ),
                  if (_offEvalResult != null) ...[
                    const SizedBox(height: 12),
                    Text('Result: ${_offEvalResult!.reasons.join(", ")}'),
                  ],
                ],
              ),
            ),
          ),
          const SizedBox(height: 16),

          ...vm.managementOfferings.map(
            (o) => Card(
              key: Key('manage-card-offering-${o.id}'),
              margin: const EdgeInsets.only(bottom: 8),
              child: ListTile(
                title: Text(
                  o.title,
                  style: const TextStyle(fontWeight: FontWeight.bold),
                ),
                subtitle: Text(
                  '${o.price != null ? "LKR ${o.price!.toStringAsFixed(0)}" : "Price on request"} • Cap ${o.maxCapacity ?? "N/A"} • Status: ${o.status}',
                ),
                trailing: PopupMenuButton<String>(
                  tooltip: 'Offering actions',
                  onSelected: (action) {
                    switch (action) {
                      case 'edit':
                        _showEditOfferingDialog(context, vm, o);
                        break;
                      case 'schedules':
                        _showScheduleManager(context, vm, o);
                        break;
                      case 'delete':
                        _deleteOffering(context, vm, o);
                        break;
                    }
                  },
                  itemBuilder: (context) => const [
                    PopupMenuItem(value: 'edit', child: Text('Edit offering')),
                    PopupMenuItem(
                      value: 'schedules',
                      child: Text('Manage departure times'),
                    ),
                    PopupMenuItem(
                      value: 'delete',
                      child: Text('Delete offering'),
                    ),
                  ],
                ),
              ),
            ),
          ),
        ],
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

    showDialog<void>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: const Text('Add Coastal Destination'),
        content: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
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
            onPressed: () => Navigator.pop(context),
            child: const Text('Cancel'),
          ),
          FilledButton(
            onPressed: () async {
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
    ).whenComplete(() {
      nameCtrl.dispose();
      slugCtrl.dispose();
      regionCtrl.dispose();
      descCtrl.dispose();
      latCtrl.dispose();
      lonCtrl.dispose();
    });
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

    showDialog<void>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: const Text('Edit Destination'),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
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
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context),
            child: const Text('Cancel'),
          ),
          FilledButton(
            onPressed: () async {
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
    ).whenComplete(() {
      nameCtrl.dispose();
      slugCtrl.dispose();
      regionCtrl.dispose();
      descCtrl.dispose();
      latCtrl.dispose();
      lonCtrl.dispose();
    });
  }

  void _showCreateActivityDialog(BuildContext context, ExperienceViewModel vm) {
    final codeCtrl = TextEditingController();
    final titleCtrl = TextEditingController();
    final catCtrl = TextEditingController();
    final descCtrl = TextEditingController();

    showDialog<void>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: const Text('Add Coastal Activity'),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            TextField(
              controller: codeCtrl,
              decoration: const InputDecoration(labelText: 'Stable code *'),
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
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context),
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
    ).whenComplete(() {
      codeCtrl.dispose();
      titleCtrl.dispose();
      catCtrl.dispose();
      descCtrl.dispose();
    });
  }

  void _showEditActivityDialog(
    BuildContext context,
    ExperienceViewModel vm,
    ActivityDto activity,
  ) {
    final nameCtrl = TextEditingController(text: activity.name);
    final categoryCtrl = TextEditingController(text: activity.category);
    final descriptionCtrl = TextEditingController(text: activity.description);
    showDialog<void>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: const Text('Edit coastal activity'),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
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
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(dialogContext),
            child: const Text('Cancel'),
          ),
          FilledButton(
            onPressed: () async {
              if (nameCtrl.text.trim().isEmpty) {
                _showDialogMessage(dialogContext, 'Enter an activity name.');
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
    ).whenComplete(() {
      nameCtrl.dispose();
      categoryCtrl.dispose();
      descriptionCtrl.dispose();
    });
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

    showDialog<void>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: const Text('Add Bookable Offering'),
        content: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              DropdownButtonFormField<String>(
                initialValue: destId,
                items: vm.managementDestinations
                    .map(
                      (d) => DropdownMenuItem(value: d.id, child: Text(d.name)),
                    )
                    .toList(),
                onChanged: (val) => destId = val ?? destId,
              ),
              DropdownButtonFormField<String>(
                initialValue: actId,
                items: vm.managementActivities
                    .map(
                      (a) => DropdownMenuItem(value: a.id, child: Text(a.name)),
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
            onPressed: () => Navigator.pop(context),
            child: const Text('Cancel'),
          ),
          FilledButton(
            onPressed: () async {
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
                      (price == null || !price.isFinite || price < 0)) ||
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
    ).whenComplete(() {
      titleCtrl.dispose();
      descCtrl.dispose();
      priceCtrl.dispose();
      durationCtrl.dispose();
      capCtrl.dispose();
    });
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
    showDialog<void>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: const Text('Edit bookable offering'),
        content: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
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
                decoration: const InputDecoration(labelText: 'Group capacity'),
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
            onPressed: () async {
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
                      (price == null || !price.isFinite || price < 0)) ||
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
                currency: price == null ? null : offering.currency ?? 'LKR',
                durationMinutes: duration,
                maxCapacity: capacity,
              );
              final updated = await vm.performCatalogueAction(
                () => vm.repository.updateOffering(offering.id, request),
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
    ).whenComplete(() {
      titleCtrl.dispose();
      descriptionCtrl.dispose();
      priceCtrl.dispose();
      durationCtrl.dispose();
      capacityCtrl.dispose();
    });
  }

  void _showScheduleManager(
    BuildContext context,
    ExperienceViewModel vm,
    OfferingDto offering,
  ) {
    showDialog<void>(
      context: context,
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

  Widget _messageCard(String message, {required bool isError}) => Padding(
    padding: const EdgeInsets.fromLTRB(16, 8, 16, 0),
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

  void _showDialogMessage(BuildContext context, String message) {
    ScaffoldMessenger.of(context)
        .showSnackBar(SnackBar(content: Text(message)));
  }
}
