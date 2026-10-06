import 'package:flutter/material.dart';

import '../auth_view_model.dart';
import '../blueverse_theme.dart';
import 'experience_view_model.dart';

class FavouritesScreen extends StatefulWidget {
  const FavouritesScreen({
    super.key,
    required this.viewModel,
    this.authViewModel,
  });

  final ExperienceViewModel viewModel;
  final AuthViewModel? authViewModel;

  @override
  State<FavouritesScreen> createState() => _FavouritesScreenState();
}

class _FavouritesScreenState extends State<FavouritesScreen> {
  String _filterType = 'ALL';

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (widget.authViewModel == null || widget.authViewModel!.user != null) {
        widget.viewModel.loadFavourites();
      } else {
        widget.viewModel.clearUserScopedState();
      }
    });
  }

  @override
  Widget build(BuildContext context) {
    final listenable = widget.authViewModel == null
        ? widget.viewModel
        : Listenable.merge([widget.viewModel, widget.authViewModel!]);
    return ListenableBuilder(
      listenable: listenable,
      builder: (context, _) {
        final vm = widget.viewModel;
        final isSignedOut =
            widget.authViewModel != null && widget.authViewModel!.user == null;

        final filtered = vm.favourites.where((fav) {
          if (_filterType == 'ALL') return true;
          return fav.targetType.toUpperCase() == _filterType;
        }).toList();

        return Scaffold(
          appBar: AppBar(title: const Text('Saved Wishlist')),
          body: RefreshIndicator(
            onRefresh: () =>
                isSignedOut ? Future<void>.value() : vm.loadFavourites(),
            child: SingleChildScrollView(
              padding: const EdgeInsets.all(16),
              physics: const AlwaysScrollableScrollPhysics(),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: isSignedOut
                    ? [
                        Card(
                          child: Padding(
                            padding: const EdgeInsets.all(24),
                            child: Column(
                              children: [
                                const Icon(Icons.bookmark_outline, size: 44),
                                const SizedBox(height: 12),
                                const Text(
                                  'Sign in to view your saved places',
                                  style: TextStyle(
                                    fontSize: 18,
                                    fontWeight: FontWeight.bold,
                                  ),
                                  textAlign: TextAlign.center,
                                ),
                                const SizedBox(height: 8),
                                const Text(
                                  'Your wishlist is private to your account. You can continue browsing without signing in.',
                                  textAlign: TextAlign.center,
                                ),
                                const SizedBox(height: 16),
                                FilledButton(
                                  onPressed: () =>
                                      Navigator.pushNamed(context, '/signin'),
                                  child: const Text('Sign in'),
                                ),
                              ],
                            ),
                          ),
                        ),
                      ]
                    : [
                        // Filter Chips
                        SingleChildScrollView(
                          scrollDirection: Axis.horizontal,
                          child: Row(
                            children: [
                              _buildFilterChip(
                                'ALL',
                                'All Saved (${vm.favourites.length})',
                              ),
                              const SizedBox(width: 8),
                              _buildFilterChip('DESTINATION', 'Destinations'),
                              const SizedBox(width: 8),
                              _buildFilterChip('ACTIVITY', 'Activities'),
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

                        if (vm.favouritesErrorMessage != null) ...[
                          Container(
                            padding: const EdgeInsets.all(12),
                            decoration: BoxDecoration(
                              color: Colors.red.shade50,
                              borderRadius: BorderRadius.circular(12),
                              border: Border.all(color: Colors.red.shade200),
                            ),
                            child: Column(
                              mainAxisSize: MainAxisSize.min,
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Text(
                                  vm.favouritesErrorMessage!,
                                  style: TextStyle(color: Colors.red.shade900),
                                ),
                                const SizedBox(height: 8),
                                Align(
                                  alignment: Alignment.centerRight,
                                  child: TextButton.icon(
                                    onPressed: vm.isLoadingFavourites
                                        ? null
                                        : vm.loadFavourites,
                                    icon: const Icon(Icons.refresh),
                                    label: const Text('Try again'),
                                  ),
                                ),
                              ],
                            ),
                          ),
                          const SizedBox(height: 12),
                        ],

                        if (vm.favouriteActionErrorMessage != null) ...[
                          Container(
                            padding: const EdgeInsets.all(12),
                            decoration: BoxDecoration(
                              color: Colors.red.shade50,
                              borderRadius: BorderRadius.circular(12),
                              border: Border.all(color: Colors.red.shade200),
                            ),
                            child: Text(
                              vm.favouriteActionErrorMessage!,
                              style: TextStyle(color: Colors.red.shade900),
                            ),
                          ),
                          const SizedBox(height: 12),
                        ],

                        if (vm.isLoadingFavourites && vm.favourites.isEmpty)
                          const Padding(
                            padding: EdgeInsets.symmetric(vertical: 40),
                            child: Center(child: CircularProgressIndicator()),
                          )
                        else if (filtered.isEmpty &&
                            vm.favouritesErrorMessage == null)
                          Card(
                            elevation: 0,
                            shape: RoundedRectangleBorder(
                              borderRadius: BorderRadius.circular(20),
                              side: const BorderSide(
                                color: BlueversePalette.coastLine,
                              ),
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
                                  Text(
                                    _filterType == 'ALL'
                                        ? 'Your wishlist is empty'
                                        : 'No ${_filterType.toLowerCase()} saved yet',
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
                                    style: TextStyle(
                                      color: Colors.grey,
                                      height: 1.4,
                                    ),
                                  ),
                                  const SizedBox(height: 20),
                                  ElevatedButton.icon(
                                    icon: const Icon(Icons.explore),
                                    label: const Text('Explore Experiences'),
                                    onPressed: () {
                                      Navigator.pushNamed(
                                        context,
                                        '/experiences',
                                      );
                                    },
                                  ),
                                ],
                              ),
                            ),
                          )
                        else if (filtered.isNotEmpty)
                          ListView.builder(
                            shrinkWrap: true,
                            physics: const NeverScrollableScrollPhysics(),
                            itemCount: filtered.length,
                            itemBuilder: (context, index) {
                              final fav = filtered[index];
                              final targetType = fav.targetType.toUpperCase();
                              final isDest = targetType == 'DESTINATION';
                              final isActivity = targetType == 'ACTIVITY';

                              return Card(
                                key: Key('card-favourite-${fav.id}'),
                                margin: const EdgeInsets.only(bottom: 12),
                                shape: RoundedRectangleBorder(
                                  borderRadius: BorderRadius.circular(16),
                                ),
                                child: ListTile(
                                  leading: CircleAvatar(
                                    backgroundColor: BlueversePalette.coastDeep
                                        .withAlpha(25),
                                    child: Icon(
                                      isDest
                                          ? Icons.place
                                          : isActivity
                                          ? Icons.waves
                                          : Icons.surfing,
                                      color: BlueversePalette.coastDeep,
                                    ),
                                  ),
                                  title: Text(
                                    fav.targetTitle ?? fav.targetId,
                                    style: const TextStyle(
                                      fontWeight: FontWeight.bold,
                                    ),
                                  ),
                                  subtitle: Text(
                                    '${fav.targetType} • Saved on ${_formatDate(fav.createdAt)}',
                                  ),
                                  trailing: IconButton(
                                    key: Key('btn-remove-favourite-${fav.id}'),
                                    icon: const Icon(
                                      Icons.delete_outline,
                                      color: Colors.red,
                                    ),
                                    tooltip: 'Remove from Wishlist',
                                    onPressed: () {
                                      vm.toggleFavourite(
                                        fav.targetType,
                                        fav.targetId,
                                      );
                                    },
                                  ),
                                  onTap: () {
                                    if (isDest) {
                                      Navigator.pushNamed(
                                        context,
                                        '/experiences/destinations/${fav.targetId}',
                                      );
                                    } else if (isActivity) {
                                      vm.updateFilters(
                                        query: '',
                                        category: '',
                                        activityId: fav.targetId,
                                      );
                                      Navigator.pushNamed(
                                        context,
                                        '/experiences',
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
