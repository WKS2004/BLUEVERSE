import 'package:timezone/data/latest.dart' as timezone_data;
import 'package:timezone/timezone.dart' as timezone;

class ExperienceTimeZone {
  ExperienceTimeZone._();

  static bool _initialized = false;
  static const defaultZoneId = 'Asia/Colombo';

  static void _ensureInitialized() {
    if (_initialized) return;
    timezone_data.initializeTimeZones();
    _initialized = true;
  }

  static timezone.Location _location(String zoneId) {
    _ensureInitialized();
    return timezone.getLocation(zoneId);
  }

  static bool isSupported(String zoneId) {
    try {
      _location(zoneId);
      return true;
    } on Object {
      return false;
    }
  }

  static DateTime inZone(DateTime instant, String zoneId) =>
      timezone.TZDateTime.from(instant.toUtc(), _location(zoneId));

  static DateTime toUtc({
    required DateTime date,
    required int hour,
    required int minute,
    String zoneId = defaultZoneId,
  }) => timezone.TZDateTime(
    _location(zoneId),
    date.year,
    date.month,
    date.day,
    hour,
    minute,
  ).toUtc();

  static DateTime today(String zoneId) {
    final now = inZone(DateTime.now(), zoneId);
    return DateTime(now.year, now.month, now.day);
  }

  static String format(DateTime instant, String zoneId) {
    if (!isSupported(zoneId)) {
      return '${instant.toUtc().toIso8601String()} (UTC; unsupported time zone: $zoneId)';
    }
    final local = inZone(instant, zoneId);
    final date = '${local.year}-${_two(local.month)}-${_two(local.day)}';
    final time = '${_two(local.hour)}:${_two(local.minute)}';
    final displayedZone = _location(zoneId).name;
    return '$date $time ($displayedZone)';
  }

  static String _two(int value) => value.toString().padLeft(2, '0');
}
