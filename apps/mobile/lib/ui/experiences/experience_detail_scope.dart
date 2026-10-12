import 'package:flutter/material.dart';

import '../auth_view_model.dart';
import 'experience_view_model.dart';

/// Each pushed detail owns its request state. The shared discovery model keeps
/// the map, filters and account wishlist, so back navigation restores them.
class ExperienceDetailScope extends StatefulWidget {
  const ExperienceDetailScope({
    super.key,
    required this.parent,
    required this.auth,
    required this.builder,
  });
  final ExperienceViewModel parent;
  final AuthViewModel auth;
  final Widget Function(ExperienceViewModel) builder;
  @override
  State<ExperienceDetailScope> createState() => _ExperienceDetailScopeState();
}

class _ExperienceDetailScopeState extends State<ExperienceDetailScope> {
  late final ExperienceViewModel _model;
  String? _userId;
  @override
  void initState() {
    super.initState();
    _userId = widget.auth.user?.id;
    _model = ExperienceViewModel(repository: widget.parent.repository);
    widget.auth.addListener(_onAccountChanged);
  }

  void _onAccountChanged() {
    final id = widget.auth.user?.id;
    if (id == _userId) return;
    _userId = id;
    _model.clearUserScopedState();
    if (id != null) _model.loadFavourites();
  }

  @override
  void dispose() {
    widget.auth.removeListener(_onAccountChanged);
    _model.dispose();
    // Refreshing the wishlist also notifies the still-mounted discovery page.
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (widget.auth.user != null) widget.parent.loadFavourites();
    });
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => widget.builder(_model);
}
