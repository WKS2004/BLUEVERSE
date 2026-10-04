import 'package:flutter/material.dart';

import '../../data/models/experience_models.dart';
import '../auth_view_model.dart';
import '../blueverse_theme.dart';
import 'experience_view_model.dart';

class CatalogueManagementScreen extends StatefulWidget {
  const CatalogueManagementScreen({
    super.key,
    required this.viewModel,
    required this.authViewModel,
  });

  final ExperienceViewModel viewModel;
  final AuthViewModel authViewModel;

  @override
  State<CatalogueManagementScreen> createState() => _CatalogueManagementScreenState();
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
    _tabController = TabController(length: 4, vsync: this);
    WidgetsBinding.instance.addPostFrameCallback((_) {
      widget.viewModel.loadDiscoveryData();
      widget.viewModel.loadDiagnostics();
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

        final canManage = auth.user != null &&
            (auth.user!.permissions.contains('experiences.catalogue.manage') ||
                auth.user!.permissions.contains('auth.role.system.manage'));

        return Scaffold(
          appBar: AppBar(
            title: const Text('Catalogue Management'),
            bottom: TabBar(
              controller: _tabController,
              isScrollable: true,
              indicatorColor: BlueversePalette.coastDeep,
              tabs: const [
                Tab(icon: Icon(Icons.place_outlined), text: 'Destinations'),
                Tab(icon: Icon(Icons.snowshoeing_outlined), text: 'Activities'),
                Tab(icon: Icon(Icons.local_offer_outlined), text: 'Offerings'),
                Tab(icon: Icon(Icons.health_and_safety_outlined), text: 'Diagnostics & Seam'),
              ],
            ),
          ),
          body: !canManage
              ? _buildAccessDenied()
              : TabBarView(
                  controller: _tabController,
                  children: [
                    _buildDestinationsManager(context, vm),
                    _buildActivitiesManager(context, vm),
                    _buildOfferingsManager(context, vm),
                    _buildDiagnosticsTab(context, vm),
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
              'Catalogue Access Restricted',
              style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold),
            ),
            const SizedBox(height: 8),
            const Text(
              'Managing coastal catalog entries requires experiences.catalogue.manage or system manager permissions.',
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

  Widget _buildDestinationsManager(BuildContext context, ExperienceViewModel vm) {
    return SingleChildScrollView(
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Text(
                'Coastal Destinations (${vm.destinations.length})',
                style: const TextStyle(fontSize: 16, fontWeight: FontWeight.bold),
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
            shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
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
                    value: _selectedDestId,
                    hint: const Text('Select Destination'),
                    items: vm.destinations
                        .map(
                          (d) => DropdownMenuItem(
                            value: d.id,
                            child: Text(d.name),
                          ),
                        )
                        .toList(),
                    onChanged: (val) => setState(() => _selectedDestId = val),
                  ),
                  const SizedBox(height: 8),
                  DropdownButtonFormField<String>(
                    value: _selectedDestStatus,
                    items: const [
                      DropdownMenuItem(value: 'DRAFT', child: Text('Target: DRAFT')),
                      DropdownMenuItem(value: 'PUBLISHED', child: Text('Target: PUBLISHED')),
                      DropdownMenuItem(value: 'ARCHIVED', child: Text('Target: ARCHIVED')),
                    ],
                    onChanged: (val) => setState(() => _selectedDestStatus = val),
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
                                final res = await vm.repository.evaluateDestinationPublication(
                                  _selectedDestId!,
                                  _selectedDestStatus ?? 'PUBLISHED',
                                );
                                setState(() => _destEvalResult = res);
                              },
                      ),
                      const SizedBox(width: 8),
                      OutlinedButton.icon(
                        icon: const Icon(Icons.publish),
                        label: const Text('Apply Publication'),
                        onPressed: _selectedDestId == null
                            ? null
                            : () async {
                                await vm.repository.updateDestinationPublication(
                                  _selectedDestId!,
                                  _selectedDestStatus ?? 'PUBLISHED',
                                );
                                await vm.loadDiscoveryData();
                              },
                      ),
                    ],
                  ),
                  if (_destEvalResult != null) ...[
                    const SizedBox(height: 12),
                    Container(
                      padding: const EdgeInsets.all(10),
                      decoration: BoxDecoration(
                        color: _destEvalResult!.canTransition ? Colors.green.shade50 : Colors.red.shade50,
                        borderRadius: BorderRadius.circular(8),
                      ),
                      child: Text(
                        'Evaluation: ${_destEvalResult!.canTransition ? "ELIGIBLE" : "NOT ELIGIBLE"} • ${_destEvalResult!.reasons.join(", ")}',
                        style: TextStyle(
                          color: _destEvalResult!.canTransition ? Colors.green.shade900 : Colors.red.shade900,
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
          ...vm.destinations.map(
            (d) => Card(
              key: Key('manage-card-destination-${d.id}'),
              margin: const EdgeInsets.only(bottom: 8),
              child: ListTile(
                title: Text(d.name, style: const TextStyle(fontWeight: FontWeight.bold)),
                subtitle: Text('${d.region} • Status: ${d.status}'),
                trailing: Row(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    IconButton(
                      icon: const Icon(Icons.edit, size: 20),
                      onPressed: () => _showEditDestinationDialog(context, vm, d),
                    ),
                    IconButton(
                      icon: const Icon(Icons.delete_outline, size: 20, color: Colors.red),
                      onPressed: () async {
                        await vm.repository.deleteDestination(d.id);
                        await vm.loadDiscoveryData();
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
                'Coastal Activities (${vm.activities.length})',
                style: const TextStyle(fontSize: 16, fontWeight: FontWeight.bold),
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
            shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Text('Activity Publication Evaluation', style: TextStyle(fontWeight: FontWeight.bold)),
                  const SizedBox(height: 12),
                  DropdownButtonFormField<String>(
                    value: _selectedActId,
                    hint: const Text('Select Activity'),
                    items: vm.activities
                        .map((a) => DropdownMenuItem(value: a.id, child: Text(a.name)))
                        .toList(),
                    onChanged: (val) => setState(() => _selectedActId = val),
                  ),
                  const SizedBox(height: 12),
                  Row(
                    children: [
                      ElevatedButton(
                        onPressed: _selectedActId == null
                            ? null
                            : () async {
                                final res = await vm.repository.evaluateActivityPublication(
                                  _selectedActId!,
                                  _selectedActStatus ?? 'PUBLISHED',
                                );
                                setState(() => _actEvalResult = res);
                              },
                        child: const Text('Evaluate Transition'),
                      ),
                      const SizedBox(width: 8),
                      OutlinedButton(
                        onPressed: _selectedActId == null
                            ? null
                            : () async {
                                await vm.repository.updateActivityPublication(
                                  _selectedActId!,
                                  _selectedActStatus ?? 'PUBLISHED',
                                );
                                await vm.loadDiscoveryData();
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

          ...vm.activities.map(
            (a) => Card(
              key: Key('manage-card-activity-${a.id}'),
              margin: const EdgeInsets.only(bottom: 8),
              child: ListTile(
                title: Text(a.name, style: const TextStyle(fontWeight: FontWeight.bold)),
                subtitle: Text('${a.category ?? "General"} • Status: ${a.status}'),
                trailing: IconButton(
                  icon: const Icon(Icons.delete_outline, color: Colors.red),
                  onPressed: () async {
                    await vm.repository.deleteActivity(a.id);
                    await vm.loadDiscoveryData();
                  },
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
                'Bookable Offerings (${vm.offerings.length})',
                style: const TextStyle(fontSize: 16, fontWeight: FontWeight.bold),
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
            shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Text('Offering Publication Evaluation', style: TextStyle(fontWeight: FontWeight.bold)),
                  const SizedBox(height: 12),
                  DropdownButtonFormField<String>(
                    value: _selectedOffId,
                    hint: const Text('Select Offering'),
                    items: vm.offerings
                        .map((o) => DropdownMenuItem(value: o.id, child: Text(o.title)))
                        .toList(),
                    onChanged: (val) => setState(() => _selectedOffId = val),
                  ),
                  const SizedBox(height: 12),
                  Row(
                    children: [
                      ElevatedButton(
                        onPressed: _selectedOffId == null
                            ? null
                            : () async {
                                final res = await vm.repository.evaluateOfferingPublication(
                                  _selectedOffId!,
                                  _selectedOffStatus ?? 'PUBLISHED',
                                );
                                setState(() => _offEvalResult = res);
                              },
                        child: const Text('Evaluate Transition'),
                      ),
                      const SizedBox(width: 8),
                      OutlinedButton(
                        onPressed: _selectedOffId == null
                            ? null
                            : () async {
                                await vm.repository.updateOfferingPublication(
                                  _selectedOffId!,
                                  _selectedOffStatus ?? 'PUBLISHED',
                                );
                                await vm.loadDiscoveryData();
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

          ...vm.offerings.map(
            (o) => Card(
              key: Key('manage-card-offering-${o.id}'),
              margin: const EdgeInsets.only(bottom: 8),
              child: ListTile(
                title: Text(o.title, style: const TextStyle(fontWeight: FontWeight.bold)),
                subtitle: Text('${o.price != null ? "LKR ${o.price!.toStringAsFixed(0)}" : "Price on request"} • Cap ${o.maxCapacity ?? "N/A"} • Status: ${o.status}'),
                trailing: IconButton(
                  icon: const Icon(Icons.delete_outline, color: Colors.red),
                  onPressed: () async {
                    await vm.repository.deleteOffering(o.id);
                    await vm.loadDiscoveryData();
                  },
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildDiagnosticsTab(BuildContext context, ExperienceViewModel vm) {
    return SingleChildScrollView(
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              const Text(
                'Subsystem Readiness & Seam',
                style: TextStyle(fontWeight: FontWeight.bold, fontSize: 16),
              ),
              IconButton(
                icon: const Icon(Icons.refresh),
                onPressed: () => vm.loadDiagnostics(),
              ),
            ],
          ),
          const SizedBox(height: 12),

          // Dependencies Status Card
          Card(
            shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: [
                      const Icon(Icons.cloud_sync, color: BlueversePalette.coastDeep),
                      const SizedBox(width: 8),
                      Text(
                        'Upstream Dependencies',
                        style: Theme.of(context).textTheme.titleSmall?.copyWith(fontWeight: FontWeight.bold),
                      ),
                    ],
                  ),
                  const SizedBox(height: 12),
                  if (vm.dependenciesStatus != null) ...[
                    Text('Status: ${vm.dependenciesStatus!.overallStatus ?? "HEALTHY"}', style: const TextStyle(fontWeight: FontWeight.bold)),
                    const SizedBox(height: 8),
                    ...vm.dependenciesStatus!.dependencies.map(
                      (dep) => Padding(
                        padding: const EdgeInsets.symmetric(vertical: 4),
                        child: Row(
                          mainAxisAlignment: MainAxisAlignment.spaceBetween,
                          children: [
                            Text(dep.serviceName),
                            Chip(
                              visualDensity: VisualDensity.compact,
                              label: Text(dep.status, style: const TextStyle(fontSize: 10)),
                              backgroundColor: dep.status == 'HEALTHY' || dep.status == 'CONNECTED'
                                  ? Colors.green.shade100
                                  : Colors.orange.shade100,
                            ),
                          ],
                        ),
                      ),
                    ),
                  ] else ...[
                    const Text('Checking subsystem availability...'),
                  ],
                ],
              ),
            ),
          ),
          const SizedBox(height: 16),

          // Agent Context Seam Card
          Card(
            shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: [
                      const Icon(Icons.smart_toy_outlined, color: BlueversePalette.coastDeep),
                      const SizedBox(width: 8),
                      Text(
                        'Agent Context Seam (Pre-G07 Safe)',
                        style: Theme.of(context).textTheme.titleSmall?.copyWith(fontWeight: FontWeight.bold),
                      ),
                    ],
                  ),
                  const SizedBox(height: 8),
                  const Text(
                    'Typed private seam provides read-only contextual boundary data. Executable tools and agent runs remain disconnected.',
                    style: TextStyle(fontSize: 12, height: 1.4),
                  ),
                  const SizedBox(height: 12),
                  if (vm.agentContext != null) ...[
                    Text('Agent: ${vm.agentContext!.agentName}', style: const TextStyle(fontWeight: FontWeight.bold)),
                    const SizedBox(height: 4),
                    Text('Status: ${vm.agentContext!.status}'),
                    const SizedBox(height: 4),
                    Text('Detail: ${vm.agentContext!.detail}'),
                  ] else ...[
                    const Text('Agent seam context unavailable.'),
                  ],
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }

  void _showCreateDestinationDialog(BuildContext context, ExperienceViewModel vm) {
    final nameCtrl = TextEditingController();
    final regionCtrl = TextEditingController(text: 'Southern Province');
    final descCtrl = TextEditingController();
    final latCtrl = TextEditingController(text: '5.9482');
    final lonCtrl = TextEditingController(text: '80.4716');

    showDialog(
      context: context,
      builder: (_) => AlertDialog(
        title: const Text('Add Coastal Destination'),
        content: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              TextField(controller: nameCtrl, decoration: const InputDecoration(labelText: 'Name *')),
              TextField(controller: regionCtrl, decoration: const InputDecoration(labelText: 'Region *')),
              TextField(controller: descCtrl, decoration: const InputDecoration(labelText: 'Description')),
              TextField(controller: latCtrl, decoration: const InputDecoration(labelText: 'Latitude *')),
              TextField(controller: lonCtrl, decoration: const InputDecoration(labelText: 'Longitude *')),
            ],
          ),
        ),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context), child: const Text('Cancel')),
          FilledButton(
            onPressed: () async {
              final req = CreateDestinationRequest(
                name: nameCtrl.text,
                region: regionCtrl.text,
                description: descCtrl.text,
                latitude: double.tryParse(latCtrl.text) ?? 5.9482,
                longitude: double.tryParse(lonCtrl.text) ?? 80.4716,
              );
              await vm.repository.createDestination(req);
              await vm.loadDiscoveryData();
              if (context.mounted) Navigator.pop(context);
            },
            child: const Text('Save'),
          ),
        ],
      ),
    );
  }

  void _showEditDestinationDialog(BuildContext context, ExperienceViewModel vm, DestinationDto dest) {
    final nameCtrl = TextEditingController(text: dest.name);
    final regionCtrl = TextEditingController(text: dest.region);
    final descCtrl = TextEditingController(text: dest.description);

    showDialog(
      context: context,
      builder: (_) => AlertDialog(
        title: const Text('Edit Destination'),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            TextField(controller: nameCtrl, decoration: const InputDecoration(labelText: 'Name')),
            TextField(controller: regionCtrl, decoration: const InputDecoration(labelText: 'Region')),
            TextField(controller: descCtrl, decoration: const InputDecoration(labelText: 'Description')),
          ],
        ),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context), child: const Text('Cancel')),
          FilledButton(
            onPressed: () async {
              final req = UpdateDestinationRequest(
                name: nameCtrl.text,
                region: regionCtrl.text,
                description: descCtrl.text,
                latitude: dest.latitude,
                longitude: dest.longitude,
              );
              await vm.repository.updateDestination(dest.id, req);
              await vm.loadDiscoveryData();
              if (context.mounted) Navigator.pop(context);
            },
            child: const Text('Update'),
          ),
        ],
      ),
    );
  }

  void _showCreateActivityDialog(BuildContext context, ExperienceViewModel vm) {
    if (vm.destinations.isEmpty) return;
    final titleCtrl = TextEditingController();
    final catCtrl = TextEditingController(text: 'Marine Life');
    final descCtrl = TextEditingController();
    var destId = vm.destinations.first.id;

    showDialog(
      context: context,
      builder: (_) => AlertDialog(
        title: const Text('Add Coastal Activity'),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            DropdownButtonFormField<String>(
              value: destId,
              items: vm.destinations.map((d) => DropdownMenuItem(value: d.id, child: Text(d.name))).toList(),
              onChanged: (val) => destId = val ?? destId,
            ),
            TextField(controller: titleCtrl, decoration: const InputDecoration(labelText: 'Name *')),
            TextField(controller: catCtrl, decoration: const InputDecoration(labelText: 'Category *')),
            TextField(controller: descCtrl, decoration: const InputDecoration(labelText: 'Description')),
          ],
        ),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context), child: const Text('Cancel')),
          FilledButton(
            onPressed: () async {
              final req = CreateActivityRequest(
                code: 'act-${DateTime.now().millisecondsSinceEpoch}',
                name: titleCtrl.text,
                category: catCtrl.text,
                description: descCtrl.text,
              );
              await vm.repository.createActivity(req);
              await vm.loadDiscoveryData();
              if (context.mounted) Navigator.pop(context);
            },
            child: const Text('Save'),
          ),
        ],
      ),
    );
  }

  void _showCreateOfferingDialog(BuildContext context, ExperienceViewModel vm) {
    if (vm.destinations.isEmpty || vm.activities.isEmpty) return;
    final titleCtrl = TextEditingController();
    final descCtrl = TextEditingController();
    final priceCtrl = TextEditingController(text: '7500');
    final capCtrl = TextEditingController(text: '6');
    var destId = vm.destinations.first.id;
    var actId = vm.activities.first.id;

    showDialog(
      context: context,
      builder: (_) => AlertDialog(
        title: const Text('Add Bookable Offering'),
        content: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              DropdownButtonFormField<String>(
                value: destId,
                items: vm.destinations.map((d) => DropdownMenuItem(value: d.id, child: Text(d.name))).toList(),
                onChanged: (val) => destId = val ?? destId,
              ),
              DropdownButtonFormField<String>(
                value: actId,
                items: vm.activities.map((a) => DropdownMenuItem(value: a.id, child: Text(a.name))).toList(),
                onChanged: (val) => actId = val ?? actId,
              ),
              TextField(controller: titleCtrl, decoration: const InputDecoration(labelText: 'Title *')),
              TextField(controller: descCtrl, decoration: const InputDecoration(labelText: 'Description')),
              TextField(controller: priceCtrl, decoration: const InputDecoration(labelText: 'Price (LKR) *')),
              TextField(controller: capCtrl, decoration: const InputDecoration(labelText: 'Capacity *')),
            ],
          ),
        ),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context), child: const Text('Cancel')),
          FilledButton(
            onPressed: () async {
              final req = CreateOfferingRequest(
                destinationId: destId,
                activityId: actId,
                title: titleCtrl.text,
                description: descCtrl.text,
                price: double.tryParse(priceCtrl.text) ?? 7500,
                maxCapacity: int.tryParse(capCtrl.text) ?? 6,
              );
              await vm.repository.createOffering(req);
              await vm.loadDiscoveryData();
              if (context.mounted) Navigator.pop(context);
            },
            child: const Text('Save'),
          ),
        ],
      ),
    );
  }
}
