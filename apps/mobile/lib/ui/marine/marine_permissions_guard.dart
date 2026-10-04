import 'package:flutter/material.dart';

import '../../data/models/auth_models.dart';
import 'marine_route_table.dart';

/// Returns [true] when the authorized account may see the marine routes.
///
/// The backend is the only source of truth for the grant checks; this helper
/// simply keeps the UI from showing disabled destinations.
bool hasMarineReadAccess(AuthUser? user) {
  if (user == null) return false;
  return user.permissions.contains('marine.profile.read');
}
