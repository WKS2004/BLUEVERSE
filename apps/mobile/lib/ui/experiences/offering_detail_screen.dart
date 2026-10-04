import 'package:flutter/material.dart';

import '../blueverse_theme.dart';
import 'experience_view_model.dart';

class OfferingDetailScreen extends StatefulWidget {
  const OfferingDetailScreen({
    super.key,
    required this.offeringId,
    required this.viewModel,
  });

  final String offeringId;
  final ExperienceViewModel viewModel;

  @override
  State<OfferingDetailScreen> createState() => _OfferingDetailScreenState();
}

class _OfferingDetailScreenState extends State<OfferingDetailScreen> {
  int _partySize = 2;
  DateTime _selectedDate = DateTime.now().add(const Duration(days: 1));

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      widget.viewModel.loadOfferingDetail(widget.offeringId);
    });
  }

  @override
  Widget build(BuildContext context) {
    return ListenableBuilder(
      listenable: widget.viewModel,
      builder: (context, _) {
        final vm = widget.viewModel;
        final off = vm.selectedOffering;

        if (vm.isLoadingOffering && off == null) {
          return Scaffold(
            appBar: AppBar(title: const Text('Loading Experience...')),
            body: const Center(child: CircularProgressIndicator()),
          );
        }

        if (off == null) {
          return Scaffold(
            appBar: AppBar(title: const Text('Offering Not Found')),
            body: Center(
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  const Text('Unable to load offering details.'),
                  const SizedBox(height: 12),
                  ElevatedButton(
                    onPressed: () => Navigator.pop(context),
                    child: const Text('Go Back'),
                  ),
                ],
              ),
            ),
          );
        }

        final isFav = vm.isFavourite('OFFERING', off.id);

        return Scaffold(
          appBar: AppBar(
            title: Text(off.title),
            actions: [
              IconButton(
                key: const Key('btn-fav-offering-detail'),
                icon: Icon(
                  isFav ? Icons.bookmark : Icons.bookmark_border,
                  color: isFav ? BlueversePalette.coastDeep : null,
                ),
                tooltip: 'Save to Wishlist',
                onPressed: () {
                  vm.toggleFavourite('OFFERING', off.id);
                },
              ),
            ],
          ),
          body: SingleChildScrollView(
            padding: const EdgeInsets.all(16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                // Experience Overview Card
                Card(
                  shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(20)),
                  elevation: 1,
                  child: Padding(
                    padding: const EdgeInsets.all(20),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Row(
                          children: [
                            Chip(
                              label: Text(off.status),
                              backgroundColor: off.status == 'PUBLISHED'
                                  ? Colors.teal.shade50
                                  : Colors.grey.shade200,
                            ),
                            const Spacer(),
                            Text(
                              off.price != null
                                  ? 'LKR ${off.price!.toStringAsFixed(0)}'
                                  : 'Price on request',
                              style: const TextStyle(
                                fontSize: 20,
                                fontWeight: FontWeight.bold,
                                color: BlueversePalette.coastDeep,
                              ),
                            ),
                            const Text(' / person', style: TextStyle(color: Colors.grey)),
                          ],
                        ),
                        const SizedBox(height: 12),
                        Text(
                          off.title,
                          style: Theme.of(context).textTheme.headlineSmall?.copyWith(
                                fontWeight: FontWeight.bold,
                              ),
                        ),
                        if (off.activityName.isNotEmpty) ...[
                          const SizedBox(height: 4),
                          Text(
                            off.activityName,
                            style: const TextStyle(
                              color: BlueversePalette.coastDeep,
                              fontWeight: FontWeight.w600,
                            ),
                          ),
                        ],
                        if (off.description != null && off.description!.isNotEmpty) ...[
                          const SizedBox(height: 12),
                          Text(
                            off.description!,
                            style: const TextStyle(height: 1.5, fontSize: 14),
                          ),
                        ],
                        const SizedBox(height: 16),
                        Row(
                          children: [
                            const Icon(Icons.group, size: 20, color: Colors.grey),
                            const SizedBox(width: 8),
                            Text(
                              off.maxCapacity != null
                                  ? 'Maximum Capacity: ${off.maxCapacity} guests'
                                  : 'Capacity: Group rates available',
                            ),
                          ],
                        ),
                      ],
                    ),
                  ),
                ),
                const SizedBox(height: 16),

                // Schedules & Slots Section
                Text(
                  'Upcoming Schedule Windows (${vm.offeringSchedules.length})',
                  style: Theme.of(context).textTheme.titleMedium?.copyWith(
                        fontWeight: FontWeight.bold,
                      ),
                ),
                const SizedBox(height: 8),

                if (vm.offeringSchedules.isEmpty)
                  Card(
                    shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
                    child: const Padding(
                      padding: EdgeInsets.all(16),
                      child: Center(
                        child: Text('No recurring schedules listed. Check availability below.'),
                      ),
                    ),
                  )
                else
                  ...vm.offeringSchedules.map(
                    (s) => Card(
                      key: Key('card-schedule-${s.id}'),
                      margin: const EdgeInsets.only(bottom: 8),
                      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
                      child: ListTile(
                        leading: const Icon(Icons.schedule, color: BlueversePalette.coastDeep),
                        title: Text('${s.startsAt.toLocal().toString().substring(0, 16)} - ${s.endsAt.toLocal().toString().substring(11, 16)}'),
                        subtitle: Text('Time Zone: ${s.timeZoneId} • Active: ${s.isActive ? "Yes" : "No"}'),
                      ),
                    ),
                  ),
                const SizedBox(height: 16),

                // Live Availability Evaluation Section (Non-CRUD)
                Card(
                  color: BlueversePalette.coastDeep.withAlpha(15),
                  shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(20)),
                  child: Padding(
                    padding: const EdgeInsets.all(20),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Row(
                          children: [
                            const Icon(Icons.event_available, color: BlueversePalette.coastDeep),
                            const SizedBox(width: 8),
                            Text(
                              'Evaluate Real-Time Availability',
                              style: Theme.of(context).textTheme.titleSmall?.copyWith(
                                    fontWeight: FontWeight.bold,
                                    color: BlueversePalette.coastDeep,
                                  ),
                            ),
                          ],
                        ),
                        const SizedBox(height: 12),
                        const Text(
                          'Test real-time coastal booking capacity against schedules and environmental safety conditions.',
                          style: TextStyle(fontSize: 13, height: 1.4),
                        ),
                        const SizedBox(height: 16),

                        // Party Size Selector
                        Row(
                          mainAxisAlignment: MainAxisAlignment.spaceBetween,
                          children: [
                            const Text('Party Size:'),
                            Row(
                              children: [
                                IconButton(
                                  icon: const Icon(Icons.remove_circle_outline),
                                  onPressed: _partySize > 1
                                      ? () => setState(() => _partySize--)
                                      : null,
                                ),
                                Text(
                                  '$_partySize',
                                  style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 16),
                                ),
                                IconButton(
                                  icon: const Icon(Icons.add_circle_outline),
                                  onPressed: off.maxCapacity == null || _partySize < off.maxCapacity!
                                      ? () => setState(() => _partySize++)
                                      : null,
                                ),
                              ],
                            ),
                          ],
                        ),
                        const SizedBox(height: 12),

                        // Date Selector
                        Row(
                          mainAxisAlignment: MainAxisAlignment.spaceBetween,
                          children: [
                            Text(
                              'Date: ${_selectedDate.year}-${_selectedDate.month.toString().padLeft(2, '0')}-${_selectedDate.day.toString().padLeft(2, '0')}',
                            ),
                            TextButton.icon(
                              icon: const Icon(Icons.calendar_month),
                              label: const Text('Change Date'),
                              onPressed: () async {
                                final picked = await showDatePicker(
                                  context: context,
                                  initialDate: _selectedDate,
                                  firstDate: DateTime.now(),
                                  lastDate: DateTime.now().add(const Duration(days: 90)),
                                );
                                if (picked != null) {
                                  setState(() => _selectedDate = picked);
                                }
                              },
                            ),
                          ],
                        ),
                        const SizedBox(height: 16),

                        // Evaluate Button
                        FilledButton.icon(
                          key: const Key('btn-evaluate-availability'),
                          style: FilledButton.styleFrom(
                            backgroundColor: BlueversePalette.coastDeep,
                            minimumSize: const Size.fromHeight(48),
                          ),
                          onPressed: vm.isEvaluatingAvailability
                              ? null
                              : () {
                                  final startUtc = DateTime.utc(
                                    _selectedDate.year,
                                    _selectedDate.month,
                                    _selectedDate.day,
                                    9,
                                    0,
                                  );
                                  final endUtc = DateTime.utc(
                                    _selectedDate.year,
                                    _selectedDate.month,
                                    _selectedDate.day,
                                    12,
                                    0,
                                  );
                                  vm.evaluateOfferingAvailability(
                                    offeringId: off.id,
                                    startUtc: startUtc,
                                    endUtc: endUtc,
                                    requestedPartySize: _partySize,
                                  );
                                },
                          icon: vm.isEvaluatingAvailability
                              ? const SizedBox(
                                  width: 18,
                                  height: 18,
                                  child: CircularProgressIndicator(
                                    color: Colors.white,
                                    strokeWidth: 2,
                                  ),
                                )
                              : const Icon(Icons.check_circle_outline),
                          label: Text(
                            vm.isEvaluatingAvailability
                                ? 'Evaluating...'
                                : 'Check Live Availability',
                          ),
                        ),

                        // Availability Result Output
                        if (vm.availabilityResult != null) ...[
                          const SizedBox(height: 16),
                          Container(
                            key: const Key('box-availability-result'),
                            padding: const EdgeInsets.all(12),
                            decoration: BoxDecoration(
                              color: vm.availabilityResult!.status == 'AVAILABLE'
                                  ? Colors.green.shade50
                                  : Colors.orange.shade50,
                              borderRadius: BorderRadius.circular(12),
                              border: Border.all(
                                color: vm.availabilityResult!.status == 'AVAILABLE'
                                    ? Colors.green.shade300
                                    : Colors.orange.shade300,
                              ),
                            ),
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Row(
                                  children: [
                                    Icon(
                                      vm.availabilityResult!.status == 'AVAILABLE'
                                          ? Icons.check_circle
                                          : Icons.info_outline,
                                      color: vm.availabilityResult!.status == 'AVAILABLE'
                                          ? Colors.green.shade800
                                          : Colors.orange.shade800,
                                    ),
                                    const SizedBox(width: 8),
                                    Text(
                                      vm.availabilityResult!.status == 'AVAILABLE'
                                          ? 'Available for Booking'
                                          : 'Status: ${vm.availabilityResult!.status}',
                                      style: TextStyle(
                                        fontWeight: FontWeight.bold,
                                        color: vm.availabilityResult!.status == 'AVAILABLE'
                                            ? Colors.green.shade900
                                            : Colors.orange.shade900,
                                      ),
                                    ),
                                  ],
                                ),
                                const SizedBox(height: 6),
                                Text(
                                  'Reason codes: ${vm.availabilityResult!.reasonCodes.join(', ')}',
                                  style: const TextStyle(fontSize: 13, height: 1.4),
                                ),
                                if (vm.availabilityResult!.operationalRestriction != null) ...[
                                  const SizedBox(height: 4),
                                  Text(
                                    'Restriction: ${vm.availabilityResult!.operationalRestriction!.reason ?? "Advisory in effect"}',
                                    style: const TextStyle(fontSize: 12, fontWeight: FontWeight.bold),
                                  ),
                                ],
                              ],
                            ),
                          ),
                        ],
                      ],
                    ),
                  ),
                ),
              ],
            ),
          ),
        );
      },
    );
  }
}
