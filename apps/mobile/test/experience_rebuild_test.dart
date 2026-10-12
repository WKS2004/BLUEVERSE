import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:mobile/data/models/experience_models.dart';
import 'package:mobile/data/repositories/experience_repository.dart';
import 'package:mobile/data/services/api_gateway_config.dart';
import 'package:mobile/data/services/experience_api_service.dart';
import 'package:mobile/data/services/experience_location_service.dart';
import 'package:mobile/ui/auth_view_model.dart';
import 'package:mobile/ui/experiences/catalogue_management_screen.dart';
import 'package:mobile/ui/experiences/experience_view_model.dart';
import 'package:mobile/ui/experiences/experience_detail_scope.dart';
import 'package:mobile/ui/experiences/experiences_discovery_screen.dart';
import 'package:mobile/ui/experiences/offering_schedule_manager_dialog.dart';

import 'support/mobile_test_support.dart';

// Requirements: Member 1 component §§5, 6, 8, 10; shared workflows
// experience-discovery, experience-destination-detail, experience-offering-detail,
// experience-catalogue-management and experience-favourites-management.
DestinationDto destination(String id, {String? name}) => DestinationDto(
  id: id,
  name: name ?? id,
  slug: id,
  region: 'Southern Province',
  latitude: 6.14,
  longitude: 80.10,
  status: 'PUBLISHED',
  createdAt: DateTime.utc(2026),
  updatedAt: DateTime.utc(2026),
);

OfferingDto offering(String id) => OfferingDto(
  id: id,
  destinationId: 'reef',
  destinationName: 'Reef',
  activityId: 'snorkel',
  activityName: 'Snorkelling',
  activityCode: 'SNORKELLING',
  title: id,
  status: 'PUBLISHED',
  createdAt: DateTime.utc(2026),
  updatedAt: DateTime.utc(2026),
);

PagedResult<T> page<T>(List<T> items, {int number = 1, int? total}) =>
    PagedResult(
      total: total ?? items.length,
      page: number,
      pageSize: 50,
      items: items,
    );

class Repo extends ExperienceRepository {
  Repo()
    : super(
        apiService: ExperienceApiService(
          client: MockClient((_) async => http.Response('{}', 500)),
          storage: MemoryAuthCredentialStore(),
        ),
      );
  Future<PagedResult<DestinationDto>> Function(String?, String?, String?, int)?
  destinations;
  Future<DestinationDto> Function(String)? detail;
  Future<MarineConditionsContextDto> Function()? marine;
  Future<OfferingDto> Function(String)? offeringDetail;
  Future<List<ScheduleDto>> Function()? schedules;
  Future<MapSearchResponseDto> Function(String)? places;
  Future<NearbyResponse> Function()? nearby;
  int favouriteReads = 0;
  @override
  Future<List<FavouriteDto>> getUserFavourites() async {
    favouriteReads++;
    return [];
  }

  Future<DestinationDto> Function(CreateDestinationRequest)? create;
  @override
  Future<DestinationDto> createDestination(
    CreateDestinationRequest request,
  ) async => create!(request);
  @override
  Future<PagedResult<DestinationDto>> getDestinations({
    String? query,
    String? region,
    String? status,
    int? page,
    int? pageSize,
  }) async => destinations == null
      ? thisPage(status, page ?? 1)
      : destinations!(query, region, status, page ?? 1);
  PagedResult<DestinationDto> thisPage(String? status, int number) => page(
    status == null || status == 'PUBLISHED' ? [destination('reef')] : [],
    number: number,
  );
  @override
  Future<PagedResult<ActivityDto>> getActivities({
    String? destinationId,
    String? category,
    String? query,
    String? status,
    int? page,
    int? pageSize,
  }) async =>
      PagedResult(total: 0, page: page ?? 1, pageSize: 50, items: const []);
  @override
  Future<PagedResult<OfferingDto>> getOfferings({
    String? destinationId,
    String? activityId,
    String? query,
    String? status,
    int? page,
    int? pageSize,
  }) async =>
      PagedResult(total: 0, page: page ?? 1, pageSize: 50, items: const []);
  @override
  Future<DestinationDto> getDestinationById(String id) async =>
      detail == null ? destination(id) : detail!(id);
  @override
  Future<MarineConditionsContextDto> getDestinationMarineConditions(
    String id,
  ) async {
    if (marine != null) return marine!();
    throw const ExperienceApiException(503, 'Unavailable');
  }

  @override
  Future<OperationalAdvisoriesResponseDto> getDestinationOperationalAdvisories(
    String id,
  ) async => throw const ExperienceApiException(503, 'Unavailable');
  @override
  Future<BiodiversityContextResponseDto> getDestinationBiodiversity(
    String id,
  ) async => throw const ExperienceApiException(503, 'Unavailable');
  @override
  Future<OfferingDto> getOfferingById(String id) async =>
      offeringDetail == null ? offering(id) : offeringDetail!(id);
  @override
  Future<List<ScheduleDto>> getOfferingSchedules(
    String id, {
    String? from,
    String? to,
  }) async => schedules == null ? [] : schedules!();
  @override
  Future<MapSearchResponseDto> searchMapPlaces(String query) async =>
      places!(query);
  @override
  Future<NearbyResponse> getNearbyExperiences({
    String? query,
    double? latitude,
    double? longitude,
    double radiusMeters = 50000,
    int limit = 10,
  }) async => nearby!();
}

class Location implements ExperienceLocationProvider {
  int calls = 0;
  final pending = Completer<ExperienceCoordinates>();
  @override
  Future<ExperienceCoordinates> getCurrentLocation() {
    calls++;
    return pending.future;
  }
}

class Gateways extends ApiGatewayConfig {
  @override
  List<Uri> get baseUris => [
    Uri.parse('http://localhost'),
    Uri.parse('http://10.0.2.2'),
  ];
}

void main() {
  test('MOB-EXP-014 discovery includes later pages and preserves independent results on failure', () async {
    final repo = Repo();
    final pages = <int>[];
    repo.destinations = (_, _, _, number) async {
      pages.add(number);
      return page([destination('place-$number')], number: number, total: 2);
    };
    final vm = ExperienceViewModel(repository: repo);
    addTearDown(vm.dispose);
    await vm.loadDiscoveryData();
    expect(pages, [1, 2]);
    expect(vm.destinations.map((d) => d.id), ['place-1', 'place-2']);
    repo.destinations = (_, _, _, _) async =>
        throw const ExperienceApiException(503, 'Unavailable');
    await vm.loadDiscoveryData();
    expect(vm.destinations.length, 2);
    expect(vm.destinationsErrorMessage, isNotNull);
    expect(vm.catalogueNotice, contains('destinations'));
    expect(vm.isLoading, isFalse);
  });

  test('MOB-EXP-015 last search wins and filters do not shrink the map destination set', () async {
    final repo = Repo();
    final old = Completer<PagedResult<DestinationDto>>();
    final vm = ExperienceViewModel(repository: repo);
    addTearDown(vm.dispose);
    await vm.loadDiscoveryData();
    repo.destinations = (query, _, _, _) async =>
        query == 'old' ? old.future : page([destination('new')]);
    vm.updateFilters(query: 'old');
    final first = vm.loadDiscoveryData();
    vm.updateFilters(query: 'new');
    await vm.loadDiscoveryData();
    old.complete(page([destination('old')]));
    await first;
    expect(vm.destinations.single.id, 'new');
    expect(vm.mapDestinations.single.id, 'reef');
    expect(vm.isLoading, isFalse);
  });

  test('MOB-EXP-016 destination renders before a slow optional peer and cannot be overwritten', () async {
    final repo = Repo();
    final peer = Completer<MarineConditionsContextDto>();
    repo.marine = () => peer.future;
    final vm = ExperienceViewModel(repository: repo);
    addTearDown(vm.dispose);
    final first = vm.loadDestinationDetail('first');
    await Future<void>.delayed(Duration.zero);
    expect(vm.selectedDestination?.id, 'first');
    expect(vm.isLoadingDestination, isFalse);
    expect(vm.isLoadingDestinationContext, isTrue);
    final second = vm.loadDestinationDetail('second');
    await Future<void>.delayed(Duration.zero);
    peer.completeError(const ExperienceApiException(503, 'Unavailable'));
    await Future.wait([first, second]);
    expect(vm.selectedDestination?.id, 'second');
    expect(vm.destinationMarine, isNull);
    expect(vm.destinationMarineError, isNotNull);
    expect(vm.isLoadingDestinationContext, isFalse);
  });

  test('MOB-EXP-017 offering renders while schedules are pending', () async {
    final repo = Repo();
    final pending = Completer<List<ScheduleDto>>();
    repo.schedules = () => pending.future;
    final vm = ExperienceViewModel(repository: repo);
    addTearDown(vm.dispose);
    final request = vm.loadOfferingDetail('tour');
    await Future<void>.delayed(Duration.zero);
    expect(vm.selectedOffering?.id, 'tour');
    expect(vm.isLoadingOffering, isFalse);
    expect(vm.isLoadingOfferingSchedules, isTrue);
    pending.completeError(const ExperienceApiException(503, 'Unavailable'));
    await request;
    expect(vm.offeringSchedulesError, isNotNull);
    expect(vm.isLoadingOfferingSchedules, isFalse);
  });

  test('MOB-EXP-018 disposed screens discard in-flight results', () async {
    final repo = Repo();
    final pending = Completer<PagedResult<DestinationDto>>();
    repo.destinations = (_, _, _, _) => pending.future;
    final vm = ExperienceViewModel(repository: repo);
    var notifications = 0;
    vm.addListener(() => notifications++);
    final request = vm.loadDiscoveryData();
    vm.dispose();
    pending.complete(page([destination('late')]));
    await request;
    expect(notifications, 1);
    expect(vm.destinations, isEmpty);
  });

  test('MOB-EXP-019 duplicate management submissions execute once and report rejection', () async {
    final vm = ExperienceViewModel(repository: Repo());
    addTearDown(vm.dispose);
    final pending = Completer<bool>();
    var calls = 0;
    Future<bool> operation() {
      calls++;
      return pending.future;
    }

    final first = vm.performCatalogueAction(
      operation,
      successText: 'Saved',
      refreshManagementCatalog: false,
    );
    expect(
      await vm.performCatalogueAction(operation, successText: 'Saved'),
      isNull,
    );
    pending.completeError(const ExperienceApiException(403, 'Denied'));
    expect(await first, isNull);
    expect(calls, 1);
    expect(vm.isPerformingCatalogueAction, isFalse);
    expect(vm.errorMessage, contains('permission'));
  });

  test('MOB-EXP-020 account switch clears management data and ignores private reads', () async {
    final repo = Repo();
    final pending = Completer<PagedResult<DestinationDto>>();
    repo.destinations = (_, _, _, _) => pending.future;
    final vm = ExperienceViewModel(repository: repo);
    addTearDown(vm.dispose);
    vm.managementDestinations = [destination('private')];
    final request = vm.loadManagementCatalog();
    vm.clearUserScopedState();
    pending.complete(page([destination('late-private')]));
    await request;
    expect(vm.managementDestinations, isEmpty);
    expect(vm.managementOfferings, isEmpty);
    expect(vm.isLoadingManagementCatalog, isFalse);
    expect(vm.favourites, isEmpty);
  });

  test('MOB-EXP-021 selecting a coast cancels pending device location without requesting nearby', () async {
    final repo = Repo();
    final location = Location();
    var nearbyCalls = 0;
    repo.nearby = () async {
      nearbyCalls++;
      throw const ExperienceApiException(503, 'Unavailable');
    };
    final vm = ExperienceViewModel(
      repository: repo,
      locationProvider: location,
    );
    addTearDown(vm.dispose);
    expect(location.calls, 0);
    final request = vm.fetchNearbyFromCurrentLocation();
    await vm.fetchNearbyFromCurrentLocation();
    expect(location.calls, 1);
    vm.clearMapSelection();
    location.pending.complete(
      const ExperienceCoordinates(latitude: 6.14, longitude: 80.1),
    );
    await request;
    expect(vm.mapFocusLocation, isNull);
    expect(nearbyCalls, 0);
    expect(vm.isLoadingNearby, isFalse);
  });

  test('MOB-EXP-022 denied location leaves manual discovery usable', () async {
    final location = Location();
    final vm = ExperienceViewModel(
      repository: Repo(),
      locationProvider: location,
    );
    addTearDown(vm.dispose);
    final request = vm.fetchNearbyFromCurrentLocation();
    location.pending.completeError(
      const ExperienceLocationException(
        'Permission denied. Search for a coastal place instead.',
      ),
    );
    await request;
    expect(vm.nearbyErrorMessage, contains('Search for a coastal place'));
    expect(vm.isGettingCurrentLocation, isFalse);
    expect(vm.isLoadingNearby, isFalse);
    await vm.loadDiscoveryData();
    expect(vm.destinations.single.id, 'reef');
  });

  test('MOB-EXP-023 malformed pagination and prediction probabilities fail explicitly', () {
    for (final data in [
      <String, dynamic>{},
      {'total': -1, 'page': 1, 'pageSize': 50, 'items': []},
      {
        'total': 1,
        'page': 1,
        'pageSize': 50,
        'items': [null],
      },
    ]) {
      expect(
        () => PagedResult.fromJson(data, DestinationDto.fromJson),
        throwsFormatException,
      );
    }
    for (final probability in [
      null,
      -0.01,
      1.01,
      double.nan,
      double.infinity,
    ]) {
      expect(
        () => FocalSpeciesPredictionDto.fromJson({
          'occurrenceProbability': probability,
        }),
        throwsFormatException,
      );
    }
    for (final probability in [0, 1]) {
      expect(
        FocalSpeciesPredictionDto.fromJson({
          'occurrenceProbability': probability,
        }).occurrenceProbability,
        probability,
      );
    }
  });

  test(
    'MOB-EXP-024 reads fail over but a timed-out creation is never replayed',
    () async {
      final calls = <http.Request>[];
      final client = MockClient((request) async {
        calls.add(request);
        if (request.url.host == 'localhost') {
          throw TimeoutException('Timed out');
        }
        return http.Response(
          '{"total":0,"page":1,"pageSize":50,"items":[]}',
          200,
        );
      });
      final api = ExperienceApiService(
        client: client,
        gateway: Gateways(),
        storage: MemoryAuthCredentialStore(),
      );
      expect((await api.getDestinations()).items, isEmpty);
      expect(calls.map((r) => r.url.host), ['localhost', '10.0.2.2']);
      calls.clear();
      final writeApi = ExperienceApiService(
        client: client,
        gateway: Gateways(),
        storage: MemoryAuthCredentialStore(),
      );
      await expectLater(
        writeApi.createDestination(
          const CreateDestinationRequest(
            name: 'Reef',
            latitude: 6.14,
            longitude: 80.1,
          ),
        ),
        throwsA(isA<ExperienceApiException>()),
      );
      expect(calls.length, 1);
      expect(calls.single.method, 'POST');
    },
  );

  test(
    'MOB-EXP-025 missing scoped token cannot use another account legacy token',
    () async {
      final storage = MemoryAuthCredentialStore();
      storage.values.addAll({
        'blueverse.active_account_id': 'new-user',
        'blueverse.access_token': 'old-user-token',
      });
      final client = MockClient((request) async {
        expect(request.headers['authorization'], isNull);
        return http.Response('[]', 200);
      });
      await ExperienceApiService(
        client: client,
        storage: storage,
      ).getUserFavourites();
    },
  );

  testWidgets(
    'MOB-EXP-026 management actions fit narrow screens with large text',
    (tester) async {
      tester.view.physicalSize = const Size(320, 760);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      final repo = Repo();
      repo.destinations = (_, _, status, _) async => page(
        status == 'PUBLISHED'
            ? [
                destination(
                  'reef',
                  name: 'A very long coastal sanctuary destination name',
                ),
              ]
            : [],
      );
      final vm = ExperienceViewModel(repository: repo);
      addTearDown(vm.dispose);
      final user = mobileTestUser(
        permissions: ['experiences.catalogue.manage'],
      );
      final authRepo = FakeAuthRepository(user: user);
      addTearDown(authRepo.close);
      final auth = AuthViewModel(repository: authRepo)..user = user;
      addTearDown(auth.dispose);
      await tester.pumpWidget(
        MaterialApp(
          builder: (context, child) => MediaQuery(
            data: MediaQuery.of(context)
                .copyWith(textScaler: const TextScaler.linear(1.5)),
            child: child!,
          ),
          home: CatalogueManagementScreen(viewModel: vm, authViewModel: auth),
        ),
      );
      await tester.pumpAndSettle();
      await tester.scrollUntilVisible(
        find.byKey(const Key('manage-card-destination-reef')),
        200,
        scrollable: find
            .byWidgetPredicate(
              (w) => w is Scrollable && w.axisDirection == AxisDirection.down,
            )
            .first,
      );
      expect(
        find.text('A very long coastal sanctuary destination name'),
        findsOneWidget,
      );
      expect(find.text('Review publishing'), findsOneWidget);
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets('MOB-EXP-027 discovery retains selected tab when reopened', (
    tester,
  ) async {
    final vm = ExperienceViewModel(repository: Repo());
    addTearDown(vm.dispose);
    await tester.pumpWidget(
      MaterialApp(home: ExperiencesDiscoveryScreen(viewModel: vm)),
    );
    await tester.pumpAndSettle();
    // State can be restored without asking for GPS or losing discovery filters.
    vm.discoveryTab = 1;
    await tester.pumpWidget(const SizedBox());
    await tester.pumpWidget(
      MaterialApp(home: ExperiencesDiscoveryScreen(viewModel: vm)),
    );
    await tester.pumpAndSettle();
    expect(find.byKey(const Key('input-map-place-search')), findsOneWidget);
    expect(vm.discoveryTab, 1);
    expect(vm.mapFocusLocation, isNull);
  });
  testWidgets(
    'MOB-EXP-028 failed create keeps fields and blocks duplicate saves and premature dismissal',
    (tester) async {
      final repo = Repo();
      final pending = Completer<DestinationDto>();
      var creates = 0;
      repo.create = (request) {
        creates++;
        expect(request.name, 'Coral bay');
        expect(request.latitude, 6.14);
        expect(request.longitude, 80.1);
        return pending.future;
      };
      final vm = ExperienceViewModel(repository: repo);
      final user = mobileTestUser(
        permissions: ['experiences.catalogue.manage'],
      );
      final authRepo = FakeAuthRepository(user: user);
      final auth = AuthViewModel(repository: authRepo)..user = user;
      addTearDown(vm.dispose);
      addTearDown(auth.dispose);
      addTearDown(authRepo.close);
      await tester.pumpWidget(
        MaterialApp(
          home: CatalogueManagementScreen(viewModel: vm, authViewModel: auth),
        ),
      );
      await tester.pumpAndSettle();
      await tester.scrollUntilVisible(
        find.text('Add Destination'),
        160,
        scrollable: find
            .byWidgetPredicate(
              (w) => w is Scrollable && w.axisDirection == AxisDirection.down,
            )
            .first,
      );
      await tester.tap(find.text('Add Destination'));
      await tester.pumpAndSettle();
      final fields = find.descendant(
        of: find.byType(AlertDialog),
        matching: find.byType(TextField),
      );
      await tester.enterText(fields.at(0), 'Coral bay');
      await tester.enterText(fields.at(4), '6.14');
      await tester.enterText(fields.at(5), '80.1');
      await tester.tap(find.text('Save'));
      await tester.pump();
      expect(creates, 1);
      expect(
        tester
            .widget<FilledButton>(find.widgetWithText(FilledButton, 'Save'))
            .onPressed,
        isNull,
      );
      expect(
        tester
            .widget<TextButton>(find.widgetWithText(TextButton, 'Cancel'))
            .onPressed,
        isNull,
      );
      await tester.binding.handlePopRoute();
      await tester.pump();
      expect(find.byType(AlertDialog), findsOneWidget);
      pending.completeError(const ExperienceApiException(403, 'Forbidden'));
      await tester.pumpAndSettle();
      expect(
        find.descendant(
          of: find.byType(AlertDialog),
          matching: find.text(
            'Your account does not have permission to do that.',
          ),
        ),
        findsOneWidget,
      );
      expect(
        tester.widget<TextField>(fields.at(0)).controller!.text,
        'Coral bay',
      );
      expect(creates, 1);
      await tester.tap(find.text('Cancel'));
      await tester.pumpAndSettle();
      expect(find.byType(AlertDialog), findsNothing);
      expect(tester.takeException(), isNull);
    },
  );

  test('MOB-EXP-029 map coordinates must be complete, finite and in range', () {
    for (final data in <Map<String, dynamic>>[
      {'longitude': 80.1},
      {'latitude': 6.1},
      {'latitude': 91, 'longitude': 80.1},
      {'latitude': 6.1, 'longitude': -181},
      {'latitude': double.nan, 'longitude': 80.1},
      {'latitude': 6.1, 'longitude': double.infinity},
      {'latitude': '6.1', 'longitude': 80.1},
    ]) {
      expect(
        () => MapSearchResultItemDto.fromJson(data),
        throwsFormatException,
      );
    }
    final boundary = MapSearchResultItemDto.fromJson({
      'latitude': -90,
      'longitude': 180,
    });
    expect(boundary.latitude, -90);
    expect(boundary.longitude, 180);
  });

  test('MOB-EXP-030 region plus offering keyword does not require a destination keyword match', () {
    final vm = ExperienceViewModel(repository: Repo());
    addTearDown(vm.dispose);
    vm.mapDestinations = [destination('reef')];
    vm.destinations = []; // The keyword did not match the destination name.
    vm.offerings = [offering('Dawn reef tour')];
    vm.updateFilters(query: 'dawn', region: 'Southern Province');
    expect(vm.visibleOfferings.map((o) => o.id), ['Dawn reef tour']);
    vm.updateFilters(region: 'Eastern Province');
    expect(vm.visibleOfferings, isEmpty);
  });

  testWidgets(
    'MOB-EXP-031 departure maintenance fits a narrow viewport and exposes both actions',
    (tester) async {
      tester.view.physicalSize = const Size(320, 760);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      final repo = Repo();
      repo.schedules = () async => [
        ScheduleDto(
          id: 'slot',
          offeringId: 'reef-tour',
          createdAt: DateTime.utc(2026),
          updatedAt: DateTime.utc(2026),
          startsAt: DateTime.utc(2026, 10, 15, 3),
          endsAt: DateTime.utc(2026, 10, 15, 6),
          timeZoneId: 'Asia/Colombo',
          isActive: true,
        ),
      ];
      final vm = ExperienceViewModel(repository: repo);
      addTearDown(vm.dispose);
      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: OfferingScheduleManagerDialog(
              viewModel: vm,
              offering: offering(
                'A guided reef snorkelling and coastal exploration tour',
              ),
            ),
          ),
        ),
      );
      await tester.pumpAndSettle();
      expect(find.text('Edit departure time'), findsOneWidget);
      expect(find.text('Remove departure time'), findsOneWidget);
      expect(find.textContaining('08:30'), findsOneWidget);
      expect(tester.takeException(), isNull);
    },
  );
  testWidgets(
    'MOB-EXP-032 popped detail releases its model and refreshes saved places after the frame',
    (tester) async {
      final repo = Repo();
      final parent = ExperienceViewModel(repository: repo);
      parent.selectedDestination = destination('original');
      final user = mobileTestUser(permissions: []);
      final authRepo = FakeAuthRepository(user: user);
      final auth = AuthViewModel(repository: authRepo)..user = user;
      addTearDown(parent.dispose);
      addTearDown(auth.dispose);
      addTearDown(authRepo.close);
      final navigator = GlobalKey<NavigatorState>();
      ExperienceViewModel? detail;
      await tester.pumpWidget(
        MaterialApp(
          navigatorKey: navigator,
          home: ListenableBuilder(
            listenable: parent,
            builder: (_, _) => const Scaffold(body: Text('Discovery')),
          ),
        ),
      );
      final popped = navigator.currentState!.push(
        MaterialPageRoute<void>(
          builder: (_) => ExperienceDetailScope(
            parent: parent,
            auth: auth,
            builder: (model) {
              detail = model;
              return const Scaffold(body: Text('Destination'));
            },
          ),
        ),
      );
      await tester.pumpAndSettle();
      expect(identical(detail, parent), isFalse);
      await detail!.loadDestinationDetail('other');
      expect(detail!.selectedDestination!.id, 'other');
      expect(parent.selectedDestination!.id, 'original');
      navigator.currentState!.pop();
      await tester.pumpAndSettle();
      await popped;
      expect(find.text('Discovery'), findsOneWidget);
      expect(repo.favouriteReads, 1);
      expect(parent.selectedDestination!.id, 'original');
      expect(tester.takeException(), isNull);
    },
  );
}
