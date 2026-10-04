import 'package:flutter/material.dart';

import '../blueverse_theme.dart';
import 'experience_view_model.dart';

class DestinationDetailScreen extends StatefulWidget {
  const DestinationDetailScreen({
    super.key,
    required this.destinationId,
    required this.viewModel,
  });

  final String destinationId;
  final ExperienceViewModel viewModel;

  @override
  State<DestinationDetailScreen> createState() => _DestinationDetailScreenState();
}

class _DestinationDetailScreenState extends State<DestinationDetailScreen> {
  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      widget.viewModel.loadDestinationDetail(widget.destinationId);
    });
  }

  @override
  Widget build(BuildContext context) {
    return ListenableBuilder(
      listenable: widget.viewModel,
      builder: (context, _) {
        final vm = widget.viewModel;
        final dest = vm.selectedDestination;

        if (vm.isLoadingDestination && dest == null) {
          return Scaffold(
            appBar: AppBar(title: const Text('Loading Destination...')),
            body: const Center(child: CircularProgressIndicator()),
          );
        }

        if (dest == null) {
          return Scaffold(
            appBar: AppBar(title: const Text('Destination Not Found')),
            body: Center(
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  const Text('Unable to load destination details.'),
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

        final isFav = vm.isFavourite('DESTINATION', dest.id);

        return Scaffold(
          appBar: AppBar(
            title: Text(dest.name),
            actions: [
              IconButton(
                key: const Key('btn-fav-destination-detail'),
                icon: Icon(
                  isFav ? Icons.bookmark : Icons.bookmark_border,
                  color: isFav ? BlueversePalette.coastDeep : null,
                ),
                tooltip: 'Save to Wishlist',
                onPressed: () {
                  vm.toggleFavourite('DESTINATION', dest.id);
                },
              ),
            ],
          ),
          body: SingleChildScrollView(
            padding: const EdgeInsets.all(16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                // Header Card
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
                            const Icon(Icons.location_on, color: BlueversePalette.coastDeep),
                            const SizedBox(width: 8),
                            Text(
                              dest.region ?? 'Coastal Region',
                              style: const TextStyle(
                                fontWeight: FontWeight.bold,
                                color: BlueversePalette.coastDeep,
                              ),
                            ),
                            const Spacer(),
                            Chip(
                              label: Text(dest.status),
                              backgroundColor: dest.status == 'PUBLISHED'
                                  ? Colors.teal.shade50
                                  : Colors.grey.shade200,
                            ),
                          ],
                        ),
                        const SizedBox(height: 12),
                        Text(
                          dest.name,
                          style: Theme.of(context).textTheme.headlineSmall?.copyWith(
                                fontWeight: FontWeight.bold,
                              ),
                        ),
                        if (dest.description != null && dest.description!.isNotEmpty) ...[
                          const SizedBox(height: 12),
                          Text(
                            dest.description!,
                            style: const TextStyle(height: 1.5, fontSize: 14),
                          ),
                        ],
                        const SizedBox(height: 12),
                        Text(
                          'Coordinates: ${dest.latitude.toStringAsFixed(4)}° N, ${dest.longitude.toStringAsFixed(4)}° E',
                          style: TextStyle(color: Colors.grey.shade600, fontSize: 12),
                        ),
                      ],
                    ),
                  ),
                ),
                const SizedBox(height: 16),

                // Live Marine Conditions Card
                Card(
                  color: Colors.blue.shade50,
                  shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
                  child: Padding(
                    padding: const EdgeInsets.all(16),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Row(
                          children: [
                            const Icon(Icons.waves, color: Colors.blue),
                            const SizedBox(width: 8),
                            Text(
                              'Live Marine Conditions',
                              style: TextStyle(
                                fontWeight: FontWeight.bold,
                                color: Colors.blue.shade900,
                                fontSize: 16,
                              ),
                            ),
                          ],
                        ),
                        const SizedBox(height: 12),
                        if (vm.destinationMarine != null) ...[
                          Row(
                            mainAxisAlignment: MainAxisAlignment.spaceAround,
                            children: [
                              _buildMetric(
                                'Wave Height',
                                vm.destinationMarine!.waveHeightMeters != null
                                    ? '${vm.destinationMarine!.waveHeightMeters!.toStringAsFixed(1)} m'
                                    : 'Calm',
                              ),
                              _buildMetric(
                                'Condition',
                                vm.destinationMarine!.waterCondition,
                              ),
                              _buildMetric(
                                'Wind Speed',
                                vm.destinationMarine!.windSpeedKnots != null
                                    ? '${vm.destinationMarine!.windSpeedKnots!.toStringAsFixed(0)} kts'
                                    : 'Moderate',
                              ),
                            ],
                          ),
                          const SizedBox(height: 12),
                          Row(
                            children: [
                              const Text('Safety Level: ', style: TextStyle(fontWeight: FontWeight.bold)),
                              Chip(
                                label: Text(vm.destinationMarine!.safetyLevel),
                                backgroundColor: vm.destinationMarine!.safetyLevel.toLowerCase() == 'normal' ||
                                        vm.destinationMarine!.safetyLevel.toLowerCase() == 'low'
                                    ? Colors.green.shade100
                                    : Colors.orange.shade100,
                              ),
                            ],
                          ),
                        ] else ...[
                          const Text('Live marine sensor conditions currently calibrating.'),
                        ],
                      ],
                    ),
                  ),
                ),
                const SizedBox(height: 16),

                // Operational Advisories Card
                if (vm.destinationAdvisories != null) ...[
                  Card(
                    color: Colors.amber.shade50,
                    shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
                    child: Padding(
                      padding: const EdgeInsets.all(16),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Row(
                            children: [
                              Icon(Icons.warning_amber_rounded, color: Colors.amber.shade900),
                              const SizedBox(width: 8),
                              Text(
                                'Operational Advisories',
                                style: TextStyle(
                                  fontWeight: FontWeight.bold,
                                  color: Colors.amber.shade900,
                                  fontSize: 16,
                                ),
                              ),
                            ],
                          ),
                          const SizedBox(height: 8),
                          Text(
                            'Status: ${vm.destinationAdvisories!.remoteStatus}',
                            style: const TextStyle(fontWeight: FontWeight.bold),
                          ),
                          const SizedBox(height: 6),
                          ...vm.destinationAdvisories!.advisories.map(
                            (adv) => Padding(
                              padding: const EdgeInsets.symmetric(vertical: 4),
                              child: Row(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: [
                                  const Text('• '),
                                  Expanded(
                                    child: Text(
                                      '${adv.title}: ${adv.description}',
                                      style: const TextStyle(fontSize: 13),
                                    ),
                                  ),
                                ],
                              ),
                            ),
                          ),
                        ],
                      ),
                    ),
                  ),
                  const SizedBox(height: 16),
                ],

                // Biodiversity Context Card
                if (vm.destinationBiodiversity != null) ...[
                  Card(
                    color: Colors.teal.shade50,
                    shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
                    child: Padding(
                      padding: const EdgeInsets.all(16),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Row(
                            children: [
                              Icon(Icons.eco_rounded, color: Colors.teal.shade800),
                              const SizedBox(width: 8),
                              Text(
                                'Biodiversity & Conservation',
                                style: TextStyle(
                                  fontWeight: FontWeight.bold,
                                  color: Colors.teal.shade900,
                                  fontSize: 16,
                                ),
                              ),
                            ],
                          ),
                          const SizedBox(height: 10),
                          Text(
                            'Species Observed: ${vm.destinationBiodiversity!.predictions.length}',
                            style: const TextStyle(fontWeight: FontWeight.bold),
                          ),
                          const SizedBox(height: 6),
                          ...vm.destinationBiodiversity!.predictions.map(
                            (sp) => Padding(
                              padding: const EdgeInsets.symmetric(vertical: 2),
                              child: Text(
                                '• ${sp.speciesName} (${sp.conservationStatus}) - ${(sp.occurrenceProbability * 100).toStringAsFixed(0)}% occurrence',
                                style: const TextStyle(fontSize: 13),
                              ),
                            ),
                          ),
                        ],
                      ),
                    ),
                  ),
                  const SizedBox(height: 16),
                ],

                // Offerings Section
                Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    Text(
                      'Experiences at this Destination (${vm.destinationOfferings.length})',
                      style: Theme.of(context).textTheme.titleMedium?.copyWith(
                            fontWeight: FontWeight.bold,
                          ),
                    ),
                  ],
                ),
                const SizedBox(height: 8),

                if (vm.destinationOfferings.isEmpty)
                  const Padding(
                    padding: EdgeInsets.symmetric(vertical: 16),
                    child: Center(
                      child: Text('No bookable offerings currently listed at this destination.'),
                    ),
                  )
                else
                  ...vm.destinationOfferings.map(
                    (off) => Card(
                      key: Key('card-destination-offering-${off.id}'),
                      margin: const EdgeInsets.only(bottom: 12),
                      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
                      child: ListTile(
                        title: Text(off.title, style: const TextStyle(fontWeight: FontWeight.bold)),
                        subtitle: Text(
                          '${off.price != null ? "LKR ${off.price!.toStringAsFixed(0)}" : "Price on request"} • ${off.maxCapacity != null ? "Up to ${off.maxCapacity} guests" : "Group rates"}',
                        ),
                        trailing: const Icon(Icons.arrow_forward_ios, size: 16),
                        onTap: () {
                          Navigator.pushNamed(
                            context,
                            '/experiences/offerings/${off.id}',
                          );
                        },
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

  Widget _buildMetric(String label, String value) {
    return Column(
      children: [
        Text(value, style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 16)),
        const SizedBox(height: 4),
        Text(label, style: TextStyle(color: Colors.grey.shade700, fontSize: 11)),
      ],
    );
  }
}
