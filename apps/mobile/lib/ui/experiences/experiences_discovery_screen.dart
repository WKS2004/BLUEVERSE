import 'package:flutter/material.dart';

import '../../data/models/experience_models.dart';
import '../blueverse_theme.dart';
import 'experience_view_model.dart';

class ExperiencesDiscoveryScreen extends StatefulWidget {
  const ExperiencesDiscoveryScreen({
    super.key,
    required this.viewModel,
    this.initialTab = 0,
  });

  final ExperienceViewModel viewModel;
  final int initialTab;

  @override
  State<ExperiencesDiscoveryScreen> createState() => _ExperiencesDiscoveryScreenState();
}

class _ExperiencesDiscoveryScreenState extends State<ExperiencesDiscoveryScreen>
    with SingleTickerProviderStateMixin {
  late TabController _tabController;
  final TextEditingController _searchController = TextEditingController();
  final TextEditingController _placeSearchController = TextEditingController();

  final List<String> _regions = const [
    'ALL',
    'Southern Province',
    'Eastern Province',
    'North Western Province',
    'Western Province',
    'Northern Province',
  ];

  final List<String> _categories = const [
    'ALL',
    'Marine Life',
    'Snorkeling & Diving',
    'Coastal Walks',
    'Water Sports',
    'Culture & Heritage',
  ];

  @override
  void initState() {
    super.initState();
    _tabController = TabController(
      length: 2,
      vsync: this,
      initialIndex: widget.initialTab,
    );
    WidgetsBinding.instance.addPostFrameCallback((_) {
      widget.viewModel.loadDiscoveryData();
    });
  }

  @override
  void dispose() {
    _tabController.dispose();
    _searchController.dispose();
    _placeSearchController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return ListenableBuilder(
      listenable: widget.viewModel,
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
            children: [
              _buildCatalogTab(context),
              _buildMapTab(context),
            ],
          ),
        );
      },
    );
  }

  Widget _buildCatalogTab(BuildContext context) {
    final vm = widget.viewModel;

    if (vm.isLoading && vm.destinations.isEmpty) {
      return const Center(child: CircularProgressIndicator());
    }

    return RefreshIndicator(
      onRefresh: () => vm.loadDiscoveryData(),
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
                          vm.loadDiscoveryData();
                        },
                      )
                    : null,
                border: OutlineInputBorder(
                  borderRadius: BorderRadius.circular(16),
                ),
              ),
              onSubmitted: (value) {
                vm.updateFilters(query: value);
                vm.loadDiscoveryData();
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
                        vm.loadDiscoveryData();
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
                children: _categories.map((cat) {
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
                        vm.loadDiscoveryData();
                      },
                    ),
                  );
                }).toList(),
              ),
            ),
            const SizedBox(height: 20),

            if (vm.errorMessage != null)
              Container(
                padding: const EdgeInsets.all(12),
                margin: const EdgeInsets.only(bottom: 16),
                decoration: BoxDecoration(
                  color: Colors.red.shade50,
                  borderRadius: BorderRadius.circular(12),
                  border: Border.all(color: Colors.red.shade200),
                ),
                child: Text(
                  vm.errorMessage!,
                  style: TextStyle(color: Colors.red.shade900),
                ),
              ),

            // Section: Destinations
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Text(
                  'Coastal Destinations (${vm.destinations.length})',
                  style: Theme.of(context).textTheme.titleMedium?.copyWith(
                        fontWeight: FontWeight.bold,
                      ),
                ),
              ],
            ),
            const SizedBox(height: 8),

            if (vm.destinations.isEmpty)
              const Padding(
                padding: EdgeInsets.symmetric(vertical: 24),
                child: Center(
                  child: Text('No coastal destinations found.'),
                ),
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
                                  backgroundColor: BlueversePalette.coastDeep.withAlpha(25),
                                  child: const Icon(
                                    Icons.place,
                                    color: BlueversePalette.coastDeep,
                                  ),
                                ),
                                const SizedBox(width: 12),
                                Expanded(
                                  child: Column(
                                    crossAxisAlignment: CrossAxisAlignment.start,
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
                                    isFav ? Icons.bookmark : Icons.bookmark_border,
                                    color: isFav ? BlueversePalette.coastDeep : Colors.grey,
                                  ),
                                  onPressed: () {
                                    vm.toggleFavourite('DESTINATION', dest.id);
                                  },
                                ),
                              ],
                            ),
                            if (dest.description != null && dest.description!.isNotEmpty) ...[
                              const SizedBox(height: 8),
                              Text(
                                dest.description!,
                                maxLines: 2,
                                overflow: TextOverflow.ellipsis,
                                style: const TextStyle(fontSize: 13, height: 1.4),
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

            // Section: Offerings
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Text(
                  'Bookable Offerings (${vm.offerings.length})',
                  style: Theme.of(context).textTheme.titleMedium?.copyWith(
                        fontWeight: FontWeight.bold,
                      ),
                ),
              ],
            ),
            const SizedBox(height: 8),

            if (vm.offerings.isEmpty)
              const Padding(
                padding: EdgeInsets.symmetric(vertical: 16),
                child: Center(
                  child: Text('No active coastal offerings found.'),
                ),
              )
            else
              ListView.builder(
                shrinkWrap: true,
                physics: const NeverScrollableScrollPhysics(),
                itemCount: vm.offerings.length,
                itemBuilder: (context, index) {
                  final off = vm.offerings[index];
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
                                    crossAxisAlignment: CrossAxisAlignment.start,
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
                                    isFav ? Icons.bookmark : Icons.bookmark_border,
                                    color: isFav ? BlueversePalette.coastDeep : Colors.grey,
                                  ),
                                  onPressed: () {
                                    vm.toggleFavourite('OFFERING', off.id);
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

    return SingleChildScrollView(
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          // Proximity Place Search Bar
          TextField(
            key: const Key('input-map-place-search'),
            controller: _placeSearchController,
            decoration: InputDecoration(
              hintText: 'Search places, bays, coordinates...',
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

          // Place search results if available
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
                separatorBuilder: (_, __) => const Divider(height: 1),
                itemBuilder: (context, index) {
                  final place = vm.placeSearchResults[index];
                  return ListTile(
                    dense: true,
                    leading: const Icon(Icons.place, color: BlueversePalette.coastDeep),
                    title: Text(place.displayName),
                    subtitle: Text('${place.latitude.toStringAsFixed(4)}, ${place.longitude.toStringAsFixed(4)}'),
                    trailing: const Icon(Icons.arrow_forward_ios, size: 14),
                    onTap: () {
                      vm.fetchNearby(
                        latitude: place.latitude,
                        longitude: place.longitude,
                      );
                    },
                  );
                },
              ),
            ),
            const SizedBox(height: 16),
          ],

          // Map Tile Visual Representation
          Container(
            height: 220,
            decoration: BoxDecoration(
              color: BlueversePalette.coastDeep.withAlpha(20),
              borderRadius: BorderRadius.circular(20),
              border: Border.all(color: BlueversePalette.coastLine),
            ),
            child: Stack(
              alignment: Alignment.center,
              children: [
                Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    const Icon(
                      Icons.map,
                      size: 48,
                      color: BlueversePalette.coastDeep,
                    ),
                    const SizedBox(height: 8),
                    Text(
                      'OpenStreetMap Coastal View',
                      style: Theme.of(context).textTheme.titleSmall?.copyWith(
                            fontWeight: FontWeight.bold,
                            color: BlueversePalette.coastDeep,
                          ),
                    ),
                    const SizedBox(height: 4),
                    Text(
                      'Sri Lanka Marine & Coastal Corridor',
                      style: TextStyle(
                        fontSize: 12,
                        color: Colors.grey.shade700,
                      ),
                    ),
                  ],
                ),
              ],
            ),
          ),
          const SizedBox(height: 16),

          // Proximity Experiences Section
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              const Text(
                'Nearby Coastal Hotspots',
                style: TextStyle(fontWeight: FontWeight.bold, fontSize: 16),
              ),
              TextButton.icon(
                icon: const Icon(Icons.my_location, size: 16),
                label: const Text('Near Me'),
                onPressed: () {
                  // Default to Southern Province (Mirissa) coordinates
                  vm.fetchNearby(latitude: 5.9482, longitude: 80.4716);
                },
              ),
            ],
          ),
          const SizedBox(height: 8),

          if (vm.nearbyExperiences != null) ...[
            Text(
              'Found ${vm.nearbyExperiences!.results.length} destinations within radius',
              style: TextStyle(color: Colors.grey.shade700, fontSize: 12),
            ),
            const SizedBox(height: 8),
            ...vm.nearbyExperiences!.results.map(
              (d) => Card(
                key: Key('card-nearby-destination-${d.destinationId}'),
                margin: const EdgeInsets.only(bottom: 8),
                shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
                child: ListTile(
                  leading: const Icon(Icons.explore, color: BlueversePalette.coastDeep),
                  title: Text(d.name),
                  subtitle: Text('${d.region ?? "Coastal"} • ${d.latitude.toStringAsFixed(3)}, ${d.longitude.toStringAsFixed(3)}'),
                  trailing: const Icon(Icons.arrow_forward_ios, size: 14),
                  onTap: () {
                    Navigator.pushNamed(context, '/experiences/destinations/${d.destinationId}');
                  },
                ),
              ),
            ),
          ] else ...[
            ...vm.destinations.take(4).map(
              (dest) => Card(
                margin: const EdgeInsets.only(bottom: 8),
                shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
                child: ListTile(
                  leading: const Icon(Icons.waves, color: BlueversePalette.coastDeep),
                  title: Text(dest.name),
                  subtitle: Text(dest.region ?? 'Coastal'),
                  trailing: const Icon(Icons.arrow_forward_ios, size: 14),
                  onTap: () {
                    Navigator.pushNamed(context, '/experiences/destinations/${dest.id}');
                  },
                ),
              ),
            ),
          ],
        ],
      ),
    );
  }
}
