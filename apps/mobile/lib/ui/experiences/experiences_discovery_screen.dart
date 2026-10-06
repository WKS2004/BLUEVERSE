import 'dart:async';

import 'package:flutter/material.dart';

import '../auth_view_model.dart';
import '../blueverse_theme.dart';
import 'experience_coastal_map.dart';
import 'experience_view_model.dart';

const _mapRegionViews = <String, ExperienceMapViewport>{
  'ALL': ExperienceMapViewport(latitude: 7.8731, longitude: 80.7718, zoom: 6.5),
  'SOUTH': ExperienceMapViewport(latitude: 5.98, longitude: 80.62, zoom: 8.5),
  'EAST': ExperienceMapViewport(latitude: 7.35, longitude: 81.55, zoom: 8),
  'WEST': ExperienceMapViewport(latitude: 7.15, longitude: 79.92, zoom: 8),
  'NORTH': ExperienceMapViewport(latitude: 9.05, longitude: 80.05, zoom: 8),
};

const _mapRegionLabels = <String, String>{
  'ALL': 'All Coastlines',
  'SOUTH': 'South Coast',
  'EAST': 'East Coast',
  'WEST': 'West Coast',
  'NORTH': 'North Coast',
};

class ExperiencesDiscoveryScreen extends StatefulWidget {
  const ExperiencesDiscoveryScreen({
    super.key,
    required this.viewModel,
    this.authViewModel,
    this.initialTab = 0,
  });

  final ExperienceViewModel viewModel;
  final AuthViewModel? authViewModel;
  final int initialTab;

  @override
  State<ExperiencesDiscoveryScreen> createState() =>
      _ExperiencesDiscoveryScreenState();
}

class _ExperiencesDiscoveryScreenState extends State<ExperiencesDiscoveryScreen>
    with SingleTickerProviderStateMixin {
  late TabController _tabController;
  final TextEditingController _searchController = TextEditingController();
  final TextEditingController _placeSearchController = TextEditingController();
  Timer? _searchDebounce;
  late int _visibleTabIndex;
  String _activeMapRegion = 'ALL';

  final List<String> _regions = const [
    'ALL',
    'Southern Province',
    'Eastern Province',
    'North Western Province',
    'Western Province',
    'Northern Province',
  ];

  @override
  void initState() {
    super.initState();
    _tabController = TabController(
      length: 2,
      vsync: this,
      initialIndex: widget.initialTab,
    );
    _visibleTabIndex = widget.initialTab;
    _tabController.addListener(_handleTabChanged);
    WidgetsBinding.instance.addPostFrameCallback((_) {
      widget.viewModel.loadDiscoveryData(
        loadFavs: widget.authViewModel?.user != null,
      );
      if (_visibleTabIndex == 1) {
        unawaited(widget.viewModel.loadMapConfig());
      }
    });
  }

  void _handleTabChanged() {
    final selectedIndex = _tabController.index;
    if (selectedIndex == _visibleTabIndex) return;
    _visibleTabIndex = selectedIndex;
    if (selectedIndex == 1) {
      unawaited(widget.viewModel.loadMapConfig());
    }
    if (mounted) setState(() {});
  }

  Future<void> _refreshNearby() async {
    final vm = widget.viewModel;
    final focus = vm.mapFocusLocation;
    final label = vm.selectedMapLocationName;
    if (focus == null || label == null) return;
    await vm.fetchNearby(
      latitude: focus.latitude,
      longitude: focus.longitude,
      centerLabel: label,
    );
  }

  @override
  void dispose() {
    _tabController.removeListener(_handleTabChanged);
    _tabController.dispose();
    _searchDebounce?.cancel();
    _searchController.dispose();
    _placeSearchController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final listenable = widget.authViewModel == null
        ? widget.viewModel
        : Listenable.merge([widget.viewModel, widget.authViewModel!]);
    final canManageCatalogue =
        widget.authViewModel?.user?.permissions.any(
          (permission) =>
              permission == 'experiences.catalogue.manage' ||
              permission == 'auth.role.system.manage',
        ) ??
        false;
    return ListenableBuilder(
      listenable: listenable,
      builder: (context, _) {
        return Scaffold(
          appBar: AppBar(
            title: const Text('Coastal Experiences'),
            actions: [
              IconButton(
                key: const Key('btn-favourites-nav'),
                tooltip: 'Saved Wishlist',
                icon: const Icon(Icons.bookmark_outline),
                onPressed: () {
                  Navigator.pushNamed(context, '/experiences/favourites');
                },
              ),
              if (canManageCatalogue)
                IconButton(
                  key: const Key('btn-manage-catalogue-nav'),
                  tooltip: 'Catalogue Management',
                  icon: const Icon(Icons.tune_outlined),
                  onPressed: () {
                    Navigator.pushNamed(context, '/experiences/manage');
                  },
                ),
            ],
            bottom: TabBar(
              controller: _tabController,
              indicatorColor: BlueversePalette.coastDeep,
              tabs: const [
                Tab(icon: Icon(Icons.grid_view_outlined), text: 'Catalog'),
                Tab(icon: Icon(Icons.map_outlined), text: 'Coastal Map'),
              ],
            ),
          ),
          body: TabBarView(
            controller: _tabController,
            children: [_buildCatalogTab(context), _buildMapTab(context)],
          ),
        );
      },
    );
  }

  Widget _buildCatalogTab(BuildContext context) {
    final vm = widget.viewModel;

    if (vm.isLoading &&
        vm.destinations.isEmpty &&
        vm.activities.isEmpty &&
        vm.offerings.isEmpty) {
      return const Center(child: CircularProgressIndicator());
    }

    return RefreshIndicator(
      onRefresh: () =>
          vm.loadDiscoveryData(loadFavs: widget.authViewModel?.user != null),
      child: SingleChildScrollView(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            // Search field
            TextField(
              key: const Key('input-experience-search'),
              controller: _searchController,
              decoration: InputDecoration(
                hintText: 'Search destinations, wildlife, experiences...',
                prefixIcon: const Icon(Icons.search),
                suffixIcon: _searchController.text.isNotEmpty
                    ? IconButton(
                        icon: const Icon(Icons.clear),
                        onPressed: () {
                          _searchController.clear();
                          vm.updateFilters(query: '');
                          _searchDebounce?.cancel();
                          vm.loadDiscoveryData(
                            loadFavs: widget.authViewModel?.user != null,
                          );
                        },
                      )
                    : null,
                border: OutlineInputBorder(
                  borderRadius: BorderRadius.circular(16),
                ),
              ),
              onChanged: (value) {
                _searchDebounce?.cancel();
                _searchDebounce = Timer(const Duration(milliseconds: 350), () {
                  if (!mounted) return;
                  vm.updateFilters(query: value);
                  vm.loadDiscoveryData(
                    loadFavs: widget.authViewModel?.user != null,
                  );
                });
              },
              onSubmitted: (value) {
                _searchDebounce?.cancel();
                vm.updateFilters(query: value);
                vm.loadDiscoveryData(
                  loadFavs: widget.authViewModel?.user != null,
                );
              },
            ),
            const SizedBox(height: 12),

            // Region filter chips
            SingleChildScrollView(
              scrollDirection: Axis.horizontal,
              child: Row(
                children: _regions.map((region) {
                  final isSelected =
                      (region == 'ALL' && vm.selectedRegion.isEmpty) ||
                      vm.selectedRegion == region;
                  return Padding(
                    padding: const EdgeInsets.only(right: 8),
                    child: FilterChip(
                      label: Text(region == 'ALL' ? 'All Regions' : region),
                      selected: isSelected,
                      onSelected: (selected) {
                        vm.updateFilters(region: region == 'ALL' ? '' : region);
                        vm.loadDiscoveryData(
                          loadFavs: widget.authViewModel?.user != null,
                        );
                      },
                    ),
                  );
                }).toList(),
              ),
            ),
            const SizedBox(height: 8),

            // Category filter chips
            SingleChildScrollView(
              scrollDirection: Axis.horizontal,
              child: Row(
                children: ['ALL', ...vm.availableCategories].map((cat) {
                  final isSelected =
                      (cat == 'ALL' && vm.selectedCategory.isEmpty) ||
                      vm.selectedCategory == cat;
                  return Padding(
                    padding: const EdgeInsets.only(right: 8),
                    child: ChoiceChip(
                      label: Text(cat == 'ALL' ? 'All Categories' : cat),
                      selected: isSelected,
                      onSelected: (selected) {
                        vm.updateFilters(category: cat == 'ALL' ? '' : cat);
                      },
                    ),
                  );
                }).toList(),
              ),
            ),
            const SizedBox(height: 20),

            if (vm.errorMessage != null)
              _messageCard(vm.errorMessage!, isError: true),
            if (vm.catalogueNotice != null)
              _messageCard(vm.catalogueNotice!, isError: false),
            if (vm.favouriteActionErrorMessage != null)
              _messageCard(vm.favouriteActionErrorMessage!, isError: true),
            if (vm.favouritesErrorMessage != null)
              _messageCard(
                'Your saved wishlist could not be loaded. ${vm.favouritesErrorMessage}',
                isError: true,
              ),
            if (vm.successMessage != null)
              _messageCard(vm.successMessage!, isError: false),

            // Section: Destinations
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Text(
                  'Coastal Destinations (${vm.destinations.length})',
                  style: Theme.of(context).textTheme.titleMedium
                      ?.copyWith(fontWeight: FontWeight.bold),
                ),
              ],
            ),
            const SizedBox(height: 8),

            if (vm.destinations.isEmpty && vm.destinationsErrorMessage != null)
              const Padding(
                padding: EdgeInsets.symmetric(vertical: 24),
                child: Center(
                  child: Text(
                    'Destinations could not be loaded. Pull down to try again.',
                    textAlign: TextAlign.center,
                  ),
                ),
              )
            else if (vm.destinations.isEmpty)
              const Padding(
                padding: EdgeInsets.symmetric(vertical: 24),
                child: Center(child: Text('No coastal destinations found.')),
              )
            else
              ListView.builder(
                shrinkWrap: true,
                physics: const NeverScrollableScrollPhysics(),
                itemCount: vm.destinations.length,
                itemBuilder: (context, index) {
                  final dest = vm.destinations[index];
                  final isFav = vm.isFavourite('DESTINATION', dest.id);

                  return Card(
                    key: Key('card-destination-${dest.id}'),
                    margin: const EdgeInsets.only(bottom: 12),
                    shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(16),
                    ),
                    elevation: 1,
                    child: InkWell(
                      borderRadius: BorderRadius.circular(16),
                      onTap: () {
                        Navigator.pushNamed(
                          context,
                          '/experiences/destinations/${dest.id}',
                        );
                      },
                      child: Padding(
                        padding: const EdgeInsets.all(16),
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Row(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                CircleAvatar(
                                  backgroundColor: BlueversePalette.coastDeep
                                      .withAlpha(25),
                                  child: const Icon(
                                    Icons.place,
                                    color: BlueversePalette.coastDeep,
                                  ),
                                ),
                                const SizedBox(width: 12),
                                Expanded(
                                  child: Column(
                                    crossAxisAlignment:
                                        CrossAxisAlignment.start,
                                    children: [
                                      Text(
                                        dest.name,
                                        style: const TextStyle(
                                          fontWeight: FontWeight.bold,
                                          fontSize: 16,
                                        ),
                                      ),
                                      const SizedBox(height: 4),
                                      Text(
                                        dest.region ?? 'Coastal Region',
                                        style: TextStyle(
                                          color: Colors.grey.shade700,
                                          fontSize: 13,
                                        ),
                                      ),
                                    ],
                                  ),
                                ),
                                IconButton(
                                  key: Key('btn-fav-destination-${dest.id}'),
                                  icon: Icon(
                                    isFav
                                        ? Icons.bookmark
                                        : Icons.bookmark_border,
                                    color: isFav
                                        ? BlueversePalette.coastDeep
                                        : Colors.grey,
                                  ),
                                  onPressed: () {
                                    _toggleFavourite(
                                      context,
                                      'DESTINATION',
                                      dest.id,
                                    );
                                  },
                                ),
                              ],
                            ),
                            if (dest.description != null &&
                                dest.description!.isNotEmpty) ...[
                              const SizedBox(height: 8),
                              Text(
                                dest.description!,
                                maxLines: 2,
                                overflow: TextOverflow.ellipsis,
                                style: const TextStyle(
                                  fontSize: 13,
                                  height: 1.4,
                                ),
                              ),
                            ],
                            const SizedBox(height: 12),
                            Wrap(
                              spacing: 8,
                              children: [
                                Chip(
                                  visualDensity: VisualDensity.compact,
                                  label: Text(
                                    dest.status,
                                    style: const TextStyle(fontSize: 11),
                                  ),
                                  backgroundColor: dest.status == 'PUBLISHED'
                                      ? Colors.teal.shade50
                                      : Colors.grey.shade200,
                                ),
                                Chip(
                                  visualDensity: VisualDensity.compact,
                                  label: Text(
                                    '${dest.latitude.toStringAsFixed(3)}, ${dest.longitude.toStringAsFixed(3)}',
                                    style: const TextStyle(fontSize: 11),
                                  ),
                                ),
                              ],
                            ),
                          ],
                        ),
                      ),
                    ),
                  );
                },
              ),

            const SizedBox(height: 16),

            Text(
              'Explore by Activity (${vm.visibleActivities.length})',
              style: Theme.of(context).textTheme.titleMedium
                  ?.copyWith(fontWeight: FontWeight.bold),
            ),
            const SizedBox(height: 8),
            if (vm.visibleActivities.isEmpty &&
                vm.activitiesErrorMessage != null)
              const Padding(
                padding: EdgeInsets.symmetric(vertical: 12),
                child: Text(
                  'Some activity data could not be loaded. Pull down to try again.',
                ),
              )
            else if (vm.visibleActivities.isEmpty)
              const Padding(
                padding: EdgeInsets.symmetric(vertical: 12),
                child: Text('No activities match these filters.'),
              )
            else
              SizedBox(
                height: 122,
                child: ListView.separated(
                  scrollDirection: Axis.horizontal,
                  itemCount: vm.visibleActivities.length,
                  separatorBuilder: (_, _) => const SizedBox(width: 8),
                  itemBuilder: (context, index) {
                    final activity = vm.visibleActivities[index];
                    final isSelected = vm.selectedActivityId == activity.id;
                    final isFav = vm.isFavourite('ACTIVITY', activity.id);
                    return SizedBox(
                      width: 230,
                      child: Card(
                        key: Key('card-activity-${activity.id}'),
                        margin: EdgeInsets.zero,
                        child: Padding(
                          padding: const EdgeInsets.all(12),
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text(
                                activity.name,
                                maxLines: 1,
                                overflow: TextOverflow.ellipsis,
                                style: const TextStyle(
                                  fontWeight: FontWeight.bold,
                                ),
                              ),
                              const SizedBox(height: 4),
                              Expanded(
                                child: Text(
                                  activity.category ?? 'Coastal activity',
                                  maxLines: 1,
                                  overflow: TextOverflow.ellipsis,
                                  style: TextStyle(color: Colors.grey.shade700),
                                ),
                              ),
                              Row(
                                children: [
                                  Expanded(
                                    child: TextButton(
                                      onPressed: () => vm.updateFilters(
                                        activityId: isSelected
                                            ? ''
                                            : activity.id,
                                      ),
                                      child: Text(
                                        isSelected
                                            ? 'Showing offers'
                                            : 'Find offers',
                                      ),
                                    ),
                                  ),
                                  IconButton(
                                    tooltip: isFav
                                        ? 'Remove saved activity'
                                        : 'Save activity',
                                    icon: Icon(
                                      isFav
                                          ? Icons.bookmark
                                          : Icons.bookmark_border,
                                    ),
                                    onPressed: () => _toggleFavourite(
                                      context,
                                      'ACTIVITY',
                                      activity.id,
                                    ),
                                  ),
                                ],
                              ),
                            ],
                          ),
                        ),
                      ),
                    );
                  },
                ),
              ),

            const SizedBox(height: 16),

            // Section: Offerings
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Text(
                  'Bookable Offerings (${vm.visibleOfferings.length})',
                  style: Theme.of(context).textTheme.titleMedium
                      ?.copyWith(fontWeight: FontWeight.bold),
                ),
              ],
            ),
            const SizedBox(height: 8),

            if (vm.visibleOfferings.isEmpty && vm.offeringsErrorMessage != null)
              const Padding(
                padding: EdgeInsets.symmetric(vertical: 16),
                child: Center(
                  child: Text(
                    'Bookable experiences could not be fully loaded. Pull down to try again.',
                    textAlign: TextAlign.center,
                  ),
                ),
              )
            else if (vm.visibleOfferings.isEmpty)
              const Padding(
                padding: EdgeInsets.symmetric(vertical: 16),
                child: Center(
                  child: Text('No bookable experiences match these filters.'),
                ),
              )
            else
              ListView.builder(
                shrinkWrap: true,
                physics: const NeverScrollableScrollPhysics(),
                itemCount: vm.visibleOfferings.length,
                itemBuilder: (context, index) {
                  final off = vm.visibleOfferings[index];
                  final isFav = vm.isFavourite('OFFERING', off.id);

                  return Card(
                    key: Key('card-offering-${off.id}'),
                    margin: const EdgeInsets.only(bottom: 12),
                    shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(16),
                    ),
                    child: InkWell(
                      borderRadius: BorderRadius.circular(16),
                      onTap: () {
                        Navigator.pushNamed(
                          context,
                          '/experiences/offerings/${off.id}',
                        );
                      },
                      child: Padding(
                        padding: const EdgeInsets.all(16),
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Row(
                              children: [
                                Expanded(
                                  child: Column(
                                    crossAxisAlignment:
                                        CrossAxisAlignment.start,
                                    children: [
                                      Text(
                                        off.title,
                                        style: const TextStyle(
                                          fontWeight: FontWeight.bold,
                                          fontSize: 15,
                                        ),
                                      ),
                                      if (off.activityName.isNotEmpty)
                                        Text(
                                          off.activityName,
                                          style: TextStyle(
                                            color: BlueversePalette.coastDeep,
                                            fontSize: 12,
                                          ),
                                        ),
                                    ],
                                  ),
                                ),
                                IconButton(
                                  key: Key('btn-fav-offering-${off.id}'),
                                  icon: Icon(
                                    isFav
                                        ? Icons.bookmark
                                        : Icons.bookmark_border,
                                    color: isFav
                                        ? BlueversePalette.coastDeep
                                        : Colors.grey,
                                  ),
                                  onPressed: () {
                                    _toggleFavourite(
                                      context,
                                      'OFFERING',
                                      off.id,
                                    );
                                  },
                                ),
                              ],
                            ),
                            const SizedBox(height: 8),
                            Row(
                              mainAxisAlignment: MainAxisAlignment.spaceBetween,
                              children: [
                                Text(
                                  off.price != null
                                      ? 'LKR ${off.price!.toStringAsFixed(0)} / person'
                                      : 'Price on request',
                                  style: const TextStyle(
                                    fontWeight: FontWeight.bold,
                                    fontSize: 14,
                                    color: BlueversePalette.coastDeep,
                                  ),
                                ),
                                Text(
                                  off.maxCapacity != null
                                      ? 'Max ${off.maxCapacity} guests'
                                      : 'Group rates apply',
                                  style: TextStyle(
                                    color: Colors.grey.shade600,
                                    fontSize: 12,
                                  ),
                                ),
                              ],
                            ),
                          ],
                        ),
                      ),
                    ),
                  );
                },
              ),
          ],
        ),
      ),
    );
  }

  Widget _buildMapTab(BuildContext context) {
    final vm = widget.viewModel;
    final mapHeight = (MediaQuery.sizeOf(context).height * 0.52)
        .clamp(380.0, 560.0)
        .toDouble();

    return SingleChildScrollView(
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          const Text(
            'Explore the coast',
            style: TextStyle(fontSize: 20, fontWeight: FontWeight.bold),
          ),
          const SizedBox(height: 4),
          Text(
            'Search for a coastal place, choose a coastline, or select a map marker.',
            style: TextStyle(color: Colors.grey.shade700, fontSize: 13),
          ),
          const SizedBox(height: 14),
          // Proximity Place Search Bar
          TextField(
            key: const Key('input-map-place-search'),
            controller: _placeSearchController,
            textInputAction: TextInputAction.search,
            decoration: InputDecoration(
              hintText: 'Search a coastal place or region',
              prefixIcon: const Icon(Icons.location_searching),
              suffixIcon: vm.isSearchingPlaces
                  ? const SizedBox(
                      width: 20,
                      height: 20,
                      child: Padding(
                        padding: EdgeInsets.all(10),
                        child: CircularProgressIndicator(strokeWidth: 2),
                      ),
                    )
                  : IconButton(
                      icon: const Icon(Icons.search),
                      onPressed: () {
                        vm.searchPlaces(_placeSearchController.text);
                      },
                    ),
              border: OutlineInputBorder(
                borderRadius: BorderRadius.circular(16),
              ),
            ),
            onSubmitted: (value) => vm.searchPlaces(value),
          ),
          const SizedBox(height: 12),

          if (vm.destinations.isNotEmpty) ...[
            const Text(
              'Coastal destinations',
              style: TextStyle(fontWeight: FontWeight.bold),
            ),
            const SizedBox(height: 6),
            SingleChildScrollView(
              scrollDirection: Axis.horizontal,
              child: Row(
                children: vm.destinations
                    .map((destination) {
                      return Padding(
                        padding: const EdgeInsets.only(right: 8),
                        child: ChoiceChip(
                          key: Key('chip-map-destination-${destination.id}'),
                          label: Text(destination.name),
                          selected:
                              vm.selectedMapDestinationId == destination.id,
                          onSelected: (_) =>
                              vm.selectMapDestination(destination.id),
                        ),
                      );
                    })
                    .toList(growable: false),
              ),
            ),
            const SizedBox(height: 8),
          ],

          if (vm.placeSearchErrorMessage != null)
            _messageCard(vm.placeSearchErrorMessage!, isError: false),

          if (vm.placeSearchResults.isNotEmpty) ...[
            Text(
              'Search Matches (${vm.placeSearchResults.length})',
              style: const TextStyle(fontWeight: FontWeight.bold),
            ),
            const SizedBox(height: 8),
            Container(
              decoration: BoxDecoration(
                border: Border.all(color: Colors.grey.shade300),
                borderRadius: BorderRadius.circular(12),
              ),
              child: ListView.separated(
                shrinkWrap: true,
                physics: const NeverScrollableScrollPhysics(),
                itemCount: vm.placeSearchResults.length,
                separatorBuilder: (_, _) => const Divider(height: 1),
                itemBuilder: (context, index) {
                  final place = vm.placeSearchResults[index];
                  return ListTile(
                    dense: true,
                    leading: const Icon(
                      Icons.place,
                      color: BlueversePalette.coastDeep,
                    ),
                    title: Text(place.displayName),
                    subtitle: Text(
                      '${place.latitude.toStringAsFixed(4)}, ${place.longitude.toStringAsFixed(4)}',
                    ),
                    trailing: const Icon(Icons.arrow_forward_ios, size: 14),
                    onTap: () {
                      vm.selectMapPlace(place);
                      vm.fetchNearby(
                        latitude: place.latitude,
                        longitude: place.longitude,
                        centerLabel: place.displayName,
                      );
                    },
                  );
                },
              ),
            ),
            const SizedBox(height: 16),
          ],

          if (vm.selectedMapLocationName != null) ...[
            Card(
              key: const Key('card-map-selected-location'),
              child: Padding(
                padding: const EdgeInsets.all(16),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    const Text(
                      'Selected coastal location',
                      style: TextStyle(fontWeight: FontWeight.bold),
                    ),
                    const SizedBox(height: 6),
                    Text(vm.selectedMapLocationName!),
                    if (!vm.mapFocusIsCurrentLocation &&
                        vm.mapFocusLocation != null) ...[
                      const SizedBox(height: 4),
                      Text(
                        '${vm.mapFocusLocation!.latitude.toStringAsFixed(4)}, ${vm.mapFocusLocation!.longitude.toStringAsFixed(4)}',
                        style: TextStyle(
                          color: Colors.grey.shade700,
                          fontSize: 12,
                        ),
                      ),
                    ] else if (vm.mapFocusIsCurrentLocation) ...[
                      const SizedBox(height: 4),
                      Text(
                        'Approximate device location is used only to find nearby destinations.',
                        style: TextStyle(
                          color: Colors.grey.shade700,
                          fontSize: 12,
                        ),
                      ),
                    ],
                    if ((vm.selectedMapDestination?.description ?? '')
                        .trim()
                        .isNotEmpty) ...[
                      const SizedBox(height: 10),
                      Text(vm.selectedMapDestination!.description!),
                    ],
                    const SizedBox(height: 12),
                    if (vm.selectedMapDestination != null)
                      OutlinedButton.icon(
                        key: const Key('btn-map-open-destination'),
                        onPressed: () => Navigator.pushNamed(
                          context,
                          '/experiences/destinations/${vm.selectedMapDestination!.id}',
                        ),
                        icon: const Icon(Icons.open_in_new),
                        label: const Text('Inspect destination & ecology'),
                      ),
                    OutlinedButton.icon(
                      key: const Key('btn-map-explore-experiences'),
                      onPressed: () {
                        final selectedName = vm.selectedMapLocationName!;
                        _searchDebounce?.cancel();
                        _searchController.text = selectedName;
                        vm.updateFilters(query: selectedName);
                        unawaited(
                          vm.loadDiscoveryData(
                            loadFavs: widget.authViewModel?.user != null,
                          ),
                        );
                        _tabController.animateTo(0);
                      },
                      icon: const Icon(Icons.explore_outlined),
                      label: const Text('Explore experiences here'),
                    ),
                  ],
                ),
              ),
            ),
            const SizedBox(height: 12),
          ],

          Row(
            children: [
              const Expanded(
                child: Text(
                  'Explore by coastline',
                  style: TextStyle(fontWeight: FontWeight.bold),
                ),
              ),
              IconButton(
                tooltip: 'Refresh map configuration',
                onPressed: vm.isLoadingMapConfig
                    ? null
                    : () => vm.loadMapConfig(force: true),
                icon: vm.isLoadingMapConfig
                    ? const SizedBox(
                        width: 18,
                        height: 18,
                        child: CircularProgressIndicator(strokeWidth: 2),
                      )
                    : const Icon(Icons.refresh),
              ),
            ],
          ),
          SizedBox(
            height: 44,
            child: ListView(
              scrollDirection: Axis.horizontal,
              children: _mapRegionLabels.entries
                  .map((entry) {
                    final selected =
                        vm.selectedMapLocationName == null &&
                        _activeMapRegion == entry.key;
                    return Padding(
                      padding: const EdgeInsets.only(right: 8),
                      child: ChoiceChip(
                        label: Text(entry.value),
                        selected: selected,
                        onSelected: (_) {
                          vm.clearMapSelection();
                          setState(() => _activeMapRegion = entry.key);
                        },
                      ),
                    );
                  })
                  .toList(growable: false),
            ),
          ),
          if (vm.mapConfigErrorMessage != null && vm.mapConfig != null) ...[
            const SizedBox(height: 8),
            _messageCard(vm.mapConfigErrorMessage!, isError: true),
          ],
          const SizedBox(height: 8),

          Container(
            height: mapHeight,
            decoration: BoxDecoration(
              color: const Color(0xFFF3F7F5),
              borderRadius: BorderRadius.circular(20),
              border: Border.all(color: BlueversePalette.coastLine),
            ),
            clipBehavior: Clip.antiAlias,
            child: _visibleTabIndex != 1
                ? const Center(
                    child: Text('Open the Coastal Map tab to load the map.'),
                  )
                : vm.isLoadingMapConfig && vm.mapConfig == null
                ? const Center(child: CircularProgressIndicator())
                : vm.mapConfigErrorMessage != null && vm.mapConfig == null
                ? Center(
                    child: Padding(
                      padding: const EdgeInsets.all(16),
                      child: Column(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          Text(
                            vm.mapConfigErrorMessage!,
                            textAlign: TextAlign.center,
                          ),
                          TextButton.icon(
                            onPressed: () => vm.loadMapConfig(force: true),
                            icon: const Icon(Icons.refresh),
                            label: const Text('Retry map'),
                          ),
                        ],
                      ),
                    ),
                  )
                : vm.mapConfig == null
                ? const Center(child: CircularProgressIndicator())
                : ExperienceCoastalMap(
                    config: vm.mapConfig!,
                    viewport: _mapRegionViews[_activeMapRegion],
                    height: mapHeight,
                    destinations: vm.destinations,
                    focusLocation: vm.mapFocusLocation,
                    focusIsCurrentLocation: vm.mapFocusIsCurrentLocation,
                    isLocationLoading: vm.isGettingCurrentLocation,
                    onCurrentLocationRequested: () =>
                        vm.fetchNearbyFromCurrentLocation(),
                    onDestinationSelected: (destinationId) {
                      vm.selectMapDestination(destinationId);
                    },
                  ),
          ),
          const SizedBox(height: 8),
          Text(
            vm.mapConfig?.attribution ?? 'Map tiles provided by OpenFreeMap.',
            style: TextStyle(color: Colors.grey.shade700, fontSize: 12),
            textAlign: TextAlign.center,
          ),
          const SizedBox(height: 4),
          Text(
            'Pan and zoom to explore. Select a marker or catalogue destination to inspect it below.',
            style: TextStyle(color: Colors.grey.shade700, fontSize: 12),
            textAlign: TextAlign.center,
          ),
          const SizedBox(height: 16),

          Row(
            children: [
              const Expanded(
                child: Text(
                  'Nearby coastal destinations',
                  style: TextStyle(fontWeight: FontWeight.bold, fontSize: 16),
                ),
              ),
              if (vm.mapFocusLocation != null &&
                  vm.selectedMapLocationName != null)
                IconButton(
                  key: const Key('btn-refresh-map-nearby'),
                  tooltip: 'Refresh nearby destinations',
                  onPressed: vm.isLoadingNearby ? null : _refreshNearby,
                  icon: const Icon(Icons.refresh),
                ),
            ],
          ),
          Text(
            'Your approximate location is used only after you tap the map location button. Place search remains available if permission is declined.',
            style: TextStyle(color: Colors.grey.shade700, fontSize: 12),
          ),
          const SizedBox(height: 8),

          if (vm.nearbyErrorMessage != null)
            _messageCard(vm.nearbyErrorMessage!, isError: true),
          if (vm.isLoadingNearby && vm.nearbyExperiences == null)
            const Padding(
              padding: EdgeInsets.symmetric(vertical: 12),
              child: Row(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  SizedBox(
                    width: 18,
                    height: 18,
                    child: CircularProgressIndicator(strokeWidth: 2),
                  ),
                  SizedBox(width: 10),
                  Text('Finding nearby destinations…'),
                ],
              ),
            ),
          if (vm.isLoadingNearby && vm.nearbyExperiences != null)
            const LinearProgressIndicator(minHeight: 2),
          if (vm.nearbyExperiences != null) ...[
            Text(
              'Nearby destinations${vm.nearbyCenterLabel == null ? '' : ' near ${vm.nearbyCenterLabel}'}',
              style: TextStyle(color: Colors.grey.shade700, fontSize: 12),
            ),
            const SizedBox(height: 8),
            ...vm.nearbyExperiences!.results.map(
              (d) => Card(
                key: Key('card-nearby-destination-${d.destinationId}'),
                margin: const EdgeInsets.only(bottom: 8),
                shape: RoundedRectangleBorder(
                  borderRadius: BorderRadius.circular(12),
                ),
                child: ListTile(
                  leading: const Icon(
                    Icons.explore,
                    color: BlueversePalette.coastDeep,
                  ),
                  title: Text(d.name),
                  subtitle: Text(
                    '${d.region ?? "Coastal destination"} • ${(d.distanceMeters / 1000).toStringAsFixed(1)} km',
                  ),
                  trailing: const Icon(Icons.arrow_forward_ios, size: 14),
                  onTap: () {
                    Navigator.pushNamed(
                      context,
                      '/experiences/destinations/${d.destinationId}',
                    );
                  },
                ),
              ),
            ),
          ] else if (!vm.isLoadingNearby && vm.nearbyErrorMessage == null) ...[
            const Padding(
              padding: EdgeInsets.symmetric(vertical: 16),
              child: Text(
                'Search for a coastal place or use the map location button to find destinations within 50 km.',
                textAlign: TextAlign.center,
              ),
            ),
          ],
        ],
      ),
    );
  }

  Widget _messageCard(String message, {required bool isError}) {
    final color = isError ? Colors.red : BlueversePalette.coastDeep;
    return Semantics(
      liveRegion: true,
      child: Container(
        padding: const EdgeInsets.all(12),
        margin: const EdgeInsets.only(bottom: 12),
        decoration: BoxDecoration(
          color: color.withAlpha(18),
          borderRadius: BorderRadius.circular(12),
          border: Border.all(color: color.withAlpha(70)),
        ),
        child: Text(
          message,
          style: TextStyle(
            color: isError ? Colors.red.shade900 : BlueversePalette.coastDeep,
          ),
        ),
      ),
    );
  }

  void _toggleFavourite(BuildContext context, String type, String id) {
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
    widget.viewModel.toggleFavourite(type, id);
  }
}
