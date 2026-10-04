import 'package:flutter/material.dart';

import '../blueverse_theme.dart';
import 'experience_view_model.dart';

class FavouritesScreen extends StatefulWidget {
  const FavouritesScreen({super.key, required this.viewModel});

  final ExperienceViewModel viewModel;

  @override
  State<FavouritesScreen> createState() => _FavouritesScreenState();
}

class _FavouritesScreenState extends State<FavouritesScreen> {
  String _filterType = 'ALL';

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      widget.viewModel.loadFavourites();
    });
  }

  @override
  Widget build(BuildContext context) {
    return ListenableBuilder(
      listenable: widget.viewModel,
      builder: (context, _) {
        final vm = widget.viewModel;

        final filtered = vm.favourites.where((fav) {
          if (_filterType == 'ALL') return true;
          return fav.targetType.toUpperCase() == _filterType;
        }).toList();

        return Scaffold(
          appBar: AppBar(
            title: const Text('Saved Wishlist'),
          ),
          body: RefreshIndicator(
            onRefresh: () => vm.loadFavourites(),
            child: SingleChildScrollView(
              padding: const EdgeInsets.all(16),
              physics: const AlwaysScrollableScrollPhysics(),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  // Filter Chips
                  SingleChildScrollView(
                    scrollDirection: Axis.horizontal,
                    child: Row(
                      children: [
                        _buildFilterChip('ALL', 'All Saved (${vm.favourites.length})'),
                        const SizedBox(width: 8),
                        _buildFilterChip('DESTINATION', 'Destinations'),
                        const SizedBox(width: 8),
                        _buildFilterChip('OFFERING', 'Offerings'),
                      ],
                    ),
                  ),
                  const SizedBox(height: 16),

                  if (vm.successMessage != null) ...[
                    Container(
                      padding: const EdgeInsets.all(12),
                      decoration: BoxDecoration(
                        color: Colors.teal.shade50,
                        borderRadius: BorderRadius.circular(12),
                        border: Border.all(color: Colors.teal.shade200),
                      ),
                      child: Text(
                        vm.successMessage!,
                        style: TextStyle(color: Colors.teal.shade900),
                      ),
                    ),
                    const SizedBox(height: 12),
                  ],

                  if (vm.errorMessage != null) ...[
                    Container(
                      padding: const EdgeInsets.all(12),
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
                    const SizedBox(height: 12),
                  ],

                  if (vm.isLoading && vm.favourites.isEmpty)
                    const Padding(
                      padding: EdgeInsets.symmetric(vertical: 40),
                      child: Center(child: CircularProgressIndicator()),
                    )
                  else if (filtered.isEmpty)
                    Card(
                      elevation: 0,
                      shape: RoundedRectangleBorder(
                        borderRadius: BorderRadius.circular(20),
                        side: const BorderSide(color: BlueversePalette.coastLine),
                      ),
                      child: Padding(
                        padding: const EdgeInsets.all(32),
                        child: Column(
                          children: [
                            const Icon(
                              Icons.bookmark_outline,
                              size: 48,
                              color: BlueversePalette.coastDeep,
                            ),
                            const SizedBox(height: 16),
                            const Text(
                              'Your wishlist is empty',
                              style: TextStyle(
                                fontSize: 18,
                                fontWeight: FontWeight.bold,
                                color: BlueversePalette.coastInk,
                              ),
                            ),
                            const SizedBox(height: 8),
                            const Text(
                              'Save your favorite coastal destinations and marine activities to easily plan your next coastal journey.',
                              textAlign: TextAlign.center,
                              style: TextStyle(color: Colors.grey, height: 1.4),
                            ),
                            const SizedBox(height: 20),
                            ElevatedButton.icon(
                              icon: const Icon(Icons.explore),
                              label: const Text('Explore Experiences'),
                              onPressed: () {
                                Navigator.pushNamed(context, '/experiences');
                              },
                            ),
                          ],
                        ),
                      ),
                    )
                  else
                    ListView.builder(
                      shrinkWrap: true,
                      physics: const NeverScrollableScrollPhysics(),
                      itemCount: filtered.length,
                      itemBuilder: (context, index) {
                        final fav = filtered[index];
                        final isDest = fav.targetType.toUpperCase() == 'DESTINATION';

                        return Card(
                          key: Key('card-favourite-${fav.id}'),
                          margin: const EdgeInsets.only(bottom: 12),
                          shape: RoundedRectangleBorder(
                            borderRadius: BorderRadius.circular(16),
                          ),
                          child: ListTile(
                            leading: CircleAvatar(
                              backgroundColor: BlueversePalette.coastDeep.withAlpha(25),
                              child: Icon(
                                isDest ? Icons.place : Icons.surfing,
                                color: BlueversePalette.coastDeep,
                              ),
                            ),
                            title: Text(
                              fav.targetTitle ?? fav.targetId,
                              style: const TextStyle(fontWeight: FontWeight.bold),
                            ),
                            subtitle: Text('${fav.targetType} • Saved on ${_formatDate(fav.createdAt)}'),
                            trailing: IconButton(
                              key: Key('btn-remove-favourite-${fav.id}'),
                              icon: const Icon(Icons.delete_outline, color: Colors.red),
                              tooltip: 'Remove from Wishlist',
                              onPressed: () {
                                vm.toggleFavourite(fav.targetType, fav.targetId);
                              },
                            ),
                            onTap: () {
                              if (isDest) {
                                Navigator.pushNamed(
                                  context,
                                  '/experiences/destinations/${fav.targetId}',
                                );
                              } else {
                                Navigator.pushNamed(
                                  context,
                                  '/experiences/offerings/${fav.targetId}',
                                );
                              }
                            },
                          ),
                        );
                      },
                    ),
                ],
              ),
            ),
          ),
        );
      },
    );
  }

  Widget _buildFilterChip(String type, String label) {
    final isSelected = _filterType == type;
    return ChoiceChip(
      label: Text(label),
      selected: isSelected,
      onSelected: (_) {
        setState(() => _filterType = type);
      },
    );
  }

  String _formatDate(DateTime dt) {
    return '${dt.year}-${dt.month.toString().padLeft(2, '0')}-${dt.day.toString().padLeft(2, '0')}';
  }
}
