import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/ui/planner_time.dart';

void main() {
  group('destinationWallTimeToUtc', () {
    test(
      'interprets destination-local time independently of device time zone',
      () {
        expect(
          destinationWallTimeToUtc('2026-10-06T09:30', 'Asia/Colombo'),
          DateTime.utc(2026, 10, 6, 4),
        );
      },
    );

    test('rejects skipped and repeated daylight-saving clocks', () {
      expect(
        () => destinationWallTimeToUtc('2026-03-08T02:30', 'America/New_York'),
        throwsA(isA<PlannerTimeException>()),
      );
      expect(
        () => destinationWallTimeToUtc('2026-11-01T01:30', 'America/New_York'),
        throwsA(isA<PlannerTimeException>()),
      );
      expect(
        destinationWallTimeToUtc('2026-11-01T03:30', 'America/New_York'),
        DateTime.utc(2026, 11, 1, 8, 30),
      );
    });

    test('rejects malformed dates and unknown destinations', () {
      expect(
        () => destinationWallTimeToUtc('2026-02-30T09:00', 'Asia/Colombo'),
        throwsA(isA<PlannerTimeException>()),
      );
      expect(
        () => destinationWallTimeToUtc('not-a-time', 'Asia/Colombo'),
        throwsA(isA<PlannerTimeException>()),
      );
      expect(
        () => destinationWallTimeToUtc('2026-10-06T09:00', 'Unknown/Coast'),
        throwsA(isA<PlannerTimeException>()),
      );
    });
  });
}
