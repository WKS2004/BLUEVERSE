import 'package:flutter/foundation.dart';

import '../data/models/planner_models.dart';
import '../data/services/planner_api_service.dart';

class PlannerHistoryViewModel extends ChangeNotifier {
  PlannerHistoryViewModel(this.api);
  final PlannerApiService api;
  List<PlannerItinerary> trips = const [];
  int page = 1, pageSize = 20;
  bool loading = false, reviewOnly = false, _disposed = false;
  int _generation = 0;
  String filter = 'All plans', search = '';
  String? error;
  bool get hasNext => trips.length == pageSize;
  List<PlannerItinerary> get visible => trips
      .where((trip) {
        final past = DateTime.parse(trip.endsAt)
            .isBefore(DateTime.now().toUtc());
        return (filter == 'All plans' ||
                filter == 'Drafts' && trip.status == 'DRAFT' ||
                filter == 'Confirmed' && trip.status == 'CONFIRMED' ||
                filter == 'Past' && past) &&
            '${trip.title} ${trip.description}'.toLowerCase().contains(
              search.trim().toLowerCase(),
            ) &&
            (!reviewOnly ||
                trip.items.any(
                  (item) =>
                      item.suitability != 'SUITABLE' ||
                      item.availability != 'AVAILABLE' ||
                      item.operations != 'OPEN',
                ));
      })
      .toList(growable: false);
  Future<void> load({int? nextPage, int? size}) async {
    if (nextPage != null || size != null) {
      filter = 'All plans';
      search = '';
      reviewOnly = false;
    }
    page = size != null ? 1 : nextPage ?? page;
    pageSize = size ?? pageSize;
    final generation = ++_generation;
    loading = true;
    error = null;
    notifyListeners();
    try {
      final value = await api.itineraries(
        page: page,
        pageSize: pageSize,
        showLoadingScreen: false,
      );
      if (_disposed || generation != _generation) return;
      trips = List.unmodifiable(value);
    } on Object catch (cause) {
      if (_disposed || generation != _generation) return;
      error = cause is PlannerApiException
          ? cause.message
          : 'We couldn’t load your saved trips. Please try again.';
    } finally {
      if (!_disposed && generation == _generation) {
        loading = false;
        notifyListeners();
      }
    }
  }

  void refine({String? status, String? query, bool? needsReview}) {
    filter = status ?? filter;
    search = query ?? search;
    reviewOnly = needsReview ?? reviewOnly;
    notifyListeners();
  }

  void reset() {
    filter = 'All plans';
    search = '';
    reviewOnly = false;
    notifyListeners();
  }

  @override
  void dispose() {
    _disposed = true;
    _generation++;
    super.dispose();
  }
}
