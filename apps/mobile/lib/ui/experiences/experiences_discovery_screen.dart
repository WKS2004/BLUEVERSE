import 'dart:async';

import 'package:flutter/material.dart';

import '../../data/models/experience_models.dart';
import '../auth_view_model.dart';
import 'experience_coastal_map.dart';
import 'experience_view_model.dart';
import 'experience_widgets.dart';

const _views = <String, ExperienceMapViewport>{
  'All Coastlines': ExperienceMapViewport(
    latitude: 7.8731,
    longitude: 80.7718,
    zoom: 6.5,
  ),
  'South Coast': ExperienceMapViewport(
    latitude: 5.98,
    longitude: 80.62,
    zoom: 8.5,
  ),
  'East Coast': ExperienceMapViewport(
    latitude: 7.35,
    longitude: 81.55,
    zoom: 8,
  ),
  'West Coast': ExperienceMapViewport(
    latitude: 7.15,
    longitude: 79.92,
    zoom: 8,
  ),
  'North Coast': ExperienceMapViewport(
    latitude: 9.05,
    longitude: 80.05,
    zoom: 8,
  ),
};

class ExperiencesDiscoveryScreen extends StatefulWidget {
  const ExperiencesDiscoveryScreen({
    super.key,
    required this.viewModel,
    this.authViewModel,
    this.initialTab,
  });
  final ExperienceViewModel viewModel;
  final AuthViewModel? authViewModel;
  final int? initialTab;
  @override
  State<ExperiencesDiscoveryScreen> createState() => _DiscoveryState();
}

class _DiscoveryState extends State<ExperiencesDiscoveryScreen>
    with SingleTickerProviderStateMixin {
  late final TabController _tabs;
  late final TextEditingController _search;
  final _place = TextEditingController();
  Timer? _debounce;
  String _coast = 'All Coastlines';
  bool _mapOpened = false;
  ExperienceViewModel get vm => widget.viewModel;
  @override
  void initState() {
    super.initState();
    _search = TextEditingController(text: vm.searchQuery);
    _tabs = TabController(
      length: 2,
      vsync: this,
      initialIndex: (widget.initialTab ?? vm.discoveryTab).clamp(0, 1),
    );
    _mapOpened = _tabs.index == 1;
    _tabs.addListener(_rememberTab);
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted) return;
      unawaited(_reload());
      if (_mapOpened) unawaited(vm.loadMapConfig());
    });
  }

  void _rememberTab() {
    vm.discoveryTab = _tabs.index;
    if (_tabs.index == 1 && !_mapOpened) {
      setState(() => _mapOpened = true);
      unawaited(vm.loadMapConfig());
    }
  }

  Future<void> _reload() =>
      vm.loadDiscoveryData(loadFavs: widget.authViewModel?.user != null);
  void _searchFor(String query) {
    _debounce?.cancel();
    vm.updateFilters(query: query);
    unawaited(_reload());
  }

  @override
  void dispose() {
    _tabs.removeListener(_rememberTab);
    _tabs.dispose();
    _search.dispose();
    _place.dispose();
    _debounce?.cancel();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => ListenableBuilder(
    listenable: Listenable.merge([
      vm,
      if (widget.authViewModel != null) widget.authViewModel!,
    ]),
    builder: (context, _) => Scaffold(
      appBar: AppBar(
        title: const Text('Coastal Experiences'),
        actions: [
          IconButton(
            key: const Key('btn-favourites-nav'),
            tooltip: 'Saved Wishlist',
            icon: const Icon(Icons.bookmark_outline),
            onPressed: () =>
                Navigator.pushNamed(context, '/experiences/favourites'),
          ),
          if (widget.authViewModel?.user?.permissions.contains(
                    'experiences.catalogue.manage',
                  ) ==
                  true ||
              widget.authViewModel?.user?.permissions.contains(
                    'auth.role.system.manage',
                  ) ==
                  true)
            IconButton(
              key: const Key('btn-manage-catalogue-nav'),
              tooltip: 'Manage Experiences',
              icon: const Icon(Icons.tune),
              onPressed: () =>
                  Navigator.pushNamed(context, '/experiences/manage'),
            ),
        ],
        bottom: TabBar(
          controller: _tabs,
          tabs: const [
            Tab(icon: Icon(Icons.explore_outlined), text: 'Explore'),
            Tab(icon: Icon(Icons.map_outlined), text: 'Coastal Map'),
          ],
        ),
      ),
      body: SafeArea(
        top: false,
        child: TabBarView(
          controller: _tabs,
          children: [
            ExperienceTabBody(child: _explore()),
            ExperienceTabBody(child: _map()),
          ],
        ),
      ),
    ),
  );

  Widget _explore() {
    if (vm.isLoading &&
        vm.destinations.isEmpty &&
        vm.activities.isEmpty &&
        vm.offerings.isEmpty) {
      return const ExperienceLoading();
    }
    return RefreshIndicator(
      onRefresh: _reload,
      child: ListView(
        key: const PageStorageKey('experience-explore-scroll'),
        physics: const AlwaysScrollableScrollPhysics(),
        padding: const EdgeInsets.all(16),
        children: [
          const ExperienceHero(
            title: 'Find your next coastal experience',
            detail: 'Discover Sri Lanka’s beaches, reefs and local experiences. Explore thoughtfully, with current context before you visit.',
          ),
          TextField(
            key: const Key('input-experience-search'),
            controller: _search,
            textInputAction: TextInputAction.search,
            decoration: InputDecoration(
              hintText: 'Search destinations, activities, experiences…',
              prefixIcon: const Icon(Icons.search),
              suffixIcon: IconButton(
                tooltip: 'Clear search',
                icon: const Icon(Icons.close),
                onPressed: () {
                  _search.clear();
                  _searchFor('');
                },
              ),
            ),
            onChanged: (value) {
              _debounce?.cancel();
              _debounce = Timer(const Duration(milliseconds: 350), () {
                if (mounted) _searchFor(value);
              });
            },
            onSubmitted: _searchFor,
          ),
          const SizedBox(height: 12),
          _chips(
            [
              'All regions',
              'Southern Province',
              'Eastern Province',
              'North Western Province',
              'Western Province',
              'Northern Province',
            ],
            (label) => FilterChip(
              label: Text(label),
              selected: label == 'All regions'
                  ? vm.selectedRegion.isEmpty
                  : vm.selectedRegion == label,
              onSelected: (_) {
                vm.updateFilters(region: label == 'All regions' ? '' : label);
                unawaited(_reload());
              },
            ),
          ),
          _chips(
            ['All activities', ...vm.availableCategories],
            (label) => ChoiceChip(
              label: Text(label),
              selected: label == 'All activities'
                  ? vm.selectedCategory.isEmpty
                  : vm.selectedCategory == label,
              onSelected: (_) => vm.updateFilters(
                category: label == 'All activities' ? '' : label,
              ),
            ),
          ),
          if (vm.isLoading) const ExperienceNotice('Updating your results…'),
          if (vm.errorMessage != null)
            ExperienceNotice(vm.errorMessage!, isError: true, onRetry: _reload),
          if (vm.catalogueNotice != null)
            ExperienceNotice(vm.catalogueNotice!, onRetry: _reload),
          if (vm.favouriteActionErrorMessage != null)
            ExperienceNotice(vm.favouriteActionErrorMessage!, isError: true),
          _heading('Coastal destinations', vm.destinations.length),
          if (vm.destinationsErrorMessage != null)
            ExperienceNotice(vm.destinationsErrorMessage!, onRetry: _reload),
          if (vm.destinations.isEmpty &&
              !vm.isLoading &&
              vm.destinationsErrorMessage == null)
            const ExperienceNotice(
              'No destinations match these filters. Try another place or region.',
            ),
          ...vm.destinations.map(
            (d) => _entry(
              key: Key('card-destination-${d.id}'),
              title: d.name,
              subtitle: d.region ?? 'Coastal destination',
              description: d.description,
              type: 'DESTINATION',
              id: d.id,
              icon: Icons.place_outlined,
              onTap: () => Navigator.pushNamed(
                context,
                '/experiences/destinations/${d.id}',
              ),
            ),
          ),
          _heading('Coastal activities', vm.visibleActivities.length),
          if (vm.activitiesErrorMessage != null)
            ExperienceNotice(vm.activitiesErrorMessage!, onRetry: _reload),
          if (vm.visibleActivities.isEmpty &&
              !vm.isLoading &&
              vm.activitiesErrorMessage == null)
            const ExperienceNotice('No activities match these filters.'),
          ...vm.visibleActivities.map(
            (a) => _entry(
              key: Key('card-activity-${a.id}'),
              title: a.name,
              subtitle: a.category ?? 'Coastal activity',
              description: a.description,
              type: 'ACTIVITY',
              id: a.id,
              icon: Icons.surfing,
              onTap: () => _activity(a),
            ),
          ),
          _heading('Experience offerings', vm.visibleOfferings.length),
          if (vm.offeringsErrorMessage != null)
            ExperienceNotice(vm.offeringsErrorMessage!, onRetry: _reload),
          if (vm.visibleOfferings.isEmpty &&
              !vm.isLoading &&
              vm.offeringsErrorMessage == null)
            const ExperienceNotice('No experiences match these filters.'),
          ...vm.visibleOfferings.map(
            (o) => _entry(
              key: Key('card-offering-${o.id}'),
              title: o.title,
              subtitle:
                  '${o.destinationName} · ${o.price == null ? 'Price on request' : '${o.currency ?? 'LKR'} ${o.price!.toStringAsFixed(0)} / person'}',
              description: o.description,
              type: 'OFFERING',
              id: o.id,
              icon: Icons.sailing_outlined,
              onTap: () => Navigator.pushNamed(
                context,
                '/experiences/offerings/${o.id}',
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _map() {
    final destinations = vm.mapDestinations.isEmpty
        ? vm.destinations
        : vm.mapDestinations;
    final height = (MediaQuery.sizeOf(context).height * .54).clamp(
      340.0,
      540.0,
    );
    return SingleChildScrollView(
      key: const PageStorageKey('experience-map-scroll'),
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text(
            'Explore the coast',
            style: Theme.of(context).textTheme.headlineSmall,
          ),
          const SizedBox(height: 8),
          const Text(
            'Search a place, choose a coastline, or select a destination marker.',
          ),
          const SizedBox(height: 16),
          TextField(
            key: const Key('input-map-place-search'),
            controller: _place,
            textInputAction: TextInputAction.search,
            decoration: InputDecoration(
              hintText: 'Search a coastal place or region',
              prefixIcon: const Icon(Icons.search),
              suffixIcon: IconButton(
                tooltip: 'Search places',
                icon: const Icon(Icons.arrow_forward),
                onPressed: vm.isSearchingPlaces
                    ? null
                    : () => vm.searchPlaces(_place.text),
              ),
            ),
            onSubmitted: (value) => vm.searchPlaces(value),
          ),
          if (vm.isSearchingPlaces)
            const ExperienceNotice('Searching coastal places…'),
          if (vm.placeSearchErrorMessage != null)
            ExperienceNotice(
              vm.placeSearchErrorMessage!,
              onRetry: () => vm.searchPlaces(_place.text),
            ),
          ...vm.placeSearchResults.map(
            (place) => ListTile(
              title: Text(place.displayName),
              leading: const Icon(Icons.place_outlined),
              trailing: const Icon(Icons.chevron_right),
              onTap: () {
                vm.selectMapPlace(place);
                unawaited(
                  vm.fetchNearby(
                    latitude: place.latitude,
                    longitude: place.longitude,
                    centerLabel: place.displayName,
                  ),
                );
              },
            ),
          ),
          const SizedBox(height: 12),
          _chips(
            _views.keys.toList(),
            (label) => ChoiceChip(
              label: Text(label),
              selected: vm.mapFocusLocation == null && _coast == label,
              onSelected: (_) {
                _place.clear();
                vm.clearMapSelection();
                setState(() => _coast = label);
              },
            ),
          ),
          const SizedBox(height: 8),
          ClipRRect(
            borderRadius: BorderRadius.circular(20),
            child: SizedBox(
              height: height,
              child: !_mapOpened
                  ? const SizedBox.shrink()
                  : vm.mapConfig == null && vm.mapConfigErrorMessage != null
                  ? ExperienceNotice(
                      vm.mapConfigErrorMessage!,
                      isError: true,
                      onRetry: () => vm.loadMapConfig(force: true),
                    )
                  : vm.mapConfig == null
                  ? const ExperienceLoading()
                  : ExperienceCoastalMap(
                      config: vm.mapConfig!,
                      height: height,
                      destinations: destinations,
                      viewport: _views[_coast],
                      focusLocation: vm.mapFocusLocation,
                      focusIsCurrentLocation: vm.mapFocusIsCurrentLocation,
                      isLocationLoading: vm.isGettingCurrentLocation,
                      onCurrentLocationRequested:
                          vm.fetchNearbyFromCurrentLocation,
                      onDestinationSelected: vm.selectMapDestination,
                    ),
            ),
          ),
          const SizedBox(height: 8),
          Text(
            vm.mapConfig?.attribution ?? 'Map tiles provided by OpenFreeMap.',
            textAlign: TextAlign.center,
            style: Theme.of(context).textTheme.bodySmall,
          ),
          const Text(
            'Pan and zoom to explore. For coastal discovery, not navigation.',
            textAlign: TextAlign.center,
          ),
          if (vm.mapConfigErrorMessage != null && vm.mapConfig != null)
            ExperienceNotice(
              vm.mapConfigErrorMessage!,
              onRetry: () => vm.loadMapConfig(force: true),
            ),
          const SizedBox(height: 16),
          Text(
            'Coastal destinations',
            style: Theme.of(context).textTheme.titleMedium,
          ),
          _chips(destinations.map((d) => d.id).toList(), (id) {
            final d = destinations.firstWhere((d) => d.id == id);
            return ChoiceChip(
              key: Key('chip-map-destination-$id'),
              label: Text(d.name),
              selected: vm.selectedMapDestinationId == id,
              onSelected: (_) => vm.selectMapDestination(id),
            );
          }),
          if (vm.selectedMapLocationName != null)
            Card(
              key: const Key('card-map-selected-location'),
              child: Padding(
                padding: const EdgeInsets.all(20),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    Text(
                      vm.selectedMapLocationName!,
                      style: Theme.of(context).textTheme.titleLarge,
                    ),
                    if (vm.selectedMapDestination?.description != null) ...[
                      const SizedBox(height: 8),
                      Text(vm.selectedMapDestination!.description!),
                    ],
                    const SizedBox(height: 12),
                    if (vm.selectedMapDestination != null) ...[
                      OutlinedButton.icon(
                        key: const Key('btn-map-open-destination'),
                        icon: const Icon(Icons.eco_outlined),
                        label: const Text('Inspect destination & ecology'),
                        onPressed: () => Navigator.pushNamed(
                          context,
                          '/experiences/destinations/${vm.selectedMapDestination!.id}',
                        ),
                      ),
                      OutlinedButton.icon(
                        key: const Key('btn-map-explore-experiences'),
                        icon: const Icon(Icons.explore_outlined),
                        label: const Text('Explore experiences here'),
                        onPressed: () {
                          vm.updateFilters(
                            region: '',
                            category: '',
                            activityId: '',
                          );
                          _search.text = vm.selectedMapDestination!.name;
                          _searchFor(_search.text);
                          _tabs.animateTo(0);
                        },
                      ),
                    ] else
                      const Text(
                        'This location is used to find nearby destinations. Select a result to explore its experiences.',
                      ),
                  ],
                ),
              ),
            ),
          Row(
            children: [
              const Expanded(
                child: Text(
                  'Nearby coastal destinations',
                  style: TextStyle(fontWeight: FontWeight.bold),
                ),
              ),
              IconButton(
                key: const Key('btn-refresh-map-nearby'),
                tooltip: 'Refresh nearby destinations',
                onPressed: vm.mapFocusLocation == null || vm.isLoadingNearby
                    ? null
                    : () => vm.fetchNearby(
                        latitude: vm.mapFocusLocation!.latitude,
                        longitude: vm.mapFocusLocation!.longitude,
                        centerLabel: vm.selectedMapLocationName,
                        isCurrentLocation: vm.mapFocusIsCurrentLocation,
                      ),
                icon: const Icon(Icons.refresh),
              ),
            ],
          ),
          const Text(
            'Precise location is requested only when you tap Near Me or the map location button. You control access in device settings; location is not saved.',
          ),
          Align(
            alignment: Alignment.centerLeft,
            child: TextButton.icon(
              onPressed: vm.isLoadingNearby
                  ? null
                  : vm.fetchNearbyFromCurrentLocation,
              icon: const Icon(Icons.my_location),
              label: const Text('Near Me'),
            ),
          ),
          if (vm.isLoadingNearby)
            ExperienceNotice(
              vm.isGettingCurrentLocation
                  ? 'Finding your current location…'
                  : 'Finding nearby destinations…',
            ),
          if (vm.nearbyErrorMessage != null)
            ExperienceNotice(vm.nearbyErrorMessage!),
          if (vm.nearbyExperiences == null &&
              !vm.isLoadingNearby &&
              vm.nearbyErrorMessage == null)
            const ExperienceNotice(
              'Choose a destination, search a place, or use Near Me to discover nearby coastal places.',
            ),
          ...?vm.nearbyExperiences?.results.map(
            (d) => Card(
              key: Key('card-nearby-destination-${d.destinationId}'),
              child: ListTile(
                title: Text(d.name),
                subtitle: Text(
                  '${d.region ?? 'Coastal destination'} · ${(d.distanceMeters / 1000).toStringAsFixed(1)} km',
                ),
                trailing: const Icon(Icons.chevron_right),
                onTap: () => Navigator.pushNamed(
                  context,
                  '/experiences/destinations/${d.destinationId}',
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _heading(String label, int count) => Padding(
    padding: const EdgeInsets.only(top: 20, bottom: 10),
    child: Text(
      '$label ($count)',
      style: Theme.of(context).textTheme.titleLarge,
    ),
  );
  Widget _chips(List<String> labels, Widget Function(String) build) =>
      SingleChildScrollView(
        scrollDirection: Axis.horizontal,
        child: Row(
          children: labels
              .map(
                (label) => Padding(
                  padding: const EdgeInsets.only(right: 8),
                  child: build(label),
                ),
              )
              .toList(),
        ),
      );
  Widget _entry({
    required Key key,
    required String title,
    required String subtitle,
    String? description,
    required String type,
    required String id,
    required IconData icon,
    required VoidCallback onTap,
  }) => Card(
    key: key,
    margin: const EdgeInsets.only(bottom: 12),
    child: Padding(
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Icon(icon),
              const SizedBox(width: 12),
              Expanded(
                child: Text(
                  title,
                  style: Theme.of(context).textTheme.titleMedium,
                ),
              ),
              IconButton(
                key: Key('btn-fav-${type.toLowerCase()}-$id'),
                tooltip: 'Save to Wishlist',
                onPressed: vm.isFavouriteActionInProgress(type, id)
                    ? null
                    : () => _save(type, id),
                icon: Icon(
                  vm.isFavourite(type, id)
                      ? Icons.bookmark
                      : Icons.bookmark_outline,
                ),
              ),
            ],
          ),
          Text(subtitle),
          if (description?.isNotEmpty == true) ...[
            const SizedBox(height: 8),
            Text(description!),
          ],
          const SizedBox(height: 8),
          TextButton.icon(
            onPressed: onTap,
            icon: const Icon(Icons.arrow_forward),
            label: const Text('Explore'),
          ),
        ],
      ),
    ),
  );
  void _save(String type, String id) {
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
    } else {
      unawaited(vm.toggleFavourite(type, id));
    }
  }

  void _activity(ActivityDto activity) {
    showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      builder: (context) => SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Text(
                activity.name,
                style: Theme.of(context).textTheme.headlineSmall,
              ),
              const SizedBox(height: 12),
              Text(
                activity.description ??
                    'Explore the available experiences for this activity.',
              ),
              const SizedBox(height: 16),
              FilledButton(
                onPressed: () {
                  vm.updateFilters(
                    query: '',
                    category: '',
                    activityId: activity.id,
                  );
                  _search.clear();
                  Navigator.pop(context);
                  unawaited(_reload());
                },
                child: const Text('Show experiences for this activity'),
              ),
              TextButton(
                onPressed: () => Navigator.pop(context),
                child: const Text('Close'),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
