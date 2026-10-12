import 'package:flutter/material.dart';

import '../blueverse_theme.dart';

import '../../data/models/marine_models.dart';
import 'marine_presentation.dart';

/// Lightweight reusable condition card used by the suitability flow and the
/// standalone history/evidence surfaces.
class MarineConditionCard extends StatelessWidget {
  const MarineConditionCard({super.key, required this.snapshot});

  final ConditionSnapshotDto snapshot;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(14),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Expanded(
                  child: Text(
                    '${snapshot.latitude}, ${snapshot.longitude}',
                    style: const TextStyle(fontWeight: FontWeight.w700),
                  ),
                ),
                _freshnessChip(snapshot.freshnessStatus),
              ],
            ),
            const SizedBox(height: 8),
            MarineReadings(snapshot: snapshot),
            const SizedBox(height: 12),
            _detail('Forecast time', snapshot.forecastTime),
            _detail('Retrieved at', snapshot.retrievedAt),
            _detail('Source', snapshot.source),
            _detail('Freshness', snapshot.freshnessStatus),
            if (snapshot.missingFields.isNotEmpty)
              _detail('Missing fields', snapshot.missingFields.join(', ')),
          ],
        ),
      ),
    );
  }

  Widget _freshnessChip(String freshness) {
    return Chip(
      label: Text(freshness),
      backgroundColor: Colors.transparent,
      labelStyle: TextStyle(
        backgroundColor: Colors.transparent,
        color: BlueversePalette.coastMuted,
      ),
    );
  }

  Widget _detail(String label, dynamic value) => Padding(
    padding: const EdgeInsets.only(bottom: 6),
    child: Row(
      children: [
        const SizedBox(width: 8),
        Text(
          '$label: ',
          style: const TextStyle(
            fontWeight: FontWeight.w600,
            color: BlueversePalette.coastMuted,
          ),
        ),
        Expanded(
          child: Text(
            value is DateTime ? value.toIso8601String() : value.toString(),
            style: const TextStyle(color: BlueversePalette.coastMuted),
          ),
        ),
      ],
    ),
  );
}
