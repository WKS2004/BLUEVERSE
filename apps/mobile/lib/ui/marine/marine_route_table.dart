/// Public registration for the Member 2 marine workflows.
///
/// Every route below is registered under a shared, backend-visible workflow.
/// These destinations mirror [main.dart]; none of them targets a backend
/// route directly by hostname, PostgreSQL, Auth or Agentic AI.
library;

import 'package:flutter/material.dart';

/// Route identifiers used by [MarineNavigator] and the UI contract registry.
class MarineRoutes {
  MarineRoutes._();

  static const conditions = '/marine/conditions';
  static const history = '/marine/history';
  static const safetyProfiles = '/marine/safety-profiles';
}

/// A tiny path matcher used by [MarineNavigator].
class MarinePath {
  const MarinePath({required this.full});

  final String full;

  MarineRouteMatch match(String route) {
    switch (route) {
      case MarineRoutes.conditions:
        return full == MarineRoutes.conditions
            ? const MarineRouteMatch.success(MarineRoutes.conditions)
            : const MarineRouteMatch.unmatched();
      case MarineRoutes.history:
        return full == MarineRoutes.history
            ? const MarineRouteMatch.success(MarineRoutes.history)
            : const MarineRouteMatch.unmatched();
      case MarineRoutes.safetyProfiles:
        return full == MarineRoutes.safetyProfiles
            ? const MarineRouteMatch.success(MarineRoutes.safetyProfiles)
            : const MarineRouteMatch.unmatched();
    }
    return const MarineRouteMatch.unmatched();
  }
}

class MarineRouteMatch {
  const MarineRouteMatch.success(this.route) : params = const {};
  const MarineRouteMatch.unmatched() : route = null, params = const {};

  final String? route;

  /// Matched named route parameters (currently unused for the marine module).
  final Map<String, String> params;
}

/// Minimal mobile navigation helper scoped to the marine module.
///
/// The rest of the app continues to use [Navigator.pushNamed] for Auth routes.
/// This helper exists so the marine feature can be tested and overridden
/// independently when the contract expands.
class MarineNavigator {
  const MarineNavigator._();

  static Future<void> toConditions(BuildContext context) =>
      Navigator.pushNamed(context, MarineRoutes.conditions);

  static Future<void> toHistory(BuildContext context) =>
      Navigator.pushNamed(context, MarineRoutes.history);

  static Future<void> toSafetyProfiles(BuildContext context) =>
      Navigator.pushNamed(context, MarineRoutes.safetyProfiles);
}
