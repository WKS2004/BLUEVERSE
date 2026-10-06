import 'package:flutter/material.dart';

import '../../data/models/experience_models.dart';
import '../auth_view_model.dart';
import '../blueverse_theme.dart';
import 'experience_view_model.dart';

class DestinationDetailScreen extends StatefulWidget {
  const DestinationDetailScreen({
    super.key,
    required this.destinationId,
    required this.viewModel,
    this.authViewModel,
  });

  final String destinationId;
  final ExperienceViewModel viewModel;
  final AuthViewModel? authViewModel;

  @override
  State<DestinationDetailScreen> createState() =>
      _DestinationDetailScreenState();
}

class _DestinationDetailScreenState extends State<DestinationDetailScreen> {
  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      widget.viewModel.loadDestinationDetail(widget.destinationId);
      if (widget.authViewModel?.user != null) {
        widget.viewModel.loadFavourites();
      }
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
                  Text(
                    widget.viewModel.errorMessage ??
                        'Unable to load destination details.',
                    textAlign: TextAlign.center,
                  ),
                  const SizedBox(height: 12),
                  ElevatedButton(
                    onPressed: () => widget.viewModel.loadDestinationDetail(
                      widget.destinationId,
                    ),
                    child: const Text('Try again'),
                  ),
                  TextButton(
                    onPressed: () => Navigator.pop(context),
                    child: const Text('Go back'),
                  ),
                ],
              ),
            ),
          );
        }

        final isFav = vm.isFavourite('DESTINATION', dest.id);
        final biodiversity = vm.destinationBiodiversity;

        return Scaffold(
          appBar: AppBar(
            title: Text(dest.name),
            actions: [
              IconButton(
                tooltip: 'Refresh destination details',
                icon: const Icon(Icons.refresh),
                onPressed: () => vm.loadDestinationDetail(widget.destinationId),
              ),
              IconButton(
                key: const Key('btn-fav-destination-detail'),
                icon: Icon(
                  isFav ? Icons.bookmark : Icons.bookmark_border,
                  color: isFav ? BlueversePalette.coastDeep : null,
                ),
                tooltip: 'Save to Wishlist',
                onPressed: () => _toggleFavourite(context, dest.id),
              ),
            ],
          ),
          body: SingleChildScrollView(
            padding: const EdgeInsets.all(16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                if (vm.favouriteActionErrorMessage != null)
                  _messageCard(vm.favouriteActionErrorMessage!, isError: true),
                if (vm.successMessage != null)
                  _messageCard(vm.successMessage!, isError: false),
                if (vm.destinationOfferingsErrorMessage != null)
                  _messageCard(
                    'Experiences at this destination could not be loaded. Use the refresh button to try again.',
                    isError: true,
                  ),
                // Header Card
                Card(
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(20),
                  ),
                  elevation: 1,
                  child: Padding(
                    padding: const EdgeInsets.all(20),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Row(
                          children: [
                            const Icon(
                              Icons.location_on,
                              color: BlueversePalette.coastDeep,
                            ),
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
                          style: Theme.of(context).textTheme.headlineSmall
                              ?.copyWith(fontWeight: FontWeight.bold),
                        ),
                        if (dest.description != null &&
                            dest.description!.isNotEmpty) ...[
                          const SizedBox(height: 12),
                          Text(
                            dest.description!,
                            style: const TextStyle(height: 1.5, fontSize: 14),
                          ),
                        ],
                        const SizedBox(height: 12),
                        Text(
                          'Coordinates: ${_formatCoordinate(dest.latitude, positive: 'N', negative: 'S')}, ${_formatCoordinate(dest.longitude, positive: 'E', negative: 'W')}',
                          style: TextStyle(
                            color: Colors.grey.shade600,
                            fontSize: 12,
                          ),
                        ),
                      ],
                    ),
                  ),
                ),
                const SizedBox(height: 16),

                // Live Marine Conditions Card
                Card(
                  color: Colors.blue.shade50,
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(16),
                  ),
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
                                    : 'Not reported',
                              ),
                              _buildMetric(
                                'Condition',
                                vm.destinationMarine!.waterCondition.isEmpty
                                    ? 'Not reported'
                                    : vm.destinationMarine!.waterCondition,
                              ),
                              _buildMetric(
                                'Wind Speed',
                                vm.destinationMarine!.windSpeedKnots != null
                                    ? '${vm.destinationMarine!.windSpeedKnots!.toStringAsFixed(0)} kts'
                                    : 'Not reported',
                              ),
                            ],
                          ),
                          const SizedBox(height: 12),
                          Row(
                            children: [
                              const Text(
                                'Safety Level: ',
                                style: TextStyle(fontWeight: FontWeight.bold),
                              ),
                              Chip(
                                label: Text(
                                  vm.destinationMarine!.safetyLevel.isEmpty
                                      ? 'Not reported'
                                      : vm.destinationMarine!.safetyLevel,
                                ),
                                backgroundColor:
                                    vm.destinationMarine!.safetyLevel
                                                .toLowerCase() ==
                                            'normal' ||
                                        vm.destinationMarine!.safetyLevel
                                                .toLowerCase() ==
                                            'low'
                                    ? Colors.green.shade100
                                    : Colors.orange.shade100,
                              ),
                            ],
                          ),
                          if (vm.destinationMarine!.disclaimer.isNotEmpty) ...[
                            const SizedBox(height: 8),
                            Text(
                              vm.destinationMarine!.disclaimer,
                              style: TextStyle(
                                color: Colors.grey.shade700,
                                fontSize: 12,
                              ),
                            ),
                          ],
                        ] else ...[
                          Text(
                            vm.destinationMarineError == null
                                ? 'Current marine conditions are not available.'
                                : 'Marine conditions could not be loaded. Check local guidance before setting out.',
                          ),
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
                    shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(16),
                    ),
                    child: Padding(
                      padding: const EdgeInsets.all(16),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Row(
                            children: [
                              Icon(
                                Icons.warning_amber_rounded,
                                color: Colors.amber.shade900,
                              ),
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
                            'Status: ${_humanizeStatus(vm.destinationAdvisories!.remoteStatus)}',
                            style: const TextStyle(fontWeight: FontWeight.bold),
                          ),
                          const SizedBox(height: 6),
                          if (vm.destinationAdvisories!.advisories.isEmpty)
                            const Text('No active advisories were returned.'),
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
                ] else ...[
                  Card(
                    color: Colors.amber.shade50,
                    child: const Padding(
                      padding: EdgeInsets.all(16),
                      child: Text(
                        'Operational advisories could not be checked. Review local guidance before setting out.',
                      ),
                    ),
                  ),
                  const SizedBox(height: 16),
                ],

                // Biodiversity Context Card
                Card(
                  color: Colors.teal.shade50,
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(16),
                  ),
                  child: Padding(
                    padding: const EdgeInsets.all(16),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Row(
                          children: [
                            Icon(
                              Icons.eco_rounded,
                              color: Colors.teal.shade800,
                            ),
                            const SizedBox(width: 8),
                            Expanded(
                              child: Text(
                                'Biodiversity context',
                                style: TextStyle(
                                  fontWeight: FontWeight.bold,
                                  color: Colors.teal.shade900,
                                  fontSize: 16,
                                ),
                              ),
                            ),
                          ],
                        ),
                        const SizedBox(height: 8),
                        if (biodiversity == null)
                          Text(
                            vm.destinationBiodiversityError == null
                                ? 'No biodiversity predictions are available right now.'
                                : 'Biodiversity information could not be loaded. Please try again later.',
                          )
                        else ...[
                          Text(
                            _biodiversityStatus(biodiversity),
                            style: const TextStyle(fontWeight: FontWeight.bold),
                          ),
                          const SizedBox(height: 6),
                          if (biodiversity.predictions.isEmpty)
                            const Text(
                              'No species predictions are available for this destination right now.',
                            )
                          else ...[
                            Text(
                              '${biodiversity.predictions.length} model prediction${biodiversity.predictions.length == 1 ? '' : 's'} • likelihood only, not confirmed sightings',
                              style: TextStyle(color: Colors.grey.shade800),
                            ),
                            const SizedBox(height: 8),
                            ...biodiversity.predictions.map((species) {
                              final probability = species.occurrenceProbability
                                  .clamp(0.0, 1.0)
                                  .toDouble();
                              return Padding(
                                padding: const EdgeInsets.symmetric(
                                  vertical: 5,
                                ),
                                child: Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    Text(
                                      species.speciesName.isEmpty
                                          ? 'Unnamed species'
                                          : species.speciesName,
                                      style: const TextStyle(
                                        fontWeight: FontWeight.w600,
                                      ),
                                    ),
                                    if (species.scientificName.isNotEmpty)
                                      Text(
                                        species.scientificName,
                                        style: const TextStyle(
                                          fontStyle: FontStyle.italic,
                                          fontSize: 12,
                                        ),
                                      ),
                                    Text(
                                      'Estimated likelihood: ${(probability * 100).toStringAsFixed(0)}%${species.conservationStatus.isEmpty ? '' : ' • Conservation status: ${species.conservationStatus}'}',
                                      style: const TextStyle(fontSize: 13),
                                    ),
                                    if (species
                                            .habitatSuitability
                                            ?.isNotEmpty ==
                                        true)
                                      Text(
                                        'Habitat: ${species.habitatSuitability}',
                                        style: const TextStyle(fontSize: 12),
                                      ),
                                    if (species.primaryThreats?.isNotEmpty ==
                                        true)
                                      Text(
                                        'Known pressures: ${species.primaryThreats}',
                                        style: const TextStyle(fontSize: 12),
                                      ),
                                  ],
                                ),
                              );
                            }),
                          ],
                          if (biodiversity.modelSource?.isNotEmpty == true ||
                              biodiversity.modelVersion?.isNotEmpty ==
                                  true) ...[
                            const SizedBox(height: 8),
                            Text(
                              [
                                if (biodiversity.modelSource?.isNotEmpty ==
                                    true)
                                  'Source: ${biodiversity.modelSource}',
                                if (biodiversity.modelVersion?.isNotEmpty ==
                                    true)
                                  'Version: ${biodiversity.modelVersion}',
                              ].join(' • '),
                              style: TextStyle(
                                color: Colors.grey.shade700,
                                fontSize: 12,
                              ),
                            ),
                          ],
                          if (biodiversity.uncertaintyNotes?.isNotEmpty ==
                              true) ...[
                            const SizedBox(height: 8),
                            Text(
                              'Uncertainty: ${biodiversity.uncertaintyNotes}',
                              style: const TextStyle(fontSize: 12),
                            ),
                          ],
                          if (biodiversity.disclaimer.isNotEmpty) ...[
                            const SizedBox(height: 8),
                            Text(
                              biodiversity.disclaimer,
                              style: TextStyle(
                                color: Colors.grey.shade700,
                                fontSize: 12,
                              ),
                            ),
                          ],
                        ],
                      ],
                    ),
                  ),
                ),
                const SizedBox(height: 16),

                // Offerings Section
                Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    Text(
                      'Experiences at this Destination (${vm.destinationOfferings.length})',
                      style: Theme.of(context).textTheme.titleMedium
                          ?.copyWith(fontWeight: FontWeight.bold),
                    ),
                  ],
                ),
                const SizedBox(height: 8),

                if (vm.destinationOfferings.isEmpty &&
                    vm.destinationOfferingsErrorMessage == null)
                  const Padding(
                    padding: EdgeInsets.symmetric(vertical: 16),
                    child: Center(
                      child: Text(
                        'No bookable experiences are listed at this destination.',
                      ),
                    ),
                  )
                else if (vm.destinationOfferings.isNotEmpty)
                  ...vm.destinationOfferings.map(
                    (off) => Card(
                      key: Key('card-destination-offering-${off.id}'),
                      margin: const EdgeInsets.only(bottom: 12),
                      shape: RoundedRectangleBorder(
                        borderRadius: BorderRadius.circular(16),
                      ),
                      child: ListTile(
                        title: Text(
                          off.title,
                          style: const TextStyle(fontWeight: FontWeight.bold),
                        ),
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
        Text(
          value,
          style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 16),
        ),
        const SizedBox(height: 4),
        Text(
          label,
          style: TextStyle(color: Colors.grey.shade700, fontSize: 11),
        ),
      ],
    );
  }

  void _toggleFavourite(BuildContext context, String destinationId) {
    if (widget.authViewModel?.user == null) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: const Text('Sign in to save coastal experiences.'),
          action: SnackBarAction(
            label: 'Sign in',
            onPressed: () => Navigator.pushNamed(context, '/signin'),
          ),
        ),
      );
      return;
    }
    widget.viewModel.toggleFavourite('DESTINATION', destinationId);
  }

  Widget _messageCard(String message, {required bool isError}) {
    final color = isError ? Colors.red : BlueversePalette.coastDeep;
    return Padding(
      padding: const EdgeInsets.only(bottom: 12),
      child: DecoratedBox(
        decoration: BoxDecoration(
          color: color.withAlpha(18),
          borderRadius: BorderRadius.circular(12),
          border: Border.all(color: color.withAlpha(70)),
        ),
        child: Padding(
          padding: const EdgeInsets.all(12),
          child: Text(
            message,
            style: TextStyle(
              color: isError ? Colors.red.shade900 : BlueversePalette.coastDeep,
            ),
          ),
        ),
      ),
    );
  }

  String _formatCoordinate(
    double value, {
    required String positive,
    required String negative,
  }) =>
      '${value.abs().toStringAsFixed(4)}° ${value >= 0 ? positive : negative}';

  String _humanizeStatus(String? status) {
    final words = (status ?? '').trim().replaceAll('_', ' ').toLowerCase();
    if (words.isEmpty) return 'Status unknown';
    return words
        .split(' ')
        .map(
          (word) => word.isEmpty
              ? word
              : '${word[0].toUpperCase()}${word.substring(1)}',
        )
        .join(' ');
  }

  String _biodiversityStatus(BiodiversityContextResponseDto result) {
    if (result.predictions.isNotEmpty && result.responded) {
      return 'Model result available';
    }
    if (!result.responded ||
        {
          'NOT_CONNECTED',
          'UNAVAILABLE',
          'ERROR',
        }.contains(result.status.toUpperCase())) {
      return 'Biodiversity model unavailable';
    }
    return 'No model result available';
  }
}
