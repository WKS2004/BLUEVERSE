import 'package:flutter/material.dart';

import '../../data/models/marine_models.dart';
import '../blueverse_theme.dart';

const marineLabels = [
  'Coastal conditions',
  'Coastal evidence',
  'Activity safety limits',
];

class MarineHero extends StatelessWidget {
  const MarineHero({super.key, required this.index});
  final int index;

  @override
  Widget build(BuildContext context) {
    const titles = [
      'Read the coast before you go.',
      'Follow the coastal evidence.',
      'Give every activity reviewed limits.',
    ];
    const descriptions = [
      'Check wind, waves and swell for a place and time. Choose an activity to compare the evidence with its reviewed safety limits.',
      'Revisit saved readings and assessments, with the sources and exact criteria that produced each result.',
      'Inspect safety limits and their sources. Authorized managers can draft, independently review and deactivate versions.',
    ];
    const assets = [
      'coastal-conditions-hero',
      'coastal-evidence-hero',
      'activity-safety-hero',
    ];
    return Card(
      margin: const EdgeInsets.only(bottom: 20),
      clipBehavior: Clip.antiAlias,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          ExcludeSemantics(
            child: Image.asset(
              'assets/coastal/marine/${assets[index]}.webp',
              height: 180,
              fit: BoxFit.cover,
              errorBuilder: (_, _, _) => const SizedBox(
                height: 180,
                child: ColoredBox(
                  color: BlueversePalette.coastGlass,
                  child: Icon(
                    Icons.waves,
                    size: 48,
                    color: BlueversePalette.coastDeep,
                  ),
                ),
              ),
            ),
          ),
          Padding(
            padding: const EdgeInsets.all(22),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  marineLabels[index].toUpperCase(),
                  style: const TextStyle(
                    color: BlueversePalette.coastBlue,
                    fontSize: 11,
                    fontWeight: FontWeight.w800,
                    letterSpacing: 1.2,
                  ),
                ),
                const SizedBox(height: 12),
                Semantics(
                  header: true,
                  child: Text(
                    titles[index],
                    style: Theme.of(context).textTheme.headlineSmall,
                  ),
                ),
                const SizedBox(height: 12),
                Text(descriptions[index], style: const TextStyle(height: 1.5)),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class MarineOutcomeExplanation extends StatelessWidget {
  const MarineOutcomeExplanation({super.key, required this.status});
  final String status;
  @override
  Widget build(BuildContext context) {
    final (title, detail) = switch (status) {
      'SUITABLE' => (
        'Within the reviewed limits',
        'The available evidence meets the reviewed activity limits. This is not a guarantee of safety; follow local advice.',
      ),
      'CAUTION' => (
        'Plan with extra care',
        'The evidence reaches a reviewed caution level. Read the factors below and consider local guidance.',
      ),
      'UNSUITABLE' => (
        'Outside the reviewed limits',
        'One or more reviewed limits are exceeded. Consider another time or activity.',
      ),
      _ => (
        'More evidence is needed',
        'Missing or stale evidence prevents a reliable assessment. Do not treat this outcome as safe.',
      ),
    };
    return Semantics(
      liveRegion: true,
      child: Padding(
        padding: const EdgeInsets.symmetric(vertical: 12),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(title, style: const TextStyle(fontWeight: FontWeight.w800)),
            const SizedBox(height: 4),
            Text(detail),
          ],
        ),
      ),
    );
  }
}

class MarineReadings extends StatelessWidget {
  const MarineReadings({super.key, required this.snapshot});
  final ConditionSnapshotDto snapshot;
  @override
  Widget build(BuildContext context) => Wrap(
    spacing: 12,
    runSpacing: 8,
    children: [
      _reading('Wind', snapshot.windSpeed, 'km/h'),
      _reading('Waves', snapshot.waveHeight, 'm'),
      _reading('Swell', snapshot.swellHeight, 'm'),
      _reading('Rain', snapshot.rain, 'mm'),
    ],
  );
  Widget _reading(String label, double? value, String unit) => Text(
    '$label: ${value == null ? 'Unavailable' : '${value.toStringAsFixed(2)} $unit'}',
    style: const TextStyle(fontWeight: FontWeight.w600),
  );
}

/// A blank time means current conditions. Unzoned input is explicitly UTC;
/// offsets are normalized and impossible calendar values are rejected.
DateTime? marineUtcTime(String raw) {
  final text = raw.trim();
  if (text.isEmpty) return null;
  final match = RegExp(
    r'^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})(?::(\d{2})(?:\.(\d{1,7}))?)?(Z|[+-]\d{2}:\d{2})?$',
  ).firstMatch(text);
  if (match == null) {
    throw const FormatException(
      'Enter a valid UTC time, e.g. 2026-10-09T10:00Z.',
    );
  }
  final parts = [for (var i = 1; i <= 6; i++) int.parse(match.group(i) ?? '0')];
  final date = DateTime.utc(
    parts[0],
    parts[1],
    parts[2],
    parts[3],
    parts[4],
    parts[5],
  );
  if (parts[0] < 1 ||
      date.year != parts[0] ||
      date.month != parts[1] ||
      date.day != parts[2] ||
      parts[3] > 23 ||
      parts[4] > 59 ||
      parts[5] > 59) {
    throw const FormatException('Enter a real calendar date and UTC time.');
  }
  final zone = match.group(8);
  if (zone != null && zone != 'Z') {
    final hours = int.parse(zone.substring(1, 3));
    final minutes = int.parse(zone.substring(4, 6));
    if (hours > 14 || minutes > 59 || (hours == 14 && minutes != 0)) {
      throw const FormatException('Enter a valid time-zone offset.');
    }
  }
  return DateTime.parse(zone == null ? '${text}Z' : text).toUtc();
}
