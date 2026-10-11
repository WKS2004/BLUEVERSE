import '../../data/models/auth_models.dart';
import 'marine_route_table.dart';

/// Returns only the marine routes an authenticated account may reach.
///
/// Reads the server-owned permission list from the current [AuthUser]. The
/// backend still enforces the grant on every request; this helper only
/// gates the UI.
List<String> marineRouteHrefsFor(AuthUser? user) {
  if (user == null) return const [];
  final grants = user.permissions.toSet();
  if (!grants.contains('marine.profile.read')) return const [];

  return [
    MarineRoutes.conditions,
    MarineRoutes.history,
    MarineRoutes.safetyProfiles,
  ];
}
