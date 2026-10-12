import 'package:timezone/data/latest.dart' as tzdata;
import 'package:timezone/timezone.dart' as tz;

class PlannerTimeException implements Exception {
  const PlannerTimeException(this.message);

  final String message;

  @override
  String toString() => message;
}

bool _timeZonesInitialized = false;

/// Converts a destination-local wall clock to its unique UTC instant.
///
/// Planning rejects daylight-saving gaps and overlaps so a time cannot be
/// silently shifted or interpreted differently by the device and API.
DateTime destinationWallTimeToUtc(String wallTime, String timeZone) {
  if (!_timeZonesInitialized) {
    tzdata.initializeTimeZones();
    _timeZonesInitialized = true;
  }

  final match = RegExp(r'^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})$')
      .firstMatch(wallTime);
  if (match == null) {
    throw const PlannerTimeException('Choose a valid date and time.');
  }
  final year = int.parse(match[1]!);
  final month = int.parse(match[2]!);
  final day = int.parse(match[3]!);
  final hour = int.parse(match[4]!);
  final minute = int.parse(match[5]!);
  final wallAsUtc = DateTime.utc(year, month, day, hour, minute);
  if (wallAsUtc.year != year ||
      wallAsUtc.month != month ||
      wallAsUtc.day != day ||
      wallAsUtc.hour != hour ||
      wallAsUtc.minute != minute) {
    throw const PlannerTimeException('Choose a valid date and time.');
  }

  late final tz.Location location;
  try {
    location = tz.getLocation(timeZone);
  } on Object {
    throw const PlannerTimeException(
      'Choose a destination with a supported time zone.',
    );
  }

  final offsets = <Duration>{};
  for (var minute = -2160; minute <= 2160; minute += 60) {
    final sample = tz.TZDateTime.from(
      wallAsUtc.add(Duration(minutes: minute)),
      location,
    );
    offsets.add(sample.timeZoneOffset);
  }

  final matches = <DateTime>{};
  for (final offset in offsets) {
    final candidate = wallAsUtc.subtract(offset);
    final local = tz.TZDateTime.from(candidate, location);
    if (local.year == year &&
        local.month == month &&
        local.day == day &&
        local.hour == hour &&
        local.minute == minute) {
      matches.add(candidate);
    }
  }

  if (matches.length != 1) {
    throw const PlannerTimeException(
      'That local time is skipped or occurs twice. Choose another time.',
    );
  }
  return matches.single;
}
